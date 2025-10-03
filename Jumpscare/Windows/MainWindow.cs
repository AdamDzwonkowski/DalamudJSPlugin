using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace Jumpscare.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly object reloadLock = new();

    private string imgPath;
    private string? soundPath;

    private GIFConvert? GIF;

    private bool preloadStarted = false;
    private bool resourcesLoaded = false;
    private bool preloadDone = false;

    private DateTime lastFrameTime;
    private DateTime? triggerTime = null;
    private TimeSpan delay = TimeSpan.Zero;

    private readonly Random rng = new();
    private Task? preloadTask;

    private bool soundPlayed = false;
    private readonly Configuration config;

    private bool isRunning = false; // ⬅ Tracks whether /jumpscare is active
    public bool IsRunning => isRunning && triggerTime.HasValue && DateTime.Now < triggerTime.Value;

    public MainWindow(string imagePath, string? wavPath, Configuration config)
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
        imgPath = imagePath;
        soundPath = wavPath;
        this.config = config;
        lastFrameTime = DateTime.Now;
    }

    public void Dispose() => GIF?.Dispose();

    public void Reload(string newImgPath, string? newSoundPath)
    {
        lock (reloadLock)
        {
            Plugin.Log.Information($"Reloading jumpscare with {newImgPath}, {newSoundPath}");
            StopPlayback();

            GIF?.Dispose();
            GIF = null;

            preloadStarted = false;
            preloadDone = false;
            resourcesLoaded = false;
            soundPlayed = false;

            imgPath = newImgPath;
            soundPath = newSoundPath;

            BeginPreload();
            if (isRunning) ScheduleNextTrigger();
        }
    }

    private void StopPlayback() => triggerTime = null;

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

            var ext = Path.GetExtension(imgPath).ToLowerInvariant();

            try
            {
                // --- GIFs ---
                if (ext == ".gif")
                {
                    GIF = new GIFConvert(imgPath);
                }
                else
                {
                    // --- Static PNG/JPG as single-frame GIFConvert ---
                    using var img = Image.Load<Rgba32>(imgPath);
                    string tempGif = Path.Combine(Path.GetTempPath(), $"single_frame_{Guid.NewGuid()}.gif");

                    img.SaveAsGif(tempGif); // Save as a single-frame GIF
                    GIF = new GIFConvert(tempGif);
                }

                Plugin.Framework.RunOnFrameworkThread(() =>
                {
                    GIF?.EnsureTexturesLoaded();
                    preloadDone = true;
                    resourcesLoaded = true;
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Preload failed: {ex}");
                resourcesLoaded = true;
            }
        });
    }

    private void ScheduleNextTrigger()
    {
        int min = Math.Clamp(config.MinTriggerSeconds, 10, 100000);
        int max = Math.Clamp(config.MaxTriggerSeconds, 10, 100000);

        if (max <= min) max = min + 1;

        int seconds = rng.Next(min, max + 1);
        delay = TimeSpan.FromSeconds(seconds);
        triggerTime = DateTime.Now + delay;
    }

    public void ResetPlayback()
    {
        lock (reloadLock)
        {
            GIF?.Dispose();
            GIF = null;

            preloadStarted = false;
            preloadDone = false;
            resourcesLoaded = false;
            triggerTime = null;
            soundPlayed = false;

            // Randomize selection only if enabled
            if (config.RandomizeImages && config.ImageOptions.Count > 0)
            {
                var idx = rng.Next(config.ImageOptions.Count);
                imgPath = Path.Combine(
                    Plugin.PluginInterface.AssemblyLocation.Directory?.FullName
                    ?? Plugin.PluginInterface.GetPluginConfigDirectory(),
                    "Data",
                    config.ImageOptions[idx]
                );
            }

            if (config.RandomizeSounds && config.SoundOptions.Count > 0)
            {
                var idx = rng.Next(config.SoundOptions.Count);
                soundPath = Path.Combine(
                    Plugin.PluginInterface.AssemblyLocation.Directory?.FullName
                    ?? Plugin.PluginInterface.GetPluginConfigDirectory(),
                    "Data",
                    config.SoundOptions[idx]
                );
            }

            BeginPreload();
            if (isRunning) ScheduleNextTrigger(); // Only schedule if user started /jumpscare
        }
    }

    public new void Toggle()
    {
        if (!IsOpen)
        {
            IsOpen = true;
            isRunning = true; // mark as running
            BeginPreload();
            ScheduleNextTrigger();
        }
        else
        {
            triggerTime = null;
            isRunning = false; // mark as stopped
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

        if (!triggerTime.HasValue)
        {
            ImGui.End();
            return;
        }

        if (DateTime.Now < triggerTime.Value)
        {
            if (config.ShowCountdownTimer)
            {
                var remaining = triggerTime.Value - DateTime.Now;
                ImGui.TextUnformatted($"Waiting... {remaining.TotalSeconds:F1}s");
            }
            ImGui.End();
            return;
        }

        if (!preloadDone)
        {
            ImGui.TextUnformatted("Preparing jumpscare...");
            ImGui.End();
            return;
        }

        if (GIF != null && GIF.FramePaths.Count > 0)
        {
            var now = DateTime.Now;
            float deltaMs = (float)(now - lastFrameTime).TotalMilliseconds;
            lastFrameTime = now;

            PlaySoundOnce();

            GIF.Update(deltaMs);

            float alpha = 1f;
            if (GIF.Finished)
            {
                alpha = 1f - Math.Min(GIF.FadeTimer / GIF.FadeDurationMs, 1f);
                if (alpha <= 0f)
                {
                    ResetPlayback();
                    ImGui.End();
                    return;
                }
            }

            GIF.Render(windowSize, alpha);
        }
        else if (resourcesLoaded)
        {
            ImGui.TextUnformatted($"Image not found or unsupported: {imgPath}");
        }

        ImGui.End();
    }

    private void PlaySoundOnce()
    {
        if (!soundPlayed && soundPath != null && File.Exists(soundPath))
        {
            try
            {
                var player = new System.Media.SoundPlayer(soundPath);
                player.Play();
                soundPlayed = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to play sound {soundPath}: {ex}");
            }
        }
    }
}
