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

    private DateTime lastFrameTime;
    private bool preloadDone = false;

    private DateTime? triggerTime = null;
    private TimeSpan delay = TimeSpan.Zero;
    private readonly Random rng = new();
    private Task? preloadTask;

    public MainWindow(string tongueImagePath)
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
        Size = ImGui.GetMainViewport().Size;
        imgPath = tongueImagePath;
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
                    });
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"GIF preload failed: {ex}");
                    resourcesLoaded = true;
                }
            }
            else resourcesLoaded = true;
        });
    }

    public void Toggle()
    {
        if (!IsOpen)
        {
            int seconds = rng.Next(10,100); //delay between 10 and 100 seconds
            delay = TimeSpan.FromSeconds(seconds);
            triggerTime = DateTime.Now + delay;

            Plugin.Log.Information($"Jumpscare scheduled in {seconds} seconds (at {triggerTime}).");
            IsOpen = true;
            BeginPreload();
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
        if (!triggerTime.HasValue)
            return;

        // Wait until trigger time
        if (DateTime.Now < triggerTime.Value)
        {
            var remaining = triggerTime.Value - DateTime.Now;
            ImGui.TextUnformatted($"Waiting... {remaining.TotalSeconds:F1}s");
            return;
        }

        // Wait until GIF textures are loaded
        if (!preloadDone)
        {
            ImGui.TextUnformatted("Preparing jumpscare...");
            return;
        }

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

        if (tongueGif != null && tongueGif.FramePaths.Count > 0)
        {
            var now = DateTime.Now;
            float deltaMs = (float)(now - lastFrameTime).TotalMilliseconds;
            lastFrameTime = now;

            // Update GIF
            tongueGif.Update(deltaMs);

            // Handle fadeout after last frame, stop fade when alpha reaches 0
            float alpha = 1f;
            if (tongueGif.Finished)
            {
                alpha = 1f - Math.Min(tongueGif.FadeTimer / tongueGif.FadeDurationMs, 1f);
                if (alpha <= 0f)
                {
                    // Close window and dispose GIF
                    tongueGif.Dispose();
                    tongueGif = null;
                    IsOpen = false;
                    Plugin.Log.Information("Jumpscare window disposed after GIF finished and faded out.");
                    ImGui.End();
                    return;
                }
            }

            // Render GIF
            tongueGif.Render(windowSize, alpha);
        }
        else if (resourcesLoaded)
        {
            ImGui.TextUnformatted($"Image not found or not a GIF: {imgPath}");
        }

        ImGui.End();
    }
}
