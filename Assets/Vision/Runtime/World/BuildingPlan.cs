using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The central building's floor plan, a port of the original's warehouse generator scaled to the building's size: a grid
    /// of cells (16 x 16 of 2.25 m), a BSP that picks rooms, a recursive-backtracker maze carved through every room and
    /// corridor cell, then extra walls knocked out for loops. The result is a maze of corridors with rooms along them, as in
    /// the original, inside a footprint with corners cut out (an L, T, U, S or a docked square). Room entrances get doors or
    /// open doorways, the outside the original's entrances, windows and the north exit gate with its loading bay.
    /// Corridor cells are gathered into hallways along their straight runs. Pure data: <see cref="SandboxWorld"/> builds it,
    /// the tests check it. Coordinates are map metres, x east and y north (the world's z).
    /// </summary>
    public sealed partial class BuildingPlan
    {
        public const float WallHeight = 2.6f;
        public const float ExteriorThickness = 0.3f, InteriorThickness = 0.16f;
        public const float DoorWidth = 1.1f, WideDoorWidth = 2.2f, ExteriorDoorWidth = 1.4f, WindowWidth = 1.4f, GateWidth = 3.2f;
        public const float DoorHeight = 2.1f;
        /// <summary>The original's lamps light 300 units: 9 m.</summary>
        public const float LampRange = 9f;
        public const int MaxBarricades = 7, MaxHallLockers = 8;

        public enum RoomType { Hallway, Office, Storage, BreakRoom, Restroom, LockerRoom, Workshop, Electrical, ServerRoom, StudioSet, LoadingBay, Boiler }
        public enum OpeningKind { Doorway, Door, Window, BoardedWindow, Gate, Gap }

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
            public bool IsPassage => Kind == OpeningKind.Door || Kind == OpeningKind.Doorway || Kind == OpeningKind.Gate || Kind == OpeningKind.Gap;
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
        public enum LampKind { Fluorescent, Bulb, Desk, Exit, Vending, Furnace, Server, Stage, Tv }

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

        /// <summary>The plan is a grid of <see cref="Cells"/> x <see cref="Cells"/> square cells (the original: 10 x 10 of 3.6 m).</summary>
        public const int Cells = 16;
        /// <summary>BSP leaves are at most this many cells across (the original: 4).</summary>
        public const int MaxLeaf = 6;
        /// <summary>Share of the remaining walls between cells knocked out for loops (the original's braid).</summary>
        public const float Braid = 0.16f;

        public float Cell => Bounds.width / Cells;

        enum Edge { Wall, Open, Door, Gate }

        // h[r, c]: the horizontal edge under cell (r, c), r in 0..Cells (row 0 is the south edge). v[r, c]: the vertical
        // edge left of cell (r, c), c in 0..Cells.
        Edge[,] h, v;
        /// <summary>Which space each cell belongs to (-1: outside, a notch).</summary>
        int[,] space;
        bool[,] inside;
        /// <summary>Which interface each cell edge lies on (-1: none, inside one space).</summary>
        int[,] hFace, vFace;

        public BuildingPlan(int seed, Rect bounds)
        {
            Seed = seed;
            Bounds = bounds;
            rng = new System.Random(seed * 131 + 17);
            ChooseNotches();
            List<RectInt> rooms = PlaceRooms(out RectInt bay);
            CarveMaze(rooms);
            MakeSpaces(rooms, bay);
            BuildInterfaces();
            AssignTypes();
            PlaceExterior();
            PlaceInteriorOpenings();
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

        Rect CellRect(RectInt r) => new Rect(Bounds.xMin + r.x * Cell, Bounds.yMin + r.y * Cell, r.width * Cell, r.height * Cell);

        // ------------------------------------------------------------------ the footprint

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
        /// Cuts whole cells out of the square's corners: an L, a T or U (two corners of one side), an S (opposite corners)
        /// or a square with one small loading-dock notch. The north wall always keeps its middle for the gate.
        /// </summary>
        void ChooseNotches()
        {
            int n = Cells;
            inside = new bool[n, n];
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) inside[r, c] = true;
            var cut = new List<RectInt>();
            RectInt Corner(int k, int w, int d) => new RectInt(k % 2 == 0 ? 0 : n - w, k < 2 ? 0 : n - d, w, d);
            double roll = rng.NextDouble();
            if (roll < 0.30) cut.Add(Corner(rng.Next(4), rng.Next(4, 7), rng.Next(4, 7)));                       // L
            else if (roll < 0.52)                                                                                // T or U
            {
                int pair = rng.Next(4);   // the south, north, west or east side
                (int k0, int k1) = pair switch { 0 => (0, 1), 1 => (2, 3), 2 => (0, 2), _ => (1, 3) };
                bool across = pair < 2;   // two corners of a north or south side: narrow, deep
                cut.Add(Corner(k0, across ? rng.Next(3, 6) : rng.Next(4, 7), across ? rng.Next(4, 7) : rng.Next(3, 6)));
                cut.Add(Corner(k1, across ? rng.Next(3, 6) : rng.Next(4, 7), across ? rng.Next(4, 7) : rng.Next(3, 6)));
            }
            else if (roll < 0.68)                                                                                // S
            {
                bool flip = Chance(0.5f);
                cut.Add(Corner(flip ? 0 : 1, rng.Next(3, 6), rng.Next(3, 6)));
                cut.Add(Corner(flip ? 3 : 2, rng.Next(3, 6), rng.Next(3, 6)));
            }
            else cut.Add(Corner(rng.Next(4), rng.Next(2, 4), rng.Next(2, 4)));                                   // a loading dock
            foreach (RectInt k in cut)
            {
                Notches.Add(CellRect(k));
                for (int r = k.y; r < k.yMax; r++) for (int c = k.x; c < k.xMax; c++) inside[r, c] = false;
            }
        }

        bool Inside(int r, int c) => r >= 0 && c >= 0 && r < Cells && c < Cells && inside[r, c];

        bool AllInside(RectInt k)
        {
            for (int r = k.y; r < k.yMax; r++) for (int c = k.x; c < k.xMax; c++) if (!Inside(r, c)) return false;
            return true;
        }

        // ------------------------------------------------------------------ rooms (the original's BSP)

        /// <summary>
        /// The loading bay on the north wall (the gate opens from it), then the original's rooms: a BSP split into leaves of at
        /// most <see cref="MaxLeaf"/> cells, about half of which become rooms (the two largest always), most shrunk a cell
        /// inside their leaf so corridors run round them. Rooms keep out of the notches and the bay.
        /// </summary>
        List<RectInt> PlaceRooms(out RectInt bay)
        {
            int n = Cells;
            // The gate spans two cells of the top row, away from the corners: try a 4 x 3 bay round it, then smaller.
            bay = default;
            var cols = new List<int>();
            for (int g = 3; g <= n - 5; g++) cols.Add(g);
            Shuffle(cols);
            foreach ((int w, int d, int left) in new[] { (4, 3, 1), (3, 3, 0), (2, 2, 0) })
            {
                foreach (int g in cols)
                {
                    var k = new RectInt(g - left, n - d, w, d);
                    if (k.x < 1 || k.xMax > n - 1 || !AllInside(k)) continue;
                    bay = k;
                    GateCol = g;
                    break;
                }
                if (bay.width > 0) break;
            }

            var leaves = new List<RectInt>();
            void Split(RectInt r)
            {
                if (r.width <= MaxLeaf && r.height <= MaxLeaf) { leaves.Add(r); return; }
                bool vertical = r.width > r.height || (r.width == r.height && Chance(0.5f));
                if (vertical)
                {
                    int s = rng.Next(2, r.width - 1);
                    Split(new RectInt(r.x, r.y, s, r.height));
                    Split(new RectInt(r.x + s, r.y, r.width - s, r.height));
                }
                else
                {
                    int s = rng.Next(2, r.height - 1);
                    Split(new RectInt(r.x, r.y, r.width, s));
                    Split(new RectInt(r.x, r.y + s, r.width, r.height - s));
                }
            }
            Split(new RectInt(0, 0, n, n));

            RectInt reserved = bay;
            bool Blocked(int r, int c) => !Inside(r, c) || (reserved.width > 0 && reserved.Contains(new Vector2Int(c, r)));
            // A leaf cut by a notch or the bay keeps its largest clear rectangle.
            RectInt Fit(RectInt k)
            {
                for (int guard = 0; guard < 32 && k.width > 0 && k.height > 0; guard++)
                {
                    Vector2Int bad = new Vector2Int(-1, -1);
                    for (int r = k.y; r < k.yMax && bad.x < 0; r++) for (int c = k.x; c < k.xMax; c++) if (Blocked(r, c)) { bad = new Vector2Int(c, r); break; }
                    if (bad.x < 0) return k;
                    RectInt best = default;
                    foreach (RectInt o in new[]
                    {
                        new RectInt(k.x, k.y, bad.x - k.x, k.height), new RectInt(bad.x + 1, k.y, k.xMax - bad.x - 1, k.height),
                        new RectInt(k.x, k.y, k.width, bad.y - k.y), new RectInt(k.x, bad.y + 1, k.width, k.yMax - bad.y - 1),
                    })
                        if (o.width > 0 && o.height > 0 && o.width * o.height > best.width * best.height) best = o;
                    k = best;
                }
                return default;
            }

            var fitted = new List<RectInt>();
            foreach (RectInt leaf in leaves) fitted.Add(Fit(leaf));
            var byArea = new List<int>();
            for (int i = 0; i < fitted.Count; i++) byArea.Add(i);
            byArea.Sort((a, b) => (fitted[b].width * fitted[b].height).CompareTo(fitted[a].width * fitted[a].height));
            var rooms = new List<RectInt>();
            for (int i = 0; i < fitted.Count; i++)
            {
                RectInt rr = fitted[i];
                bool forced = i == byArea[0] || (byArea.Count > 1 && i == byArea[1]);
                if (rr.width < 2 || rr.height < 2) continue;
                if (!forced && !Chance(0.5f)) continue;
                if (!forced && rr.width > 2 && Chance(0.5f)) { rr.width -= 1; if (Chance(0.5f)) rr.x += 1; }
                if (!forced && rr.height > 2 && Chance(0.5f)) { rr.height -= 1; if (Chance(0.5f)) rr.y += 1; }
                rooms.Add(rr);
            }
            return rooms;
        }

        int GateCol = -1;

        // ------------------------------------------------------------------ the maze (the original's backtracker and braid)

        void CarveMaze(List<RectInt> rooms)
        {
            int n = Cells;
            h = new Edge[n + 1, n];
            v = new Edge[n, n + 1];
            var roomOf = new int[n, n];
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) roomOf[r, c] = -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                RectInt rr = rooms[i];
                for (int r = rr.y; r < rr.yMax; r++)
                    for (int c = rr.x; c < rr.xMax; c++)
                    {
                        roomOf[r, c] = i;
                        if (c > rr.x) v[r, c] = Edge.Open;
                        if (r > rr.y) h[r, c] = Edge.Open;
                    }
            }
            cellRoom = roomOf;

            // Recursive backtracker (iterative). Entering a room visits all its cells.
            var visited = new bool[n, n];
            var stack = new List<Vector2Int>();
            void Visit(int r, int c)
            {
                int ri = roomOf[r, c];
                if (ri >= 0)
                {
                    RectInt rr = rooms[ri];
                    for (int y = rr.y; y < rr.yMax; y++)
                        for (int x = rr.x; x < rr.xMax; x++)
                            if (!visited[y, x]) { visited[y, x] = true; stack.Add(new Vector2Int(x, y)); }
                }
                else { visited[r, c] = true; stack.Add(new Vector2Int(c, r)); }
            }
            var all = new List<Vector2Int>();
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) if (inside[r, c]) all.Add(new Vector2Int(c, r));
            Vector2Int first = Pick(all);
            Visit(first.y, first.x);
            var options = new List<Vector2Int>(4);
            while (stack.Count > 0)
            {
                Vector2Int cell = stack[stack.Count - 1];
                options.Clear();
                foreach (Vector2Int d in Dirs)
                {
                    int nr = cell.y + d.y, nc = cell.x + d.x;
                    if (Inside(nr, nc) && !visited[nr, nc]) options.Add(new Vector2Int(nc, nr));
                }
                if (options.Count == 0) { stack.RemoveAt(stack.Count - 1); continue; }
                Vector2Int next = Pick(options);
                SetBetween(cell, next, Edge.Open);
                Visit(next.y, next.x);
            }

            // Braid: remove extra walls for loops.
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    if (!inside[r, c]) continue;
                    if (Inside(r, c + 1) && v[r, c + 1] == Edge.Wall && Chance(Braid)) v[r, c + 1] = Edge.Open;
                    if (Inside(r + 1, c) && h[r + 1, c] == Edge.Wall && Chance(Braid)) h[r + 1, c] = Edge.Open;
                }
        }

        static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1) };

        int[,] cellRoom;

        void SetBetween(Vector2Int a, Vector2Int b, Edge e)
        {
            if (a.y == b.y) v[a.y, Mathf.Max(a.x, b.x)] = e;
            else h[Mathf.Max(a.y, b.y), a.x] = e;
        }

        // ------------------------------------------------------------------ spaces

        /// <summary>
        /// The bay and rooms become spaces; the corridor cells are gathered into hallways along their straight runs (the
        /// longest first), so a hallway is a corridor you can see down. A corridor cell left over is a hallway of one cell.
        /// </summary>
        void MakeSpaces(List<RectInt> rooms, RectInt bay)
        {
            int n = Cells;
            space = new int[n, n];
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) space[r, c] = -1;
            void Claim(RectInt k, RoomType type)
            {
                int id = Rooms.Count;
                Rooms.Add(new Room { Id = id, Area = CellRect(k), Type = type });
                for (int r = k.y; r < k.yMax; r++) for (int c = k.x; c < k.xMax; c++) space[r, c] = id;
            }
            if (bay.width > 0)
            {
                // The bay is open inside, and its cells join the maze on every open edge they already had.
                for (int r = bay.y; r < bay.yMax; r++)
                    for (int c = bay.x; c < bay.xMax; c++)
                    {
                        if (c > bay.x) v[r, c] = Edge.Open;
                        if (r > bay.y) h[r, c] = Edge.Open;
                    }
                GateRoom = Rooms.Count;
                Claim(bay, RoomType.LoadingBay);
            }
            foreach (RectInt k in rooms) Claim(k, RoomType.Storage);

            // Straight corridor runs: consecutive corridor cells joined by open edges.
            bool Corridor(int r, int c) => Inside(r, c) && space[r, c] < 0;
            var runs = new List<RectInt>();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    if (!Corridor(r, c)) continue;
                    if (!(c > 0 && Corridor(r, c - 1) && v[r, c] == Edge.Open))
                    {
                        int e = c;
                        while (e + 1 < n && Corridor(r, e + 1) && v[r, e + 1] == Edge.Open) e++;
                        if (e > c) runs.Add(new RectInt(c, r, e - c + 1, 1));
                    }
                    if (!(r > 0 && Corridor(r - 1, c) && h[r, c] == Edge.Open))
                    {
                        int e = r;
                        while (e + 1 < n && Corridor(e + 1, c) && h[e + 1, c] == Edge.Open) e++;
                        if (e > r) runs.Add(new RectInt(c, r, 1, e - r + 1));
                    }
                }
            runs.Sort((a, b) => Mathf.Max(b.width, b.height).CompareTo(Mathf.Max(a.width, a.height)));
            foreach (RectInt run in runs)
            {
                // The unclaimed stretches of this run, two cells or more.
                bool alongX = run.width > 1;
                int len = alongX ? run.width : run.height, start = -1;
                for (int i = 0; i <= len; i++)
                {
                    bool free = i < len && space[run.y + (alongX ? 0 : i), run.x + (alongX ? i : 0)] < 0;
                    if (free && start < 0) start = i;
                    if (!free && start >= 0)
                    {
                        if (i - start >= 2) Claim(alongX ? new RectInt(run.x + start, run.y, i - start, 1) : new RectInt(run.x, run.y + start, 1, i - start), RoomType.Hallway);
                        start = -1;
                    }
                }
            }
            for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) if (Corridor(r, c)) Claim(new RectInt(c, r, 1, 1), RoomType.Hallway);
        }

        // ------------------------------------------------------------------ interfaces

        /// <summary>Each line of cell edges between the same two spaces (or a space and the outside) is one interface.</summary>
        void BuildInterfaces()
        {
            int n = Cells;
            hFace = new int[n + 1, n];
            vFace = new int[n, n + 1];
            float C = Cell;
            for (int line = 0; line <= n; line++)
            {
                // Horizontal line y = line: the cell below is (line - 1, c), above (line, c).
                int run = -1, ra = 0, rb = 0;
                for (int c = 0; c <= n; c++)
                {
                    int below = c < n && Inside(line - 1, c) ? space[line - 1, c] : -1, above = c < n && Inside(line, c) ? space[line, c] : -1;
                    bool edge = c < n && below != above;
                    if (c < n) hFace[line, c] = -1;
                    if (edge && run >= 0 && below == ra && above == rb) { hFace[line, c] = run; Interfaces[run].P1 = new Vector2(Bounds.xMin + (c + 1) * C, Bounds.yMin + line * C); continue; }
                    run = -1;
                    if (!edge) continue;
                    ra = below;
                    rb = above;
                    var p0 = new Vector2(Bounds.xMin + c * C, Bounds.yMin + line * C);
                    var p1 = p0 + new Vector2(C, 0f);
                    if (below < 0) Interfaces.Add(new Interface { A = above, B = -1, P0 = p0, P1 = p1, Normal = Vector2.down });
                    else if (above < 0) Interfaces.Add(new Interface { A = below, B = -1, P0 = p0, P1 = p1, Normal = Vector2.up });
                    else Interfaces.Add(new Interface { A = below, B = above, P0 = p0, P1 = p1, Normal = Vector2.up });
                    run = Interfaces.Count - 1;
                    hFace[line, c] = run;
                }
            }
            for (int line = 0; line <= n; line++)
            {
                // Vertical line x = line: the cell left is (r, line - 1), right (r, line).
                int run = -1, ra = 0, rb = 0;
                for (int r = 0; r <= n; r++)
                {
                    int left = r < n && Inside(r, line - 1) ? space[r, line - 1] : -1, right = r < n && Inside(r, line) ? space[r, line] : -1;
                    bool edge = r < n && left != right;
                    if (r < n) vFace[r, line] = -1;
                    if (edge && run >= 0 && left == ra && right == rb) { vFace[r, line] = run; Interfaces[run].P1 = new Vector2(Bounds.xMin + line * C, Bounds.yMin + (r + 1) * C); continue; }
                    run = -1;
                    if (!edge) continue;
                    ra = left;
                    rb = right;
                    var p0 = new Vector2(Bounds.xMin + line * C, Bounds.yMin + r * C);
                    var p1 = p0 + new Vector2(0f, C);
                    if (left < 0) Interfaces.Add(new Interface { A = right, B = -1, P0 = p0, P1 = p1, Normal = Vector2.left });
                    else if (right < 0) Interfaces.Add(new Interface { A = left, B = -1, P0 = p0, P1 = p1, Normal = Vector2.right });
                    else Interfaces.Add(new Interface { A = left, B = right, P0 = p0, P1 = p1, Normal = Vector2.right });
                    run = Interfaces.Count - 1;
                    vFace[r, line] = run;
                }
            }
        }

        public IEnumerable<int> InterfacesOf(int room)
        {
            for (int k = 0; k < Interfaces.Count; k++) if (Interfaces[k].A == room || Interfaces[k].B == room) yield return k;
        }

        /// <summary>An opening of <paramref name="width"/> centred on a cell edge (horizontal: under cell (r, c); else left of it).</summary>
        Opening Cut(bool horizontal, int r, int c, float width, OpeningKind kind)
        {
            int face = horizontal ? hFace[r, c] : vFace[r, c];
            if (face < 0) return null;
            float C = Cell;
            Vector2 mid = horizontal ? new Vector2(Bounds.xMin + (c + 0.5f) * C, Bounds.yMin + r * C) : new Vector2(Bounds.xMin + c * C, Bounds.yMin + (r + 0.5f) * C);
            Vector2 along = horizontal ? Vector2.right : Vector2.up;
            var o = new Opening { Kind = kind, Interface = face, A = mid - along * (width * 0.5f), B = mid + along * (width * 0.5f), Exterior = Interfaces[face].Exterior };
            Openings.Add(o);
            return o;
        }

        // ------------------------------------------------------------------ the outside: gate, entrances, windows

        /// <summary>Wall to the gate's east, for the lever.</summary>
        const float LeverRoom = 1.0f;

        /// <summary>One exterior cell edge: which edge, its side, and the point outside it.</summary>
        struct OuterEdge
        {
            public bool Horizontal;
            public int R, C;
            public char Side;
            public Vector2 Mid, Normal;
        }

        List<OuterEdge> OuterEdges()
        {
            int n = Cells;
            float C = Cell;
            var list = new List<OuterEdge>();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    if (!inside[r, c]) continue;
                    if (!Inside(r - 1, c)) list.Add(new OuterEdge { Horizontal = true, R = r, C = c, Side = 's', Normal = Vector2.down, Mid = new Vector2(Bounds.xMin + (c + 0.5f) * C, Bounds.yMin + r * C) });
                    if (!Inside(r + 1, c)) list.Add(new OuterEdge { Horizontal = true, R = r + 1, C = c, Side = 'n', Normal = Vector2.up, Mid = new Vector2(Bounds.xMin + (c + 0.5f) * C, Bounds.yMin + (r + 1) * C) });
                    if (!Inside(r, c - 1)) list.Add(new OuterEdge { Horizontal = false, R = r, C = c, Side = 'w', Normal = Vector2.left, Mid = new Vector2(Bounds.xMin + c * C, Bounds.yMin + (r + 0.5f) * C) });
                    if (!Inside(r, c + 1)) list.Add(new OuterEdge { Horizontal = false, R = r, C = c + 1, Side = 'e', Normal = Vector2.right, Mid = new Vector2(Bounds.xMin + (c + 1) * C, Bounds.yMin + (r + 0.5f) * C) });
                }
            return list;
        }

        /// <summary>
        /// As the original: the gate in the north wall (two cells of the loading bay), two entrances south, one north, one or
        /// two east and west, 40% of them open with a pallet beside them, the rest doors; a window in every third cell of wall.
        /// </summary>
        void PlaceExterior()
        {
            int n = Cells;
            float C = Cell;
            if (GateRoom >= 0)
            {
                GateX = Bounds.xMin + (GateCol + 1) * C;
                int face = hFace[n, GateCol];
                Openings.Add(new Opening { Kind = OpeningKind.Gate, Interface = face, A = new Vector2(GateX - GateWidth * 0.5f, Bounds.yMax), B = new Vector2(GateX + GateWidth * 0.5f, Bounds.yMax), Exterior = true });
                h[n, GateCol] = h[n, GateCol + 1] = Edge.Gate;
                Rect bayArea = Rooms[GateRoom].Area;
                Lever = new Vector2(Mathf.Min(GateX + GateWidth * 0.5f + 0.45f, bayArea.xMax - 0.4f), Bounds.yMax - 0.35f);
            }

            List<OuterEdge> outer = OuterEdges();
            var sides = new (char side, int count)[] { ('s', 2), ('n', 1), ('w', rng.Next(1, 3)), ('e', rng.Next(1, 3)) };
            foreach ((char side, int count) in sides)
            {
                var options = outer.FindAll(e => e.Side == side);
                Shuffle(options);
                int placed = 0;
                foreach (OuterEdge e in options)
                {
                    if (placed >= count) break;
                    if ((e.Horizontal ? h[e.R, e.C] : v[e.R, e.C]) != Edge.Wall) continue;
                    // Away from the corners, the gate and the other doors on this side.
                    int along = e.Horizontal ? e.C : e.R;
                    if (along < 1 || along > n - 2) continue;
                    Vector2 outside = e.Mid + e.Normal * 2.1f;
                    if (side == 'n' && Mathf.Abs(e.Mid.x - GateX) < GateWidth * 0.5f + 2f * C) continue;
                    bool spaced = true;
                    foreach ((Vector2 p, char s) in Entrances) if (s == side && Vector2.Distance(p, outside) < 3f * C) spaced = false;
                    if (!spaced) continue;
                    bool open = Chance(0.4f);
                    if (e.Horizontal) h[e.R, e.C] = open ? Edge.Open : Edge.Door;
                    else v[e.R, e.C] = open ? Edge.Open : Edge.Door;
                    Opening o = Cut(e.Horizontal, e.R, e.C, ExteriorDoorWidth, open ? OpeningKind.Doorway : OpeningKind.Door);
                    if (open && o != null) Barricades.Add(new Barricade { A = o.A, B = o.B, Side = -e.Normal });
                    Entrances.Add((outside, side));
                    placed++;
                }
            }

            // Exterior walls get a window every third cell: glass stops you, not your sight.
            foreach (OuterEdge e in outer)
            {
                if ((e.Horizontal ? h[e.R, e.C] : v[e.R, e.C]) != Edge.Wall) continue;
                int along = e.Horizontal ? e.C : e.R;
                if (along % 3 != 1) continue;
                Room room = Rooms[space[e.Horizontal ? (e.Side == 's' ? e.R : e.R - 1) : e.R, e.Horizontal ? e.C : (e.Side == 'w' ? e.C : e.C - 1)]];
                if (room.Type == RoomType.Restroom || room.Type == RoomType.ServerRoom) continue;
                Cut(e.Horizontal, e.R, e.C, WindowWidth, Chance(0.25f) ? OpeningKind.BoardedWindow : OpeningKind.Window);
            }
        }

        // ------------------------------------------------------------------ inside: gaps, doorways and doors

        static bool Big(RoomType t) => t == RoomType.StudioSet || t == RoomType.LoadingBay || t == RoomType.Workshop || t == RoomType.Storage;

        /// <summary>
        /// Every open edge between two spaces becomes a passage: hallway to hallway, no wall at all (a gap the cell's width);
        /// into a room, a doorway, half of them with a door as in the original (restrooms always). Some doors stand open.
        /// </summary>
        void PlaceInteriorOpenings()
        {
            int n = Cells;
            float C = Cell;
            void Passage(bool horizontal, int r, int c)
            {
                Edge e = horizontal ? h[r, c] : v[r, c];
                int face = horizontal ? hFace[r, c] : vFace[r, c];
                if (e != Edge.Open || face < 0 || Interfaces[face].Exterior) return;
                Room a = Rooms[Interfaces[face].A], b = Rooms[Interfaces[face].B];
                if (a.IsHallway && b.IsHallway) { Cut(horizontal, r, c, C, OpeningKind.Gap); return; }
                Room room = a.IsHallway ? b : a;
                bool door = room.Type == RoomType.Restroom || (room.Type != RoomType.LoadingBay && Chance(0.5f));
                if (door)
                {
                    Opening o = Cut(horizontal, r, c, DoorWidth, OpeningKind.Door);
                    o.StartsOpen = Chance(0.45f);
                }
                else Cut(horizontal, r, c, Big(room.Type) && Chance(0.5f) ? C - 0.3f : Mathf.Min(C - 0.5f, 1.6f), OpeningKind.Doorway);
            }
            for (int r = 1; r < n; r++) for (int c = 0; c < n; c++) Passage(true, r, c);
            for (int r = 0; r < n; r++) for (int c = 1; c < n; c++) Passage(false, r, c);
        }

        bool HasPassage(int iface)
        {
            foreach (Opening o in Openings) if (o.Interface == iface && o.IsPassage) return true;
            return false;
        }

        /// <summary>Pallets beside some open doorways (up to the original's seven), spread apart.</summary>
        void PlaceBarricades()
        {
            var inner = new List<Opening>();
            foreach (Opening o in Openings)
            {
                if (o.Kind != OpeningKind.Doorway || o.Exterior) continue;
                Interface f = Interfaces[o.Interface];
                bool mixed = !Rooms[f.A].IsHallway || !Rooms[f.B].IsHallway;
                if (mixed || Chance(0.15f)) inner.Add(o);
            }
            Shuffle(inner);
            foreach (Opening o in inner)
            {
                if (Barricades.Count >= MaxBarricades) break;
                if (!Chance(0.45f)) continue;
                bool spaced = true;
                foreach (Barricade b in Barricades) if (Vector2.Distance((b.A + b.B) * 0.5f, o.Centre) < Cell * 1.5f) spaced = false;
                if (!spaced) continue;
                Interface f = Interfaces[o.Interface];
                Barricades.Add(new Barricade { A = o.A, B = o.B, Side = Chance(0.5f) ? f.Normal : -f.Normal });
            }
            while (Barricades.Count > MaxBarricades) Barricades.RemoveAt(Barricades.Count - 1);
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
            foreach ((RoomType t, float min, float max) in new[] { (RoomType.Restroom, 9f, 31f), (RoomType.BreakRoom, 20f, 61f), (RoomType.LockerRoom, 20f, 61f), (RoomType.ServerRoom, 9f, 31f) })
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
                (RoomType, float)[] weights = area < 31f
                    ? new[] { (RoomType.Office, 3f), (RoomType.Storage, 3f), (RoomType.Restroom, 1.5f), (RoomType.ServerRoom, 0.7f) }
                    : area < 61f
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
