using System.Text.Json.Serialization;

namespace AirPlayReceiver.App.Config;

/// <summary>
/// Configuración del receptor. Se serializa a JSON en disco.
/// </summary>
public sealed class ReceiverConfig
{
    /// <summary>
    /// Plantilla del nombre con el que la PC se anuncia por AirPlay.
    /// Admite tokens <c>{NOMBRE}</c> que se resuelven contra las variables de
    /// entorno y contra los grupos capturados por <see cref="HostnamePattern"/>.
    /// </summary>
    public string NameTemplate { get; set; } = "{COMPUTERNAME}";

    /// <summary>
    /// Regex opcional aplicada al hostname para extraer partes reutilizables en
    /// la plantilla. Los grupos con nombre quedan disponibles como tokens.
    /// Ejemplo: <c>^PC-AULA-(?&lt;aula&gt;\d+)$</c> junto con
    /// <see cref="NameTemplate"/> = <c>"Aula {aula}"</c>.
    /// </summary>
    public string? HostnamePattern { get; set; }

    /// <summary>
    /// Plantilla a usar cuando <see cref="HostnamePattern"/> está definida pero
    /// no matchea el hostname del equipo. Evita que una PC fuera de convención
    /// se anuncie con un nombre a medio construir.
    /// </summary>
    public string FallbackNameTemplate { get; set; } = "{COMPUTERNAME}";

    /// <summary>
    /// Si es <c>false</c> se pasa <c>-nh</c> a UxPlay para que no agregue el
    /// sufijo <c>@hostname</c> al nombre publicado.
    /// </summary>
    public bool AppendHostname { get; set; }

    /// <summary>Sink de video de GStreamer. D3D11 es el acelerado en Windows.</summary>
    public string VideoSink { get; set; } = "d3d11videosink";

    /// <summary>Sink de audio de GStreamer.</summary>
    public string AudioSink { get; set; } = "wasapisink";

    /// <summary>Abrir el espejado en pantalla completa.</summary>
    public bool Fullscreen { get; set; }

    /// <summary>Arrancar el motor apenas se abre la app.</summary>
    public bool StartEngineOnLaunch { get; set; } = true;

    /// <summary>
    /// Argumentos extra que se agregan tal cual a la línea de comandos de
    /// UxPlay. Escotilla de escape para opciones que la app todavía no expone
    /// (por ejemplo <c>-pin</c> o <c>-p</c>).
    /// </summary>
    public List<string> ExtraArguments { get; set; } = new();

    [JsonIgnore]
    public string EffectiveFallbackTemplate =>
        string.IsNullOrWhiteSpace(FallbackNameTemplate) ? NameTemplate : FallbackNameTemplate;
}
