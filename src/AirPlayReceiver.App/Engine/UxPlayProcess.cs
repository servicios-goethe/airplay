using System.Diagnostics;
using AirPlayReceiver.App.Config;
using AirPlayReceiver.App.Diagnostics;

namespace AirPlayReceiver.App.Engine;

public enum EngineState
{
    Stopped,
    Starting,
    Running,
    Restarting,
    Failed,
}

public sealed class EngineStateChangedEventArgs(EngineState state, string? detail) : EventArgs
{
    public EngineState State { get; } = state;
    public string? Detail { get; } = detail;
}

/// <summary>
/// Supervisa el proceso <c>uxplay.exe</c>: lo lanza, escucha su salida y lo
/// reinicia si se cae solo.
///
/// El reinicio automático es deliberado: en un aula nadie va a mirar la bandeja
/// del sistema para notar que el receptor murió, y una caída dejaría la clase
/// sin proyección hasta que alguien reinicie la PC.
/// </summary>
public sealed class UxPlayProcess : IDisposable
{
    private static readonly TimeSpan InitialRestartDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRestartDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Si el motor se mantuvo vivo más que esto, la caída se considera un
    /// incidente aislado y el backoff vuelve a empezar de cero.
    /// </summary>
    private static readonly TimeSpan StableRunThreshold = TimeSpan.FromSeconds(60);

    private readonly EnginePaths _paths;
    private readonly object _gate = new();

    private Process? _process;
    private CancellationTokenSource? _supervisorCts;
    private TimeSpan _restartDelay = InitialRestartDelay;

    public UxPlayProcess(EnginePaths? paths = null)
    {
        _paths = paths ?? new EnginePaths();
    }

    public event EventHandler<EngineStateChangedEventArgs>? StateChanged;
    public event EventHandler<string>? OutputReceived;

    public EngineState State { get; private set; } = EngineState.Stopped;

    public string? ReceiverName { get; private set; }

    public bool IsRunning => State is EngineState.Running or EngineState.Starting;

    public void Start(ReceiverConfig config, string receiverName)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_gate)
        {
            if (_supervisorCts is not null)
            {
                Log.Warn("Se pidió iniciar el motor pero ya estaba corriendo.");
                return;
            }

            if (!_paths.IsInstalled)
            {
                SetState(EngineState.Failed, $"No se encontró el motor en '{_paths.ExecutablePath}'.");
                return;
            }

            ReceiverName = receiverName;
            _restartDelay = InitialRestartDelay;
            _supervisorCts = new CancellationTokenSource();

            var token = _supervisorCts.Token;
            _ = Task.Run(() => SuperviseAsync(config, receiverName, token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Process? process;

        lock (_gate)
        {
            cts = _supervisorCts;
            process = _process;
            _supervisorCts = null;
        }

        if (cts is null)
        {
            return;
        }

        // Cancelar primero evita que el supervisor interprete la salida del
        // proceso como una caída y lo reinicie.
        cts.Cancel();
        KillQuietly(process);
        cts.Dispose();

        SetState(EngineState.Stopped, null);
        Log.Info("Motor detenido.");
    }

    private async Task SuperviseAsync(
        ReceiverConfig config,
        string receiverName,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;
            int exitCode;

            try
            {
                exitCode = await RunOnceAsync(config, receiverName, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"No se pudo lanzar el motor: {ex.Message}");
                SetState(EngineState.Failed, ex.Message);
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (DateTime.UtcNow - startedAt >= StableRunThreshold)
            {
                _restartDelay = InitialRestartDelay;
            }

            Log.Warn($"El motor terminó con código {exitCode}. Reintentando en {_restartDelay.TotalSeconds:0}s.");
            SetState(EngineState.Restarting, $"Reintentando en {_restartDelay.TotalSeconds:0}s");

            try
            {
                await Task.Delay(_restartDelay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _restartDelay = TimeSpan.FromTicks(Math.Min(_restartDelay.Ticks * 2, MaxRestartDelay.Ticks));
        }
    }

    private async Task<int> RunOnceAsync(
        ReceiverConfig config,
        string receiverName,
        CancellationToken token)
    {
        var arguments = EngineArguments.Build(config, receiverName);

        var startInfo = new ProcessStartInfo
        {
            FileName = _paths.ExecutablePath,
            WorkingDirectory = _paths.EngineDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _paths.ApplyTo(startInfo.Environment);

        Log.Info($"Iniciando motor: uxplay.exe {EngineArguments.Describe(arguments)}");
        SetState(EngineState.Starting, receiverName);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => HandleOutput(e.Data);
        process.ErrorDataReceived += (_, e) => HandleOutput(e.Data);

        if (!process.Start())
        {
            throw new InvalidOperationException("El sistema no pudo crear el proceso del motor.");
        }

        lock (_gate)
        {
            _process = process;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        SetState(EngineState.Running, receiverName);
        Log.Info($"Motor corriendo, publicado como '{receiverName}'.");

        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _process = null;
            }
        }

        var exitCode = process.ExitCode;
        process.Dispose();
        return exitCode;
    }

    private void HandleOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        Log.Engine(line);
        OutputReceived?.Invoke(this, line);
    }

    private void SetState(EngineState state, string? detail)
    {
        State = state;
        StateChanged?.Invoke(this, new EngineStateChangedEventArgs(state, detail));
    }

    private static void KillQuietly(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                // entireProcessTree: UxPlay lanza gst-plugin-scanner como hijo y
                // un scanner huérfano deja el registro de plugins a medio escribir.
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // El proceso ya había terminado por su cuenta.
        }
    }

    public void Dispose() => Stop();
}
