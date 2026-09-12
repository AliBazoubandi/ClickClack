using System.IO;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class SoundServiceTests
{
    [TestMethod]
    public void Test_SoundService_Disabled_NoOp()
    {
        bool original = SoundService.Enabled;
        try
        {
            SoundService.Enabled = false;
            // When disabled, calls return immediately and must never throw
            SoundService.Play(SoundKind.Clack);
            SoundService.Play(SoundKind.Pop);
            SoundService.Play(SoundKind.Slide);
        }
        finally
        {
            SoundService.Enabled = original;
        }
    }

    [TestMethod]
    public void Test_SoundService_Enabled_GracefulHeadlessOrMissingDevice()
    {
        bool original = SoundService.Enabled;
        try
        {
            SoundService.Enabled = true;
            // In headless CI or environments with no audio hardware, SoundService catches
            // and suppresses errors gracefully without unhandled exceptions.
            SoundService.Play(SoundKind.Clack);
            SoundService.Play(SoundKind.Pop);
            SoundService.Play(SoundKind.Slide);
        }
        finally
        {
            SoundService.Enabled = original;
        }
    }

    [TestMethod]
    public void Test_SoundService_CreateAndLoadPlayer_ForcesSynchronousLoad()
    {
        // Minimal valid 44-byte PCM WAV header (silent 0 samples)
        byte[] wavHeader = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            36, 0, 0, 0, // chunk size (36 + data size)
            (byte)'W', (byte)'A', (byte)'V', (byte)'E',
            (byte)'f', (byte)'m', (byte)'t', (byte)' ',
            16, 0, 0, 0, // subchunk1 size (16 for PCM)
            1, 0,        // audio format 1 = PCM
            1, 0,        // num channels = 1
            0x44, 0xAC, 0, 0, // sample rate = 44100
            0x88, 0x58, 0x01, 0, // byte rate = 44100 * 1 * 16 / 8 = 88200
            2, 0,        // block align = 2
            16, 0,       // bits per sample = 16
            (byte)'d', (byte)'a', (byte)'t', (byte)'a',
            0, 0, 0, 0   // subchunk2 size (0 bytes of data)
        };

        using var ms = new MemoryStream(wavHeader);
        using var player = SoundService.CreateAndLoadPlayer(ms);
        Assert.IsTrue(player.IsLoadCompleted, "SoundPlayer must have completed synchronous load before Play()");
    }
}
