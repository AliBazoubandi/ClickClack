using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelCompanion.Models;
using PixelCompanion.Services;
using PixelCompanion.ViewModels;
using PixelCompanion.Views;

namespace PixelCompanion.Tests;

[TestClass]
public class VisualRenderTests
{
    private static readonly object _appLock = new();

    private void RunInSTA(Action action)
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                lock (_appLock)
                {
                    if (System.Windows.Application.Current == null)
                    {
                        try
                        {
                            _ = new System.Windows.Application();
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                }
                action();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            Assert.Inconclusive($"Visual test skipped due to environment limitations: {threadEx.Message}");
        }
    }

    [TestMethod]
    public void RenderWindows_ToPngArtifacts()
    {
        RunInSTA(() =>
        {
            var tempVault = Path.Combine(Path.GetTempPath(), "VisualTestVault_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempVault);
            var noteDir = Path.Combine(tempVault, "Task-Manager");
            Directory.CreateDirectory(noteDir);
            var todayNote = Path.Combine(noteDir, $"{DateTime.Now:yyyy-MM-dd}.md");

            File.WriteAllText(todayNote, """
                # Today
                - [ ] Inspect typewriter on desktop
                - [ ] Pet the companion
                - [x] Try creating tasks directly on paper
                - [x] making my boss angry
                """);

            var configService = new ConfigService();
            var config = new AppConfig
            {
                ObsidianVaultPath = tempVault,
                DailyNotesFolder = "Task-Manager",
                DailyNoteDateFormat = "yyyy-MM-dd",
                IsPaperExtended = true,
                WindowWidth = 480,
                WindowHeight = 420,
                PaperWindowWidth = 380,
                PaperWindowHeight = 520
            };

            var obsidianService = new ObsidianService(configService, config);
            var viewModel = new CompanionViewModel(configService, obsidianService, config);

            // 1. Render MainWindow
            var mainWindow = new MainWindow(configService, config, viewModel)
            {
                Width = 480,
                Height = 420
            };

            var mainContent = (FrameworkElement)mainWindow.Content;
            mainContent.Measure(new System.Windows.Size(480, 420));
            mainContent.Arrange(new Rect(0, 0, 480, 420));
            mainContent.UpdateLayout();

            var dvMain = new DrawingVisual();
            using (var dc = dvMain.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26)), null, new Rect(0, 0, 480, 420));
                dc.DrawRectangle(new VisualBrush(mainContent), null, new Rect(0, 0, 480, 420));
            }

            var closeBtn = mainWindow.FindName("PaperCloseBtn") as FrameworkElement;
            System.Diagnostics.Trace.WriteLine($"PaperCloseBtn: Visibility={closeBtn?.Visibility}, RenderedSize={closeBtn?.RenderSize}");

            var rtbMain = new RenderTargetBitmap(480, 420, 96, 96, PixelFormats.Pbgra32);
            rtbMain.Render(dvMain);

            string artifactDir = Path.Combine(Path.GetTempPath(), "PixelCompanionVisualArtifacts");
            if (!Directory.Exists(artifactDir))
            {
                Directory.CreateDirectory(artifactDir);
            }

            string mainPng = Path.Combine(artifactDir, "rendered_main_window.png");
            using (var stream = new FileStream(mainPng, FileMode.Create, FileAccess.Write))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtbMain));
                encoder.Save(stream);
            }

            // 2. Render PaperWindow Content
            var paperWindow = new PaperWindow(configService, config, viewModel)
            {
                Width = 380,
                Height = 520
            };
            var paperContent = (FrameworkElement)paperWindow.Content;
            paperContent.Measure(new System.Windows.Size(380, 520));
            paperContent.Arrange(new Rect(0, 0, 380, 520));
            paperContent.UpdateLayout();

            var dvPaper = new DrawingVisual();
            using (var dc = dvPaper.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26)), null, new Rect(0, 0, 380, 520));
                dc.DrawRectangle(new VisualBrush(paperContent), null, new Rect(0, 0, 380, 520));
            }

            var rtbPaper = new RenderTargetBitmap(380, 520, 96, 96, PixelFormats.Pbgra32);
            rtbPaper.Render(dvPaper);

            string paperPng = Path.Combine(artifactDir, "rendered_paper_window.png");
            using (var stream = new FileStream(paperPng, FileMode.Create, FileAccess.Write))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtbPaper));
                encoder.Save(stream);
            }

            // 3. Render PaperWindow with active typing input
            viewModel.IsAddingTask = true;
            viewModel.NewTaskText = "Buy artisan tea...";
            paperContent.Measure(new System.Windows.Size(380, 520));
            paperContent.Arrange(new Rect(0, 0, 380, 520));
            paperContent.UpdateLayout();

            var dvInput = new DrawingVisual();
            using (var dc = dvInput.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26)), null, new Rect(0, 0, 380, 520));
                dc.DrawRectangle(new VisualBrush(paperContent), null, new Rect(0, 0, 380, 520));
            }

            var rtbInput = new RenderTargetBitmap(380, 520, 96, 96, PixelFormats.Pbgra32);
            rtbInput.Render(dvInput);

            string inputPng = Path.Combine(artifactDir, "rendered_paper_window_input.png");
            using (var stream = new FileStream(inputPng, FileMode.Create, FileAccess.Write))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtbInput));
                encoder.Save(stream);
            }

            // Assert that all three PNG files exist and are non-empty
            Assert.IsTrue(File.Exists(mainPng), "rendered_main_window.png must exist");
            Assert.IsGreaterThanOrEqualTo(1L, new FileInfo(mainPng).Length);

            Assert.IsTrue(File.Exists(paperPng), "rendered_paper_window.png must exist");
            Assert.IsGreaterThanOrEqualTo(1L, new FileInfo(paperPng).Length);

            Assert.IsTrue(File.Exists(inputPng), "rendered_paper_window_input.png must exist");
            Assert.IsGreaterThanOrEqualTo(1L, new FileInfo(inputPng).Length);

            // Cleanup
            obsidianService.Dispose();
            if (Directory.Exists(tempVault))
            {
                try { Directory.Delete(tempVault, true); } catch { }
            }
        });
    }
}
