using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace Jumpscare.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string imgPath;
    private GIFConvert? tongueGif;
    private bool preloadStarted = false;
    private bool resourcesLoaded = false;
    private bool preloadDone = false;

    private DateTime lastFrameTime;
    private DateTime? triggerTime = null;
    private TimeSpan delay = TimeSpan.Zero;
    private readonly Random rng = new();
    private Task? preloadTask;

    private bool soundPlayed = false;

    private readonly string? soundPath;

    public MainWindow(string tongueImagePath, string? wavPath)
        : base("Jumpscare##HiddenID",
               ImGuiWindowFlags.NoTitleBar
             | ImGuiWindowFlags.NoScrollbar
             | ImGuiWindowFlags.NoDecoration
             | ImGuiWindowFlags.NoFocusOnAppearing
             | ImGuiWindowFlags.NoNavFocus
             | ImGuiWindowFlags.NoInputs
             | ImGuiWindowFlags.NoMouseInputs
             | ImGuiWindowFlags.NoBackground)
    {
        imgPath = tongueImagePath;
        soundPath = wavPath;
        lastFrameTime = DateTime.Now;
    }


    public void Dispose() => tongueGif?.Dispose();

    private void BeginPreload()
    {
        if (preloadStarted) return;
        preloadStarted = true;

        preloadTask = Task.Run(() =>
        {
            if (!File.Exists(imgPath))
            {
                Plugin.Log.Error($"Image not found: {imgPath}");
                resourcesLoaded = true;
                return;
            }

            if (Path.GetExtension(imgPath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    tongueGif = new GIFConvert(imgPath);

                    Plugin.Framework.RunOnFrameworkThread(() =>
                    {
                        tongueGif.EnsureTexturesLoaded();
                        preloadDone = true;
                        resourcesLoaded = true;
                    });
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"GIF preload failed: {ex}");
                    resourcesLoaded = true;
                }
            }
            else
            {
                resourcesLoaded = true;
            }
        });
    }

    private void ScheduleNextTrigger()
    {
        int seconds = rng.Next(10, 100); // Random delay between 10-100s
        delay = TimeSpan.FromSeconds(seconds);
        triggerTime = DateTime.Now + delay;
        Plugin.Log.Information($"Next jumpscare scheduled in {seconds} seconds (at {triggerTime}).");
    }

    private void ResetGIF()
    {
        tongueGif?.Dispose();
        tongueGif = null;
        preloadStarted = false;
        preloadDone = false;
        resourcesLoaded = false;
        triggerTime = null;

        // Start preload again
        BeginPreload();

        // Schedule next random trigger
        ScheduleNextTrigger();
    }

    public void Toggle()
    {
        if (!IsOpen)
        {
            IsOpen = true;
            BeginPreload();
            ScheduleNextTrigger();
        }
        else
        {
            Plugin.Log.Information("Jumpscare cancelled.");
            triggerTime = null;
            IsOpen = false;
        }
    }

    public override void Draw()
    {
        Vector2 windowSize = ImGui.GetMainViewport().Size;
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);

        ImGui.Begin("MainWindow",
                    ImGuiWindowFlags.NoScrollbar
                  | ImGuiWindowFlags.NoTitleBar
                  | ImGuiWindowFlags.NoDecoration
                  | ImGuiWindowFlags.NoFocusOnAppearing
                  | ImGuiWindowFlags.NoNavFocus
                  | ImGuiWindowFlags.NoInputs
                  | ImGuiWindowFlags.NoMouseInputs
                  | ImGuiWindowFlags.NoBackground);

        // Wait until trigger time
        if (!triggerTime.HasValue)
        {
            ImGui.End();
            return;
        }

        if (DateTime.Now < triggerTime.Value)
        {
            var remaining = triggerTime.Value - DateTime.Now;
            ImGui.TextUnformatted($"Waiting... {remaining.TotalSeconds:F1}s");
            ImGui.End();
            return;
        }

        // Wait until GIF textures are loaded
        if (!preloadDone)
        {
            ImGui.TextUnformatted("Preparing jumpscare...");
            ImGui.End();
            return;
        }

        if (tongueGif != null && tongueGif.FramePaths.Count > 0)
        {
            var now = DateTime.Now;
            float deltaMs = (float)(now - lastFrameTime).TotalMilliseconds;
            lastFrameTime = now;

            if (!soundPlayed && File.Exists(soundPath))
            {
                var player = new System.Media.SoundPlayer(soundPath);
                player.Play(); // non-blocking
                soundPlayed = true;
            }

            tongueGif.Update(deltaMs);

            float alpha = 1f;
            if (tongueGif.Finished)
            {
                alpha = 1f - Math.Min(tongueGif.FadeTimer / tongueGif.FadeDurationMs, 1f);
                if (alpha <= 0f)
                {
                    ResetGIF();
                    soundPlayed = false;
                    ImGui.End();
                    return;
                }
            }

            tongueGif.Render(windowSize, alpha);
        }


        else if (resourcesLoaded)
        {
            ImGui.TextUnformatted($"Image not found or not a GIF: {imgPath}");
        }

        ImGui.End();
    }
}
