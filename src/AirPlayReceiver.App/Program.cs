using AirPlayReceiver.App.Diagnostics;

namespace AirPlayReceiver.App;

internal static class Program
{
    /// <summary>
    /// Global\ hace que el mutex sea visible entre sesiones: en una PC de aula
    /// con cambio rápido de usuario, dos instancias intentarían publicar el
    /// mismo servicio mDNS y el iPad vería el aula duplicada.
    /// </summary>
    private const string SingleInstanceMutexName = @"Global\GoetheAirPlayReceiver";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            Log.Info("Ya hay una instancia corriendo; esta se cierra.");
            return;
        }

        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, e) =>
            Log.Error($"Excepción no controlada en la UI: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error($"Excepción no controlada: {e.ExceptionObject}");

        Log.Info("Receptor AirPlay iniciado.");

        using var context = new TrayApplicationContext();
        Application.Run(context);

        Log.Info("Receptor AirPlay finalizado.");
    }
}
