using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using System;
using System.IO;
using System.Numerics;

namespace Jumpscare.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private GIFConvert? tongueGif;
        private ISharedImmediateTexture? tongueTexture;

        private DateTime lastFrameTime;
        private readonly string imgPath;
        private bool loaded = false;

        public MainWindow(Plugin plugin, string tongueImagePath)
            : base("My Amazing Window##WithHiddenID",
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

        private void LoadResources()
        {
            if (loaded) return;

            if (!File.Exists(imgPath))
            {
                Plugin.Log.Error($"Image not found: {imgPath}");
                loaded = true;
                return;
            }

            if (Path.GetExtension(imgPath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                tongueGif = new GIFConvert(imgPath, frameDelayMs: 10);
                tongueGif.EnsureTexturesLoaded();
            }
            else
            {
                tongueTexture = Plugin.TextureProvider.GetFromFile(imgPath);
            }

            loaded = true;
        }

        public override void Draw()
        {
            LoadResources(); // ensures resources only load once

            Vector2 windowSize = new(1920f, 1080f);
            Vector2 centerPos = new(0f, 0f);

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

            if (tongueGif != null)
            {
                float deltaMs = (float)(DateTime.Now - lastFrameTime).TotalMilliseconds;
                lastFrameTime = DateTime.Now;

                tongueGif.Update(deltaMs);
                tongueGif.Render(windowSize);
            }
            else if (tongueTexture != null)
            {
                var wrap = tongueTexture.GetWrapOrEmpty();
                ImGui.Image(wrap.Handle, windowSize);
            }
            else
            {
                ImGui.TextUnformatted($"Image not found: {imgPath}");
            }

            ImGui.End();
        }
    }
}
