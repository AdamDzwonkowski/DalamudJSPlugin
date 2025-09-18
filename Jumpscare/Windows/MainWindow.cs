using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.IO;
using System.Numerics;

namespace Jumpscare.Windows;

public class MainWindow : Window, IDisposable
{
    private GIFConvert tongueGif;
    private DateTime lastFrameTime;

    private string pluginDir;
    private string imgPath;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string tongueImagePath)
    : base("My Amazing Window##With a hidden ID",
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
        pluginDir = Plugin.PluginInterface.GetPluginConfigDirectory();
        imgPath = Path.Combine(pluginDir, "profile.png");
        tongueGif = new GIFConvert(imgPath, 10);
        lastFrameTime = DateTime.Now;
    }

    public void Dispose() {
        tongueGif?.Dispose();
    }

    public override void Draw()
    {
        Vector2 windowSize = new Vector2(1920f, 1080f);
        Vector2 centerPos = new Vector2(0f, 0f);

        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.SetNextWindowPos(centerPos, ImGuiCond.Always);

        ImGui.Begin("MainWindow",
                    ImGuiWindowFlags.NoScrollbar
                  | ImGuiWindowFlags.NoTitleBar
                  | ImGuiWindowFlags.NoDecoration
                  | ImGuiWindowFlags.NoFocusOnAppearing
                  | ImGuiWindowFlags.NoNavFocus
                  | ImGuiWindowFlags.NoInputs
                  | ImGuiWindowFlags.NoMouseInputs
                  | ImGuiWindowFlags.NoBackground);

        if (Path.GetExtension(imgPath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.Now;
            float deltaMs = (float)(now - lastFrameTime).TotalMilliseconds;
            lastFrameTime = now;

            tongueGif?.Update(deltaMs);
            tongueGif?.Render(windowSize);
        }
        else
        {
            var tongueTexture = Plugin.TextureProvider.GetFromFile(imgPath)?.GetWrapOrDefault();
            if (tongueTexture != null)
                ImGui.Image(tongueTexture.Handle, windowSize);
            else
                ImGui.TextUnformatted("Image not found.");
        }

        ImGui.End();
    }
}
