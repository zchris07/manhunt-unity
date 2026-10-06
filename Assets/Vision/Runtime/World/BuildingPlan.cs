using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The central building's floor plan, generated from a seed like the original's warehouse but at full scale: a
    /// single storey of hallways and rooms. Hallways are cut through the footprint recursively (a wide spine first, then
    /// narrower branches, so they differ in width and length); the blocks between them are split into rooms that keep a
    /// wall on a hallway where they can. Rooms get a door onto a hallway (a few get two, and back rooms a door through a
    /// neighbour) so every room is reachable with a few loops. The outside gets entrances on every side, windows and the
    /// north exit gate. Pure data: <see cref="SandboxWorld"/> builds it, the tests check it.
    /// Coordinates are map metres, x east and y north (the world's z).
    /// </summary>
    public sealed partial class BuildingPlan
    {
        public const float WallHeight = 2.6f;
        public const float ExteriorThickness = 0.3f, InteriorThickness = 0.16f;
        public const float DoorWidth = 1.1f, WideDoorWidth = 2.2f, ExteriorDoorWidth = 1.4f, WindowWidth = 1.4f, GateWidth = 3.2f;
        public const float DoorHeight = 2.1f;
        public const float HallMin = 1.4f, HallMax = 3.6f;
        public const float RoomMin = 3f, RoomLong = 10f, RoomShort = 8f;
        /// <summary>The original's lamps light 300 units: 9 m.</summary>
        public const float LampRange = 9f;
        public const int MaxBarricades = 7, MaxHallLockers = 8;

        public enum RoomType { Hallway, Office, Storage, BreakRoom, Restroom, LockerRoom, Workshop, Electrical, ServerRoom, StudioSet, LoadingBay, Boiler }
        public enum OpeningKind { Doorway, Door, Window, BoardedWindow, Gate }

        public sealed class Room
        {
            public int Id;
            public Rect Area;
            public RoomType Type;
            public bool HasGenerator;
            public bool IsHallway => Type == RoomType.Hallway;
            public float FloorArea => Area.width * Area.height;
            public string Name => Names[(int)Type];
            static readonly string[] Names = { "Hallway", "Office", "Storage", "Break room", "Restroom", "Locker room", "Workshop", "Electrical room", "Server room", "Studio set", "Loading bay", "Boiler room" };
        }

        /// <summary>A straight stretch of wall between two spaces (<see cref="B"/> = -1: the outside), running P0 to P1.</summary>
        public sealed class Interface
        {
            public int A, B;
            public Vector2 P0, P1;
            /// <summary>Two hallways meet here: no wall at all.</summary>
            public bool Open;
            /// <summary>Unit normal pointing from A into B (out of the building for exterior walls).</summary>
            public Vector2 Normal;
            public bool Exterior => B < 0;
            public float Length => Vector2.Distance(P0, P1);
            public Vector2 Along => (P1 - P0).normalized;
            public float Thickness => Exterior ? ExteriorThickness : InteriorThickness;
            public int Other(int room) => room == A ? B : A;
        }

        public sealed class Opening
        {
            public OpeningKind Kind;
            public int Interface;
            /// <summary>The gap in the wall, A to B along it.</summary>
            public Vector2 A, B;
            public bool Exterior;
            public bool StartsOpen;
            public Vector2 Centre => (A + B) * 0.5f;
            public float Width => Vector2.Distance(A, B);
            public bool IsPassage => Kind == OpeningKind.Door || Kind == OpeningKind.Doorway || Kind == OpeningKind.Gate;
        }

        /// <summary>A solid run of wall to build (openings already cut out).</summary>
        public struct WallRun
        {
            public Vector2 A, B;
            public bool Exterior;
            public float Thickness => Exterior ? ExteriorThickness : InteriorThickness;
        }

        /// <summary>A pallet standing beside an open doorway, ready to be slammed across it.</summary>
        public struct Barricade
        {
            public Vector2 A, B;
            /// <summary>The side of the wall the pallet stands on.</summary>
            public Vector2 Side;
        }

        /// <summary>What gives the light: a ceiling fitting, or something in the room that would glow.</summary>
        public enum LampKind { Fluorescent, Bulb, Desk, Exit, Vending, Furnace, Server, Stage }

        public struct Lamp
        {
            public Vector2 Position;
            public int Room;
            public bool Working;
            public float Flicker;
            public LampKind Kind;
            /// <summary>For lamps that belong to a piece of furniture or a doorway: its yaw and model-local size.</summary>
            public float Yaw;
            public Vector2 Size;
            /// <summary>Height of the light above the floor.</summary>
            public float Elevation;
            public bool Fluorescent => Kind == LampKind.Fluorescent;
            public bool Ceiling => Kind == LampKind.Fluorescent || Kind == LampKind.Bulb;
        }

        public readonly int Seed;
        public readonly Rect Bounds;
        /// <summary>Corner pieces of <see cref="Bounds"/> that are not part of the building (an L, T, U or S footprint).</summary>
        public readonly List<Rect> Notches = new List<Rect>();
        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<Interface> Interfaces = new List<Interface>();
        public readonly List<Opening> Openings = new List<Opening>();
        public readonly List<WallRun> Walls = new List<WallRun>();
        public readonly List<Barricade> Barricades = new List<Barricade>();
        public readonly List<Lamp> Lamps = new List<Lamp>();
        /// <summary>Points just outside each entrance (the paths lead there), with the side: n, s, e or w.</summary>
        public readonly List<(Vector2 point, char side)> Entrances = new List<(Vector2, char)>();
        public float GateX;
        public int GateRoom = -1;
        /// <summary>The gate lever on the inside of the north wall, beside the gate.</summary>
        public Vector2 Lever;

        readonly System.Random rng;
        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Chance(float p) => rng.NextDouble() < p;
        T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

        void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public BuildingPlan(int seed, Rect bounds)
        {
            Seed = seed;
            Bounds = bounds;
            rng = new System.Random(seed * 131 + 17);
            ChooseNotches();
            var blocks = new List<Rect>();
            SplitHalls(bounds, 0, blocks);
            foreach (Rect b in blocks) SplitRooms(b);
            for (int i = 0; i < Rooms.Count; i++) Rooms[i].Id = i;
            FindInterfaces();
            PlaceGate();
            AssignTypes();
            PlaceDoors();
            PlaceEntrances();
            PlaceWindows();
            PlaceBarricades();
            BuildWalls();
            Furnish();
        }

        /// <summary>The room containing a point (-1 outside).</summary>
        public int RoomAt(Vector2 p)
        {
            for (int i = 0; i < Rooms.Count; i++) if (Rooms[i].Area.Contains(p)) return i;
            return -1;
        }

        // ------------------------------------------------------------------ hallways and rooms

        /// <summary>True in the open ground of a notch, at least <paramref name="margin"/> from the building's walls (paths may cross it).</summary>
        public bool InNotch(Vector2 p, float margin)
        {
            foreach (Rect n in Notches)
            {
                float x0 = n.xMin > Bounds.xMin + 0.01f ? n.xMin + margin : n.xMin - 10f, x1 = n.xMax < Bounds.xMax - 0.01f ? n.xMax - margin : n.xMax + 10f;
                float y0 = n.yMin > Bounds.yMin + 0.01f ? n.yMin + margin : n.yMin - 10f, y1 = n.yMax < Bounds.yMax - 0.01f ? n.yMax - margin : n.yMax + 10f;
                if (p.x > x0 && p.x < x1 && p.y > y0 && p.y < y1) return true;
            }
            return false;
        }

        /// <summary>Floor area of the building (the bounds less its notches).</summary>
        public float FootprintArea
        {
            get
            {
                float a = Bounds.width * Bounds.height;
                foreach (Rect n in Notches) a -= n.width * n.height;
                return a;
            }
        }

        /// <summary>
        /// Cuts corners out of the square: an L, a T or U (two corners of one side), an S (opposite corners) or a square
        /// with one small loading-dock notch. Each notch stays well inside a corner so the north wall keeps room for the
        /// gate and every side keeps a long enough wall for its entrances.
        /// </summary>
        void ChooseNotches()
        {
            Rect Corner(int c, float nx, float ny) => new Rect(c % 2 == 0 ? Bounds.xMin : Bounds.xMax - nx, c < 2 ? Bounds.yMin : Bounds.yMax - ny, nx, ny);
            float roll = (float)rng.NextDouble();
            if (roll < 0.30f) Notches.Add(Corner(rng.Next(4), Range(7f, 12f), Range(7f, 12f)));                  // L
            else if (roll < 0.52f)                                                                               // T or U
            {
                int pair = rng.Next(4);   // the south, north, west or east side
                (int c0, int c1) = pair switch { 0 => (0, 1), 1 => (2, 3), 2 => (0, 2), _ => (1, 3) };
                bool horizontal = pair < 2;
                Notches.Add(Corner(c0, horizontal ? Range(6f, 10.5f) : Range(7f, 12f), horizontal ? Range(7f, 12f) : Range(6f, 10.5f)));
                Notches.Add(Corner(c1, horizontal ? Range(6f, 10.5f) : Range(7f, 12f), horizontal ? Range(7f, 12f) : Range(6f, 10.5f)));
            }
            else if (roll < 0.68f)                                                                               // S
            {
                bool flip = Chance(0.5f);
                Notches.Add(Corner(flip ? 0 : 1, Range(6.5f, 10.5f), Range(6.5f, 10.5f)));
                Notches.Add(Corner(flip ? 3 : 2, Range(6.5f, 10.5f), Range(6.5f, 10.5f)));
            }
            else Notches.Add(Corner(rng.Next(4), Range(4.5f, 7f), Range(4.5f, 7f)));                             // a loading dock
        }

        static Rect Intersect(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), x1 = Mathf.Min(a.xMax, b.xMax), y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
            return new Rect(x0, y0, Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
        }

        float HallWidth(int depth)
        {
            if (depth == 0) return Range(2.6f, HallMax);
            float t = (float)rng.NextDouble();
            return t < 0.34f ? Range(HallMin, 1.75f) : t < 0.76f ? Range(1.8f, 2.5f) : Range(2.7f, HallMax);
        }

        void AddHall(Rect hall) => Rooms.Add(new Room { Area = hall, Type = RoomType.Hallway });

        /// <summary>
        /// Cuts <paramref name="r"/> into blocks: hallways run through it (a wide spine first, then narrow and wide branches
        /// of every length), sometimes after a plain wall has split it so the halls on either side don't line up. Where the
        /// footprint is notched, a hallway runs along the notch's edge so no room is ever part notch.
        /// </summary>
        void SplitHalls(Rect r, int depth, List<Rect> blocks)
        {
            if (r.width < 0.5f || r.height < 0.5f) return;
            foreach (Rect notch in Notches)
            {
                Rect n = Intersect(r, notch);
                if (n.width <= 0.05f || n.height <= 0.05f) continue;
                bool spansX = n.width >= r.width - 0.05f, spansY = n.height >= r.height - 0.05f;
                if (spansX && spansY) return;   // all notch
                CutAtNotch(r, n, spansY || (!spansX && Chance(0.5f)), depth, blocks);
                return;
            }

            float limit = Range(7f, 10f);
            float longSide = Mathf.Max(r.width, r.height);
            if (depth > 0 && (longSide <= limit || depth >= 7)) { blocks.Add(r); return; }
            bool cutX = r.width > r.height * 1.1f || (r.height <= r.width * 1.1f && Chance(0.5f));
            float span = cutX ? r.width : r.height;

            if (depth >= 1 && span >= 10f && longSide > 14f && Chance(0.3f))
            {
                float cut = Mathf.Lerp(4.5f, span - 4.5f, Range(0.3f, 0.7f));
                Rect wa = cutX ? new Rect(r.xMin, r.yMin, cut, r.height) : new Rect(r.xMin, r.yMin, r.width, cut);
                Rect wb = cutX ? new Rect(r.xMin + cut, r.yMin, r.width - cut, r.height) : new Rect(r.xMin, r.yMin + cut, r.width, r.height - cut);
                SplitHalls(wa, depth + 1, blocks);
                SplitHalls(wb, depth + 1, blocks);
                return;
            }

            float w = HallWidth(depth);
            if (span < 2f * 4f + w) { blocks.Add(r); return; }
            float at = Mathf.Lerp(4f, span - 4f - w, Range(0.25f, 0.75f));
            Rect hall, ra, rb;
            if (cutX)
            {
                hall = new Rect(r.xMin + at, r.yMin, w, r.height);
                ra = new Rect(r.xMin, r.yMin, at, r.height);
                rb = new Rect(hall.xMax, r.yMin, r.xMax - hall.xMax, r.height);
            }
            else
            {
                hall = new Rect(r.xMin, r.yMin + at, r.width, w);
                ra = new Rect(r.xMin, r.yMin, r.width, at);
                rb = new Rect(r.xMin, hall.yMax, r.width, r.yMax - hall.yMax);
            }
            AddHall(hall);
            SplitHalls(ra, depth + 1, blocks);
            SplitHalls(rb, depth + 1, blocks);
        }

        /// <summary>Cuts along the edge of a notch, with a hallway on the building side of it.</summary>
        void CutAtNotch(Rect r, Rect n, bool cutX, int depth, List<Rect> blocks)
        {
            float w = Chance(0.3f) ? Range(2.6f, HallMax) : Range(HallMin, 2.6f);
            Rect keep, notchSide, hall;
            bool hasHall;
            if (cutX)
            {
                bool east = n.xMax >= r.xMax - 0.05f;
                float c = east ? n.xMin : n.xMax, room = east ? c - r.xMin : r.xMax - c;
                hasHall = room >= w + 3.2f;
                if (!hasHall) w = 0f;
                hall = east ? new Rect(c - w, r.yMin, w, r.height) : new Rect(c, r.yMin, w, r.height);
                keep = east ? new Rect(r.xMin, r.yMin, c - w - r.xMin, r.height) : new Rect(c + w, r.yMin, r.xMax - c - w, r.height);
                notchSide = east ? new Rect(c, r.yMin, r.xMax - c, r.height) : new Rect(r.xMin, r.yMin, c - r.xMin, r.height);
            }
            else
            {
                bool north = n.yMax >= r.yMax - 0.05f;
                float c = north ? n.yMin : n.yMax, room = north ? c - r.yMin : r.yMax - c;
                hasHall = room >= w + 3.2f;
                if (!hasHall) w = 0f;
                hall = north ? new Rect(r.xMin, c - w, r.width, w) : new Rect(r.xMin, c, r.width, w);
                keep = north ? new Rect(r.xMin, r.yMin, r.width, c - w - r.yMin) : new Rect(r.xMin, c + w, r.width, r.yMax - c - w);
                notchSide = north ? new Rect(r.xMin, c, r.width, r.yMax - c) : new Rect(r.xMin, r.yMin, r.width, c - r.yMin);
            }
            if (hasHall) AddHall(hall);
            SplitHalls(keep, depth + 1, blocks);
            SplitHalls(notchSide, depth + 1, blocks);
        }

        /// <summary>Length of the edge a rectangle shares with any hallway.</summary>
        float HallContact(Rect r)
        {
            float best = 0f;
            foreach (Room h in Rooms)
                if (h.IsHallway) best = Mathf.Max(best, Shared(r, h.Area, out _, out _));
            return best;
        }

        void SplitRooms(Rect r)
        {
            float longSide = Mathf.Max(r.width, r.height), shortSide = Mathf.Min(r.width, r.height);
            bool fits = longSide <= RoomLong && shortSide <= RoomShort;
            bool canX = r.width >= 2f * RoomMin, canY = r.height >= 2f * RoomMin;
            float area = r.width * r.height;
            if (!canX && !canY) { AddRoom(r); return; }
            if (fits && (area < 14f ? Chance(0.92f) : area < 40f ? Chance(0.82f) : Chance(0.62f))) { AddRoom(r); return; }

            // Try a few cuts; prefer ones that leave both halves on a hallway, then ones across the long side.
            float bestScore = float.MinValue;
            Rect bestA = default, bestB = default;
            for (int t = 0; t < 8; t++)
            {
                bool x = canX && (!canY || Chance(r.width / (r.width + r.height)));
                float span = x ? r.width : r.height;
                float at = Range(RoomMin, span - RoomMin);
                Rect a = x ? new Rect(r.xMin, r.yMin, at, r.height) : new Rect(r.xMin, r.yMin, r.width, at);
                Rect b = x ? new Rect(r.xMin + at, r.yMin, r.width - at, r.height) : new Rect(r.xMin, r.yMin + at, r.width, r.height - at);
                float score = (HallContact(a) >= 1.8f ? 2f : 0f) + (HallContact(b) >= 1.8f ? 2f : 0f) + (span == longSide ? 1f : 0f) + Range(0f, 0.8f);
                if (score > bestScore) { bestScore = score; bestA = a; bestB = b; }
            }
            SplitRooms(bestA);
            SplitRooms(bestB);
        }

        void AddRoom(Rect r) => Rooms.Add(new Room { Area = r, Type = RoomType.Storage });

        // ------------------------------------------------------------------ walls between spaces

        /// <summary>The length of edge two rectangles share (0 when they don't touch), and its ends.</summary>
        static float Shared(Rect a, Rect b, out Vector2 p0, out Vector2 p1)
        {
            const float eps = 0.01f;
            p0 = p1 = default;
            if (Mathf.Abs(a.xMax - b.xMin) < eps || Mathf.Abs(b.xMax - a.xMin) < eps)
            {
                float x = Mathf.Abs(a.xMax - b.xMin) < eps ? a.xMax : a.xMin;
                float lo = Mathf.Max(a.yMin, b.yMin), hi = Mathf.Min(a.yMax, b.yMax);
                if (hi - lo <= 0.05f) return 0f;
                p0 = new Vector2(x, lo);
                p1 = new Vector2(x, hi);
                return hi - lo;
            }
            if (Mathf.Abs(a.yMax - b.yMin) < eps || Mathf.Abs(b.yMax - a.yMin) < eps)
            {
                float y = Mathf.Abs(a.yMax - b.yMin) < eps ? a.yMax : a.yMin;
                float lo = Mathf.Max(a.xMin, b.xMin), hi = Mathf.Min(a.xMax, b.xMax);
                if (hi - lo <= 0.05f) return 0f;
                p0 = new Vector2(lo, y);
                p1 = new Vector2(hi, y);
                return hi - lo;
            }
            return 0f;
        }

        /// <summary>Two hallways meet: most are open, but a wider join sometimes gets a wall and a door (a bulkhead), which breaks the sight line.</summary>
        bool ClosesJoin(Room a, Room b, float length) =>
            length >= 2.3f && Mathf.Min(Mathf.Min(a.Area.width, a.Area.height), Mathf.Min(b.Area.width, b.Area.height)) < 2.7f && Chance(0.3f);

        void FindInterfaces()
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                Rect a = Rooms[i].Area;
                for (int j = i + 1; j < Rooms.Count; j++)
                {
                    float shared = Shared(a, Rooms[j].Area, out Vector2 p0, out Vector2 p1);
                    if (shared <= 0f) continue;
                    Vector2 n = Mathf.Abs(p0.x - p1.x) < 1e-4f ? new Vector2(Mathf.Sign(Rooms[j].Area.center.x - a.center.x), 0f) : new Vector2(0f, Mathf.Sign(Rooms[j].Area.center.y - a.center.y));
                    bool bothHalls = Rooms[i].IsHallway && Rooms[j].IsHallway;
                    Interfaces.Add(new Interface { A = i, B = j, P0 = p0, P1 = p1, Normal = n, Open = bothHalls && !ClosesJoin(Rooms[i], Rooms[j], shared) });
                }
                ExteriorEdges(i);
            }
        }

        /// <summary>Each stretch of a room's edge that no other room covers faces the outside (the square's edge or a notch).</summary>
        void ExteriorEdges(int i)
        {
            Rect a = Rooms[i].Area;
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side == 0 || side == 2;
                float line = side == 0 ? a.yMin : side == 1 ? a.xMax : side == 2 ? a.yMax : a.xMin;
                float lo = horizontal ? a.xMin : a.yMin, hi = horizontal ? a.xMax : a.yMax;
                var covered = new List<(float, float)>();
                for (int j = 0; j < Rooms.Count; j++)
                {
                    if (j == i || Shared(a, Rooms[j].Area, out Vector2 p0, out Vector2 p1) <= 0f) continue;
                    if (horizontal ? (Mathf.Abs(p0.y - line) > 0.01f || Mathf.Abs(p1.y - line) > 0.01f) : (Mathf.Abs(p0.x - line) > 0.01f || Mathf.Abs(p1.x - line) > 0.01f)) continue;
                    covered.Add(horizontal ? (Mathf.Min(p0.x, p1.x), Mathf.Max(p0.x, p1.x)) : (Mathf.Min(p0.y, p1.y), Mathf.Max(p0.y, p1.y)));
                }
                covered.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                Vector2 normal = side == 0 ? Vector2.down : side == 1 ? Vector2.right : side == 2 ? Vector2.up : Vector2.left;
                float at = lo;
                void Stretch(float from, float to)
                {
                    if (to - from <= 0.05f) return;
                    Vector2 q0 = horizontal ? new Vector2(from, line) : new Vector2(line, from), q1 = horizontal ? new Vector2(to, line) : new Vector2(line, to);
                    Interfaces.Add(new Interface { A = i, B = -1, P0 = q0, P1 = q1, Normal = normal });
                }
                foreach ((float c0, float c1) in covered)
                {
                    Stretch(at, c0);
                    at = Mathf.Max(at, c1);
                }
                Stretch(at, hi);
            }
        }

        public IEnumerable<int> InterfacesOf(int room)
        {
            for (int k = 0; k < Interfaces.Count; k++) if (Interfaces[k].A == room || Interfaces[k].B == room) yield return k;
        }

        /// <summary>Cuts an opening of <paramref name="width"/> into an interface at a free spot, keeping clear of its ends and other openings.</summary>
        bool TryOpening(int iface, float width, OpeningKind kind, float margin, out Opening opening, float atFraction = -1f)
        {
            opening = null;
            Interface f = Interfaces[iface];
            float len = f.Length, lo = margin, hi = len - margin - width;
            if (hi < lo) return false;
            for (int t = 0; t < 10; t++)
            {
                float s = atFraction >= 0f && t == 0 ? Mathf.Lerp(lo, hi, atFraction) : Range(lo, hi);
                Vector2 a = f.P0 + f.Along * s, b = a + f.Along * width;
                bool clear = true;
                foreach (Opening o in Openings)
                {
                    if (o.Interface != iface) continue;
                    float o0 = Vector2.Dot(o.A - f.P0, f.Along), o1 = Vector2.Dot(o.B - f.P0, f.Along);
                    if (s < Mathf.Max(o0, o1) + 0.5f && s + width > Mathf.Min(o0, o1) - 0.5f) { clear = false; break; }
                }
                if (!clear) continue;
                opening = new Opening { Kind = kind, Interface = iface, A = a, B = b, Exterior = f.Exterior };
                Openings.Add(opening);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the gate and room types

        /// <summary>Wall to the gate's east, for the lever.</summary>
        const float LeverRoom = 1.0f;

        void PlaceGate()
        {
            // A room on the north wall, wide enough, in the middle band so the yard has room: it becomes the loading bay.
            // If the notches leave too little wall, the band and the lever room shrink until something fits.
            for (int pass = 0; pass < 6 && GateRoom < 0; pass++)
            {
                float band = pass < 2 ? 9f : pass == 2 ? 5f : pass == 3 ? 3f : 1.5f;
                float lever = pass < 4 ? LeverRoom : 0.7f;
                var candidates = new List<int>();
                for (int k = 0; k < Interfaces.Count; k++)
                {
                    Interface f = Interfaces[k];
                    if (!f.Exterior || f.Normal != Vector2.up || Mathf.Abs(f.P0.y - Bounds.yMax) > 0.01f) continue;
                    if (pass == 0 && Rooms[f.A].IsHallway) continue;
                    float lo = Mathf.Max(f.P0.x + GateWidth * 0.5f + 0.4f, Bounds.xMin + band), hi = Mathf.Min(f.P1.x - GateWidth * 0.5f - lever, Bounds.xMax - band);
                    if (lo <= hi) candidates.Add(k);
                }
                if (candidates.Count == 0) continue;
                int iface = Pick(candidates);
                Interface g = Interfaces[iface];
                float x0 = Mathf.Max(g.P0.x + GateWidth * 0.5f + 0.4f, Bounds.xMin + band), x1 = Mathf.Min(g.P1.x - GateWidth * 0.5f - lever, Bounds.xMax - band);
                GateX = Range(x0, x1);
                GateRoom = g.A;
                Openings.Add(new Opening { Kind = OpeningKind.Gate, Interface = iface, A = new Vector2(GateX - GateWidth * 0.5f, g.P0.y), B = new Vector2(GateX + GateWidth * 0.5f, g.P0.y), Exterior = true });
                Lever = new Vector2(GateX + GateWidth * 0.5f + lever * 0.45f, g.P0.y - 0.35f);
                if (!Rooms[GateRoom].IsHallway) Rooms[GateRoom].Type = RoomType.LoadingBay;
            }
        }

        /// <summary>Footprint the generator needs: the machine (1.3 x 0.9) and a working space round it.</summary>
        public static readonly Vector2 GeneratorFootprint = new Vector2(1.3f + 1.2f, 0.9f + 1.2f);
        /// <summary>The generator (with its working space) takes at most this share of its room's floor.</summary>
        public const float GeneratorShare = 0.4f;

        public static bool CanHoldGenerator(Rect r) =>
            GeneratorFootprint.x * GeneratorFootprint.y <= GeneratorShare * r.width * r.height &&
            Mathf.Max(r.width, r.height) >= 1.3f + 2f * GeneratorWalkway && Mathf.Min(r.width, r.height) >= 0.9f + 2f * GeneratorWalkway;

        /// <summary>Room the generator leaves to every wall, enough that it stays clear of the doorways.</summary>
        public const float GeneratorWalkway = 1.2f;

        void AssignTypes()
        {
            var free = new List<Room>();
            foreach (Room r in Rooms) if (!r.IsHallway && r.Id != GateRoom) free.Add(r);
            Shuffle(free);

            // Two generator rooms of any reasonable size, far apart (never a hallway).
            var eligible = free.FindAll(r => CanHoldGenerator(r.Area));
            if (eligible.Count > 0)
            {
                Room first = Pick(eligible);
                eligible.Remove(first);
                eligible.Sort((a, b) => (b.Area.center - first.Area.center).sqrMagnitude.CompareTo((a.Area.center - first.Area.center).sqrMagnitude));
                Room second = eligible.Count > 0 ? eligible[rng.Next(Mathf.Min(3, eligible.Count))] : null;
                RoomType[] kinds = { RoomType.Electrical, RoomType.Boiler, RoomType.Workshop, RoomType.Storage, RoomType.Office, RoomType.Storage };
                first.HasGenerator = true;
                first.Type = Pick(kinds);
                free.Remove(first);
                if (second != null)
                {
                    second.HasGenerator = true;
                    do second.Type = Pick(kinds); while (second.Type == first.Type && kinds.Length > 1);
                    free.Remove(second);
                }
            }

            var count = new Dictionary<RoomType, int>();
            int Count(RoomType t) => count.TryGetValue(t, out int c) ? c : 0;
            void Set(Room r, RoomType t) { r.Type = t; count[t] = Count(t) + 1; free.Remove(r); }

            // One of each signature room first: the studio set takes the biggest room.
            free.Sort((a, b) => b.FloorArea.CompareTo(a.FloorArea));
            if (free.Count > 0 && free[0].FloorArea >= 30f) Set(free[0], RoomType.StudioSet);
            Shuffle(free);
            foreach ((RoomType t, float min, float max) in new[] { (RoomType.Restroom, 9f, 20f), (RoomType.BreakRoom, 14f, 45f), (RoomType.LockerRoom, 14f, 45f), (RoomType.ServerRoom, 9f, 25f) })
            {
                Room r = free.Find(x => x.FloorArea >= min && x.FloorArea <= max);
                if (r != null) Set(r, t);
            }

            var caps = new Dictionary<RoomType, int>
            {
                { RoomType.Restroom, 3 }, { RoomType.BreakRoom, 2 }, { RoomType.LockerRoom, 2 }, { RoomType.ServerRoom, 2 },
                { RoomType.Electrical, 1 }, { RoomType.StudioSet, 2 }, { RoomType.Workshop, 3 }, { RoomType.Boiler, 1 },
            };
            foreach (Room r in free.ToArray())
            {
                float area = r.FloorArea;
                (RoomType, float)[] weights = area < 14f
                    ? new[] { (RoomType.Office, 3f), (RoomType.Storage, 3f), (RoomType.Restroom, 1.5f), (RoomType.ServerRoom, 0.7f) }
                    : area < 35f
                        ? new[] { (RoomType.Office, 4f), (RoomType.Storage, 2f), (RoomType.BreakRoom, 1f), (RoomType.Workshop, 2f), (RoomType.LockerRoom, 0.8f), (RoomType.ServerRoom, 0.6f), (RoomType.Electrical, 0.8f), (RoomType.Boiler, 0.6f) }
                        : new[] { (RoomType.Storage, 3f), (RoomType.Workshop, 2f), (RoomType.StudioSet, 1.5f), (RoomType.Office, 1.5f), (RoomType.BreakRoom, 0.8f) };
                float total = 0f;
                foreach ((RoomType t, float w) in weights) if (!caps.TryGetValue(t, out int cap) || Count(t) < cap) total += w;
                float pick = Range(0f, total);
                RoomType chosen = RoomType.Storage;
                foreach ((RoomType t, float w) in weights)
                {
                    if (caps.TryGetValue(t, out int cap) && Count(t) >= cap) continue;
                    chosen = t;
                    if ((pick -= w) <= 0f) break;
                }
                Set(r, chosen);
            }
        }

        // ------------------------------------------------------------------ doors, entrances, windows, barricades

        static bool Big(RoomType t) => t == RoomType.StudioSet || t == RoomType.LoadingBay || t == RoomType.Workshop || t == RoomType.Storage;

        void AddDoor(int iface, Room room)
        {
            bool wide = Big(room.Type) && Interfaces[iface].Length >= WideDoorWidth + 1.2f && Chance(0.4f);
            float width = wide ? WideDoorWidth : DoorWidth;
            OpeningKind kind = wide ? OpeningKind.Doorway : room.Type == RoomType.Restroom || Chance(0.68f) ? OpeningKind.Door : OpeningKind.Doorway;
            if (TryOpening(iface, width, kind, 0.4f + InteriorThickness, out Opening o) || (!wide && TryOpening(iface, 0.9f, kind, 0.25f, out o)))
                o.StartsOpen = kind == OpeningKind.Door && Chance(0.45f);
        }

        void PlaceDoors()
        {
            // A closed join between hallways gets a door of its own.
            for (int k = 0; k < Interfaces.Count; k++)
            {
                Interface f = Interfaces[k];
                if (!f.Exterior && !f.Open && Rooms[f.A].IsHallway && Rooms[f.B].IsHallway) AddDoor(k, Rooms[f.A]);
            }
            foreach (Room room in Rooms)
            {
                if (room.IsHallway) continue;
                var onHall = new List<int>();
                foreach (int k in InterfacesOf(room.Id))
                {
                    Interface f = Interfaces[k];
                    if (!f.Exterior && Rooms[f.Other(room.Id)].IsHallway && f.Length >= DoorWidth + 1.1f) onHall.Add(k);
                }
                if (onHall.Count == 0) continue;
                onHall.Sort((a, b) => Interfaces[b].Length.CompareTo(Interfaces[a].Length));
                AddDoor(Chance(0.7f) ? onHall[0] : Pick(onHall), room);
                float second = Big(room.Type) ? 0.45f : 0.22f;
                foreach (int k in onHall)
                    if (!HasPassage(k) && Chance(second)) { AddDoor(k, room); break; }
            }

            // Back rooms: a door through a neighbour until everything is reachable.
            for (int guard = 0; guard < Rooms.Count; guard++)
            {
                bool[] reach = Reachable();
                bool changed = false;
                foreach (Room room in Rooms)
                {
                    if (reach[room.Id]) continue;
                    var options = new List<int>();
                    foreach (int k in InterfacesOf(room.Id))
                    {
                        Interface f = Interfaces[k];
                        if (!f.Exterior && reach[f.Other(room.Id)] && f.Length >= 1.5f) options.Add(k);
                    }
                    if (options.Count == 0) continue;
                    options.Sort((a, b) => Interfaces[b].Length.CompareTo(Interfaces[a].Length));
                    AddDoor(options[0], room);
                    changed = true;
                    break;
                }
                if (!changed) break;
            }

            // A few extra doors between rooms for loops.
            for (int k = 0; k < Interfaces.Count; k++)
            {
                Interface f = Interfaces[k];
                if (f.Exterior || f.Open || HasPassage(k) || f.Length < 2.4f) continue;
                Room a = Rooms[f.A], b = Rooms[f.B];
                if (a.IsHallway || b.IsHallway || a.Type == RoomType.Restroom || b.Type == RoomType.Restroom) continue;
                if (Chance(0.1f)) AddDoor(k, a);
            }
        }

        bool HasPassage(int iface)
        {
            foreach (Opening o in Openings) if (o.Interface == iface && o.IsPassage) return true;
            return false;
        }

        /// <summary>Which rooms can be walked to from the hallways (through open hallway joins and doorways).</summary>
        public bool[] Reachable()
        {
            var reach = new bool[Rooms.Count];
            var stack = new Stack<int>();
            foreach (Room r in Rooms) if (r.IsHallway) { reach[r.Id] = true; stack.Push(r.Id); break; }
            var links = new List<(int, int)>();
            foreach (Interface f in Interfaces) if (f.Open) links.Add((f.A, f.B));
            foreach (Opening o in Openings)
            {
                Interface f = Interfaces[o.Interface];
                if (o.IsPassage && !f.Exterior) links.Add((f.A, f.B));
            }
            while (stack.Count > 0)
            {
                int r = stack.Pop();
                foreach ((int a, int b) in links)
                {
                    int other = a == r ? b : b == r ? a : -1;
                    if (other >= 0 && !reach[other]) { reach[other] = true; stack.Push(other); }
                }
            }
            return reach;
        }

        void PlaceEntrances()
        {
            // As the original: two in the south, one in the north (besides the gate), one or two east and west.
            var sides = new (Vector2 normal, char name, int count)[] { (Vector2.down, 's', 2), (Vector2.up, 'n', 1), (Vector2.left, 'w', rng.Next(1, 3)), (Vector2.right, 'e', rng.Next(1, 3)) };
            foreach ((Vector2 normal, char name, int count) in sides)
            {
                var halls = new List<int>();
                var rooms = new List<int>();
                for (int k = 0; k < Interfaces.Count; k++)
                {
                    Interface f = Interfaces[k];
                    if (!f.Exterior || f.Normal != normal || f.Length < ExteriorDoorWidth + 1.4f) continue;
                    Room r = Rooms[f.A];
                    if (r.IsHallway) halls.Add(k);
                    else if (!r.HasGenerator && r.Type != RoomType.Restroom) rooms.Add(k);
                }
                Shuffle(halls);
                Shuffle(rooms);
                halls.AddRange(rooms);
                int placed = 0;
                foreach (int k in halls)
                {
                    if (placed >= count) break;
                    if (!TryOpening(k, ExteriorDoorWidth, OpeningKind.Door, 0.55f, out Opening o, 0.5f)) continue;
                    bool spaced = true;
                    foreach ((Vector2 p, char s) in Entrances) if (s == name && Vector2.Distance(p, o.Centre + normal * 2.1f) < 7f) spaced = false;
                    if (name == 'n' && Mathf.Abs(o.Centre.x - GateX) < GateWidth * 0.5f + 3f) spaced = false;
                    if (!spaced) { Openings.Remove(o); continue; }
                    // Some entrances stand open with a pallet beside them instead of a door, as in the original.
                    if (Chance(0.4f)) o.Kind = OpeningKind.Doorway;
                    Entrances.Add((o.Centre + normal * 2.1f, name));
                    placed++;
                }
            }
        }

        void PlaceWindows()
        {
            for (int k = 0; k < Interfaces.Count; k++)
            {
                Interface f = Interfaces[k];
                if (!f.Exterior) continue;
                Room r = Rooms[f.A];
                if (r.Type == RoomType.Restroom || r.Type == RoomType.ServerRoom) continue;
                float len = f.Length;
                for (float s = Range(0.7f, 1.6f); s + WindowWidth < len - 0.7f; s += WindowWidth + Range(1.4f, 2.8f))
                {
                    if (!Chance(r.IsHallway ? 0.55f : 0.7f)) continue;
                    Vector2 a = f.P0 + f.Along * s, b = a + f.Along * WindowWidth;
                    bool clear = true;
                    foreach (Opening o in Openings)
                    {
                        if (o.Interface != k) continue;
                        float o0 = Vector2.Dot(o.A - f.P0, f.Along), o1 = Vector2.Dot(o.B - f.P0, f.Along);
                        float gap = o.IsPassage ? 1.3f : 0.6f;   // keep clear of the doors, where the paths arrive
                        if (s < Mathf.Max(o0, o1) + gap && s + WindowWidth > Mathf.Min(o0, o1) - gap) clear = false;
                    }
                    if (!clear) continue;
                    Openings.Add(new Opening { Kind = Chance(0.25f) ? OpeningKind.BoardedWindow : OpeningKind.Window, Interface = k, A = a, B = b, Exterior = true });
                }
            }
        }

        void PlaceBarricades()
        {
            var candidates = new List<Opening>();
            foreach (Opening o in Openings)
                if (o.Kind == OpeningKind.Doorway && o.Exterior) candidates.Add(o);
            var inner = new List<Opening>();
            foreach (Opening o in Openings)
                if (o.Kind == OpeningKind.Doorway && !o.Exterior && Chance(0.5f)) inner.Add(o);
            Shuffle(inner);
            candidates.AddRange(inner);
            foreach (Opening o in candidates)
            {
                if (Barricades.Count >= MaxBarricades) break;
                bool spaced = true;
                foreach (Barricade b in Barricades) if (Vector2.Distance((b.A + b.B) * 0.5f, o.Centre) < 5.4f) spaced = false;
                if (!spaced) continue;
                Interface f = Interfaces[o.Interface];
                Vector2 side = f.Exterior ? -f.Normal : (Chance(0.5f) ? f.Normal : -f.Normal);
                Barricades.Add(new Barricade { A = o.A, B = o.B, Side = side });
            }
        }

        // ------------------------------------------------------------------ wall runs

        void BuildWalls()
        {
            // Solid stretches of each interface between its openings (windows keep a sill, built separately).
            var runs = new List<(bool vertical, float line, float a, float b, bool exterior)>();
            for (int k = 0; k < Interfaces.Count; k++)
            {
                Interface f = Interfaces[k];
                if (f.Open) continue;
                bool vertical = Mathf.Abs(f.P0.x - f.P1.x) < 1e-4f;
                float line = vertical ? f.P0.x : f.P0.y;
                var cuts = new List<(float, float)>();
                foreach (Opening o in Openings)
                {
                    if (o.Interface != k) continue;
                    float a = vertical ? o.A.y : o.A.x, b = vertical ? o.B.y : o.B.x;
                    cuts.Add((Mathf.Min(a, b), Mathf.Max(a, b)));
                }
                cuts.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                float s = vertical ? f.P0.y : f.P0.x, end = vertical ? f.P1.y : f.P1.x;
                foreach ((float c0, float c1) in cuts)
                {
                    if (c0 - s > 0.02f) runs.Add((vertical, line, s, c0, f.Exterior));
                    s = Mathf.Max(s, c1);
                }
                if (end - s > 0.02f) runs.Add((vertical, line, s, end, f.Exterior));
            }
            // Merge collinear runs that touch, so long walls are one piece.
            runs.Sort((x, y) => x.vertical != y.vertical ? x.vertical.CompareTo(y.vertical) : Mathf.Abs(x.line - y.line) > 1e-3f ? x.line.CompareTo(y.line) : x.a.CompareTo(y.a));
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                while (i + 1 < runs.Count && runs[i + 1].vertical == r.vertical && Mathf.Abs(runs[i + 1].line - r.line) < 1e-3f
                       && runs[i + 1].exterior == r.exterior && runs[i + 1].a <= r.b + 1e-3f)
                {
                    r.b = Mathf.Max(r.b, runs[i + 1].b);
                    i++;
                }
                Walls.Add(r.vertical
                    ? new WallRun { A = new Vector2(r.line, r.a), B = new Vector2(r.line, r.b), Exterior = r.exterior }
                    : new WallRun { A = new Vector2(r.a, r.line), B = new Vector2(r.b, r.line), Exterior = r.exterior });
            }
        }
    }
}
