using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.IO;
using System.Numerics;

namespace Jumpscare.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Configuration###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 180);
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
        // Example checkboxes
        var configValue = configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Random Config Bool", ref configValue))
        {
            configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            configuration.Save();
        }

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Separator();

        // === Image selection ===
        string[] imageOptions =
        {
            "visual/1758028660865.gif",
            "visual/foxy-jumpscare.gif",
            "visual/profile.png"
        };

        int currentImageIndex = Array.IndexOf(imageOptions, configuration.SelectedImage);
        if (currentImageIndex < 0) currentImageIndex = 0;

        if (ImGui.BeginCombo("Image", imageOptions[currentImageIndex]))
        {
            for (int i = 0; i < imageOptions.Length; i++)
            {
                bool isSelected = (i == currentImageIndex);
                if (ImGui.Selectable(imageOptions[i], isSelected))
                {
                    configuration.SelectedImage = imageOptions[i];
                    configuration.Save();

                    var dllDir = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName
                                 ?? Plugin.PluginInterface.GetPluginConfigDirectory();
                    var imgPath = Path.Combine(dllDir, "Data", configuration.SelectedImage);
                    var soundPath = Path.Combine(dllDir, "Data", configuration.SelectedSound);

                    plugin.MainWindow.Reload(imgPath, soundPath);
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        // === Sound selection ===
        string[] soundOptions =
        {
            "audio/apocbird.wav",
            "audio/foxy.wav",
            "audio/scream.mp3"
        };

        int currentSoundIndex = Array.IndexOf(soundOptions, configuration.SelectedSound);
        if (currentSoundIndex < 0) currentSoundIndex = 0;

        if (ImGui.BeginCombo("Sound", soundOptions[currentSoundIndex]))
        {
            for (int i = 0; i < soundOptions.Length; i++)
            {
                bool isSelected = (i == currentSoundIndex);
                if (ImGui.Selectable(soundOptions[i], isSelected))
                {
                    configuration.SelectedSound = soundOptions[i];
                    configuration.Save();

                    var dllDir = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName
                                 ?? Plugin.PluginInterface.GetPluginConfigDirectory();
                    var imgPath = Path.Combine(dllDir, "Data", configuration.SelectedImage);
                    var soundPath = Path.Combine(dllDir, "Data", configuration.SelectedSound);

                    plugin.MainWindow.Reload(imgPath, soundPath);
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }
}
