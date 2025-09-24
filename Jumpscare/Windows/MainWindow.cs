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
    private float fadeAlpha = 1f;
    private bool preloadDone = false;

    // Delay handling
    private DateTime? triggerTime = null;
    private TimeSpan delay = TimeSpan.Zero;
    private readonly Random rng = new();

    // Background preload task
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
        Size = new Vector2(1920f, 1080f);
        imgPath = tongueImagePath;
        lastFrameTime = DateTime.Now;
    }

    public void Dispose()
    {
        tongueGif?.Dispose();
    }

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
                    var gif = new GIFConvert(imgPath);

                    // Hand GIF back to main thread for texture creation
                    Plugin.Framework.RunOnFrameworkThread(() =>
                    {
                        tongueGif = gif;
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
            else
            {
                resourcesLoaded = true;
            }
        });
    }

    public void Toggle()
    {
        if (!IsOpen)
        {
            // Schedule random delay
            int seconds = rng.Next(5, 10); // between 100 and 10000 seconds
            delay = TimeSpan.FromSeconds(seconds);
            triggerTime = DateTime.Now + delay;

            Plugin.Log.Information($"Jumpscare scheduled in {seconds} seconds (at {triggerTime}).");
            IsOpen = true;

            // Start background GIF decode
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

        if (DateTime.Now < triggerTime.Value)
        {
            var remaining = triggerTime.Value - DateTime.Now;
            ImGui.TextUnformatted($"Waiting... {remaining.TotalSeconds:F1}s");
            return;
        }

        if (!preloadDone)
        {
            ImGui.TextUnformatted("Preparing jumpscare..."); return; // Don't render GIF until textures are ready
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

        if (tongueGif != null)
        {
            var now = DateTime.Now;
            float deltaMs = (float)(now - lastFrameTime).TotalMilliseconds;
            lastFrameTime = now;

            tongueGif.Update(deltaMs);

            // Fade out last frame once finished
            if (tongueGif.Finished && fadeAlpha > 0f)
            {
                fadeAlpha -= deltaMs / 1000f; // 1 second fade
                if (fadeAlpha < 0f)
                    fadeAlpha = 0f;
            }

            tongueGif.Render(windowSize, fadeAlpha);
        }
        else if (resourcesLoaded)
        {
            ImGui.TextUnformatted($"Image not found or not a GIF: {imgPath}");
        }

        ImGui.End();
    }
}
