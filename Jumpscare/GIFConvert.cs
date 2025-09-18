using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Jumpscare
{
    public class GIFConvert : IDisposable
    {
        private class Frame
        {
            public IDalamudTextureWrap Texture { get; }
            public int DelayMs { get; }

            public Frame(IDalamudTextureWrap texture, int delayMs)
            {
                Texture = texture;
                DelayMs = delayMs;
            }
        }

        private readonly List<Frame> frames = new();
        private int currentFrame = 0;
        private float elapsedMs = 0f;
        private readonly string tempFolder;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public GIFConvert(string gifPath, int frameDelayMs)
        {
            tempFolder = Path.Combine(Path.GetTempPath(), "DalamudGifFrames");
            Directory.CreateDirectory(tempFolder);

            DecodeGifToPngs(gifPath, frameDelayMs);
        }

        private void DecodeGifToPngs(string gifPath, int frameDelayMs)
        {
            using var img = Image.Load<Rgba32>(gifPath);

            for (int i = 0; i < img.Frames.Count; i++)
            {
                var frame = img.Frames.CloneFrame(i);

                // Save frame to temporary PNG
                string framePath = Path.Combine(tempFolder, $"frame_{i}.png");
                frame.Save(framePath, new PngEncoder());

                // Load texture using the plugin instance
                var tex = Plugin.TextureProvider.GetFromFile(framePath).GetWrapOrDefault();
                if (tex != null)
                {
                    frames.Add(new Frame(tex, frameDelayMs));

                    Width = tex.Width;
                    Height = tex.Height;
                }
            }
        }

        public void Update(float deltaMs)
        {
            if (frames.Count == 0) return;

            elapsedMs += deltaMs;
            if (elapsedMs >= frames[currentFrame].DelayMs)
            {
                elapsedMs = 0f;
                currentFrame = (currentFrame + 1) % frames.Count;
            }
        }

        public void Render(Vector2 size)
        {
            if (frames.Count == 0) return;

            ImGui.Image(frames[currentFrame].Texture.Handle, size);
        }

        public void Dispose()
        {
            foreach (var f in frames)
                f.Texture.Dispose();

            try { Directory.Delete(tempFolder, true); } catch { }
        }
    }
}
