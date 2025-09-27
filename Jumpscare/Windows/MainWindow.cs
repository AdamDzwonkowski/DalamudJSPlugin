using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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

    public MainWindow(string imagePath, string? wavPath)
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
        lastFrameTime = DateTime.Now;
    }

    public void Dispose() => tongueGif?.Dispose();

    public void Reload(string newImgPath, string? newSoundPath)
    {
        lock (reloadLock)
        {
            Plugin.Log.Information($"Reloading jumpscare with {newImgPath}, {newSoundPath}");
            StopPlayback();

            tongueGif?.Dispose();
            tongueGif = null;

            preloadStarted = false;
            preloadDone = false;
            resourcesLoaded = false;
            soundPlayed = false;

            imgPath = newImgPath;
            soundPath = newSoundPath;

            BeginPreload();
            ScheduleNextTrigger();
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
                    tongueGif = new GIFConvert(imgPath);
                }
                else
                {
                    // --- Static PNG/JPG as single-frame GIFConvert ---
                    using var img = Image.Load<Rgba32>(imgPath);
                    string tempGif = Path.Combine(Path.GetTempPath(), $"single_frame_{Guid.NewGuid()}.gif");

                    img.SaveAsGif(tempGif); // Save as a single-frame GIF
                    tongueGif = new GIFConvert(tempGif);
                }

                Plugin.Framework.RunOnFrameworkThread(() =>
                {
                    tongueGif?.EnsureTexturesLoaded();
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
        int seconds = rng.Next(10, 100);
        delay = TimeSpan.FromSeconds(seconds);
        triggerTime = DateTime.Now + delay;
        Plugin.Log.Information($"Next jumpscare scheduled in {seconds} seconds (at {triggerTime}).");
    }

    private void ResetPlayback()
    {
        lock (reloadLock)
        {
            tongueGif?.Dispose();
            tongueGif = null;

            preloadStarted = false;
            preloadDone = false;
            resourcesLoaded = false;
            triggerTime = null;
            soundPlayed = false;

            BeginPreload();
            ScheduleNextTrigger();
        }
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

            PlaySoundOnce();

            tongueGif.Update(deltaMs);

            float alpha = 1f;
            if (tongueGif.Finished)
            {
                alpha = 1f - Math.Min(tongueGif.FadeTimer / tongueGif.FadeDurationMs, 1f);
                if (alpha <= 0f)
                {
                    ResetPlayback();
                    ImGui.End();
                    return;
                }
            }

            tongueGif.Render(windowSize, alpha);
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
