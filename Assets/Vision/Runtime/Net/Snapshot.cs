using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Net
{
    /// <summary>
    /// The host's view of the match, cut into sections (the roster, each player, their movement and inventory, each NPC,
    /// the match's own fields, each generator, the gate, and the lists of things in flight). A section is written whole
    /// (lists) or field by field; <see cref="SnapshotWriter"/> sends a peer only what changed since its last snapshot
    /// (TCP keeps them in order, so no acknowledgements are needed).
    /// </summary>
    public sealed class SnapshotFrame
    {
        public enum Kind : byte { End = 0, Roster = 1, Player = 2, Move = 3, Inventory = 4, Npc = 5, Match = 6, Gen = 7, Gate = 8, List = 9, Result = 10 }

        public sealed class Section
        {
            public int Key;
            public bool Fields;
            public byte[] Bytes;
            public int[] Offsets;
            /// <summary>What a hunter gets instead (a hidden survivor, placed nowhere), or null for the same.</summary>
            public byte[] HunterBytes;
            public int[] HunterOffsets;
            public bool HuntersOnly;
        }

        public int Tick;
        public float Time;
        public readonly List<Section> Sections = new List<Section>();

        public static int Key(Kind k, int index) => ((int)k << 16) | (index & 0xFFFF);

        /// <summary>The lists sent whole, in a fixed order (the index of a List section).</summary>
        public static readonly (string name, Type element, bool huntersOnly)[] Lists =
        {
            ("Drops", typeof(DropItem), false), ("Thrown", typeof(ThrownItem), false), ("Traps", typeof(Trap), false), ("Gases", typeof(GasCloud), false),
            ("Bursts", typeof(BurstWave), false), ("Vapes", typeof(VapeCloud), false), ("Snipes", typeof(Bullet), false), ("Trails", typeof(TrailPoint), true),
        };

        public static IList ListOf(MatchSim sim, int i) => i switch
        {
            0 => sim.Drops, 1 => sim.Thrown, 2 => sim.Traps, 3 => sim.Gases, 4 => sim.Bursts, 5 => sim.Vapes, 6 => sim.Snipes, _ => sim.Trails,
        };

        static readonly string[] PlayerSkip = { "IsLocal", "PrevButtons", "LastTickPos" };
        static readonly string[] NpcSkip = { "Sim", "Rnd", "C" };
        static readonly string[] MatchSkip = { "TestMode", "LastPellets" };
        public static FieldPlan PlayerPlan => FieldPlan.For(typeof(SimPlayer), false, PlayerSkip);
        public static FieldPlan MovePlan => FieldPlan.For(typeof(MoveState), false, "PrevButtons");
        public static FieldPlan NpcPlan(Npc n) => FieldPlan.For(n.GetType(), true, NpcSkip);
        public static FieldPlan MatchPlan => FieldPlan.For(typeof(MatchSim), false, MatchSkip);

        readonly MemoryStream ms = new MemoryStream(1 << 14);
        BinaryWriter w;

        /// <summary>Takes the match as it is now.</summary>
        public static SnapshotFrame Build(MatchSim sim)
        {
            var f = new SnapshotFrame { Tick = sim.Tick, Time = sim.Time };
            f.w = new BinaryWriter(f.ms);
            f.Whole(Key(Kind.Roster, 0), w => WriteRoster(w, sim));
            foreach (SimPlayer p in sim.Order)
            {
                Section s = f.Object(Key(Kind.Player, p.Id), PlayerPlan, p);
                if (p.Role == Role.Survivor && p.HideState == 2)
                {
                    // Zach never learns where a hidden survivor is.
                    Vector2 pos = p.Pos;
                    int spot = p.HideSpot;
                    p.Pos = Vector2.zero;
                    p.HideSpot = -1;
                    Section h = f.Object(0, PlayerPlan, p);
                    p.Pos = pos;
                    p.HideSpot = spot;
                    s.HunterBytes = h.Bytes;
                    s.HunterOffsets = h.Offsets;
                    f.Sections.Remove(h);
                }
                f.Object(Key(Kind.Move, p.Id), MovePlan, p.Move);
                f.Whole(Key(Kind.Inventory, p.Id), w => WriteInventory(w, p.Inv));
            }
            for (int i = 0; i < sim.Npcs.Count; i++) f.Object(Key(Kind.Npc, i), NpcPlan(sim.Npcs[i]), sim.Npcs[i]);
            f.Object(Key(Kind.Match, 0), MatchPlan, sim);
            for (int i = 0; i < sim.Gens.Length; i++) f.Object(Key(Kind.Gen, i), FieldPlan.For(typeof(GenState)), sim.Gens[i]);
            f.Object(Key(Kind.Gate, 0), FieldPlan.For(typeof(GateState)), sim.Gate);
            for (int i = 0; i < Lists.Length; i++)
            {
                int li = i;
                Section s = f.Whole(Key(Kind.List, i), w => FieldPlan.WriteList(w, ListOf(sim, li), Lists[li].element));
                s.HuntersOnly = Lists[i].huntersOnly;
            }
            if (sim.Result != null) f.Whole(Key(Kind.Result, 0), w => WriteResult(w, sim.Result));
            return f;
        }

        Section Whole(int key, Action<BinaryWriter> write)
        {
            ms.SetLength(0);
            write(w);
            w.Flush();
            var s = new Section { Key = key, Fields = false, Bytes = ms.ToArray() };
            Sections.Add(s);
            return s;
        }

        Section Object(int key, FieldPlan plan, object o)
        {
            ms.SetLength(0);
            var offsets = new int[plan.Count + 1];
            plan.WriteAll(w, o, offsets);
            w.Flush();
            var s = new Section { Key = key, Fields = true, Bytes = ms.ToArray(), Offsets = offsets };
            Sections.Add(s);
            return s;
        }

        static void WriteRoster(BinaryWriter w, MatchSim sim)
        {
            w.Write(sim.Order.Count);
            foreach (SimPlayer p in sim.Order)
            {
                w.Write(p.Id);
                Wire.WriteString(w, p.Name);
                w.Write((byte)p.Role);
                w.Write(p.IsDummy);
            }
        }

        public static void WriteInventory(BinaryWriter w, Inventory inv)
        {
            w.Write(inv.Infinite);
            for (int i = 0; i < Inventory.TestingSlots; i++)
            {
                Inventory.Slot s = inv.SlotAt(i);
                w.Write((byte)s.item);
                w.Write(s.count);
                w.Write(s.golden);
                w.Write(s.amounts.Count);
                foreach (float a in s.amounts) w.Write(a);
            }
        }

        public static void ReadInventory(BinaryReader r, Inventory inv)
        {
            inv.Infinite = r.ReadBoolean();
            for (int i = 0; i < Inventory.TestingSlots; i++)
            {
                Inventory.Slot s = inv.SlotAt(i);
                s.item = (ItemType)r.ReadByte();
                s.count = r.ReadInt32();
                s.golden = r.ReadBoolean();
                int n = r.ReadInt32();
                s.amounts.Clear();
                for (int k = 0; k < n; k++) s.amounts.Add(r.ReadSingle());
            }
        }

        static void WriteResult(BinaryWriter w, MatchResult res)
        {
            w.Write((byte)res.Winner);
            Wire.WriteString(w, res.Reason);
            w.Write(res.DurationSec);
            w.Write(res.Escaped);
            w.Write(res.Eliminated);
            w.Write(res.Survivors);
            w.Write(res.Hunters);
            w.Write(res.GeneratorsRepaired);
            w.Write(res.GeneratorsRequired);
            w.Write(res.Stats.Count);
            FieldPlan sp = FieldPlan.For(typeof(MatchStats));
            foreach (var (id, name, role, stats) in res.Stats)
            {
                w.Write(id);
                Wire.WriteString(w, name);
                w.Write((byte)role);
                sp.WriteAll(w, stats, new int[sp.Count + 1]);
            }
        }

        public static MatchResult ReadResult(BinaryReader r)
        {
            var res = new MatchResult
            {
                Winner = (Winner)r.ReadByte(), Reason = Wire.ReadString(r), DurationSec = r.ReadSingle(), Escaped = r.ReadInt32(), Eliminated = r.ReadInt32(),
                Survivors = r.ReadInt32(), Hunters = r.ReadInt32(), GeneratorsRepaired = r.ReadInt32(), GeneratorsRequired = r.ReadInt32(),
            };
            int n = r.ReadInt32();
            FieldPlan sp = FieldPlan.For(typeof(MatchStats));
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                string name = Wire.ReadString(r);
                var role = (Role)r.ReadByte();
                var stats = new MatchStats();
                sp.ReadAll(r, stats);
                res.Stats.Add((id, name, role, stats));
            }
            return res;
        }
    }

    /// <summary>Writes one peer's snapshots: each section only when (or where) it changed since the last one sent to them.</summary>
    public sealed class SnapshotWriter
    {
        readonly Dictionary<int, (byte[] bytes, int[] offsets)> last = new Dictionary<int, (byte[], int[])>();

        /// <summary>Forget what was sent (the next snapshot is complete), e.g. after the peer reloaded the level.</summary>
        public void Reset() => last.Clear();

        public byte[] Write(SnapshotFrame f, bool hunter, IList<GameEvent> events)
        {
            BinaryWriter w = Wire.Begin(Msg.Snapshot);
            w.Write(f.Tick);
            w.Write(f.Time);
            foreach (SnapshotFrame.Section s in f.Sections)
            {
                if (s.HuntersOnly && !hunter) continue;
                byte[] bytes = hunter && s.HunterBytes != null ? s.HunterBytes : s.Bytes;
                int[] offsets = hunter && s.HunterBytes != null ? s.HunterOffsets : s.Offsets;
                last.TryGetValue(s.Key, out var prev);
                if (!s.Fields)
                {
                    if (prev.bytes != null && Same(prev.bytes, 0, prev.bytes.Length, bytes, 0, bytes.Length)) continue;
                    w.Write(s.Key);
                    w.Write(bytes.Length);
                    w.Write(bytes);
                }
                else
                {
                    int n = offsets.Length - 1;
                    var mask = new byte[(n + 7) / 8];
                    bool any = false;
                    for (int i = 0; i < n; i++)
                    {
                        bool changed = prev.bytes == null || prev.offsets.Length != offsets.Length ||
                                       !Same(prev.bytes, prev.offsets[i], prev.offsets[i + 1] - prev.offsets[i], bytes, offsets[i], offsets[i + 1] - offsets[i]);
                        if (!changed) continue;
                        mask[i >> 3] |= (byte)(1 << (i & 7));
                        any = true;
                    }
                    if (!any) continue;
                    w.Write(s.Key);
                    w.Write((ushort)n);
                    w.Write(mask);
                    for (int i = 0; i < n; i++)
                        if ((mask[i >> 3] & (1 << (i & 7))) != 0) w.Write(bytes, offsets[i], offsets[i + 1] - offsets[i]);
                }
                last[s.Key] = (bytes, offsets);
            }
            w.Write(0);
            w.Write((ushort)events.Count);
            foreach (GameEvent e in events) Wire.Write(w, e);
            return Wire.End(w);
        }

        static bool Same(byte[] a, int ao, int an, byte[] b, int bo, int bn)
        {
            if (an != bn) return false;
            for (int i = 0; i < an; i++) if (a[ao + i] != b[bo + i]) return false;
            return true;
        }
    }

    /// <summary>
    /// Puts the host's snapshots onto a client's copy of the match. The local player keeps their own movement: their
    /// position only changes when the rules placed them, and only the rules' own changes to their movement come through
    /// (a knockback, a slow, a drink, a stopped lunge, a lunge charge).
    /// </summary>
    public sealed class SnapshotReader
    {
        readonly MoveState hostMove = new MoveState(Role.Survivor);
        readonly MoveState prevHost = new MoveState(Role.Survivor);
        int lungeStopSeen = -1, hostMoveFor = -1;
        public int LastTick { get; private set; }

        public void Reset()
        {
            lungeStopSeen = -1;
            hostMoveFor = -1;
        }

        public List<GameEvent> Apply(MatchSim sim, byte[] data, int offset, int localId)
        {
            var r = new BinaryReader(new MemoryStream(data, offset, data.Length - offset));
            LastTick = r.ReadInt32();
            sim.Tick = LastTick;
            sim.Time = r.ReadSingle();
            while (true)
            {
                int key = r.ReadInt32();
                if (key == 0) break;
                var kind = (SnapshotFrame.Kind)(key >> 16);
                int index = key & 0xFFFF;
                switch (kind)
                {
                    case SnapshotFrame.Kind.Roster:
                        r.ReadInt32();
                        ReadRoster(r, sim);
                        break;
                    case SnapshotFrame.Kind.Player:
                    {
                        SimPlayer p = sim.Get(index);
                        if (p == null) { Skip(r, SnapshotFrame.PlayerPlan); break; }
                        if (p.Id != localId) { Fields(r, SnapshotFrame.PlayerPlan, p); break; }
                        // The local player: their pose is theirs unless the rules placed them.
                        Vector2 pos = p.Pos;
                        float facing = p.Facing, aimDist = p.AimDist;
                        Gait gait = p.Gait;
                        int sel = p.SelSlot, placed = p.PlaceVersion;
                        Fields(r, SnapshotFrame.PlayerPlan, p);
                        if (p.PlaceVersion == placed) p.Pos = pos;
                        p.Facing = facing;
                        p.AimDist = aimDist;
                        p.Gait = gait;
                        p.SelSlot = sel;
                        break;
                    }
                    case SnapshotFrame.Kind.Move:
                    {
                        SimPlayer p = sim.Get(index);
                        if (p == null) { Skip(r, SnapshotFrame.MovePlan); break; }
                        if (p.Id != localId) { Fields(r, SnapshotFrame.MovePlan, p.Move); break; }
                        if (hostMoveFor != p.Id)
                        {
                            hostMoveFor = p.Id;
                            Copy(p.Move, hostMove);
                            lungeStopSeen = p.LungeStopVersion;
                        }
                        Copy(hostMove, prevHost);
                        Fields(r, SnapshotFrame.MovePlan, hostMove);
                        Merge(p);
                        break;
                    }
                    case SnapshotFrame.Kind.Inventory:
                    {
                        int n = r.ReadInt32();
                        SimPlayer p = sim.Get(index);
                        if (p == null) { r.ReadBytes(n); break; }
                        SnapshotFrame.ReadInventory(r, p.Inv);
                        break;
                    }
                    case SnapshotFrame.Kind.Npc:
                        if (index < sim.Npcs.Count) Fields(r, SnapshotFrame.NpcPlan(sim.Npcs[index]), sim.Npcs[index]);
                        else throw new InvalidDataException("unknown NPC");
                        break;
                    case SnapshotFrame.Kind.Match:
                        Fields(r, SnapshotFrame.MatchPlan, sim);
                        break;
                    case SnapshotFrame.Kind.Gen:
                        Fields(r, FieldPlan.For(typeof(GenState)), sim.Gens[index]);
                        break;
                    case SnapshotFrame.Kind.Gate:
                        Fields(r, FieldPlan.For(typeof(GateState)), sim.Gate);
                        break;
                    case SnapshotFrame.Kind.List:
                        r.ReadInt32();
                        FieldPlan.ReadList(r, SnapshotFrame.ListOf(sim, index), SnapshotFrame.Lists[index].element);
                        break;
                    case SnapshotFrame.Kind.Result:
                        r.ReadInt32();
                        sim.Result = SnapshotFrame.ReadResult(r);
                        break;
                    default:
                        throw new InvalidDataException($"unknown section {key:x}");
                }
            }
            int count = r.ReadUInt16();
            var events = new List<GameEvent>(count);
            for (int i = 0; i < count; i++) events.Add(Wire.ReadEvent(r));
            return events;
        }

        static void Fields(BinaryReader r, FieldPlan plan, object o)
        {
            int n = r.ReadUInt16();
            if (n != plan.Count) throw new InvalidDataException("field count mismatch (different versions?)");
            byte[] mask = r.ReadBytes((n + 7) / 8);
            for (int i = 0; i < n; i++)
                if ((mask[i >> 3] & (1 << (i & 7))) != 0) plan.Read(r, o, i);
        }

        /// <summary>Reads a field section into a throwaway (a player who isn't here any more).</summary>
        static void Skip(BinaryReader r, FieldPlan plan)
        {
            object scratch = plan == SnapshotFrame.MovePlan ? new MoveState(Role.Survivor) : (object)new SimPlayer(0, "", Role.Survivor, Vector2.zero);
            Fields(r, plan, scratch);
        }

        static void Copy(MoveState from, MoveState to)
        {
            FieldPlan plan = SnapshotFrame.MovePlan;
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            plan.WriteAll(w, from, new int[plan.Count + 1]);
            w.Flush();
            ms.Position = 0;
            plan.ReadAll(new BinaryReader(ms), to);
        }

        /// <summary>The rules' changes to the local player's movement, taken where the host's value went up.</summary>
        void Merge(SimPlayer p)
        {
            MoveState m = p.Move, h = hostMove, before = prevHost;
            const float Eps = 1e-4f;
            if (h.SlowT > before.SlowT + Eps) { m.SlowT = h.SlowT; m.SlowMul = h.SlowMul; }
            if (h.HasteT > before.HasteT + Eps) m.HasteT = h.HasteT;
            if (h.HempT > before.HempT + Eps) m.HempT = h.HempT;
            if (h.BoostT > before.BoostT + Eps) m.BoostT = h.BoostT;
            if (h.StaminaLock > before.StaminaLock + Eps) m.StaminaLock = h.StaminaLock;
            if (h.KbT > before.KbT + Eps)
            {
                m.KbT = h.KbT;
                m.KbDur = h.KbDur;
                m.KbPeak = h.KbPeak;
                m.KbAng = h.KbAng;
            }
            if (h.SprintBlocked && !before.SprintBlocked) m.SprintBlocked = true;
            if (h.LungeCharges > before.LungeCharges) m.LungeCharges += h.LungeCharges - before.LungeCharges;
            if (p.LungeStopVersion != lungeStopSeen)
            {
                lungeStopSeen = p.LungeStopVersion;
                m.LungeT = 0f;
            }
        }

        static void ReadRoster(BinaryReader r, MatchSim sim)
        {
            int n = r.ReadInt32();
            var present = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                string name = Wire.ReadString(r);
                var role = (Role)r.ReadByte();
                bool dummy = r.ReadBoolean();
                present.Add(id);
                SimPlayer p = sim.Get(id);
                if (p == null || p.Role != role)
                {
                    if (p != null) sim.DropPlayer(id);
                    p = sim.AddPlayer(id, name, role, Vector2.zero);
                }
                p.Name = name;
                p.IsDummy = dummy;
            }
            foreach (SimPlayer p in sim.Order.ToArray())
                if (!present.Contains(p.Id)) sim.DropPlayer(p.Id);
        }
    }
}
