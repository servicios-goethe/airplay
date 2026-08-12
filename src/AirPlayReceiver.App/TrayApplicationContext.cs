using AirPlayReceiver.App.Config;
using AirPlayReceiver.App.Diagnostics;
using AirPlayReceiver.App.Engine;
using AirPlayReceiver.App.Setup;

namespace AirPlayReceiver.App;

/// <summary>
/// Ícono de bandeja y ciclo de vida de la app. No hay ventana principal: el
/// receptor tiene que ser invisible salvo cuando algo falla.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ConfigStore _configStore = new();
    private readonly UxPlayProcess _engine;
    private readonly SynchronizationContext _uiContext;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _autoStartItem;

    private ReceiverConfig _config;
    private string _receiverName;

    public TrayApplicationContext()
    {
        // Capturado en el constructor, que corre en el hilo de UI: los eventos
        // del motor llegan desde hilos del pool y no pueden tocar el NotifyIcon.
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _config = _configStore.Load();
        _receiverName = ReceiverNameResolver.Resolve(_config, Environment.MachineName);
        _engine = new UxPlayProcess();

        Log.Info($"Configuración: {_configStore.LoadedFrom ?? "valores por defecto"}");
        Log.Info($"Nombre publicado: '{_receiverName}'");

        _statusItem = new ToolStripMenuItem("Detenido") { Enabled = false };
        _toggleItem = new ToolStripMenuItem("Iniciar", null, (_, _) => ToggleEngine());
        _autoStartItem = new ToolStripMenuItem("Iniciar con Windows", null, (_, _) => ToggleAutoStart())
        {
            CheckOnClick = false,
            Checked = AutoStart.IsEnabled(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripMenuItem("Reparar regla de firewall", null, (_, _) => RepairFirewall()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Ver registro", null, (_, _) => OpenLog()));
        menu.Items.Add(new ToolStripMenuItem("Salir", null, (_, _) => ExitApplication()));

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true,
            Text = BuildTooltip(EngineState.Stopped),
        };

        _engine.StateChanged += OnEngineStateChanged;

        if (_config.StartEngineOnLaunch)
        {
            StartEngine();
        }
    }

    private void ToggleEngine()
    {
        if (_engine.IsRunning)
        {
            _engine.Stop();
        }
        else
        {
            StartEngine();
        }
    }

    private void StartEngine()
    {
        // Se relee la config en cada arranque para que un cambio desplegado por
        // GPO se aplique sin reinstalar ni cerrar sesión.
        _config = _configStore.Load();
        _receiverName = ReceiverNameResolver.Resolve(_config, Environment.MachineName);
        _engine.Start(_config, _receiverName);
    }

    private void ToggleAutoStart()
    {
        var target = !AutoStart.IsEnabled();
        if (!AutoStart.SetEnabled(target))
        {
            ShowBalloon("No se pudo cambiar el auto-inicio", ToolTipIcon.Warning);
        }
        _autoStartItem.Checked = AutoStart.IsEnabled();
    }

    private void RepairFirewall()
    {
        var ok = FirewallRule.Ensure();
        ShowBalloon(
            ok ? "Regla de firewall creada." : "No se pudo crear la regla de firewall.",
            ok ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    private void OpenLog()
    {
        try
        {
            if (!File.Exists(Log.LogFilePath))
            {
                ShowBalloon("Todavía no hay registro.", ToolTipIcon.Info);
                return;
            }

            using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Log.LogFilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ShowBalloon($"No se pudo abrir el registro: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    private void OnEngineStateChanged(object? sender, EngineStateChangedEventArgs e)
        => _uiContext.Post(_ => ApplyState(e), null);

    private void ApplyState(EngineStateChangedEventArgs e)
    {
        var status = e.State switch
        {
            EngineState.Stopped => "Detenido",
            EngineState.Starting => "Iniciando…",
            EngineState.Running => $"Activo como «{_receiverName}»",
            EngineState.Restarting => $"Reiniciando… {e.Detail}",
            EngineState.Failed => "Error: " + (e.Detail ?? "desconocido"),
            _ => "Estado desconocido",
        };

        _statusItem.Text = status;
        _toggleItem.Text = _engine.IsRunning ? "Detener" : "Iniciar";
        _notifyIcon.Text = BuildTooltip(e.State);

        // Solo se molesta al usuario cuando hay algo que no se recupera solo.
        if (e.State == EngineState.Failed)
        {
            ShowBalloon(status, ToolTipIcon.Error);
        }
    }

    private string BuildTooltip(EngineState state)
    {
        var suffix = state switch
        {
            EngineState.Running => $"activo como {_receiverName}",
            EngineState.Starting => "iniciando",
            EngineState.Restarting => "reiniciando",
            EngineState.Failed => "con error",
            _ => "detenido",
        };

        // El tooltip de NotifyIcon se trunca a 63 caracteres.
        var text = $"Receptor AirPlay — {suffix}";
        return text.Length <= 63 ? text : text[..63];
    }

    private void ShowBalloon(string message, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = "Receptor AirPlay";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (path is not null)
            {
                var extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            Log.Warn($"No se pudo extraer el ícono del ejecutable: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    private void ExitApplication()
    {
        _engine.Stop();
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _engine.StateChanged -= OnEngineStateChanged;
            _engine.Dispose();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
