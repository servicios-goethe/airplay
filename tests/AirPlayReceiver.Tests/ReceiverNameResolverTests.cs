using System.Text;
using AirPlayReceiver.App.Config;
using Xunit;

namespace AirPlayReceiver.Tests;

public class ReceiverNameResolverTests
{
    private static readonly Dictionary<string, string> NoEnvironment = new();

    [Fact]
    public void Default_template_uses_the_machine_name()
    {
        var config = new ReceiverConfig();

        var name = ReceiverNameResolver.Resolve(config, "PC-AULA-203", NoEnvironment);

        Assert.Equal("PC-AULA-203", name);
    }

    [Fact]
    public void Named_group_from_hostname_feeds_the_template()
    {
        // El caso que motiva la feature: una sola imagen para todas las PCs.
        var config = new ReceiverConfig
        {
            HostnamePattern = @"^PC-AULA-(?<aula>\d+)$",
            NameTemplate = "Aula {aula}",
        };

        var name = ReceiverNameResolver.Resolve(config, "PC-AULA-203", NoEnvironment);

        Assert.Equal("Aula 203", name);
    }

    [Fact]
    public void Numbered_groups_are_available_as_tokens()
    {
        var config = new ReceiverConfig
        {
            HostnamePattern = @"^(\w+)-(\d+)$",
            NameTemplate = "{1} {2}",
        };

        var name = ReceiverNameResolver.Resolve(config, "LAB-7", NoEnvironment);

        Assert.Equal("LAB 7", name);
    }

    [Fact]
    public void Hostname_outside_the_convention_falls_back()
    {
        var config = new ReceiverConfig
        {
            HostnamePattern = @"^PC-AULA-(?<aula>\d+)$",
            NameTemplate = "Aula {aula}",
            FallbackNameTemplate = "{COMPUTERNAME}",
        };

        var name = ReceiverNameResolver.Resolve(config, "NOTEBOOK-DIRECCION", NoEnvironment);

        Assert.Equal("NOTEBOOK-DIRECCION", name);
    }

    [Fact]
    public void Invalid_pattern_falls_back_instead_of_throwing()
    {
        // Una regex rota en la config desplegada no puede impedir que arranque
        // el receptor del aula.
        var config = new ReceiverConfig
        {
            HostnamePattern = "([unclosed",
            NameTemplate = "Aula {aula}",
            FallbackNameTemplate = "{COMPUTERNAME}",
        };

        var name = ReceiverNameResolver.Resolve(config, "PC-AULA-203", NoEnvironment);

        Assert.Equal("PC-AULA-203", name);
    }

    [Fact]
    public void Unknown_tokens_are_dropped_and_spacing_collapses()
    {
        var config = new ReceiverConfig { NameTemplate = "Aula {inexistente} 12" };

        var name = ReceiverNameResolver.Resolve(config, "PC1", NoEnvironment);

        Assert.Equal("Aula 12", name);
    }

    [Fact]
    public void Environment_variables_resolve_as_tokens()
    {
        var config = new ReceiverConfig { NameTemplate = "{EDIFICIO} {COMPUTERNAME}" };
        var environment = new Dictionary<string, string> { ["EDIFICIO"] = "Sede Norte" };

        var name = ReceiverNameResolver.Resolve(config, "PC1", environment);

        Assert.Equal("Sede Norte PC1", name);
    }

    [Fact]
    public void Tokens_are_case_insensitive()
    {
        var config = new ReceiverConfig { NameTemplate = "{computername}" };

        var name = ReceiverNameResolver.Resolve(config, "PC-AULA-9", NoEnvironment);

        Assert.Equal("PC-AULA-9", name);
    }

    [Fact]
    public void Empty_result_falls_back_to_the_hostname()
    {
        var config = new ReceiverConfig { NameTemplate = "{nada}" };

        var name = ReceiverNameResolver.Resolve(config, "PC-AULA-5", NoEnvironment);

        Assert.Equal("PC-AULA-5", name);
    }

    [Fact]
    public void Falls_back_to_a_constant_when_everything_is_empty()
    {
        var config = new ReceiverConfig { NameTemplate = string.Empty };

        var name = ReceiverNameResolver.Resolve(config, string.Empty, NoEnvironment);

        Assert.Equal(ReceiverNameResolver.LastResortName, name);
    }

    [Fact]
    public void Long_names_are_truncated_to_the_dns_sd_byte_limit()
    {
        var config = new ReceiverConfig { NameTemplate = new string('A', 200) };

        var name = ReceiverNameResolver.Resolve(config, "PC1", NoEnvironment);

        Assert.Equal(ReceiverNameResolver.MaxNameLengthBytes, Encoding.UTF8.GetByteCount(name));
    }

    [Fact]
    public void Truncation_counts_bytes_and_never_splits_a_character()
    {
        // "ñ" ocupa 2 bytes en UTF-8: el limite es de bytes, no de caracteres,
        // y cortar por la mitad produciria un nombre mDNS invalido.
        var config = new ReceiverConfig { NameTemplate = new string('ñ', 100) };

        var name = ReceiverNameResolver.Resolve(config, "PC1", NoEnvironment);

        Assert.True(Encoding.UTF8.GetByteCount(name) <= ReceiverNameResolver.MaxNameLengthBytes);
        Assert.DoesNotContain('�', name);
        Assert.Equal(31, name.Length);
    }
}
