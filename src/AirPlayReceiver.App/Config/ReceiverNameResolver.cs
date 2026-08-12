using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AirPlayReceiver.App.Config;

/// <summary>
/// Construye el nombre con el que la PC se anuncia por AirPlay a partir de una
/// plantilla y del hostname del equipo.
///
/// El objetivo es que una única imagen de instalación sirva para todas las PCs:
/// en vez de configurar máquina por máquina, el nombre del aula se deriva del
/// hostname que ya asigna el área de sistemas.
/// </summary>
public static class ReceiverNameResolver
{
    /// <summary>
    /// Límite de un nombre de servicio DNS-SD. Es de bytes UTF-8, no de
    /// caracteres: "Aula Ñandú" ocupa más bytes que letras.
    /// </summary>
    public const int MaxNameLengthBytes = 63;

    /// <summary>Nombre usado si la plantilla y el hostname quedan vacíos.</summary>
    public const string LastResortName = "AirPlay";

    private static readonly Regex TokenPattern =
        new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    private static readonly Regex WhitespaceRun =
        new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Una regex mal escrita en la config no puede colgar el arranque del
    /// receptor, así que el matcheo corre con timeout.
    /// </summary>
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(250);

    public static string Resolve(
        ReceiverConfig config,
        string? hostname,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        hostname ??= string.Empty;

        var tokens = BuildTokens(hostname, environment);
        var template = SelectTemplate(config, hostname, tokens);
        var expanded = Expand(template, tokens);
        var normalized = Normalize(expanded);

        if (normalized.Length == 0)
        {
            normalized = Normalize(hostname);
        }
        if (normalized.Length == 0)
        {
            normalized = LastResortName;
        }

        return TruncateToBytes(normalized, MaxNameLengthBytes);
    }

    private static Dictionary<string, string> BuildTokens(
        string hostname,
        IReadOnlyDictionary<string, string>? environment)
    {
        // Los tokens son case-insensitive: {COMPUTERNAME} y {computername} son
        // lo mismo, que es lo que espera quien escribe la plantilla a mano.
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (environment is null)
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    tokens[key] = value;
                }
            }
        }
        else
        {
            foreach (var (key, value) in environment)
            {
                tokens[key] = value;
            }
        }

        // El hostname que nos pasan gana sobre la variable de entorno: en tests
        // y en equipos recién renombrados son distintos.
        tokens["COMPUTERNAME"] = hostname;
        tokens["HOSTNAME"] = hostname;

        return tokens;
    }

    private static string SelectTemplate(
        ReceiverConfig config,
        string hostname,
        Dictionary<string, string> tokens)
    {
        if (string.IsNullOrWhiteSpace(config.HostnamePattern))
        {
            return config.NameTemplate;
        }

        Match match;
        try
        {
            match = Regex.Match(
                hostname,
                config.HostnamePattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                PatternTimeout);
        }
        catch (ArgumentException)
        {
            // Regex inválida en la config: se degrada al fallback en vez de
            // impedir que el receptor arranque.
            return config.EffectiveFallbackTemplate;
        }
        catch (RegexMatchTimeoutException)
        {
            return config.EffectiveFallbackTemplate;
        }

        if (!match.Success)
        {
            return config.EffectiveFallbackTemplate;
        }

        // Grupos con nombre y numerados quedan disponibles como tokens.
        foreach (var groupName in match.Groups.Keys)
        {
            var group = match.Groups[groupName];
            if (group.Success)
            {
                tokens[groupName] = group.Value;
            }
        }

        return config.NameTemplate;
    }

    private static string Expand(string? template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        // Un token desconocido se resuelve a vacío en vez de quedar literal:
        // es preferible "Aula" a que el proyector muestre "Aula {aula}".
        return TokenPattern.Replace(
            template,
            m => tokens.TryGetValue(m.Groups[1].Value, out var value) ? value : string.Empty);
    }

    private static string Normalize(string value)
        => WhitespaceRun.Replace(value ?? string.Empty, " ").Trim();

    /// <summary>
    /// Recorta a <paramref name="maxBytes"/> en UTF-8 sin partir un carácter ni
    /// un cluster de grafemas por la mitad.
    /// </summary>
    private static string TruncateToBytes(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var builder = new StringBuilder();
        var bytes = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(value);

        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;
            var elementBytes = Encoding.UTF8.GetByteCount(element);
            if (bytes + elementBytes > maxBytes)
            {
                break;
            }
            builder.Append(element);
            bytes += elementBytes;
        }

        return builder.ToString().TrimEnd();
    }
}
