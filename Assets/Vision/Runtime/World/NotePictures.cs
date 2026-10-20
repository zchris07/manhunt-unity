using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The notes' photos: the original's six (Resources/Images/note-1 to note-6) and any the user added for their own
    /// builds (local_note-7 and on, kept out of git: photos of real people), in that order. Every one lies somewhere on the
    /// map (<see cref="SandboxWorld"/>'s notes).
    /// </summary>
    public static class NotePictures
    {
        static List<string> names;

        /// <summary>The photos' resource names (under Resources/Images), in order.</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                if (names != null) return names;
                names = new List<string>();
                for (int i = 1; i <= Game.Balance.Notes.Images; i++) names.Add($"note-{i}");
                // The user's own, numbered on from the original's, as long as they run.
                for (int i = Game.Balance.Notes.Images + 1; i < Game.Balance.Notes.Images + 32; i++)
                {
                    string local = $"local_note-{i}";
                    if (Resources.Load<Texture2D>("Images/" + local) == null) break;
                    names.Add(local);
                }
                return names;
            }
        }

        public static int Count => Names.Count;

        /// <summary>Photo <paramref name="index"/> (0-based; out of range wraps round).</summary>
        public static string Name(int index) => Count == 0 ? "note-1" : Names[((index % Count) + Count) % Count];

        static readonly Dictionary<string, Texture2D> mono = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Photo <paramref name="index"/> as the notes show it: black and white, every one (the user's rule), whatever colour
        /// the file is (made once, from the imported picture through the GPU, so it needn't be readable).
        /// </summary>
        public static Texture2D Picture(int index)
        {
            string name = Name(index);
            if (mono.TryGetValue(name, out Texture2D done) && done != null) return done;
            Texture2D src = Resources.Load<Texture2D>("Images/" + name);
            if (src == null) return null;
            Texture2D bw = Monochrome(src);
            mono[name] = bw;
            return bw;
        }

        /// <summary>A black and white copy of a picture (Rec. 709 luminance).</summary>
        public static Texture2D Monochrome(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGB24, false) { name = src.name + " (b&w)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                byte y = (byte)Mathf.Clamp(Mathf.RoundToInt(px[i].r * 0.2126f + px[i].g * 0.7152f + px[i].b * 0.0722f), 0, 255);
                px[i] = new Color32(y, y, y, 255);
            }
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            names = null;
            mono.Clear();
        }
    }
}
