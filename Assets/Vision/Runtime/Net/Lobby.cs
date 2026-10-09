using System;
using System.Collections.Generic;
using System.IO;
using Vision.Game;

namespace Vision.Net
{
    public enum RolePref : byte { Any = 0, Survivor = 1, Hunter = 2 }
    public enum Assigned : byte { Auto = 0, Hunter = 1, Survivor = 2, Spectator = 3 }

    public sealed class LobbyPlayer
    {
        public int Id;
        public string Name;
        public string Token;
        public RolePref Pref;
        public Assigned Assigned;
        public bool Ready, Connected = true, Owner;
        public float JoinedAt, LeftAt;
        public int Ping;
    }

    /// <summary>The host's match settings (the original's: hunters and survivors 1-9, a seed, testing mode) and its pace.</summary>
    public sealed class LobbySettings
    {
        public int Hunters = 1, Survivors = 9;
        public string Seed = "";
        public bool TestMode;
        /// <summary>The host's movement pace (everyone moves at it online).</summary>
        public float Pace = 1f;

        public LobbySettings Clamped() => new LobbySettings
        {
            Hunters = Math.Max(1, Math.Min(9, Hunters)),
            Survivors = Math.Max(1, Math.Min(9, Survivors)),
            Seed = (Seed ?? "").Length > 32 ? Seed.Substring(0, 32) : Seed ?? "",
            TestMode = TestMode,
            Pace = Math.Max(Scale.MinPace, Math.Min(Scale.MaxPace, Pace)),
        };

        public void Write(BinaryWriter w)
        {
            w.Write(Hunters);
            w.Write(Survivors);
            Wire.WriteString(w, Seed);
            w.Write(TestMode);
            w.Write(Pace);
        }

        public static LobbySettings Read(BinaryReader r) => new LobbySettings
        {
            Hunters = r.ReadInt32(), Survivors = r.ReadInt32(), Seed = Wire.ReadString(r) ?? "", TestMode = r.ReadBoolean(), Pace = r.ReadSingle(),
        };

