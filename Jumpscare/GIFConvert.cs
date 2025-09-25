using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

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
        private readonly List<(string Path, int DelayMs)> framePaths = new();
        public IReadOnlyList<(string Path, int DelayMs)> FramePaths => framePaths;

        private int currentFrame = 0;
        private float timeAccumulator = 0f;
        private readonly string tempFolder;
        private bool texturesLoaded = false;

        private int previousFrame = -1;


        public bool Finished { get; private set; } = false;
        public float FadeTimer { get; private set; } = 0f;
        public float FadeDurationMs { get; set; } = 1000f;
        public bool ShouldCloseWindow => Finished && FadeTimer >= FadeDurationMs;
        private bool firstUpdate = true;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public GIFConvert(string gifPath)
        {
            tempFolder = Path.Combine(Path.GetTempPath(), "DalamudGifFrames");
            Directory.CreateDirectory(tempFolder);
            Plugin.Log.Info($"GIFConvert: Created temp folder at {tempFolder}");

            DecodeGifToPngs(gifPath);
        }

        private void DecodeGifToPngs(string gifPath)
        {
            Plugin.Log.Info($"GIFConvert: Loading GIF {gifPath}");

            using var img = Image.Load<Rgba32>(gifPath);

            for (int i = 0; i < img.Frames.Count; i++)
            {
                var frame = img.Frames[i];

                int delayMs = 100; // fallback
                try
                {
                    delayMs = frame.Metadata.GetGifMetadata().FrameDelay * 10;
                    if (delayMs <= 0) delayMs = 100;
                    Plugin.Log.Info($"GIFConvert: Frame {i} delay set to {delayMs}ms");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warning($"GIFConvert: Failed to read frame delay for frame {i}, using fallback. Exception: {ex}");
                }

                // Create a buffer for pixel data
                var pixelBuffer = new Rgba32[frame.Width * frame.Height];
                frame.CopyPixelDataTo(pixelBuffer);

                // Create a new image from the buffer
                using var singleFrameImage = Image.LoadPixelData<Rgba32>(
                    pixelBuffer,
                    frame.Width,
                    frame.Height
                );

                string framePath = Path.Combine(tempFolder, $"frame_{i}.png");
                singleFrameImage.Save(framePath); // Save as PNG

                framePaths.Add((framePath, delayMs));
                Plugin.Log.Info($"GIFConvert: Saved frame {i} as PNG at {framePath}");
            }
        }

        public void EnsureTexturesLoaded()
        {
            if (texturesLoaded || framePaths.Count == 0) return;

            foreach (var (path, delayMs) in framePaths)
            {
                var tex = Plugin.TextureProvider.GetFromFile(path);
                if (tex != null)
                {
                    frames.Add(new Frame(tex, delayMs));
                    var wrap = tex.GetWrapOrEmpty();
                    Width = wrap.Width;
                    Height = wrap.Height;
                }
            }

            texturesLoaded = frames.Count > 0;
        }

        public void Update(float deltaMs)
        {
            if (frames.Count == 0) return;
            if (firstUpdate) { firstUpdate = false; return; }

            if (!Finished)
            {
                timeAccumulator += deltaMs;

                while (timeAccumulator >= frames[currentFrame].DelayMs)
                {
                    timeAccumulator -= frames[currentFrame].DelayMs;
                    previousFrame = currentFrame; // store old frame
                    currentFrame++;
                    if (currentFrame >= frames.Count)
                    {
                        currentFrame = frames.Count - 1;
                        Finished = true;
                        FadeTimer = 0f;
                        break;
                    }
                }
            }
            else
            {
                FadeTimer = Math.Min(FadeTimer + deltaMs, FadeDurationMs);
            }
        }


        public void Render(Vector2 size, float alpha = 1f)
        {
            if (frames.Count == 0) return;

            // Only draw previous frame if current frame is NOT the last frame
            if (previousFrame >= 0 && previousFrame != currentFrame && currentFrame < frames.Count - 1)
            {
                var prevTex = frames[previousFrame].Texture;
                var prevWrap = prevTex.GetWrapOrEmpty();
                ImGui.SetCursorPos(Vector2.Zero);
                ImGui.Image(prevWrap.Handle, size, Vector2.Zero, Vector2.One, new Vector4(1f, 1f, 1f, alpha));
            }

            // Draw current frame on top
            var tex = frames[currentFrame].Texture;
            var wrap = tex.GetWrapOrEmpty();
            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.Image(wrap.Handle, size, Vector2.Zero, Vector2.One, new Vector4(1f, 1f, 1f, alpha));
        }



        public void Dispose()
        {
            foreach (var (path, _) in framePaths)
                if (File.Exists(path)) File.Delete(path);

            try { Directory.Delete(tempFolder, true); } catch { }
        }
    }
}
