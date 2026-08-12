namespace AirPlayReceiver.App.Engine;

/// <summary>
/// Ubica los archivos del motor UxPlay y arma el entorno que necesita GStreamer
/// para correr embebido, sin GStreamer instalado en la máquina.
/// </summary>
public sealed class EnginePaths
{
    /// <summary>Permite apuntar a otra carpeta de motor durante el desarrollo.</summary>
    public const string OverrideVariable = "AIRPLAY_ENGINE_DIR";

    public EnginePaths(string? engineDirectory = null)
    {
        EngineDirectory = engineDirectory
            ?? Environment.GetEnvironmentVariable(OverrideVariable)
            ?? Path.Combine(AppContext.BaseDirectory, "engine");
    }

    public string EngineDirectory { get; }

    public string ExecutablePath => Path.Combine(EngineDirectory, "uxplay.exe");

    public string PluginDirectory => Path.Combine(EngineDirectory, "lib", "gstreamer-1.0");

    public string PluginScannerPath => Path.Combine(PluginDirectory, "gst-plugin-scanner.exe");

    /// <summary>
    /// Caché del registro de plugins. Va sí o sí a una ruta escribible por el
    /// usuario: si GStreamer intenta escribirla junto al ejecutable, en una
    /// instalación bajo "Archivos de programa" falla y el video no arranca.
    /// </summary>
    public string RegistryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoetheAirPlay",
        "gst-registry.bin");

    public bool IsInstalled => File.Exists(ExecutablePath);

    /// <summary>
    /// Escribe en <paramref name="environment"/> las variables que hacen que el
    /// GStreamer embebido encuentre sus propios plugins y no los de una
    /// instalación ajena que hubiera en la PC.
    /// </summary>
    public void ApplyTo(IDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);

        environment["GST_PLUGIN_PATH"] = PluginDirectory;
        environment["GST_PLUGIN_SYSTEM_PATH"] = PluginDirectory;
        environment["GST_PLUGIN_SCANNER"] = PluginScannerPath;
        environment["GST_REGISTRY"] = RegistryPath;

        // Evita que una instalación de GStreamer preexistente inyecte rutas de
        // plugins incompatibles con las DLLs que empaquetamos.
        environment["GST_PLUGIN_SYSTEM_PATH_1_0"] = PluginDirectory;
        environment["GST_PLUGIN_PATH_1_0"] = PluginDirectory;

        // Las DLLs del motor viven junto al .exe; anteponerlas al PATH evita
        // cargar por accidente una versión distinta que esté en el sistema.
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        environment["PATH"] = EngineDirectory + Path.PathSeparator + currentPath;
    }
}
