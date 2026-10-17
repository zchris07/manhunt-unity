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

        public static Texture2D Picture(int index) => Resources.Load<Texture2D>("Images/" + Name(index));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => names = null;
    }
}
