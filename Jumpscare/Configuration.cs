using Dalamud.Bindings.ImGui;
using Dalamud.Configuration;
using System;

namespace Jumpscare;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public string SelectedImage { get; set; } = "visual/1758028660865.gif";
    public string SelectedSound { get; set; } = "audio/foxy.wav";

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
