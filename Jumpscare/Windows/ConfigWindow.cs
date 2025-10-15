using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using System.IO;

namespace Jumpscare.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    private string newImagePath = "";
    private string newSoundPath = "";

    // Store rejection messages so they persist between frames
    private string imageRejectionMessage = "";
    private string soundRejectionMessage = "";

    public ConfigWindow(Plugin plugin) : base("Configuration###WithConstantID")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        SizeCondition = ImGuiCond.Always;

        this.plugin = plugin;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        // --- Status indicator ---
        if (plugin.MainWindow.IsRunning)
            ImGui.TextColored(new Vector4(0f, 1f, 0f, 1f), "Jumpscare timer is ACTIVE");
        else
            ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "Jumpscare timer is INACTIVE");

        // --- Jumpscare toggle button ---
        if (plugin.MainWindow.IsRunning)
        {
            if (ImGui.Button("Stop Timer"))
            {
                plugin.MainWindow.Toggle(); // stops jumpscare
            }
        }
        else
        {
            if (ImGui.Button("Start Timer"))
            {
                plugin.MainWindow.Toggle(); // starts jumpscare
            }
        }

        ImGui.Separator();
        ImGui.Text("Changing Settings (aside from Trigger Timing) resets the timer");

        bool reloadNeeded = false;

        // --- Images ---
        var selectedImage = configuration.SelectedImage;
        DrawSelection("GIF/PNG/JPG/WEBP", configuration.ImageOptions, ref selectedImage, ref newImagePath);
        if (configuration.SelectedImage != selectedImage)
        {
            configuration.SelectedImage = selectedImage;
            configuration.Save();
            reloadNeeded = true;
        }

        if (ImGui.Button("Reset Images to Defaults"))
        {
            configuration.ResetImageOptions();
            configuration.Save();
            reloadNeeded = true;
        }

        ImGui.Separator();

        // --- Sounds ---
        var selectedSound = configuration.SelectedSound;
        DrawSelection("WAV/MP3", configuration.SoundOptions, ref selectedSound, ref newSoundPath);
        if (configuration.SelectedSound != selectedSound)
        {
            configuration.SelectedSound = selectedSound;
            configuration.Save();
            reloadNeeded = true;
        }

        if (ImGui.Button("Reset Sounds to Defaults"))
        {
            configuration.ResetSoundOptions();
            configuration.Save();
            reloadNeeded = true;
        }

        if (reloadNeeded)
            ReloadMedia();

        ImGui.Separator();
        ImGui.Text("Randomization");

        bool randomizeImages = configuration.RandomizeImages;
        if (ImGui.Checkbox("Randomize Images", ref randomizeImages))
        {
            configuration.RandomizeImages = randomizeImages;
            configuration.Save();

            var paths = ResolveCurrentMediaPaths();
            plugin.MainWindow.Reload(paths.imagePath, paths.soundPath);
            plugin.MainWindow.ResetPlayback();
        }

        bool randomizeSounds = configuration.RandomizeSounds;
        if (ImGui.Checkbox("Randomize Sounds", ref randomizeSounds))
        {
            configuration.RandomizeSounds = randomizeSounds;
            configuration.Save();

            var paths = ResolveCurrentMediaPaths();
            plugin.MainWindow.Reload(paths.imagePath, paths.soundPath);
            plugin.MainWindow.ResetPlayback();
        }

        ImGui.Separator();
        ImGui.Text("Trigger Timing");

        // --- Min seconds ---
        int minSecs = configuration.MinTriggerSeconds;
        if (ImGui.InputInt("Min Seconds (10)", ref minSecs))
        {
            // Clamp minSecs between 10 and (maxSecs - 1)
            minSecs = Math.Clamp(minSecs, 10, configuration.MaxTriggerSeconds - 1);
            configuration.MinTriggerSeconds = minSecs;
            configuration.Save();
        }

        // --- Max seconds ---
        int maxSecs = configuration.MaxTriggerSeconds;
        if (ImGui.InputInt("Max Seconds (100000)", ref maxSecs))
        {
            // Clamp maxSecs between (minSecs + 1) and 100000
            maxSecs = Math.Clamp(maxSecs, configuration.MinTriggerSeconds + 1, 100000);
            configuration.MaxTriggerSeconds = maxSecs;
            configuration.Save();
        }
        bool showTimer = configuration.ShowCountdownTimer;
        if (ImGui.Checkbox("Show Countdown Timer", ref showTimer))
        {
            configuration.ShowCountdownTimer = showTimer;
            configuration.Save();
        }
    }

    private (string imagePath, string soundPath) ResolveCurrentMediaPaths()
    {
        string baseDir = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName
                         ?? Plugin.PluginInterface.GetPluginConfigDirectory();

        string imgPath = configuration.RandomizeImages && configuration.ImageOptions.Count > 0
            ? Path.Combine(baseDir, "Data", configuration.ImageOptions[new Random().Next(configuration.ImageOptions.Count)])
            : Path.Combine(baseDir, "Data", configuration.SelectedImage);

        string sndPath = configuration.RandomizeSounds && configuration.SoundOptions.Count > 0
            ? Path.Combine(baseDir, "Data", configuration.SoundOptions[new Random().Next(configuration.SoundOptions.Count)])
            : Path.Combine(baseDir, "Data", configuration.SelectedSound);

        return (imgPath, sndPath);
    }

    private void DrawSelection(
    string label,
    System.Collections.Generic.List<string> options,
    ref string selectedOption,
    ref string newPathBuffer)
    {
        int currentIndex = options.IndexOf(selectedOption);
        if (currentIndex < 0) currentIndex = 0;

        if (ImGui.BeginCombo(label, options[currentIndex]))
        {
            for (int i = 0; i < options.Count; i++)
            {
                bool isSelected = (i == currentIndex);
                if (ImGui.Selectable(options[i], isSelected))
                {
                    selectedOption = options[i];
                    configuration.Save();
                    ReloadMedia();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.InputText($"New {label} Path", ref newPathBuffer, 256);

        if (ImGui.Button($"Add {label}") && !string.IsNullOrWhiteSpace(newPathBuffer))
        {
            if (!File.Exists(newPathBuffer))
            {
                string msg = $"File not found: {newPathBuffer}";
                Plugin.Log.Warning(msg);

                if (label == "GIF/PNG/JPG/WEBP") imageRejectionMessage = msg;
                if (label == "WAV/MP3") soundRejectionMessage = msg;
            }
            else
            {
                FileInfo fileInfo = new FileInfo(newPathBuffer);
                long maxSizeBytes = 30 * 1024 * 1024; // 30MB

                if (fileInfo.Length > maxSizeBytes)
                {
                    string msg = $"File too large (>30MB)";
                    Plugin.Log.Warning($"Rejected {label} upload: {fileInfo.Length / (1024 * 1024)} MB");

                    if (label == "GIF/PNG/JPG/WEBP") imageRejectionMessage = msg;
                    if (label == "WAV/MP3") soundRejectionMessage = msg;
                }
                else
                {
                    string ext = Path.GetExtension(newPathBuffer).ToLowerInvariant();
                    bool isImage = (label == "GIF/PNG/JPG/WEBP") && (ext == ".gif" || ext == ".png" || ext == ".jpeg" || ext == ".jpg" || ext == ".webp");
                    bool isSound = (label == "WAV/MP3") && (ext == ".wav" || ext == ".mp3");

                    if (isImage || isSound)
                    {
                        if (!options.Contains(newPathBuffer))
                        {
                            options.Add(newPathBuffer);
                            selectedOption = newPathBuffer;
                            configuration.Save();
                            ReloadMedia();
                        }

                        // Clear rejection message if valid
                        if (label == "GIF/PNG/JPG/WEBP") imageRejectionMessage = "";
                        if (label == "WAV/MP3") soundRejectionMessage = "";
                    }
                    else
                    {
                        string msg = $"Not a {label}: {ext}";
                        Plugin.Log.Warning($"Rejected {label} upload: unsupported file type {ext}");

                        // Store rejection persistently
                        if (label == "GIF/PNG/JPG/WEBP") imageRejectionMessage = msg;
                        if (label == "WAV/MP3") soundRejectionMessage = msg;
                    }
                }
            }

            newPathBuffer = "";
        }

        // Display rejection message persistently
        string rejectionMessage = label == "GIF/PNG/JPG/WEBP" ? imageRejectionMessage : soundRejectionMessage;
        if (!string.IsNullOrEmpty(rejectionMessage))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), rejectionMessage);
        }
    }


    private void ReloadMedia()
    {
        var paths = ResolveCurrentMediaPaths();
        plugin.MainWindow.Reload(paths.imagePath, paths.soundPath);
    }
}
