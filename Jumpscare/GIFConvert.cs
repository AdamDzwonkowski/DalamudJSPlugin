using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;

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

        public bool Finished { get; private set; } = false;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public GIFConvert(string gifPath)
        {
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
                int delayMs = 100; // default 100ms

                try
                {
                    var gifMeta = frame.Metadata.GetGifMetadata();

                    if (gifMeta != null)
                    {
                        var t = gifMeta.GetType();
                        bool found = false;

                        // properties
                        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (!prop.Name.ToLowerInvariant().Contains("delay")) continue;
                            var val = prop.GetValue(gifMeta);
                            if (TryInterpretDelayValue(val, i, out var ms))
                            {
                                delayMs = ms;
                                found = true;
                                break;
                            }
                        }

                        // fields
                        if (!found)
                        {
                            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (!field.Name.ToLowerInvariant().Contains("delay")) continue;
                                var val = field.GetValue(gifMeta);
                                if (TryInterpretDelayValue(val, i, out var ms))
                                {
                                    delayMs = ms;
                                    found = true;
                                    break;
                                }
                            }
                        }

                        // fallback candidates
                        if (!found)
                        {
                            var candidates = new[] { "FrameDelay", "frameDelay", "_frameDelays", "frameDelays", "FrameDelays" };
                            foreach (var name in candidates)
                            {
                                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (f != null && TryInterpretDelayValue(f.GetValue(gifMeta), i, out var ms))
                                {
                                    delayMs = ms;
                                    break;
                                }

                                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (p != null && TryInterpretDelayValue(p.GetValue(gifMeta), i, out ms))
                                {
                                    delayMs = ms;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.Error($"GIFConvert: error probing frame {i} delay: {ex}");
                }

                string framePath = Path.Combine(tempFolder, $"frame_{i}.png");
                frame.Save(framePath, new PngEncoder());
                framePaths.Add((framePath, delayMs));

                Plugin.Log?.Info($"GIFConvert: frame {i} delay = {delayMs}ms (saved {framePath})");
            }
        }

        private static bool TryInterpretDelayValue(object? value, int frameIndex, out int delayMs)
        {
            delayMs = 100;
            if (value == null) return false;

            try
            {
                if (value is Array arr && arr.Length > 0)
                {
                    object? elem = frameIndex < arr.Length ? arr.GetValue(frameIndex) : arr.GetValue(0);
                    if (elem != null)
                        return TryInterpretDelaySingle(elem, out delayMs);
                }

                return TryInterpretDelaySingle(value, out delayMs);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInterpretDelaySingle(object value, out int delayMs)
        {
            delayMs = 100;
            switch (value)
            {
                case byte b: delayMs = Math.Max(b * 10, 10); return true;
                case sbyte sb: delayMs = Math.Max(sb * 10, 10); return true;
                case ushort us: delayMs = Math.Max(us * 10, 10); return true;
                case short s: delayMs = Math.Max(s * 10, 10); return true;
                case int i: delayMs = i <= 1000 ? Math.Max(i * 10, 10) : i; return true;
                case long l: delayMs = l <= 1000 ? Math.Max((int)l * 10, 10) : (int)l; return true;
                case double d: delayMs = d <= 1000 ? Math.Max((int)(d * 10), 10) : (int)d; return true;
                case float f: delayMs = f <= 1000 ? Math.Max((int)(f * 10), 10) : (int)f; return true;
                default:
                    if (value is IConvertible)
                    {
                        try
                        {
                            var iv = Convert.ToInt32(value);
                            delayMs = iv <= 1000 ? Math.Max(iv * 10, 10) : iv;
                            return true;
                        }
                        catch { }
                    }
                    break;
            }
            return false;
        }

        public void EnsureTexturesLoaded()
        {
            if (texturesLoaded || framePaths.Count == 0)
                return;

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

            texturesLoaded = true;
        }

        public void Update(float deltaMs)
        {
            if (Finished || frames.Count == 0) return;

            timeAccumulator += deltaMs;

            if (timeAccumulator >= frames[currentFrame].DelayMs)
            {
                timeAccumulator = 0f;
                currentFrame++;

                if (currentFrame >= frames.Count)
                {
                    currentFrame = frames.Count - 1; // stay on last frame
                    Finished = true;
                }
            }
        }

        public void Render(Vector2 size, float alpha = 1f)
        {
            if (frames.Count == 0) return;

            var tex = frames[currentFrame].Texture;
            if (tex == null) return;

            var wrap = tex.GetWrapOrEmpty();
            if (wrap == null) return;

            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.Image(wrap.Handle, size, Vector2.Zero, Vector2.One,
                        new Vector4(1f, 1f, 1f, alpha));
        }


        public void Dispose()
        {
            try
            {
                foreach (var (path, _) in framePaths)
                    if (File.Exists(path))
                        File.Delete(path);

                Directory.Delete(tempFolder, true);
            }
            catch { }
        }
    }
}
