using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;
using System.IO;

namespace Jumpscare
{
    public static class Sounds
    {
        private static SoundPlayer? player;

        public static void PlayWav(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    player = new SoundPlayer(path);
                    player.Play(); // non-blocking
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
            player?.Stop();
            player = null;
        }
    }
}
