using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace Jumpscare;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public string SelectedImage { get; set; } = DefaultImages[0];
    public string SelectedSound { get; set; } = DefaultSounds[0];

    public bool RandomizeImages { get; set; } = false;
    public bool RandomizeSounds { get; set; } = false;

    public List<string> ImageOptions { get; set; } = new();
    public List<string> SoundOptions { get; set; } = new();

    // ⏱ New random timer settings
    public int MinTriggerSeconds { get; set; } = 10;
    public int MaxTriggerSeconds { get; set; } = 100;

    private static readonly string[] DefaultImages =
    {
        "visual/1758028660865.gif",
        "visual/foxy-jumpscare.gif",
        "visual/profile.png"
    };

    private static readonly string[] DefaultSounds =
    {
        "audio/apocbird.wav",
        "audio/foxy.wav",
        "audio/scream.mp3"
    };

    public void EnsureDefaults()
    {
        if (ImageOptions == null || ImageOptions.Count == 0)
            ImageOptions = new List<string>(DefaultImages);

        if (SoundOptions == null || SoundOptions.Count == 0)
            SoundOptions = new List<string>(DefaultSounds);

        if (string.IsNullOrEmpty(SelectedImage))
            SelectedImage = ImageOptions[0];

        if (string.IsNullOrEmpty(SelectedSound))
            SelectedSound = SoundOptions[0];

        // ✅ Clamp min/max between 10–100000
        if (MinTriggerSeconds < 10) MinTriggerSeconds = 10;
        if (MaxTriggerSeconds > 100000) MaxTriggerSeconds = 100000;
        if (MaxTriggerSeconds <= MinTriggerSeconds)
            MaxTriggerSeconds = MinTriggerSeconds + 1;
    }

    public void ResetImageOptions()
    {
        ImageOptions = new List<string>(DefaultImages);
        SelectedImage = DefaultImages[0];
        Save();
    }

    public void ResetSoundOptions()
    {
        SoundOptions = new List<string>(DefaultSounds);
        SelectedSound = DefaultSounds[0];
        Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
