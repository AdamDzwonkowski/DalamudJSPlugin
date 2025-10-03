using System;
using System.Media;
using System.IO;

namespace Jumpscare
{
    public static class Sounds
    {
        private static SoundPlayer? Player;

        public static void PlayWav(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Player = new SoundPlayer(path);
                    Player.Play(); // non-blocking
                }
                else
                {
                    Plugin.Log.Warning($"Sound file not found: {path}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to play sound: {ex}");
            }
        }

        public static void Stop()
        {
            Player?.Stop();
            Player = null;
        }
    }
}
