using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
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
            public ISharedImmediateTexture Texture { get; }
            public int DelayMs { get; }

            public Frame(ISharedImmediateTexture texture, int delayMs)
            {
                Texture = texture;
                DelayMs = delayMs;
            }
        }

        private readonly List<Frame> frames = new();
        private int currentFrame = 0;
        private float elapsedMs = 0f;
        private readonly string tempFolder;
        private readonly int frameDelayMs;

        private readonly List<string> framePaths = new();
        private bool texturesLoaded = false;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public GIFConvert(string gifPath, int frameDelayMs)
        {
            this.frameDelayMs = frameDelayMs;
            tempFolder = Path.Combine(Path.GetTempPath(), "DalamudGifFrames");
            Directory.CreateDirectory(tempFolder);

            DecodeGifToPngs(gifPath);
        }

        private void DecodeGifToPngs(string gifPath)
        {
            using var img = Image.Load<Rgba32>(gifPath);

            for (int i = 0; i < img.Frames.Count; i++)
            {
                var frame = img.Frames.CloneFrame(i);

                // Save frame to temporary PNG
                string framePath = Path.Combine(tempFolder, $"frame_{i}.png");
                frame.Save(framePath, new PngEncoder());

                framePaths.Add(framePath);
            }
        }

        /// <summary>
        /// Load textures lazily (on-demand) on the game thread.
        /// </summary>
        public void EnsureTexturesLoaded()
        {
            if (texturesLoaded || framePaths.Count == 0)
                return;

            foreach (var path in framePaths)
            {
                var tex = Plugin.TextureProvider.GetFromFile(path);
                if (tex != null)
                {
                    var wrap = tex.GetWrapOrEmpty();
                    frames.Add(new Frame(tex, frameDelayMs));
                    Width = wrap.Width;
                    Height = wrap.Height;
                }
            }

            texturesLoaded = true;
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

            var wrap = frames[currentFrame].Texture.GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, size);
        }

        public void Dispose()
        {
            try { Directory.Delete(tempFolder, true); } catch { }
        }
    }
}
