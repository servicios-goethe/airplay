using System.Text.Json;

namespace AirPlayReceiver.App.Config;

/// <summary>
/// Carga y guarda la configuración del receptor.
///
/// Hay dos ubicaciones posibles y la de máquina gana <em>entera</em> sobre la de
/// usuario: en un despliegue por GPO el área de sistemas necesita garantizar el
/// nombre del aula, y una fusión clave por clave haría que un usuario pudiera
/// pisar solo algunos campos y dejar el equipo en un estado difícil de explicar.
/// </summary>
public sealed class ConfigStore
{
    private const string FolderName = "GoetheAirPlay";
    private const string FileName = "config.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Config por máquina, la que fija el despliegue. Tiene prioridad.</summary>
    public static string MachineConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        FolderName,
        FileName);

    /// <summary>Config por usuario, editable desde la app.</summary>
    public static string UserConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName,
        FileName);

    /// <summary>Ruta efectivamente usada en la última carga, o <c>null</c> si se usaron los valores por defecto.</summary>
    public string? LoadedFrom { get; private set; }

    /// <summary><c>true</c> si manda la config de máquina; la app no debe ofrecer editarla.</summary>
    public bool IsMachineManaged { get; private set; }

    public ReceiverConfig Load()
    {
        foreach (var path in new[] { MachineConfigPath, UserConfigPath })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<ReceiverConfig>(json, SerializerOptions);
                if (config is not null)
                {
                    LoadedFrom = path;
                    IsMachineManaged = path == MachineConfigPath;
                    return config;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Una config corrupta no puede dejar el aula sin receptor:
                // se ignora el archivo y se sigue con el siguiente candidato.
                AirPlayReceiver.App.Diagnostics.Log.Warn(
                    $"No se pudo leer la configuración '{path}': {ex.Message}");
            }
        }

        LoadedFrom = null;
        IsMachineManaged = false;
        return new ReceiverConfig();
    }

    public void SaveUserConfig(ReceiverConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var path = UserConfigPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, SerializerOptions));
    }
}
