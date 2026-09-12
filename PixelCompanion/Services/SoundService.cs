using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;

namespace PixelCompanion.Services;

public enum SoundKind
{
    Clack,
    Pop,
    Slide
}

public static class SoundService
{
    public static bool Enabled { get; set; } = true;

    // Design Decision & Documentation:
    // We use System.Media.SoundPlayer from the .NET BCL because it supports playing directly from an
    // in-memory Stream (Application.GetResourceStream) without requiring any external native or NuGet
    // dependencies (e.g. NAudio). The inherent limitation of SoundPlayer is that it lacks software volume
    // control or attenuation APIs. Therefore, all sound volumes are intentionally kept quiet and gentle by
    // baking low peak amplitudes directly into the audio synthesis parameters in scripts/generate_sounds.py.
    public static void Play(SoundKind kind)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            string? soundFileName = kind switch
            {
                SoundKind.Clack => "clack.wav",
                SoundKind.Pop => "pop.wav",
                SoundKind.Slide => "slide.wav",
                _ => null
            };

            if (soundFileName == null)
            {
                return;
            }

            var packUri = new Uri($"pack://application:,,,/ClickClack;component/Assets/Sounds/{soundFileName}", UriKind.Absolute);
            var streamResourceInfo = System.Windows.Application.GetResourceStream(packUri);
            if (streamResourceInfo?.Stream != null)
            {
                using var stream = streamResourceInfo.Stream;
                using var player = CreateAndLoadPlayer(stream);
                player.Play();
            }
        }
        catch (Exception ex)
        {
            // Missing resource, audio device unavailable, or test runner headless environment
            // must remain a silent no-op and never crash the application.
            Debug.WriteLine($"SoundService.Play({kind}) error: {ex.Message}");
        }
    }

    internal static SoundPlayer CreateAndLoadPlayer(Stream stream)
    {
        var player = new SoundPlayer(stream);
        player.Load();
        return player;
    }
}
