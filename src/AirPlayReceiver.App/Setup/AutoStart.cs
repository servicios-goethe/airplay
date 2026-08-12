using Microsoft.Win32;
using AirPlayReceiver.App.Diagnostics;

namespace AirPlayReceiver.App.Setup;

/// <summary>
/// Arranque automático con la sesión, vía la clave <c>Run</c> del usuario.
///
/// Se usa HKCU y no HKLM a propósito: el receptor necesita una sesión con
/// escritorio para poder mostrar el espejado, así que no tiene sentido
/// arrancarlo a nivel máquina antes de que alguien inicie sesión.
/// </summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GoetheAirPlayReceiver";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Warn($"No se pudo leer el estado de auto-inicio: {ex.Message}");
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
                ?? throw new IOException($"No se pudo abrir HKCU\\{RunKey}");

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            Log.Info($"Auto-inicio {(enabled ? "activado" : "desactivado")}.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Error($"No se pudo cambiar el auto-inicio: {ex.Message}");
            return false;
        }
    }
}
