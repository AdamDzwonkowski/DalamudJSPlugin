using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.IO;

namespace Jumpscare.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    private string newImagePath = "";
    private string newSoundPath = "";

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
        // --- Images ---
        var selectedImage = configuration.SelectedImage;
        DrawSelection("Image", configuration.ImageOptions, ref selectedImage, ref newImagePath);
        configuration.SelectedImage = selectedImage;

        // Reset images button
        if (ImGui.Button("Reset Images to Defaults"))
        {
            configuration.ResetImageOptions();
            ReloadMedia();
        }

        ImGui.Separator();

        // --- Sounds ---
        var selectedSound = configuration.SelectedSound;
        DrawSelection("Sound", configuration.SoundOptions, ref selectedSound, ref newSoundPath);
        configuration.SelectedSound = selectedSound;

        // Reset sounds button
        if (ImGui.Button("Reset Sounds to Defaults"))
        {
            configuration.ResetSoundOptions();
            ReloadMedia();
        }

        ImGui.Separator();
        ImGui.Text("Randomization");

        bool randomImages = configuration.RandomizeImages;
        if (ImGui.Checkbox("Randomize Images", ref randomImages))
        {
            configuration.RandomizeImages = randomImages;
            configuration.Save();
        }

        bool randomSounds = configuration.RandomizeSounds;
        if (ImGui.Checkbox("Randomize Sounds", ref randomSounds))
        {
            configuration.RandomizeSounds = randomSounds;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.Text("Trigger Timing");

        // Min seconds
        int minSecs = configuration.MinTriggerSeconds;
        if (ImGui.InputInt("Min Seconds (10)", ref minSecs))
        {
            configuration.MinTriggerSeconds = Math.Clamp(minSecs, 10, 100000);
            configuration.Save();
        }

        // Max seconds
        int maxSecs = configuration.MaxTriggerSeconds;
        if (ImGui.InputInt("Max Seconds (100000)", ref maxSecs))
        {
            configuration.MaxTriggerSeconds = Math.Clamp(maxSecs, configuration.MinTriggerSeconds + 1, 100000);
            configuration.Save();
        }
    }

    private void DrawSelection(string label, System.Collections.Generic.List<string> options, ref string selectedOption, ref string newPathBuffer)
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
            if (!options.Contains(newPathBuffer))
            {
                options.Add(newPathBuffer);
                selectedOption = newPathBuffer;
                configuration.Save();
                ReloadMedia();
            }
            newPathBuffer = "";
        }
    }

    private void ReloadMedia()
    {
        var dllDir = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName
                     ?? Plugin.PluginInterface.GetPluginConfigDirectory();
        var imgPath = Path.Combine(dllDir, "Data", configuration.SelectedImage);
        var soundPath = Path.Combine(dllDir, "Data", configuration.SelectedSound);

        plugin.MainWindow.Reload(imgPath, soundPath);
    }
}
