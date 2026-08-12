using AirPlayReceiver.App.Config;

namespace AirPlayReceiver.App.Engine;

/// <summary>
/// Traduce la configuración del receptor a la línea de comandos de UxPlay.
/// </summary>
public static class EngineArguments
{
    /// <summary>
    /// Construye la lista de argumentos. Se devuelve como lista y no como
    /// string para poder pasarla por <c>ProcessStartInfo.ArgumentList</c>, que
    /// se encarga del entrecomillado (un nombre de aula con espacios rompería
    /// una línea de comandos armada a mano).
    /// </summary>
    public static IReadOnlyList<string> Build(ReceiverConfig config, string receiverName)
    {
        ArgumentNullException.ThrowIfNull(config);

        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(receiverName))
        {
            args.Add("-n");
            args.Add(receiverName);
        }

        if (!config.AppendHostname)
        {
            // Sin esto UxPlay publica "Aula 203@PC-AULA-203", que en el selector
            // del iPad se lee peor.
            args.Add("-nh");
        }

        if (!string.IsNullOrWhiteSpace(config.VideoSink))
        {
            args.Add("-vs");
            args.Add(config.VideoSink);
        }

        if (!string.IsNullOrWhiteSpace(config.AudioSink))
        {
            args.Add("-as");
            args.Add(config.AudioSink);
        }

        if (config.Fullscreen)
        {
            args.Add("-fs");
        }

        foreach (var extra in config.ExtraArguments)
        {
            if (!string.IsNullOrWhiteSpace(extra))
            {
                args.Add(extra);
            }
        }

        return args;
    }

    /// <summary>Representación legible, solo para mostrar en el log.</summary>
    public static string Describe(IReadOnlyList<string> arguments)
        => string.Join(' ', arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
}
