using AirPlayReceiver.App.Config;
using AirPlayReceiver.App.Engine;
using Xunit;

namespace AirPlayReceiver.Tests;

public class EngineArgumentsTests
{
    [Fact]
    public void Publishes_the_receiver_name()
    {
        var args = EngineArguments.Build(new ReceiverConfig(), "Aula 203").ToList();

        var index = args.IndexOf("-n");
        Assert.True(index >= 0, "faltó el flag -n");
        Assert.Equal("Aula 203", args[index + 1]);
    }

    [Fact]
    public void Name_stays_a_single_argument_even_with_spaces()
    {
        // Se pasa por ArgumentList, así que el nombre no debe venir pre-citado
        // ni partido: UxPlay tiene que recibir "Aula 203" como un solo argv.
        var args = EngineArguments.Build(new ReceiverConfig(), "Aula 203").ToList();

        Assert.Contains("Aula 203", args);
        Assert.DoesNotContain("\"Aula", args);
    }

    [Fact]
    public void Suppresses_the_hostname_suffix_by_default()
    {
        var args = EngineArguments.Build(new ReceiverConfig(), "Aula 203").ToList();

        Assert.Contains("-nh", args);
    }

    [Fact]
    public void Keeps_the_hostname_suffix_when_requested()
    {
        var config = new ReceiverConfig { AppendHostname = true };

        var args = EngineArguments.Build(config, "Aula 203").ToList();

        Assert.DoesNotContain("-nh", args);
    }

    [Fact]
    public void Passes_the_configured_sinks()
    {
        var config = new ReceiverConfig { VideoSink = "d3d11videosink", AudioSink = "wasapisink" };

        var args = EngineArguments.Build(config, "Aula").ToList();

        Assert.Equal("d3d11videosink", args[args.IndexOf("-vs") + 1]);
        Assert.Equal("wasapisink", args[args.IndexOf("-as") + 1]);
    }

    [Fact]
    public void Omits_sinks_when_left_blank()
    {
        var config = new ReceiverConfig { VideoSink = "", AudioSink = "  " };

        var args = EngineArguments.Build(config, "Aula").ToList();

        Assert.DoesNotContain("-vs", args);
        Assert.DoesNotContain("-as", args);
    }

    [Fact]
    public void Adds_fullscreen_only_when_enabled()
    {
        Assert.DoesNotContain("-fs", EngineArguments.Build(new ReceiverConfig(), "Aula"));
        Assert.Contains("-fs", EngineArguments.Build(new ReceiverConfig { Fullscreen = true }, "Aula"));
    }

    [Fact]
    public void Appends_extra_arguments_verbatim()
    {
        var config = new ReceiverConfig { ExtraArguments = { "-pin", "" } };

        var args = EngineArguments.Build(config, "Aula").ToList();

        Assert.Contains("-pin", args);
        Assert.DoesNotContain("", args);
    }
}
