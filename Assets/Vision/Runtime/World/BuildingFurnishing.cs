using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// What fills the building: the two generators, furniture and clutter by room type, lockers along the hallways, pipes,
    /// ducts and electrics, decorations and a sparse set of lamps. Each room is filled by a <see cref="Filler"/> that keeps
    /// the doorways clear, keeps furniture to under half the floor (so there is always room to move and loop) and stops
    /// tall pieces from covering windows.
    /// </summary>
    public sealed partial class BuildingPlan
    {
        public enum Furn
        {
            Generator, Desk, OfficeChair, FilingCabinet, Shelf, Rack, Crate, CrateStack, Boxes, Barrel, Pallet, PalletStack,
            Table, Chair, Sofa, Fridge, Vending, Counter, Toilet, Stall, Sink, Locker, LockerBank, Bench, Workbench, CableSpool,
            ServerRack, Boiler, Transformer, SetFlat, LightRig, Camera, DirectorChair, Mannequin, Bed, Wardrobe, Mattress, Forklift,
            // On the walls
            BreakerPanel, JunctionBox, Poster, Clock, Extinguisher, VentGrille, Mirror, ToolBoard, Whiteboard,
            // Loose on the floor, and overhead runs
            Papers, Bottles, Stain, Debris, Cobweb, DeadPlant, Pipes, Duct,
            // The lounge's TV on its stand.
            Tv,
        }

        public struct Item
        {
            public Furn Kind;
            /// <summary>Footprint centre on the floor (runs of pipe or duct: their start).</summary>
            public Vector2 Position;
            /// <summary>Degrees about up; the model's front (+Z) turned by this. Against a wall the front faces into the room.</summary>
            public float Yaw;
            /// <summary>Model-local width (x) and depth (z); runs: length in x.</summary>
            public Vector2 Size;
            public float Elevation;
            public int Room;
            public bool Solid, Tall, Hide;
            public int Variant;

            /// <summary>The axis-aligned floor footprint.</summary>
            public Rect Footprint
            {
                get
                {
                    bool turned = Mathf.Abs(Mathf.Sin(Yaw * Mathf.Deg2Rad)) > 0.7f;
                    Vector2 s = turned ? new Vector2(Size.y, Size.x) : Size;
                    return new Rect(Position - s * 0.5f, s);
                }
            }
        }

        public readonly List<Item> Items = new List<Item>();
        /// <summary>Floor spots clear of furniture where supplies may lie (the original: a fifth of its 3.6 m cells).</summary>
        public readonly List<Vector2> LootSpots = new List<Vector2>();
        public IEnumerable<Item> Generators { get { foreach (Item i in Items) if (i.Kind == Furn.Generator) yield return i; } }

        readonly List<Vector2> hallLockers = new List<Vector2>();

        /// <summary>The lounge (the original's): the biggest room without a generator, with a couch facing a TV.</summary>
        public int LoungeRoom = -1;
        /// <summary>Where Chacko sits on the couch, and the TV he watches.</summary>
        public Vector2 LoungeSeat, LoungeTv;

        void Furnish()
        {
            float best = 0f;
            foreach (Room room in Rooms)
                if (!room.HasGenerator && !room.IsHallway && room.Id != GateRoom && room.FloorArea > best)
                {
                    best = room.FloorArea;
                    LoungeRoom = room.Id;
                }
            foreach (Room room in Rooms)
            {
                var f = new Filler(this, room);
                if (room.HasGenerator) f.Generator();
                if (room.Id == LoungeRoom) f.Lounge(out LoungeSeat, out LoungeTv);
                else switch (room.Type)
                {
                    case RoomType.Hallway: f.Hallway(); break;
                    case RoomType.Office: f.Office(); break;
                    case RoomType.Storage: f.Storage(); break;
                    case RoomType.BreakRoom: f.BreakRoom(); break;
                    case RoomType.Restroom: f.Restroom(); break;
                    case RoomType.LockerRoom: f.LockerRoom(); break;
                    case RoomType.Workshop: f.Workshop(); break;
                    case RoomType.Electrical: f.Electrical(); break;
                    case RoomType.ServerRoom: f.ServerRoom(); break;
                    case RoomType.StudioSet: f.StudioSet(); break;
                    case RoomType.LoadingBay: f.LoadingBay(); break;
                    case RoomType.Boiler: f.Boiler(); break;
                }
                f.Common();
                int cells = Mathf.Max(1, Mathf.RoundToInt(room.FloorArea / 13f)), loot = 0;
                for (int i = 0; i < cells; i++) if (Chance(0.2f)) loot++;
                f.Loot(loot);
            }
            PlaceLamps();
        }

        /// <summary>
        /// Only a fraction of the rooms and hallways are lit, each by something that belongs there: a fluorescent fitting in
        /// hallways, offices and restrooms, a bare bulb in storage and workshops, a desk lamp, a vending machine's glow, the
        /// boiler's fire, a server rack's lights, a stage light on the set, and green exit signs over some doors. The rest of the
        /// building is dark, with a few dead fixtures.
        /// </summary>
        void PlaceLamps()
        {
            var lit = new HashSet<int>();
            if (GateRoom >= 0 && Chance(0.7f)) lit.Add(GateRoom);
            var order = new List<Room>(Rooms);
            Shuffle(order);
            int want = Mathf.Clamp(Mathf.RoundToInt(Rooms.Count * Range(0.32f, 0.42f)), 6, 15);
            foreach (Room r in order)
            {
                if (lit.Count >= want) break;
                lit.Add(r.Id);
            }
            foreach (Room r in Rooms) LightSpace(r, lit.Contains(r.Id));
            ExitSigns();
            EnoughLight();
        }

        static LampKind CeilingKind(RoomType t) =>
            t == RoomType.Storage || t == RoomType.Boiler || t == RoomType.Workshop || t == RoomType.Electrical ? LampKind.Bulb : LampKind.Fluorescent;

        bool FindItem(int room, Furn kind, out Item found)
        {
            foreach (Item i in Items)
                if (i.Room == room && i.Kind == kind) { found = i; return true; }
            found = default;
            return false;
        }

        void LightSpace(Room r, bool on)
        {
            Rect a = r.Area;
            LampKind ceiling = CeilingKind(r.Type);
            Vector2 Centre() => a.center + new Vector2(Range(-0.4f, 0.4f), Range(-0.4f, 0.4f)) * Mathf.Min(a.width, a.height) * 0.3f;
            if (r.IsHallway)
            {
                bool alongX = a.width >= a.height;
                float len = alongX ? a.width : a.height;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / Range(8f, 11f)));
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    Vector2 p = alongX ? new Vector2(Mathf.Lerp(a.xMin, a.xMax, t), a.center.y) : new Vector2(a.center.x, Mathf.Lerp(a.yMin, a.yMax, t));
                    if (!on && !Chance(0.3f)) continue;
                    AddLamp(p, r, on && Chance(0.75f), LampKind.Fluorescent);
                }
                return;
            }
            // The lounge: the TV is always on, a cold blue-green glow over the room.
            if (r.Id == LoungeRoom && FindItem(r.Id, Furn.Tv, out Item tv))
            {
                AddAsset(LampKind.Tv, tv, 0.9f);
                if (on) AddLamp(Centre(), r, Chance(0.5f), ceiling);
                return;
            }
            if (!on)
            {
                if (r.Type != RoomType.ServerRoom && Chance(0.3f)) AddLamp(Centre(), r, false, ceiling);
                return;
            }
            switch (r.Type)
            {
                case RoomType.Office:
                    if (FindItem(r.Id, Furn.Desk, out Item desk) && Chance(0.55f)) AddAsset(LampKind.Desk, desk, 0.95f, true);
                    else AddLamp(Centre(), r, true, ceiling);
                    break;
                case RoomType.BreakRoom:
                    AddLamp(Centre(), r, true, ceiling);
                    if (FindItem(r.Id, Furn.Vending, out Item vend)) AddAsset(LampKind.Vending, vend, 1.2f);
                    break;
                case RoomType.Boiler:
                    if (FindItem(r.Id, Furn.Boiler, out Item boiler)) AddAsset(LampKind.Furnace, boiler, 0.5f);
                    else AddLamp(Centre(), r, true, ceiling);
                    break;
                case RoomType.ServerRoom:
                    if (FindItem(r.Id, Furn.ServerRack, out Item rack)) AddAsset(LampKind.Server, rack, 1.2f);
                    break;
                case RoomType.StudioSet:
                {
                    int rigs = 0;
                    foreach (Item it in Items)
                        if (it.Room == r.Id && it.Kind == Furn.LightRig && rigs < 2) { AddAsset(LampKind.Stage, it, 1.9f); rigs++; }
                    if (rigs == 0) AddLamp(Centre(), r, true, ceiling);
                    break;
                }
                default:
                    AddLamp(Centre(), r, true, ceiling);
                    break;
            }
            if (r.FloorArea > 45f && r.Type != RoomType.ServerRoom && r.Type != RoomType.Boiler && Chance(0.5f))
            {
                bool alongX = a.width >= a.height;
                Vector2 p = alongX ? new Vector2(Mathf.Lerp(a.xMin, a.xMax, 0.8f), a.center.y) : new Vector2(a.center.x, Mathf.Lerp(a.yMin, a.yMax, 0.8f));
                AddLamp(p, r, Chance(0.6f), ceiling);
            }
        }

        /// <summary>A green exit sign over some of the doors to the outside.</summary>
        void ExitSigns()
        {
            foreach (Opening o in Openings)
            {
                if (!o.Exterior || (o.Kind != OpeningKind.Door && o.Kind != OpeningKind.Doorway) || !Chance(0.6f)) continue;
                Interface f = Interfaces[o.Interface];
                Vector2 inward = -f.Normal;
                Lamps.Add(new Lamp
                {
                    Position = o.Centre + inward * 0.3f, Room = f.A, Working = Chance(0.75f), Kind = LampKind.Exit,
                    Yaw = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg, Elevation = 2.25f, Flicker = 0.06f,
                });
            }
        }

        /// <summary>Between 6 and 16 working lights, whatever the rooms came out like: the building is dark but a way through can be found.</summary>
        void EnoughLight()
        {
            int Working() { int n = 0; foreach (Lamp l in Lamps) if (l.Working && l.Kind != LampKind.Exit) n++; return n; }
            var dead = new List<int>();
            for (int i = 0; i < Lamps.Count; i++) if (!Lamps[i].Working && Lamps[i].Ceiling) dead.Add(i);
            Shuffle(dead);
            for (int i = 0; i < dead.Count && Working() < 6; i++) SetWorking(dead[i], true);
            var free = new List<Room>();
            foreach (Room r in Rooms) if (r.Type != RoomType.ServerRoom && r.Type != RoomType.Boiler) free.Add(r);
            Shuffle(free);
            for (int i = 0; i < free.Count && Working() < 6; i++)
            {
                bool has = false;
                foreach (Lamp l in Lamps) if (l.Room == free[i].Id && l.Working) has = true;
                if (!has) AddLamp(free[i].Area.center, free[i], true, free[i].IsHallway ? LampKind.Fluorescent : CeilingKind(free[i].Type));
            }
            var working = new List<int>();
            for (int i = 0; i < Lamps.Count; i++) if (Lamps[i].Working && Lamps[i].Ceiling) working.Add(i);
            Shuffle(working);
            for (int i = 0; i < working.Count && Working() > 16; i++) SetWorking(working[i], false);
        }

        void AddLamp(Vector2 p, Room r, bool working, LampKind kind) =>
            Lamps.Add(new Lamp { Position = p, Room = r.Id, Working = working, Kind = kind, Elevation = 2.55f, Flicker = working && Chance(0.4f) ? Range(0.12f, 0.35f) : 0.04f });

        /// <summary>A light that is part of something in the room (its own position and yaw).</summary>
        void AddAsset(LampKind kind, Item item, float height, bool onTop = false)
        {
            Vector2 pos = item.Position;
            if (onTop)
            {
                // At the end of the desk: model-local x runs (cos, -sin) in the plan, z (sin, cos).
                float yaw = item.Yaw * Mathf.Deg2Rad;
                pos += new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)) * item.Size.x * -0.3f;
            }
            Lamps.Add(new Lamp
            {
                Position = pos, Room = item.Room, Working = true, Kind = kind, Yaw = item.Yaw, Size = item.Size, Elevation = height,
                Flicker = kind == LampKind.Furnace ? 0.25f : kind == LampKind.Tv ? 0.24f : 0.04f,
            });
        }

        void SetWorking(int i, bool working)
        {
            Lamp l = Lamps[i];
            l.Working = working;
            Lamps[i] = l;
        }

        // ------------------------------------------------------------------ filling one room

        /// <summary>Sides of a room: 0 south, 1 east, 2 north, 3 west.</summary>
        static float YawFacingIn(int side) => side switch { 0 => 0f, 1 => -90f, 2 => 180f, _ => 90f };

        sealed class Filler
        {
            readonly BuildingPlan p;
            readonly Room room;
            readonly Rect inner;
            // Door zones and solid footprints (nothing solid may overlap these).
            readonly List<Rect> blocked = new List<Rect>();
            readonly List<Rect> solids = new List<Rect>();
            readonly List<(int side, float lo, float hi, bool passage)> gaps = new List<(int, float, float, bool)>();
            readonly float maxSolid;
            float solidArea;

            const float WallHalf = 0.16f;

            public Filler(BuildingPlan plan, Room room)
            {
                p = plan;
                this.room = room;
                Rect a = room.Area;
                inner = new Rect(a.xMin + WallHalf, a.yMin + WallHalf, a.width - 2f * WallHalf, a.height - 2f * WallHalf);
                maxSolid = inner.width * inner.height * (room.IsHallway ? 0.18f : 0.42f);
                foreach (int k in plan.InterfacesOf(room.Id))
                {
                    Interface f = plan.Interfaces[k];
                    int side = SideOf(f);
                    if (f.Open)
                    {
                        float lo = side % 2 == 0 ? Mathf.Min(f.P0.x, f.P1.x) : Mathf.Min(f.P0.y, f.P1.y);
                        float hi = side % 2 == 0 ? Mathf.Max(f.P0.x, f.P1.x) : Mathf.Max(f.P0.y, f.P1.y);
                        gaps.Add((side, lo, hi, true));
                        blocked.Add(Zone(side, lo, hi, 1.0f));
                        continue;
                    }
                    foreach (Opening o in plan.Openings)
                    {
                        if (o.Interface != k) continue;
                        float a0 = side % 2 == 0 ? o.A.x : o.A.y, a1 = side % 2 == 0 ? o.B.x : o.B.y;
                        float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1);
                        gaps.Add((side, lo, hi, o.IsPassage));
                        if (o.IsPassage) blocked.Add(Zone(side, lo - 0.35f, hi + 0.35f, o.Kind == OpeningKind.Gate ? 2.5f : 1.35f));
                    }
                }
                if (room.Id == plan.GateRoom) blocked.Add(new Rect(plan.Lever - Vector2.one * 0.6f, Vector2.one * 1.2f));
            }

            int SideOf(Interface f)
            {
                Rect a = room.Area;
                if (Mathf.Abs(f.P0.x - f.P1.x) < 1e-4f) return Mathf.Abs(f.P0.x - a.xMin) < 0.01f ? 3 : 1;
                return Mathf.Abs(f.P0.y - a.yMin) < 0.01f ? 0 : 2;
            }

            /// <summary>A strip of floor against a side, from <paramref name="lo"/> to <paramref name="hi"/> along it, <paramref name="depth"/> into the room.</summary>
            Rect Zone(int side, float lo, float hi, float depth)
            {
                Rect a = room.Area;
                return side switch
                {
                    0 => new Rect(lo, a.yMin, hi - lo, depth),
                    2 => new Rect(lo, a.yMax - depth, hi - lo, depth),
                    3 => new Rect(a.xMin, lo, depth, hi - lo),
                    _ => new Rect(a.xMax - depth, lo, depth, hi - lo),
                };
            }

            (float lo, float hi) Span(int side) => side % 2 == 0 ? (inner.xMin, inner.xMax) : (inner.yMin, inner.yMax);

            Rect Against(int side, float c, float w, float d, float gap = 0.02f) => side switch
            {
                0 => new Rect(c - w * 0.5f, inner.yMin + gap, w, d),
                2 => new Rect(c - w * 0.5f, inner.yMax - gap - d, w, d),
                3 => new Rect(inner.xMin + gap, c - w * 0.5f, d, w),
                _ => new Rect(inner.xMax - gap - d, c - w * 0.5f, d, w),
            };

            bool GapHit(int side, float lo, float hi, bool windowsToo)
            {
                foreach ((int s, float a, float b, bool passage) in gaps)
                    if (s == side && (passage || windowsToo) && lo < b + 0.15f && hi > a - 0.15f) return true;
                return false;
            }

            static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

            bool Inside(Rect r) => r.xMin >= inner.xMin - 1e-3f && r.yMin >= inner.yMin - 1e-3f && r.xMax <= inner.xMax + 1e-3f && r.yMax <= inner.yMax + 1e-3f;

            bool Fits(Rect r, bool solid, float clearance)
            {
                if (!Inside(r)) return false;
                if (!solid)
                {
                    foreach (Rect s in solids) if (s.Overlaps(r)) return false;
                    return true;
                }
                if (solidArea + r.width * r.height > maxSolid) return false;
                Rect g = Grow(r, clearance);
                foreach (Rect b in blocked) if (b.Overlaps(g)) return false;
                return true;
            }

            void Add(Furn kind, Vector2 centre, float yaw, Vector2 size, bool solid, bool tall, bool hide = false, int variant = 0, float elevation = 0f)
            {
                var item = new Item { Kind = kind, Position = centre, Yaw = yaw, Size = size, Room = room.Id, Solid = solid, Tall = tall, Hide = hide, Variant = variant, Elevation = elevation };
                p.Items.Add(item);
                if (!solid) return;
                Rect r = item.Footprint;
                blocked.Add(r);
                solids.Add(r);
                solidArea += r.width * r.height;
            }

            /// <summary>Backs a piece against a wall (a random side unless given), facing into the room.</summary>
            bool Wall(Furn kind, float w, float d, bool solid = true, bool tall = false, int side = -1, bool hide = false, int variant = 0, System.Func<Vector2, bool> ok = null)
            {
                for (int t = 0; t < 14; t++)
                {
                    int s = side >= 0 ? side : p.rng.Next(4);
                    (float lo, float hi) = Span(s);
                    if (hi - lo < w + 0.1f) continue;
                    float c = p.Range(lo + w * 0.5f, hi - w * 0.5f);
                    if (GapHit(s, c - w * 0.5f, c + w * 0.5f, tall)) continue;
                    Rect r = Against(s, c, w, d);
                    if (!Fits(r, solid, 0f) || (ok != null && !ok(r.center))) continue;
                    Add(kind, r.center, YawFacingIn(s), new Vector2(w, d), solid, tall, hide, variant);
                    return true;
                }
                return false;
            }

            /// <summary>Stands a piece in the open with <paramref name="clearance"/> all round (aisles), turned to the room's axes.</summary>
            bool Free(Furn kind, float w, float d, float clearance, bool solid = true, bool tall = false, float? yaw = null, bool hide = false, int variant = 0)
            {
                for (int t = 0; t < 16; t++)
                {
                    float y = yaw ?? 90f * p.rng.Next(4);
                    bool turned = Mathf.Abs(Mathf.Sin(y * Mathf.Deg2Rad)) > 0.7f;
                    float fw = turned ? d : w, fd = turned ? w : d;
                    if (inner.width < fw + 2f * clearance || inner.height < fd + 2f * clearance) return false;
                    var c = new Vector2(p.Range(inner.xMin + clearance + fw * 0.5f, inner.xMax - clearance - fw * 0.5f),
                                        p.Range(inner.yMin + clearance + fd * 0.5f, inner.yMax - clearance - fd * 0.5f));
                    var r = new Rect(c.x - fw * 0.5f, c.y - fd * 0.5f, fw, fd);
                    if (!Fits(r, solid, clearance)) continue;
                    Add(kind, c, y, new Vector2(w, d), solid, tall, hide, variant);
                    return true;
                }
                return false;
            }

            /// <summary>Loose clutter anywhere on the floor that isn't under furniture.</summary>
            void Loose(Furn kind, float size, int count = 1)
            {
                for (int n = 0; n < count; n++)
                    for (int t = 0; t < 8; t++)
                    {
                        var c = new Vector2(p.Range(inner.xMin + size * 0.5f, inner.xMax - size * 0.5f), p.Range(inner.yMin + size * 0.5f, inner.yMax - size * 0.5f));
                        var r = new Rect(c - Vector2.one * size * 0.5f, Vector2.one * size);
                        if (!Fits(r, false, 0f)) continue;
                        Add(kind, c, p.Range(0f, 360f), Vector2.one * size, false, false, false, p.rng.Next(4));
                        break;
                    }
            }

            /// <summary>Hangs a fitting on a wall at <paramref name="elevation"/>, clear of doors, windows and tall furniture.</summary>
            bool Mount(Furn kind, float w, float elevation, int variant = -1, int side = -1)
            {
                for (int t = 0; t < 12; t++)
                {
                    int s = side >= 0 ? side : p.rng.Next(4);
                    (float lo, float hi) = Span(s);
                    if (hi - lo < w + 0.3f) continue;
                    float c = p.Range(lo + w * 0.5f + 0.1f, hi - w * 0.5f - 0.1f);
                    if (GapHit(s, c - w * 0.5f, c + w * 0.5f, true)) continue;
                    Rect strip = Against(s, c, w, 0.3f);
                    bool covered = false;
                    foreach (Rect r in solids) if (r.Overlaps(strip)) covered = true;
                    if (covered && elevation < 2f) continue;
                    Add(kind, strip.center, YawFacingIn(s), new Vector2(w, 0.1f), false, false, false, variant >= 0 ? variant : p.rng.Next(4), elevation);
                    return true;
                }
                return false;
            }

            /// <summary>A run of pipes or duct along one side, under the ceiling.</summary>
            void Run(Furn kind, float elevation, int variant, int side = -1)
            {
                int s = side >= 0 ? side : p.rng.Next(4);
                (float lo, float hi) = Span(s);
                float inset = kind == Furn.Duct ? 0.28f : 0.12f;
                Vector2 start, dir;
                switch (s)
                {
                    case 0: start = new Vector2(lo, inner.yMin + inset); dir = Vector2.right; break;
                    case 2: start = new Vector2(lo, inner.yMax - inset); dir = Vector2.right; break;
                    case 3: start = new Vector2(inner.xMin + inset, lo); dir = Vector2.up; break;
                    default: start = new Vector2(inner.xMax - inset, lo); dir = Vector2.up; break;
                }
                p.Items.Add(new Item { Kind = kind, Position = start, Yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, Size = new Vector2(hi - lo, 0.3f), Elevation = elevation, Room = room.Id, Variant = variant });
            }

            int LongSide() => inner.width >= inner.height ? (p.Chance(0.5f) ? 0 : 2) : (p.Chance(0.5f) ? 1 : 3);
            bool AlongX => inner.width >= inner.height;
            float Area => inner.width * inner.height;

            // ---------------------------------------------------------------- the generator

            public void Generator()
            {
                bool turned = inner.height > inner.width;
                float fw = turned ? 0.9f : 1.3f, fd = turned ? 1.3f : 0.9f;
                const float wall = 0.85f, work = 0.6f;
                for (int pass = 0; pass < 2; pass++)
                    for (int t = 0; t <= 24; t++)
                    {
                        // Random spots with a walkway to the walls; last of all, the middle of the room.
                        var c = t == 24 ? inner.center : new Vector2(p.Range(inner.xMin + wall + fw * 0.5f, inner.xMax - wall - fw * 0.5f), p.Range(inner.yMin + wall + fd * 0.5f, inner.yMax - wall - fd * 0.5f));
                        var r = new Rect(c.x - fw * 0.5f, c.y - fd * 0.5f, fw, fd);
                        Rect space = Grow(r, work);
                        bool clear = true;
                        foreach (Rect b in blocked) if (b.Overlaps(pass == 0 ? space : r)) clear = false;
                        if (!clear) continue;
                        p.Items.Add(new Item { Kind = Furn.Generator, Position = c, Yaw = (turned ? 90f : 0f) + (p.Chance(0.5f) ? 180f : 0f), Size = new Vector2(1.3f, 0.9f), Room = room.Id, Solid = true });
                        blocked.Add(space);
                        solids.Add(r);
                        solidArea += space.width * space.height;
                        return;
                    }
            }

            // ---------------------------------------------------------------- room recipes

            public void Hallway()
            {
                float width = Mathf.Min(inner.width, inner.height);
                if (width >= 1.75f && p.hallLockers.Count < MaxHallLockers)
                {
                    int want = p.rng.Next(1, 3);
                    for (int i = 0; i < want && p.hallLockers.Count < MaxHallLockers; i++)
                    {
                        bool far(Vector2 c) { foreach (Vector2 l in p.hallLockers) if ((l - c).magnitude < 5.4f) return false; return true; }
                        if (Wall(Furn.Locker, 0.6f, 0.5f, true, true, LongSide(), true, 0, far))
                            p.hallLockers.Add(p.Items[p.Items.Count - 1].Position);
                    }
                }
                if (width >= 2f && p.Chance(0.3f)) Wall(Furn.Bench, 1.4f, 0.4f, side: LongSide());
                if (p.Chance(0.35f)) Wall(Furn.Boxes, 0.7f, 0.5f, side: LongSide(), variant: p.rng.Next(3));
                if (p.Chance(0.5f)) Mount(Furn.Extinguisher, 0.25f, 1.0f);
                if (p.Chance(0.6f)) Mount(Furn.Poster, 0.6f, 1.45f);
                if (p.Chance(0.25f)) Mount(Furn.Clock, 0.32f, 2.0f);
                if (p.Chance(0.55f)) Run(Furn.Duct, 2.28f, 0, LongSide());
                if (p.Chance(0.4f)) Run(Furn.Pipes, 2.4f, p.rng.Next(1, 4), LongSide());
                Loose(Furn.Papers, 0.5f, p.rng.Next(0, 3));
                Loose(Furn.Debris, 0.4f, p.rng.Next(1, 3));
            }

            public void Office()
            {
                int desks = Mathf.Clamp(Mathf.RoundToInt(Area / 9f), 1, 4);
                for (int i = 0; i < desks; i++)
                {
                    bool placed = p.Chance(0.5f) ? Wall(Furn.Desk, 1.4f, 0.7f) : Free(Furn.Desk, 1.4f, 0.7f, 0.85f);
                    if (!placed) continue;
                    Item desk = p.Items[p.Items.Count - 1];
                    Vector2 front = new Vector2(Mathf.Sin(desk.Yaw * Mathf.Deg2Rad), Mathf.Cos(desk.Yaw * Mathf.Deg2Rad));
                    bool toppled = p.Chance(0.2f);
                    Add(Furn.OfficeChair, desk.Position + front * 0.6f, desk.Yaw + 180f + p.Range(-35f, 35f), new Vector2(0.55f, 0.55f), false, false, false, toppled ? 1 : 0);
                }
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.FilingCabinet, 0.5f, 0.6f, tall: true);
                if (p.Chance(0.5f)) Wall(Furn.Shelf, 1.0f, 0.4f, tall: true);
                if (p.Chance(0.3f)) Mount(Furn.Whiteboard, 1.4f, 1.1f);
                if (p.Chance(0.4f)) Mount(Furn.Clock, 0.32f, 2.0f);
                if (p.Chance(0.4f)) Wall(Furn.DeadPlant, 0.45f, 0.45f, false);
                if (p.Chance(0.3f)) Wall(Furn.Boxes, 0.7f, 0.5f, variant: p.rng.Next(3));
                Loose(Furn.Papers, 0.5f, p.rng.Next(2, 6));
            }

            public void Storage()
            {
                int racks = Mathf.Clamp(Mathf.RoundToInt(Area / 8f), 1, 6);
                float len = Mathf.Clamp(Mathf.Max(inner.width, inner.height) - 2.6f, 1.2f, 4f);
                for (int i = 0; i < racks; i++)
                {
                    if (p.Chance(0.5f)) Wall(Furn.Rack, len, 0.6f, tall: true, side: LongSide(), variant: p.rng.Next(3));
                    else Free(Furn.Rack, len, 0.6f, 0.75f, tall: true, yaw: AlongX ? 0f : 90f, variant: p.rng.Next(3));
                }
                for (int i = p.rng.Next(2, 5); i > 0; i--) if (!Wall(Furn.Crate, 0.8f, 0.8f, variant: p.rng.Next(3))) Free(Furn.Crate, 0.8f, 0.8f, 0.7f);
                if (p.Chance(0.5f)) Wall(Furn.CrateStack, 0.9f, 0.9f, tall: true);
                Wall(Furn.Barrel, 0.62f, 0.62f, hide: p.Chance(0.5f), variant: p.rng.Next(3));
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.Boxes, 0.7f, 0.5f, variant: p.rng.Next(3));
                if (p.Chance(0.4f)) Loose(Furn.Pallet, 1.1f);
                if (p.Chance(0.25f)) Free(Furn.Mannequin, 0.5f, 0.4f, 0.4f, false);
            }

            /// <summary>The lounge: a couch backed against a wall, a TV on a low stand facing it, a poster and some mess.</summary>
            public void Lounge(out Vector2 seat, out Vector2 tv)
            {
                seat = room.Area.center;
                tv = seat + Vector2.up * 2f;
                // Backed against the north wall if it fits (Chacko then faces the camera), else a side wall, else any.
                if (!Wall(Furn.Sofa, 1.9f, 0.85f, side: 2) && !Wall(Furn.Sofa, 1.9f, 0.85f, side: 1) && !Wall(Furn.Sofa, 1.9f, 0.85f, side: 3) && !Wall(Furn.Sofa, 1.9f, 0.85f))
                {
                    if (!Free(Furn.Sofa, 1.9f, 0.85f, 0.6f)) return;
                }
                Item sofa = p.Items[p.Items.Count - 1];
                var front = new Vector2(Mathf.Sin(sofa.Yaw * Mathf.Deg2Rad), Mathf.Cos(sofa.Yaw * Mathf.Deg2Rad));
                seat = sofa.Position + front * 0.12f;
                float extent = Mathf.Abs(front.x) > 0.5f ? inner.width : inner.height;
                tv = sofa.Position + front * Mathf.Min(2.6f, extent - 0.9f);
                for (float dist = Mathf.Min(2.6f, extent - 0.9f); dist >= 1.4f; dist -= 0.3f)
                {
                    Vector2 c = sofa.Position + front * dist;
                    bool turned = Mathf.Abs(front.x) > 0.5f;
                    var r = new Rect(c - (turned ? new Vector2(0.45f, 1.3f) : new Vector2(1.3f, 0.45f)) * 0.5f, turned ? new Vector2(0.45f, 1.3f) : new Vector2(1.3f, 0.45f));
                    if (!Fits(r, true, 0f)) continue;
                    Add(Furn.Tv, c, sofa.Yaw + 180f, new Vector2(1.3f, 0.45f), true, false);
                    tv = c;
                    break;
                }
                if (p.Chance(0.6f)) Mount(Furn.Poster, 0.6f, 1.45f);
                Loose(Furn.Bottles, 0.4f, p.rng.Next(1, 4));
                Loose(Furn.Papers, 0.5f, p.rng.Next(0, 2));
            }

            public void BreakRoom()
            {
                for (int i = Area > 25f ? 2 : 1; i > 0; i--)
                {
                    if (!Free(Furn.Table, 1.2f, 0.75f, 0.9f)) continue;
                    Item table = p.Items[p.Items.Count - 1];
                    for (int c = 0; c < 4; c++)
                    {
                        if (!p.Chance(0.75f)) continue;
                        float a = c * 90f + table.Yaw;
                        Vector2 dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad));
                        float reach = c % 2 == 0 ? 0.65f : 0.85f;
                        Add(Furn.Chair, table.Position + dir * reach, a + 180f + p.Range(-20f, 20f), new Vector2(0.44f, 0.44f), false, false, false, p.Chance(0.2f) ? 1 : 0);
                    }
                }
                Wall(Furn.Counter, Mathf.Min(2.4f, Mathf.Max(inner.width, inner.height) - 1f), 0.6f);
                Wall(Furn.Fridge, 0.7f, 0.7f, tall: true);
                if (p.Chance(0.7f)) Wall(Furn.Vending, 1.0f, 0.8f, tall: true, variant: p.rng.Next(2));
                if (p.Chance(0.6f)) Wall(Furn.Sofa, 1.9f, 0.85f);
                if (p.Chance(0.5f)) Mount(Furn.Clock, 0.32f, 2.0f);
                if (p.Chance(0.6f)) Mount(Furn.Poster, 0.6f, 1.45f);
                Loose(Furn.Bottles, 0.4f, p.rng.Next(1, 4));
                Loose(Furn.Papers, 0.5f, p.rng.Next(0, 2));
            }

            public void Restroom()
            {
                // Stalls along the longest side without a doorway, sinks and a mirror opposite.
                int stallSide = -1;
                float best = 0f;
                for (int s = 0; s < 4; s++)
                {
                    (float lo, float hi) = Span(s);
                    if (GapHit(s, lo, hi, false)) continue;
                    if (hi - lo > best) { best = hi - lo; stallSide = s; }
                }
                if (stallSide >= 0)
                {
                    (float lo, float hi) = Span(stallSide);
                    int stalls = Mathf.Clamp(Mathf.FloorToInt((hi - lo) / 1.0f), 1, 4);
                    float w = (hi - lo) / stalls;
                    for (int i = 0; i < stalls; i++)
                    {
                        float c = lo + (i + 0.5f) * w;
                        Rect r = Against(stallSide, c, 0.45f, 0.7f);
                        if (!Fits(r, true, 0f)) continue;
                        Add(Furn.Toilet, r.center, YawFacingIn(stallSide), new Vector2(0.45f, 0.7f), true, false);
                        if (i > 0)
                        {
                            Rect wall = Against(stallSide, lo + i * w, 0.06f, 1.5f);
                            if (Fits(wall, true, 0f)) Add(Furn.Stall, wall.center, YawFacingIn(stallSide), new Vector2(0.06f, 1.5f), true, true);
                        }
                    }
                }
                int sinkSide = (stallSide + 2) % 4;
                for (int i = p.rng.Next(1, 3); i > 0; i--)
                    if (Wall(Furn.Sink, 0.6f, 0.45f, side: stallSide >= 0 ? sinkSide : -1))
                    {
                        Item sink = p.Items[p.Items.Count - 1];
                        p.Items.Add(new Item { Kind = Furn.Mirror, Position = sink.Position, Yaw = sink.Yaw, Size = new Vector2(0.55f, 0.1f), Elevation = 1.25f, Room = room.Id, Variant = p.rng.Next(3) });
                    }
                Loose(Furn.Stain, p.Range(0.6f, 1.2f), 2);
            }

            public void LockerRoom()
            {
                for (int i = 0; i < 4; i++) Wall(Furn.LockerBank, 1.35f, 0.5f, tall: true, variant: p.rng.Next(3));
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.Locker, 0.6f, 0.5f, tall: true, hide: true);
                if (!Free(Furn.Bench, 1.6f, 0.4f, 0.7f, yaw: AlongX ? 0f : 90f)) Wall(Furn.Bench, 1.6f, 0.4f);
                if (p.Chance(0.5f)) Mount(Furn.Mirror, 0.55f, 1.25f);
                Loose(Furn.Papers, 0.4f, p.rng.Next(0, 2));
                Loose(Furn.Bottles, 0.4f, p.rng.Next(0, 2));
            }

            public void Workshop()
            {
                for (int i = Mathf.Clamp(Mathf.RoundToInt(Area / 12f), 1, 3); i > 0; i--)
                    if (Wall(Furn.Workbench, 1.8f, 0.7f, variant: p.rng.Next(3)))
                    {
                        Item bench = p.Items[p.Items.Count - 1];
                        if (p.Chance(0.6f)) p.Items.Add(new Item { Kind = Furn.ToolBoard, Position = bench.Position, Yaw = bench.Yaw, Size = new Vector2(1.3f, 0.1f), Elevation = 1.15f, Room = room.Id, Variant = p.rng.Next(3) });
                    }
                if (p.Chance(0.6f)) Wall(Furn.Shelf, 1.0f, 0.4f, tall: true);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.Barrel, 0.62f, 0.62f, hide: p.Chance(0.3f), variant: p.rng.Next(3));
                if (p.Chance(0.6f)) Free(Furn.CableSpool, 0.8f, 0.8f, 0.7f);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.Crate, 0.8f, 0.8f, variant: p.rng.Next(3));
                Loose(Furn.Stain, p.Range(0.8f, 1.6f), p.rng.Next(1, 3));
                Loose(Furn.Debris, 0.4f, p.rng.Next(1, 4));
            }

            public void Electrical()
            {
                for (int i = p.rng.Next(2, 5); i > 0; i--) Mount(Furn.BreakerPanel, 0.55f, 1.0f);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Mount(Furn.JunctionBox, 0.3f, 1.6f);
                if (!room.HasGenerator || p.Chance(0.4f)) Wall(Furn.Transformer, 1.0f, 0.8f);
                if (p.Chance(0.5f)) Wall(Furn.CableSpool, 0.8f, 0.8f);
                if (p.Chance(0.5f)) Wall(Furn.Shelf, 1.0f, 0.4f, tall: true);
                Run(Furn.Pipes, 2.4f, 1);
                Loose(Furn.Debris, 0.4f, p.rng.Next(1, 3));
            }

            public void ServerRoom()
            {
                int racks = Mathf.Clamp(Mathf.RoundToInt(Area / 4f), 2, 6);
                for (int i = 0; i < racks; i++)
                    if (!Free(Furn.ServerRack, 0.6f, 1.0f, 0.65f, tall: true, yaw: AlongX ? 90f : 0f, variant: p.rng.Next(3)))
                        Wall(Furn.ServerRack, 0.6f, 1.0f, tall: true, variant: p.rng.Next(3));
                Mount(Furn.BreakerPanel, 0.55f, 1.0f);
                Loose(Furn.Debris, 0.4f, p.rng.Next(1, 3));
            }

            public void StudioSet()
            {
                // A fake cabin bedroom built for the Crystal Lake night shoot: two set flats, a bed and a wardrobe on the set,
                // lights and a camera watching it.
                int setSide = LongSide();
                Wall(Furn.SetFlat, Mathf.Min(3.2f, Mathf.Max(inner.width, inner.height) * 0.45f), 0.25f, tall: true, side: setSide, variant: p.rng.Next(3));
                if (p.Chance(0.6f)) Wall(Furn.SetFlat, 2.2f, 0.25f, tall: true, variant: p.rng.Next(3));
                Wall(Furn.Bed, 1.0f, 2.0f, side: setSide, hide: true);
                if (p.Chance(0.6f)) Wall(Furn.Wardrobe, 1.0f, 0.55f, tall: true, side: setSide, hide: true);
                for (int i = p.rng.Next(2, 5); i > 0; i--) Free(Furn.LightRig, 0.7f, 0.7f, 0.5f, false);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Free(Furn.Camera, 0.7f, 0.7f, 0.5f, false);
                if (p.Chance(0.7f)) Free(Furn.DirectorChair, 0.6f, 0.6f, 0.4f, false);
                for (int i = p.rng.Next(1, 4); i > 0; i--) Free(Furn.Mannequin, 0.5f, 0.4f, 0.4f, false);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.CrateStack, 0.9f, 0.9f, tall: true);
                Loose(Furn.Debris, 0.4f, p.rng.Next(2, 4));
                Loose(Furn.Papers, 0.5f, p.rng.Next(1, 3));
            }

            public void LoadingBay()
            {
                if (Area > 30f && p.Chance(0.7f)) Free(Furn.Forklift, 1.2f, 2.4f, 0.8f, tall: false);
                for (int i = p.rng.Next(2, 5); i > 0; i--) if (!Wall(Furn.PalletStack, 1.2f, 1.0f, variant: p.rng.Next(3))) Free(Furn.PalletStack, 1.2f, 1.0f, 0.8f);
                if (p.Chance(0.6f)) Wall(Furn.CrateStack, 0.9f, 0.9f, tall: true);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.Barrel, 0.62f, 0.62f, hide: p.Chance(0.4f), variant: p.rng.Next(3));
                Loose(Furn.Pallet, 1.1f, p.rng.Next(0, 3));
                Loose(Furn.Stain, p.Range(0.8f, 1.8f), p.rng.Next(1, 3));
            }

            public void Boiler()
            {
                if (!room.HasGenerator || Area > 20f) Wall(Furn.Boiler, 1.4f, 1.4f, tall: true);
                for (int i = p.rng.Next(1, 3); i > 0; i--) Wall(Furn.Barrel, 0.62f, 0.62f, hide: p.Chance(0.4f), variant: p.rng.Next(3));
                if (p.Chance(0.5f)) Wall(Furn.Shelf, 1.0f, 0.4f, tall: true);
                Run(Furn.Pipes, 2.35f, 3);
                Run(Furn.Pipes, 2.2f, 2);
                Loose(Furn.Stain, p.Range(0.8f, 1.6f), p.rng.Next(1, 3));
            }

            /// <summary>Floor spots for supplies, clear of anything solid.</summary>
            public void Loot(int count)
            {
                for (int n = 0; n < count; n++)
                    for (int t = 0; t < 12; t++)
                    {
                        var c = new Vector2(p.Range(inner.xMin + 0.4f, inner.xMax - 0.4f), p.Range(inner.yMin + 0.4f, inner.yMax - 0.4f));
                        var r = new Rect(c - Vector2.one * 0.35f, Vector2.one * 0.7f);
                        if (!Fits(r, false, 0f)) continue;
                        bool near = false;
                        foreach (Vector2 l in p.LootSpots) if ((l - c).sqrMagnitude < 1.5f * 1.5f) near = true;
                        if (near) continue;
                        p.LootSpots.Add(c);
                        break;
                    }
            }

            /// <summary>Every room: decorations and services.</summary>
            public void Common()
            {
                if (p.Chance(0.5f))
                {
                    int corner = p.rng.Next(4);
                    var at = new Vector2(corner % 2 == 0 ? inner.xMin + 0.15f : inner.xMax - 0.15f, corner < 2 ? inner.yMin + 0.15f : inner.yMax - 0.15f);
                    p.Items.Add(new Item { Kind = Furn.Cobweb, Position = at, Yaw = corner switch { 0 => 45f, 1 => -45f, 2 => 135f, _ => -135f }, Size = Vector2.one * 0.6f, Elevation = 2.25f, Room = room.Id });
                }
                bool industrial = room.Type == RoomType.Storage || room.Type == RoomType.Workshop || room.Type == RoomType.LoadingBay || room.Type == RoomType.Electrical;
                if (industrial && p.Chance(0.5f)) Run(Furn.Pipes, 2.4f, p.rng.Next(1, 3));
                if (industrial && p.Chance(0.3f)) Mount(Furn.JunctionBox, 0.3f, 1.6f);
                if (!room.IsHallway && p.Chance(0.35f)) Mount(Furn.VentGrille, 0.5f, 2.15f);
                if (!room.IsHallway && room.Type != RoomType.Restroom && p.Chance(0.3f)) Mount(Furn.Poster, 0.6f, 1.45f);
                if (p.Chance(0.45f)) Loose(Furn.Stain, p.Range(0.5f, 1.4f));
            }
        }
    }
}