        /// <summary>The map seed: the number typed, a word's stable hash, or a random one when blank.</summary>
        public int SeedValue(Random rng)
        {
            string s = (Seed ?? "").Trim();
            if (s.Length == 0) return rng.Next(1, 1000000);
            if (int.TryParse(s, out int n)) return n;
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return (int)(h % 999999) + 1;
            }
        }
    }

    /// <summary>One player in a match about to start: who they are and what they play.</summary>
    public struct RosterEntry
    {
        public int Id;
        public string Name;
        public Role Role;
    }

    /// <summary>
    /// The lobby: who is in it, their preferences and readiness, the host's settings, and the original's role split
    /// (lobby.ts): the host's assignments win; hunter slots are filled by preference (Zach, then either, then survivor),
    /// survivors take the rest up to the cap, and anyone left over spectates.
    /// </summary>
    public sealed class Lobby
    {
        public readonly List<LobbyPlayer> Players = new List<LobbyPlayer>();
        public LobbySettings Settings = new LobbySettings();
        int nextId = 1;

        public LobbyPlayer Get(int id) => Players.Find(p => p.Id == id);
        public LobbyPlayer ByToken(string token) => string.IsNullOrEmpty(token) ? null : Players.Find(p => p.Token == token);

        public LobbyPlayer Add(string name, string token, float now)
        {
            int id = nextId;
            // Ids stay below the testing dummies' (100 on).
            while (Get(id) != null || id == 0) id = id % 99 + 1;
            nextId = id % 99 + 1;
            var p = new LobbyPlayer { Id = id, Name = UniqueName(name, -1), Token = token, JoinedAt = now };
            Players.Add(p);
            return p;
        }

        public void Remove(int id) => Players.RemoveAll(p => p.Id == id);

        public string UniqueName(string name, int excludeId)
        {
            bool Taken(string n) => Players.Exists(p => p.Id != excludeId && string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase));
            if (!Taken(name)) return name;
            for (int i = 2; i < 20; i++)
            {
                string n = $"{(name.Length > 13 ? name.Substring(0, 13) : name)} {i}";
                if (!Taken(n)) return n;
            }
            return name;
        }

        /// <summary>Who plays what, as the original splits them.</summary>
        public (List<int> hunters, List<int> survivors, List<int> spectators) ResolveRoles(Random rng)
        {
            var present = Players.FindAll(p => p.Connected);
            present.Sort((a, b) => a.JoinedAt.CompareTo(b.JoinedAt));
            var hunters = new List<int>();
            var survivors = new List<int>();
            var spectators = new List<int>();
            var free = new List<LobbyPlayer>();
            foreach (LobbyPlayer p in present)
            {
                if (p.Assigned == Assigned.Hunter) hunters.Add(p.Id);
                else if (p.Assigned == Assigned.Survivor) survivors.Add(p.Id);
                else if (p.Assigned == Assigned.Spectator) spectators.Add(p.Id);
                else free.Add(p);
            }
            int wantH = Math.Max(0, Math.Min(Settings.Hunters, present.Count - 1) - hunters.Count);
            Shuffle(free, rng);
            int Rank(LobbyPlayer p) => p.Pref == RolePref.Hunter ? 0 : p.Pref == RolePref.Any ? 1 : 2;
            var order = new List<LobbyPlayer>(free);
            StableSort(order, (a, b) => Rank(a).CompareTo(Rank(b)));
            for (int i = 0; i < order.Count && i < wantH; i++) hunters.Add(order[i].Id);
            var rest = order.GetRange(Math.Min(wantH, order.Count), Math.Max(0, order.Count - wantH));
            StableSort(rest, (a, b) => (a.Pref == RolePref.Survivor ? 0 : 1).CompareTo(b.Pref == RolePref.Survivor ? 0 : 1));
            int wantS = Math.Max(0, Settings.Survivors - survivors.Count);
            for (int i = 0; i < rest.Count; i++) (i < wantS ? survivors : spectators).Add(rest[i].Id);
            return (hunters, survivors, spectators);
        }

        /// <summary>The host's Shuffle roles: everyone present gets a fixed role at random.</summary>
        public void Shuffle(Random rng)
        {
            var present = Players.FindAll(p => p.Connected);
            Shuffle(present, rng);
            int h = Math.Min(Settings.Hunters, Math.Max(1, present.Count - 1));
            for (int i = 0; i < present.Count; i++)
                present[i].Assigned = i < h ? Assigned.Hunter : i - h < Settings.Survivors ? Assigned.Survivor : Assigned.Spectator;
        }

        /// <summary>The split the next match would have (for the lobby's preview line).</summary>
        public (int h, int s, int spec) Preview()
        {
            var r = ResolveRoles(new Random(1));
            return (r.hunters.Count, r.survivors.Count, r.spectators.Count);
        }

        public bool AllReady() => Players.TrueForAll(p => !p.Connected || p.Ready || p.Owner);

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static void StableSort<T>(List<T> list, Comparison<T> cmp)
        {
            for (int i = 1; i < list.Count; i++)
            {
                T x = list[i];
                int j = i - 1;
                while (j >= 0 && cmp(list[j], x) > 0) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
        }

        public void Write(BinaryWriter w)
        {
            Settings.Write(w);
            w.Write(Players.Count);
            foreach (LobbyPlayer p in Players)
            {
                w.Write(p.Id);
                Wire.WriteString(w, p.Name);
                w.Write((byte)p.Pref);
                w.Write((byte)p.Assigned);
                w.Write(p.Ready || p.Owner);
                w.Write(p.Connected);
                w.Write(p.Owner);
                w.Write(p.Ping);
            }
        }

        public static Lobby Read(BinaryReader r)
        {
            var l = new Lobby { Settings = LobbySettings.Read(r) };
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
                l.Players.Add(new LobbyPlayer
                {
                    Id = r.ReadInt32(), Name = Wire.ReadString(r), Pref = (RolePref)r.ReadByte(), Assigned = (Assigned)r.ReadByte(),
                    Ready = r.ReadBoolean(), Connected = r.ReadBoolean(), Owner = r.ReadBoolean(), Ping = r.ReadInt32(), JoinedAt = i,
                });
            return l;
        }
    }
}
