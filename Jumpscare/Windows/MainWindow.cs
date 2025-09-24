using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace Jumpscare.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string imgPath;
    private GIFConvert? tongueGif;
    private bool resourcesLoaded = false;
    private bool preloadDone = false;

    private DateTime lastFrameTime;

    // Delay handling
    private DateTime? triggerTime = null;
    private TimeSpan delay = TimeSpan.Zero;
    private readonly Random rng = new();

    // Background preload task
    private Task? preloadTask;

    public MainWindow(string tongueImagePath)
        : base("My Amazing Window##HiddenID",
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

    /// <summary>
    /// Starts background GIF decoding and schedules texture creation on the main thread.
    /// </summary>
    private void StartPreload()
    {
        if (preloadTask != null) return;

        preloadTask = Task.Run(() =>
        {
            try
            {
                // Decode frames (CPU-heavy work)
                var gif = new GIFConvert(imgPath);

                // Schedule texture creation on the main thread
                Plugin.Framework.RunOnFrameworkThread(() =>
                {
                    tongueGif = gif;
                    tongueGif.EnsureTexturesLoaded(); // Texture creation must happen on main thread
                    preloadDone = true;
                    Plugin.Log.Information("GIF preloaded successfully.");
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"GIF preload failed: {ex}");
            }
        });
    }

    public void Toggle()
    {
        if (!IsOpen)
        {
            // Schedule random delay
            int seconds = rng.Next(10, 100); // 100–10000 seconds
            delay = TimeSpan.FromSeconds(seconds);
            triggerTime = DateTime.Now + delay;

            Plugin.Log.Information($"Jumpscare scheduled in {seconds} seconds (at {triggerTime}).");
            IsOpen = true;

            // Start background GIF decode
            StartPreload();
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
            ImGui.TextUnformatted("Preparing jumpscare...");
            return; // Don't render GIF until textures are ready
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
            tongueGif.Render(windowSize);
        }
        else
        {
            ImGui.TextUnformatted($"Image not found or not a GIF: {imgPath}");
        }

        ImGui.End();
    }
}
