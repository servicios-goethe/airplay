using System.Text;

namespace AirPlayReceiver.App.Diagnostics;

/// <summary>
/// Log a archivo, rotado por tamaño. La app corre sin consola en la bandeja del
/// sistema, así que sin esto un fallo en un aula es invisible.
/// </summary>
public static class Log
{
    private const long MaxSizeBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoetheAirPlay",
        "logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "receptor.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    /// <summary>Línea emitida por el proceso del motor, se registra sin nivel propio.</summary>
    public static void Engine(string message) => Write("MOTOR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                Rotate();
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Si no se puede escribir el log no hay nada mejor que hacer:
                // fallar acá dejaría el receptor caído por un problema de disco.
            }
        }
    }

    private static void Rotate()
    {
        var file = new FileInfo(LogFilePath);
        if (!file.Exists || file.Length < MaxSizeBytes)
        {
            return;
        }

        var previous = LogFilePath + ".1";
        File.Delete(previous);
        File.Move(LogFilePath, previous);
    }
}
