using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Where everything goes on the map, decided before anything is built. Follows the original 2D game's
    /// generator (shared/src/map/generate.ts) at <see cref="Unit"/> metres per original unit: a 6000-unit map is
    /// 180 m, its 1200-unit central building 36 m. North is +Z (the original's y runs south). Deterministic from the
    /// seed. Positions are design units (metres) in the level root's local space, origin at the map centre.
    /// </summary>
    public sealed class MapLayout
    {
        /// <summary>Metres per original map unit.</summary>
        public const float Unit = 0.03f;
        public const float OriginalMapSize = 6000f;
        public const float OriginalBuildingSize = 1200f;
        public static float HalfExtentFor() => OriginalMapSize * Unit * 0.5f;

        public struct Clearing
        {
            public Vector2 Centre;
            public float Radius;
        }

        public enum KitKind { LWall, Shack, Wreck }

        public struct Kit
        {
            public KitKind Kind;
            public Vector2 Centre;
            /// <summary>Quarter turns, as in the original.</summary>
            public int Turns;
        }

        public struct Cabin
        {
            public Rect Area;
            /// <summary>Door side: 0 north, 1 east, 2 south, 3 west.</summary>
            public int DoorSide;
        }

        public struct Segment
        {
            public Vector2 A, B;
            /// <summary>For fences: the gap along the segment (fractions).</summary>
            public float GapStart, GapEnd;
        }

        public readonly int Seed;
        public readonly float HalfExtent;
        public Rect Building;
        public Rect Yard;
        public float GateX;
        public Vector2 Spawn;
        public readonly List<Clearing> Clearings = new List<Clearing>();
        public int HunterClearing = -1;
        public readonly List<Cabin> Cabins = new List<Cabin>();
        public readonly List<int> CabinClearings = new List<int>();
        public readonly List<Vector2> WoodsGenerators = new List<Vector2>();
        public readonly List<Kit> Kits = new List<Kit>();
        public readonly List<Clearing> GrassPatches = new List<Clearing>();
        public readonly List<Segment> Fences = new List<Segment>();
        public readonly List<Segment> Logs = new List<Segment>();
        public readonly List<Vector2> Campfires = new List<Vector2>();
        public readonly List<Vector2> BuildingEntrances = new List<Vector2>();

        public Vector2 LakeCentre;
        public float LakeRadius;
        /// <summary>Shore radius per angle step (28 around), multiplying <see cref="LakeRadius"/>.</summary>
        public readonly float[] LakeShape = new float[28];
        public Vector2 DockStart, DockEnd;
        public float DockHalfWidth;

        public Rect Graveyard;
        public float GraveyardYaw;
        public Rect Playground;
        public Vector2 HangingTree;
        public readonly List<Vector2> PowerPoles = new List<Vector2>();

        /// <summary>A circle or rectangle no tree, rock or other scatter may use.</summary>
        public readonly List<(Vector2 centre, float radius)> KeepCircles = new List<(Vector2, float)>();
        public readonly List<Rect> KeepRects = new List<Rect>();

        System.Random rng;
        float U(float originalUnits) => originalUnits * Unit;
        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public MapLayout(int seed, float halfExtent = 90f)
        {
            Seed = seed;
            HalfExtent = halfExtent;
            rng = new System.Random(seed * 31 + 7);
            float wh = U(OriginalBuildingSize);
            Building = new Rect(-wh * 0.5f, -wh * 0.5f, wh, wh);
            // The gate sits in one of the building's north wall cells 3-6 (10 cells across), its yard outside.
            float cell = wh / 10f;
            GateX = Building.xMin + (rng.Next(3, 7) + 0.5f) * cell;
            Yard = new Rect(GateX - U(150f), Building.yMax, U(300f), U(250f));
            KeepRects.Add(Expand(Building, U(150f)));
            KeepRects.Add(Expand(Yard, U(110f)));
            DefaultEntrances();

            PlaceLake();
            PlaceSpawnAndClearings();
            PlaceCabins();
            PlaceGenerators();
            PlaceFeatures();
            PlaceGrass();
            PlaceFences();
            PlaceLogsAndFires();
            PlacePowerLine();
        }

        static Rect Expand(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

        /// <summary>Building doorways the paths lead to (the building generator may replace these).</summary>
        void DefaultEntrances()
        {
            float m = U(70f);
            BuildingEntrances.Add(new Vector2(GateX, Yard.yMax + m));
            BuildingEntrances.Add(new Vector2(Building.xMin + Building.width * 0.3f, Building.yMin - m));
            BuildingEntrances.Add(new Vector2(Building.xMin + Building.width * 0.7f, Building.yMin - m));
            BuildingEntrances.Add(new Vector2(Building.xMin - m, Building.center.y));
            BuildingEntrances.Add(new Vector2(Building.xMax + m, Building.center.y));
        }

        // ------------------------------------------------------------------ the lake

        void PlaceLake()
        {
            int corner = rng.Next(4);
            float edge = HalfExtent - U(1050f);
            LakeCentre = new Vector2((corner % 2 == 0 ? -1f : 1f) * edge + U(Range(-150f, 150f)), (corner < 2 ? 1f : -1f) * edge + U(Range(-150f, 150f)));
            LakeRadius = U(Range(480f, 580f));
            for (int i = 0; i < LakeShape.Length; i++) LakeShape[i] = 0.8f + (float)rng.NextDouble() * 0.28f;
            // The dock starts at the shore point facing the map centre and runs into the lake.
            Vector2 toCentre = (-LakeCentre).normalized;
            float a = Mathf.Atan2(toCentre.y, toCentre.x);
            DockStart = LakeCentre + toCentre * ShoreRadius(a);
            DockEnd = DockStart - toCentre * U(280f);
            DockHalfWidth = U(38f);
            KeepCircles.Add((LakeCentre, LakeRadius * 1.15f));
        }

        /// <summary>Distance from the lake centre to the shore in a direction (radians).</summary>
        public float ShoreRadius(float angle)
        {
            float f = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * LakeShape.Length;
            int i = Mathf.FloorToInt(f) % LakeShape.Length;
            float t = f - Mathf.Floor(f);
            t = t * t * (3f - 2f * t);
            return LakeRadius * Mathf.Lerp(LakeShape[i], LakeShape[(i + 1) % LakeShape.Length], t);
        }

        /// <summary>How far inside the shore a point is (negative outside).</summary>
        public float LakeDepth(Vector2 p)
        {
            Vector2 d = p - LakeCentre;
            return ShoreRadius(Mathf.Atan2(d.y, d.x)) - d.magnitude;
        }

        public bool LakeClear(Vector2 p, float pad) => (p - LakeCentre).magnitude > LakeRadius * 1.1f + pad;

        // ------------------------------------------------------------------ spawn and clearings

        bool NearBuilding(Vector2 p, float pad) =>
            Mathf.Max(Mathf.Abs(p.x - Building.center.x) - Building.width * 0.5f, Mathf.Abs(p.y - Building.center.y) - Building.height * 0.5f) < pad;

        static bool InRect(Rect r, Vector2 p, float pad) => Expand(r, pad).Contains(p);

        void PlaceSpawnAndClearings()
        {
            Spawn = new Vector2(0f, -HalfExtent + U(450f));
            for (int i = 0; i < 50; i++)
            {
                var s = new Vector2(Range(-HalfExtent + U(1400f), HalfExtent - U(1400f)), -HalfExtent + U(Range(420f, 650f)));
                if (!LakeClear(s, U(350f))) continue;
                Spawn = s;
                break;
            }
            Clearings.Add(new Clearing { Centre = Spawn, Radius = U(230f) });
            const int want = 10;
            float lim = HalfExtent - U(420f);
            for (int tries = 0; tries < 4000 && Clearings.Count < want + 1; tries++)
            {
                float r = U(Range(220f, 300f));
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (NearBuilding(p, r + U(300f)) || InRect(Yard, p, r + U(200f)) || !LakeClear(p, r + U(150f))) continue;
                float minD = U(tries < 2500 ? 820f : 650f);
                bool close = false;
                foreach (Clearing c in Clearings) if ((c.Centre - p).magnitude < minD) { close = true; break; }
                if (close) continue;
                Clearings.Add(new Clearing { Centre = p, Radius = r });
            }
            // The hunter's clearing is the one farthest from the survivors.
            float far = -1f;
            for (int i = 1; i < Clearings.Count; i++)
            {
                float d = (Clearings[i].Centre - Spawn).magnitude;
                if (d > far) { far = d; HunterClearing = i; }
            }
            foreach (Clearing c in Clearings) KeepCircles.Add((c.Centre, c.Radius * 0.85f));
        }

        void PlaceCabins()
        {
            var order = new List<int>();
            for (int i = 1; i < Clearings.Count; i++) order.Add(i);
            Shuffle(order);
            foreach (int i in order)
            {
                if (Cabins.Count >= 3) break;
                if (i == HunterClearing) continue;
                Clearing c = Clearings[i];
                c.Radius = Mathf.Max(c.Radius, U(300f));
                Clearings[i] = c;
                float w = U(280f), h = U(200f);
                var area = new Rect(c.Centre.x - w * 0.5f, c.Centre.y - h * 0.5f + U(40f), w, h);
                Cabins.Add(new Cabin { Area = area, DoorSide = rng.Next(4) });
                CabinClearings.Add(i);
                KeepRects.Add(Expand(area, U(70f)));
            }
        }

        // ------------------------------------------------------------------ generators and their cover

        void PlaceGenerators()
        {
            var candidates = new List<int>();
            for (int i = 1; i < Clearings.Count; i++) if (i != HunterClearing) candidates.Add(i);
            var chosen = new List<int>();
            // Farthest-point selection spreads them; cabins are a last resort (the building holds two more).
            while (chosen.Count < 3 && chosen.Count < candidates.Count)
            {
                int best = -1;
                float bestScore = float.MinValue;
                foreach (int i in candidates)
                {
                    if (chosen.Contains(i)) continue;
                    float d = chosen.Count == 0 ? (float)rng.NextDouble() * 30f : float.MaxValue;
                    foreach (int j in chosen) d = Mathf.Min(d, (Clearings[j].Centre - Clearings[i].Centre).magnitude);
                    d = Mathf.Min(d, (Clearings[i].Centre - Building.center).magnitude);
                    float score = d - (CabinClearings.Contains(i) ? U(400f) : 0f);
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                if (best < 0) break;
                chosen.Add(best);
            }
            foreach (int i in chosen)
            {
                Clearing c = Clearings[i];
                float a = Range(0f, Mathf.PI * 2f);
                Vector2 g = c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * c.Radius * 0.3f;
                int cabin = CabinClearings.IndexOf(i);
                if (cabin >= 0)
                {
                    // Behind the cabin, never in front of its door.
                    Cabin cb = Cabins[cabin];
                    g = new Vector2(c.Centre.x, cb.DoorSide == 0 ? cb.Area.yMin - U(110f) : cb.Area.yMax + U(110f));
                }
                WoodsGenerators.Add(g);
                KeepCircles.Add((g, U(120f)));
                PlaceKits(g, a);
            }
        }

        static float KitRadius(KitKind k) => k == KitKind.LWall ? 160f : k == KitKind.Shack ? 140f : 190f;

        /// <summary>Two pieces of cover (an L-wall, a shack, or a car wreck with boulders) around a woods generator.</summary>
        void PlaceKits(Vector2 g, float a)
        {
            var types = new List<KitKind> { KitKind.LWall, KitKind.Shack, KitKind.Wreck };
            Shuffle(types);
            int placed = 0;
            for (int tries = 0; tries < 60 && placed < 2; tries++)
            {
                KitKind type = types[(placed + tries / 20) % 3];
                float r = U(KitRadius(type));
                float ang = a + Mathf.PI + (placed == 0 ? -1f : 1f) * Range(0.6f, 1.6f) + (tries > 20 ? Range(-2f, 2f) : 0f);
                float dist = r + U(95f) + U(Range(0f, 40f));
                Vector2 k = g + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                float lim = HalfExtent - r - U(60f);
                if (Mathf.Abs(k.x) > lim || Mathf.Abs(k.y) > lim) continue;
                if (NearBuilding(k, r + U(160f)) || !LakeClear(k, r + U(80f)) || InRect(Yard, k, r + U(120f))) continue;
                bool bad = false;
                foreach (Cabin cb in Cabins) if (InRect(cb.Area, k, r + U(90f))) bad = true;
                foreach (Kit o in Kits) if ((o.Centre - k).magnitude < U(KitRadius(o.Kind)) + r + U(90f)) bad = true;
                foreach (Vector2 o in WoodsGenerators) if ((o - k).magnitude < r + U(80f)) bad = true;
                if ((Spawn - k).magnitude < r + U(260f)) bad = true;
                if (bad) continue;
                Kits.Add(new Kit { Kind = type, Centre = k, Turns = rng.Next(4) });
                KeepCircles.Add((k, r + U(70f)));
                placed++;
            }
        }

        // ------------------------------------------------------------------ graveyard, playground, hanging tree

        bool FreeSite(Vector2 p, float r)
        {
            float lim = HalfExtent - r - 4f;
            if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim) return false;
            if (NearBuilding(p, r + 10f) || InRect(Yard, p, r + 6f) || !LakeClear(p, r + 6f)) return false;
            if ((p - Spawn).magnitude < r + 18f) return false;
            foreach (Clearing c in Clearings) if ((c.Centre - p).magnitude < c.Radius + r + 6f) return false;
            foreach (Kit k in Kits) if ((k.Centre - p).magnitude < U(KitRadius(k.Kind)) + r + 4f) return false;
            foreach (Rect k in KeepRects) if (InRect(k, p, r)) return false;
            return true;
        }

        Vector2 Site(float r)
        {
            for (int tries = 0; tries < 3000; tries++)
            {
                var p = new Vector2(Range(-HalfExtent, HalfExtent), Range(-HalfExtent, HalfExtent));
                if (FreeSite(p, r + (tries < 1500 ? 6f : 0f))) return p;
            }
            return new Vector2(HalfExtent - r - 6f, 0f);
        }

        void PlaceFeatures()
        {
            Vector2 g = Site(12f);
            Graveyard = new Rect(g.x - 9f, g.y - 7f, 18f, 14f);
            GraveyardYaw = rng.Next(4) * 90f + Range(-12f, 12f);
            KeepRects.Add(Expand(Graveyard, 1.5f));
            Vector2 p = Site(10f);
            Playground = new Rect(p.x - 7.5f, p.y - 6f, 15f, 12f);
            KeepRects.Add(Expand(Playground, 1.5f));
            HangingTree = Site(7f);
            KeepCircles.Add((HangingTree, 5f));
        }

        // ------------------------------------------------------------------ tall grass, fences, logs, fires

        public bool Blocked(Vector2 p)
        {
            foreach (var (c, r) in KeepCircles) if ((c - p).sqrMagnitude < r * r) return true;
            foreach (Rect r in KeepRects) if (r.Contains(p)) return true;
            return false;
        }

        void PlaceGrass()
        {
            float lim = HalfExtent - U(300f);
            for (int tries = 0; tries < 800 && GrassPatches.Count < 12; tries++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                float r = U(Range(80f, 125f));
                if (Blocked(p) || !LakeClear(p, r + U(60f))) continue;
                bool close = false;
                foreach (Clearing g in GrassPatches) if ((g.Centre - p).magnitude < U(600f)) close = true;
                if (close || (Spawn - p).magnitude < U(500f)) continue;
                GrassPatches.Add(new Clearing { Centre = p, Radius = r });
                KeepCircles.Add((p, U(55f)));
            }
        }

        void PlaceFences()
        {
            float lim = HalfExtent - U(400f);
            for (int tries = 0; tries < 300 && Fences.Count < 7; tries++)
            {
                var a = new Vector2(Range(-lim, lim), Range(-lim, lim));
                float ang = Range(0f, Mathf.PI), len = U(Range(260f, 440f));
                Vector2 b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                bool ok = true;
                for (float t = 0f; t <= 1.001f && ok; t += 0.1f)
                {
                    Vector2 q = Vector2.Lerp(a, b, t);
                    if (Blocked(q) || !LakeClear(q, U(60f))) ok = false;
                }
                if (!ok) continue;
                float g0 = Range(0.35f, 0.55f);
                Fences.Add(new Segment { A = a, B = b, GapStart = g0, GapEnd = g0 + U(90f) / len });
                for (float t = 0f; t <= 1.001f; t += 0.1f) KeepCircles.Add((Vector2.Lerp(a, b, t), U(40f)));
            }
        }

        void PlaceLogsAndFires()
        {
            for (int i = 1; i < Clearings.Count; i++)
            {
                Clearing c = Clearings[i];
                if (rng.NextDouble() >= 0.55 || CabinClearings.Contains(i)) continue;
                float a = Range(0f, Mathf.PI * 2f);
                Vector2 p = c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * c.Radius * 0.62f;
                bool nearGen = false;
                foreach (Vector2 g in WoodsGenerators) if ((g - p).magnitude < U(150f)) nearGen = true;
                if (nearGen) continue;
                float ang = Range(0f, Mathf.PI), len = U(140f);
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len * 0.5f;
                Logs.Add(new Segment { A = p - d, B = p + d });
            }
            var order = new List<int>();
            for (int i = 1; i < Clearings.Count; i++) order.Add(i);
            Shuffle(order);
            for (int n = 0; n < order.Count && n < 4; n++)
            {
                int i = order[n];
                if (CabinClearings.Contains(i)) continue;
                Clearing c = Clearings[i];
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2f;
                    Vector2 p = c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * c.Radius * 0.45f;
                    bool bad = false;
                    foreach (Vector2 g in WoodsGenerators) if ((g - p).magnitude < U(170f)) bad = true;
                    foreach (Segment l in Logs) if (((l.A + l.B) * 0.5f - p).magnitude < U(110f)) bad = true;
                    if (bad) continue;
                    Campfires.Add(p);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ power line

        /// <summary>A line of power poles crossing the woods from one map edge to the opposite one, bending around the building.</summary>
        void PlacePowerLine()
        {
            bool alongX = rng.NextDouble() < 0.5;
            float off = Range(0.35f, 0.7f) * HalfExtent * (rng.NextDouble() < 0.5 ? -1f : 1f);
            float bend = Range(-12f, 12f);
            const float spacing = 22f;
            int n = Mathf.FloorToInt((HalfExtent * 2f - 8f) / spacing);
            for (int i = 0; i <= n; i++)
            {
                float t = -HalfExtent + 4f + i * spacing;
                float side = off + bend * Mathf.Sin(t / HalfExtent * Mathf.PI);
                Vector2 p = alongX ? new Vector2(t, side) : new Vector2(side, t);
                // Step a pole off anything it would stand in.
                for (int k = 0; k < 12 && (Blocked(p) || !LakeClear(p, 2f)); k++)
                    p += (alongX ? Vector2.up : Vector2.right) * 2.5f * (k % 2 == 0 ? 1f : -1f) * (k / 2 + 1);
                if (Blocked(p) || !LakeClear(p, 2f) || NearBuilding(p, 4f)) continue;
                PowerPoles.Add(p);
                KeepCircles.Add((p, 1.2f));
            }
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Every place the paths must join: spawn, the clearings, the building's doorways and the special sites.</summary>
        public List<Vector2> PathPoints()
        {
            var pts = new List<Vector2>();
            foreach (Clearing c in Clearings) pts.Add(c.Centre);
            pts.AddRange(BuildingEntrances);
            pts.Add(new Vector2(Graveyard.center.x, Graveyard.yMin - 1f));
            pts.Add(new Vector2(Playground.center.x, Playground.yMin - 1f));
            pts.Add(HangingTree + Vector2.down * 4f);
            return pts;
        }
    }
}
