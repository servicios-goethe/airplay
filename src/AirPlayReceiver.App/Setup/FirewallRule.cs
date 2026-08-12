using System.Diagnostics;
using AirPlayReceiver.App.Diagnostics;
using AirPlayReceiver.App.Engine;

namespace AirPlayReceiver.App.Setup;

/// <summary>
/// Alta de la regla de entrada del Firewall de Windows para el motor.
///
/// Sin esta regla el equipo no responde al descubrimiento mDNS y sencillamente
/// no aparece en el selector AirPlay del iPad. Windows normalmente muestra un
/// diálogo la primera vez, pero en un aula con usuarios sin privilegios ese
/// diálogo nunca se acepta, así que la regla se crea explícitamente.
/// </summary>
public static class FirewallRule
{
    public const string RuleName = "Receptor AirPlay (Goethe)";

    /// <summary>
    /// Crea las reglas de entrada TCP y UDP para <c>uxplay.exe</c>. Requiere
    /// privilegios de administrador: se lanza <c>netsh</c> con elevación, lo que
    /// muestra el diálogo de UAC.
    /// </summary>
    /// <returns><c>true</c> si ambas reglas se crearon correctamente.</returns>
    public static bool Ensure(EnginePaths? paths = null)
    {
        paths ??= new EnginePaths();

        if (!paths.IsInstalled)
        {
            Log.Error("No se puede crear la regla de firewall: falta el motor.");
            return false;
        }

        // Se borra primero para que reinstalar en otra ruta no deje una regla
        // vieja apuntando a un ejecutable que ya no existe.
        RunNetsh($"advfirewall firewall delete rule name=\"{RuleName}\"", ignoreFailure: true);

        var tcp = RunNetsh(
            $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow " +
            $"program=\"{paths.ExecutablePath}\" enable=yes profile=any protocol=TCP");

        var udp = RunNetsh(
            $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow " +
            $"program=\"{paths.ExecutablePath}\" enable=yes profile=any protocol=UDP");

        var ok = tcp && udp;
        Log.Info(ok
            ? "Reglas de firewall creadas."
            : "No se pudieron crear las reglas de firewall.");
        return ok;
    }

    private static bool RunNetsh(string arguments, bool ignoreFailure = false)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                // UseShellExecute + verb runas es la única forma de pedir
                // elevación desde un proceso sin privilegios.
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(30_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Win32Exception incluye el caso "el usuario canceló el UAC".
            if (!ignoreFailure)
            {
                Log.Error($"Falló netsh: {ex.Message}");
            }
            return false;
        }
    }
}
