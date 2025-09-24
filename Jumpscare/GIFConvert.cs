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
        private float elapsedMs = 0f;
        private readonly string tempFolder;
        private bool texturesLoaded = false;

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

            // Try to extract delays for all frames
            for (int i = 0; i < img.Frames.Count; i++)
            {
                var frame = img.Frames.CloneFrame(i);
                int delayMs = 100; // default 100ms

                try
                {
                    // Attempt several strategies to find frame delay robustly
                    var gifMeta = frame.Metadata.GetGifMetadata();

                    if (gifMeta != null)
                    {
                        // 1) Look for properties/fields with "delay" in the name (public + non-public)
                        var t = gifMeta.GetType();
                        bool found = false;

                        // check properties
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

                        // if not found, check fields
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

                        // 2) If still not found, try a few known internal names
                        if (!found)
                        {
                            var candidates = new[] { "FrameDelay", "frameDelay", "_frameDelays", "frameDelays", "FrameDelays" };
                            foreach (var name in candidates)
                            {
                                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (f != null)
                                {
                                    var val = f.GetValue(gifMeta);
                                    if (TryInterpretDelayValue(val, i, out var ms))
                                    {
                                        delayMs = ms;
                                        found = true;
                                        break;
                                    }
                                }

                                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (p != null)
                                {
                                    var val = p.GetValue(gifMeta);
                                    if (TryInterpretDelayValue(val, i, out var ms))
                                    {
                                        delayMs = ms;
                                        found = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // don't crash on metadata probing — fallback will be used
                    Plugin.Log?.Error($"GIFConvert: error probing frame {i} delay: {ex}");
                }

                // Save frame to temporary PNG
                string framePath = Path.Combine(tempFolder, $"frame_{i}.png");
                frame.Save(framePath, new PngEncoder());

                framePaths.Add((framePath, delayMs));

                Plugin.Log?.Info($"GIFConvert: frame {i} delay = {delayMs}ms (saved {framePath})");
            }
        }

        // Interpret a variety of value types (single numeric or arrays)
        private static bool TryInterpretDelayValue(object? value, int frameIndex, out int delayMs)
        {
            delayMs = 100;
            if (value == null) return false;

            try
            {
                // Arrays: try to pick the element at frameIndex if present, otherwise first element
                if (value is Array arr)
                {
                    if (arr.Length == 0) return false;

                    object? elem = null;
                    if (frameIndex < arr.Length) elem = arr.GetValue(frameIndex);
                    else elem = arr.GetValue(0);

                    if (elem == null) return false;

                    return TryInterpretDelaySingle(elem, out delayMs);
                }

                // Single numeric-like value
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
                case byte b:
                    delayMs = Math.Max(b * 10, 10);
                    return true;
                case sbyte sb:
                    delayMs = Math.Max((sb) * 10, 10);
                    return true;
                case ushort us:
                    delayMs = Math.Max(us * 10, 10);
                    return true;
                case short s:
                    delayMs = Math.Max(s * 10, 10);
                    return true;
                case int i:
                    // heuristic: small ints likely centiseconds
                    delayMs = i <= 1000 ? Math.Max(i * 10, 10) : i;
                    return true;
                case long l:
                    delayMs = l <= 1000 ? Math.Max((int)l * 10, 10) : (int)l;
                    return true;
                case double d:
                    delayMs = d <= 1000 ? Math.Max((int)(d * 10), 10) : (int)d;
                    return true;
                case float f:
                    delayMs = f <= 1000 ? Math.Max((int)(f * 10), 10) : (int)f;
                    return true;
                default:
                    // try convertible
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

        /// <summary>
        /// Lazy-load textures on the main thread.
        /// </summary>
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
            if (frames.Count == 0) return;

            elapsedMs += deltaMs;
            if (elapsedMs >= frames[currentFrame].DelayMs)
            {
                elapsedMs -= frames[currentFrame].DelayMs; // subtract (not reset) to avoid time drift
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
            try
            {
                // only delete temp files; do not dispose shared textures
                foreach (var (path, _) in framePaths)
                    if (File.Exists(path))
                        File.Delete(path);

                Directory.Delete(tempFolder, true);
            }
            catch { }
        }
    }
}
