using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>How big a ruin is: a lone broken wall, two walls meeting at a corner, or the shell of a building.</summary>
    public enum RuinTier { Fragment, Corner, Structure }

    /// <summary>
    /// Lays out a ruin on its own plan, all of it upright walls on one flat base (no stairs, ramps or floors), every
    /// length a whole number of bricks: a fragment (one wall, broken at its free ends, perhaps a window, door or arch in
    /// it); a corner (two walls bonded at a square corner); or a building's shell: a church (a nave with arched windows
    /// down its sides, a gable end with an arched door, a many-sided apse and perhaps a tower), a warehouse (a long
    /// shed with a wide door, an arched door in a gable end, a row of high little windows, perhaps a lean-to) or a house
    /// (an L or a T, a front door into a short hallway, rooms with doorways between them, a nook, windows). Every wall
    /// is broken along its top, a few have fallen in places, and the corners stand longest.
    /// </summary>
    public static class RuinPlanner
    {
        /// <summary>A plan's lengths are whole bricks; its points sit on this grid.</summary>
        public const float Grid = LowPolyModels.BrickLength;
        /// <summary>The longest side of any ruin.</summary>
        public const float MaxExtent = 18.5f;
        /// <summary>Inner hallways: their width, and longest.</summary>
        public const float HallWidth = 1.2f, HallMaxLength = 3.5f;
        /// <summary>Passages between rooms.</summary>
        public const float PassageMin = 0.9f, PassageMax = 1.5f;

        public static RuinPlan Generate(System.Random rng, RuinTier tier) => tier switch
        {
            RuinTier.Fragment => Fragment(rng),
            RuinTier.Corner => Corner(rng),
            _ => (rng.Next(3)) switch { 0 => Church(rng), 1 => Warehouse(rng), _ => Residential(rng) },
        };

        public static RuinPlan Generate(System.Random rng, RuinArchetype kind) => kind switch
        {
            RuinArchetype.Fragment => Fragment(rng),
            RuinArchetype.Corner => Corner(rng),
            RuinArchetype.Church => Church(rng),
            RuinArchetype.Warehouse => Warehouse(rng),
            _ => Residential(rng),
        };

        static float R(System.Random rng, float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        static bool Chance(System.Random rng, float p) => rng.NextDouble() < p;
        static float Snap(float v) => Mathf.Round(v / Grid) * Grid;

        static RuinRun Run(RuinPlan plan, System.Random rng, Vector2 a, Vector2 b, float height, bool exterior = true)
        {
            var r = new RuinRun { A = a, B = b, Height = height, Seed = rng.Next(), Exterior = exterior };
            plan.Runs.Add(r);
            return r;
        }

        /// <summary>A free end broken away, falling to a stub.</summary>
        static void BreakEnd(System.Random rng, RuinRun r, bool start)
        {
            float reach = R(rng, 0.6f, Mathf.Max(0.8f, r.Length * 0.45f)), low = R(rng, 0.05f, 0.6f);
            if (start) { r.StartBreak = reach; r.StartLow = low; }
            else { r.EndBreak = reach; r.EndLow = low; }
        }

        /// <summary>Part of a long wall fallen (sometimes).</summary>
        static void MaybeBreach(System.Random rng, RuinRun r, float chance)
        {
            if (r.Length < 4f || !Chance(rng, chance)) return;
            float w = R(rng, 1f, Mathf.Min(2.6f, r.Length * 0.35f));
            r.Breaches.Add(new Vector3(R(rng, 1.4f + w * 0.5f, r.Length - 1.4f - w * 0.5f), w, R(rng, 0f, 0.75f)));
        }

        /// <summary>A closed loop of walls through the points, bonded at their square corners.</summary>
        static List<RuinRun> Loop(RuinPlan plan, System.Random rng, IList<Vector2> pts, System.Func<int, float> height)
        {
            var runs = new List<RuinRun>();
            for (int i = 0; i < pts.Count; i++) runs.Add(Run(plan, rng, pts[i], pts[(i + 1) % pts.Count], height(i)));
            for (int i = 0; i < runs.Count; i++)
            {
                RuinRun a = runs[i], b = runs[(i + 1) % runs.Count];
                a.JoinEnd = b.JoinStart = true;
                if (Mathf.Abs(Vector2.Dot(a.Dir, b.Dir)) < 0.05f)
                {
                    a.BondEnd = 0;
                    b.BondStart = 1;
                }
            }
            foreach (Vector2 p in pts) plan.Outline.Add(p);
            return runs;
        }

        /// <summary>
        /// Tries to put an opening in a wall at about <paramref name="at"/> (metres along it): kept clear of its ends, of the
        /// places other walls meet it, and of the openings already in it.
        /// </summary>
        static bool Open(RuinRun r, RuinOpening o, List<float> keepOut = null)
        {
            float len = r.Length, margin = 0.6f;
            o.Center = Mathf.Clamp(o.Center, margin + o.Half, len - margin - o.Half);
            if (o.Center - o.Half < margin - 1e-3f || o.Center + o.Half > len - margin + 1e-3f) return false;
            if (keepOut != null)
                foreach (float k in keepOut)
                    if (Mathf.Abs(k - o.Center) < o.Half + 0.55f) return false;
            foreach (RuinOpening e in r.Openings)
            {
                // (One over another is fine, a good way apart: an arched window over a door.)
                bool apart = o.Sill > e.Peak + LowPolyModels.BrickLength + 0.3f || e.Sill > o.Peak + LowPolyModels.BrickLength + 0.3f;
                if (!apart && Mathf.Abs(e.Center - o.Center) < e.Half + o.Half + 0.6f) return false;
            }
            r.Openings.Add(o);
            return true;
        }

        static RuinOpening Window(float at, float w, float sill, float top) => new RuinOpening { Kind = RuinOpeningKind.Window, Center = at, Width = w, Sill = sill, Top = top };
        static RuinOpening Door(float at, float w, float top) => new RuinOpening { Kind = RuinOpeningKind.Door, Center = at, Width = w, Sill = 0f, Top = top };
        static RuinOpening Arch(float at, float w, float sill, float spring) => new RuinOpening { Kind = RuinOpeningKind.Arch, Center = at, Width = w, Sill = sill, Top = spring };

        /// <summary>One of the three kinds of opening, sized for a fragment of wall.</summary>
        static RuinOpening AnyOpening(System.Random rng, float at, float height)
        {
            switch (rng.Next(3))
            {
                case 0:
                {
                    float sill = R(rng, 0.65f, 0.95f);
                    return Window(at, Snap(R(rng, 0.8f, 1.1f)), sill, Mathf.Min(sill + R(rng, 0.9f, 1.2f), Mathf.Max(height - 0.25f, sill + 0.6f)));
                }
                case 1: return Door(at, Snap(R(rng, 0.9f, 1.1f)), R(rng, 1.9f, 2.1f));
                default: return Arch(at, Snap(R(rng, 0.9f, 1.3f)), Chance(rng, 0.5f) ? 0f : R(rng, 0.7f, 0.9f), R(rng, 1.4f, 1.7f));
            }
        }

        /// <summary>A lone stretch of broken wall.</summary>
        static RuinPlan Fragment(System.Random rng)
        {
            var plan = new RuinPlan { Archetype = RuinArchetype.Fragment };
            float len = Snap(R(rng, 2.6f, 4.6f));
            RuinRun r = Run(plan, rng, new Vector2(-len * 0.5f, 0f), new Vector2(len * 0.5f, 0f), R(rng, 1.4f, 2.6f));
            if (Chance(rng, 0.7f)) BreakEnd(rng, r, true);
            if (Chance(rng, 0.7f)) BreakEnd(rng, r, false);
            if (len >= 3f && Chance(rng, 0.5f)) Open(r, AnyOpening(rng, len * R(rng, 0.35f, 0.65f), r.Height));
            return plan;
        }

        /// <summary>Two walls bonded at a square corner, their free ends broken.</summary>
        static RuinPlan Corner(System.Random rng)
        {
            var plan = new RuinPlan { Archetype = RuinArchetype.Corner };
            float l1 = Snap(R(rng, 2f, 4.4f)), l2 = Snap(R(rng, 1.8f, 4f));
            Vector2 o = new Vector2(-l1 * 0.5f, -l2 * 0.5f);
            RuinRun a = Run(plan, rng, o, o + new Vector2(l1, 0f), R(rng, 1.4f, 2.8f));
            RuinRun b = Run(plan, rng, o, o + new Vector2(0f, l2), R(rng, 1.4f, 2.8f));
            a.JoinStart = b.JoinStart = true;
            a.BondStart = 0;
            b.BondStart = 1;
            if (Chance(rng, 0.75f)) BreakEnd(rng, a, false);
            if (Chance(rng, 0.75f)) BreakEnd(rng, b, false);
            int n = rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                RuinRun w = i == 0 ? a : b;
                if (w.Length >= 3f) Open(w, AnyOpening(rng, w.Length * R(rng, 0.45f, 0.7f), w.Height));
            }
            return plan;
        }

        /// <summary>
        /// A church's shell: a nave with tall arched windows down both sides, a gable end with a big arched door (and an
        /// arched window over it), a many-sided apse with a narrow arched window, and perhaps the stub of a tower.
        /// </summary>
        static RuinPlan Church(System.Random rng)
        {
            var plan = new RuinPlan { Archetype = RuinArchetype.Church };
            float hw = 2.8f, len = Snap(R(rng, 10f, 11.6f)), hl = len * 0.5f;
            int sides = Chance(rng, 0.5f) ? 3 : 5;
            var pts = new List<Vector2> { new Vector2(-hw, -hl), new Vector2(hw, -hl), new Vector2(hw, hl) };
            for (int k = 1; k < sides; k++)
            {
                float a = k * Mathf.PI / sides;
                pts.Add(new Vector2(hw * Mathf.Cos(a), hl + hw * Mathf.Sin(a)));
            }
            pts.Add(new Vector2(-hw, hl));
            float side = R(rng, 1.9f, 2.5f);
            List<RuinRun> runs = Loop(plan, rng, pts, i => i == 0 ? R(rng, 2.6f, 3f) : i == 1 || i == pts.Count - 1 ? side + R(rng, -0.3f, 0.3f) : R(rng, 1.6f, 2.3f));
            RuinRun west = runs[0], south = runs[1], north = runs[runs.Count - 1];
            west.Gable = R(rng, 1.4f, 1.8f);
            Open(west, Arch(west.Length * 0.5f, 1.6f, 0f, 2f));
            Open(west, Arch(west.Length * 0.5f, 0.8f, 3.3f, 3.9f));
            foreach (RuinRun s in new[] { south, north })
            {
                int n = Mathf.Max(2, Mathf.FloorToInt((s.Length - 2.6f) / 3.2f));
                for (int k = 0; k < n; k++) Open(s, Arch(1.6f + (s.Length - 3.2f) * (k + 0.5f) / n, 0.9f, 1f, 2f));
                MaybeBreach(rng, s, 0.45f);
            }
            RuinRun mid = runs[2 + (sides - 1) / 2];
            Open(mid, Arch(mid.Length * 0.5f, 0.6f, 1f, 1.7f));
            if (Chance(rng, 0.35f))
            {
                // The tower's stub, beside the nave at its west end.
                var t0 = new Vector2(-hw, -hl + 0.2f);
                float ts = 2.4f;
                RuinRun t1 = Run(plan, rng, t0, t0 + new Vector2(-ts, 0f), R(rng, 2.6f, 3.4f));
                RuinRun t2 = Run(plan, rng, t0 + new Vector2(-ts, 0f), t0 + new Vector2(-ts, ts), R(rng, 2.6f, 3.4f));
                RuinRun t3 = Run(plan, rng, t0 + new Vector2(-ts, ts), t0 + new Vector2(0f, ts), R(rng, 2.6f, 3.4f));
                t1.JoinStart = t3.JoinEnd = true;
                t1.JoinEnd = t2.JoinStart = true;
                t1.BondEnd = 0;
                t2.BondStart = 1;
                t2.JoinEnd = t3.JoinStart = true;
                t2.BondEnd = 0;
                t3.BondStart = 1;
                Open(t2, Arch(ts * 0.5f, 0.9f, 0f, 1.5f));
            }
            return plan;
        }

        /// <summary>
        /// A warehouse's shell: a long shed with gable ends, a wide door in one long side, a big arched door in a gable end,
        /// a row of high little windows down both sides, and perhaps a lean-to against one side.
        /// </summary>
        static RuinPlan Warehouse(System.Random rng)
        {
            var plan = new RuinPlan { Archetype = RuinArchetype.Warehouse };
            float hw = 3.6f, len = Snap(R(rng, 11.6f, 13.2f)), hl = len * 0.5f;
            var pts = new[] { new Vector2(-hw, -hl), new Vector2(hw, -hl), new Vector2(hw, hl), new Vector2(-hw, hl) };
            float h = R(rng, 1.8f, 2.4f);
            List<RuinRun> runs = Loop(plan, rng, pts, i => h + R(rng, -0.3f, 0.3f));
            RuinRun south = runs[0], east = runs[1], north = runs[2], west = runs[3];
            south.Gable = north.Gable = R(rng, 1f, 1.4f);
            south.Height = north.Height = h + 0.5f;
            Open(east, Door(east.Length * 0.5f, 3f, 2.4f));
            Open(north, Arch(north.Length * 0.5f, 2.2f, 0f, 1.6f));
            bool leanTo = Chance(rng, 0.4f);
            float la = Snap(-R(rng, 2.4f, 3.2f)), lb = Snap(R(rng, 2.4f, 3.2f));
            var westKeep = new List<float>();
            if (leanTo)
            {
                westKeep.Add(west.Length - (la + hl));
                westKeep.Add(west.Length - (lb + hl));
                // A way through into the lean-to.
                Open(west, Door(west.Length - ((la + lb) * 0.5f + hl), 1.2f, 2.1f));
            }
            foreach (RuinRun s in new[] { east, west })
            {
                for (float x = 1.3f; x < s.Length - 1f; x += 2.2f) Open(s, Window(x, 0.7f, 1.45f, 2f), s == west ? westKeep : null);
                MaybeBreach(rng, s, 0.4f);
            }
            if (leanTo)
            {
                // A lean-to against the west side, with its own door.
                float lw = 2.6f, lh = R(rng, 1.4f, 2f);
                RuinRun l1 = Run(plan, rng, new Vector2(-hw, la), new Vector2(-hw - lw, la), lh);
                RuinRun l2 = Run(plan, rng, new Vector2(-hw - lw, la), new Vector2(-hw - lw, lb), lh);
                RuinRun l3 = Run(plan, rng, new Vector2(-hw - lw, lb), new Vector2(-hw, lb), lh);
                l1.JoinStart = l3.JoinEnd = true;
                l1.JoinEnd = l2.JoinStart = true;
                l1.BondEnd = 0;
                l2.BondStart = 1;
                l2.JoinEnd = l3.JoinStart = true;
                l2.BondEnd = 0;
                l3.BondStart = 1;
                Open(l2, Door(l2.Length * 0.5f, 1.2f, 2f));
            }
            return plan;
        }

        /// <summary>
        /// A house's shell, an L or a T: a front door into a short hallway between two walls, a room either side of it,
        /// a doorway through to the room beyond, a nook against the back wall, and windows round the outside.
        /// </summary>
        static RuinPlan Residential(System.Random rng)
        {
            var plan = new RuinPlan { Archetype = RuinArchetype.Residential };
            float w = Snap(R(rng, 6.4f, 7.6f)), d = Snap(R(rng, 5.2f, 6.4f)), ww = Snap(R(rng, 3.4f, 4.2f)), wd = Snap(R(rng, 3f, 4f));
            float hw = w * 0.5f, hd = d * 0.5f;
            bool tee = Chance(rng, 0.35f);
            List<Vector2> pts = tee
                ? new List<Vector2> { new Vector2(-hw, -hd), new Vector2(hw, -hd), new Vector2(hw, hd), new Vector2(ww * 0.5f, hd), new Vector2(ww * 0.5f, hd + wd), new Vector2(-ww * 0.5f, hd + wd), new Vector2(-ww * 0.5f, hd), new Vector2(-hw, hd) }
                : new List<Vector2> { new Vector2(-hw, -hd), new Vector2(hw, -hd), new Vector2(hw, hd), new Vector2(-hw + ww, hd), new Vector2(-hw + ww, hd + wd), new Vector2(-hw, hd + wd) };
            List<RuinRun> runs = Loop(plan, rng, pts, i => R(rng, 1.7f, 2.5f));
            var keep = new Dictionary<RuinRun, List<float>>();
            foreach (RuinRun r in runs) keep[r] = new List<float>();
            RuinRun front = runs[0];

            // The front door into a hallway between two walls.
            float hall = Snap(R(rng, -hw + 2.4f, hw - 2.4f)), hallLen = Snap(R(rng, 2.4f, HallMaxLength - 0.2f));
            Open(front, Door(hall + hw, 1f, R(rng, 2f, 2.2f)));
            float hp = HallWidth * 0.5f;
            RuinRun left = Run(plan, rng, new Vector2(hall - hp, -hd), new Vector2(hall - hp, -hd + hallLen), R(rng, 1.3f, 2f), false);
            RuinRun right = Run(plan, rng, new Vector2(hall + hp, -hd), new Vector2(hall + hp, -hd + hallLen), R(rng, 1.3f, 2f), false);
            left.JoinStart = right.JoinStart = true;
            keep[front].Add(hall - hp + hw);
            keep[front].Add(hall + hp + hw);
            plan.Hallways.Add(new Vector2(HallWidth, hallLen));

            // A wall from the west side to the hallway, with a doorway through it.
            float cross = -hd + Snap(hallLen * R(rng, 0.45f, 0.75f));
            RuinRun west = runs[runs.Count - 1];
            RuinRun across = Run(plan, rng, new Vector2(-hw, cross), new Vector2(hall - hp, cross), R(rng, 1.3f, 2f), false);
            across.JoinStart = across.JoinEnd = true;
            keep[west].Add(west.Length - (cross + hd));
            float pass = Snap(R(rng, PassageMin, PassageMax));
            if (across.Length >= pass + 1.2f && Open(across, Door(across.Length * 0.5f, pass, 2f))) plan.Passages.Add(pass);

            // Into the wing: a doorway where it meets the main room (or just open).
            if (Chance(rng, 0.6f))
            {
                float x0 = tee ? -ww * 0.5f : -hw, x1 = tee ? ww * 0.5f : -hw + ww;
                RuinRun into = Run(plan, rng, new Vector2(x0, hd), new Vector2(x1, hd), R(rng, 1.3f, 2f), false);
                into.JoinStart = into.JoinEnd = true;
                float p2 = Snap(R(rng, PassageMin, PassageMax));
                if (Open(into, Door(into.Length * 0.5f, p2, 2f))) plan.Passages.Add(p2);
            }

            // A nook: two short walls out from the back wall of the main room.
            RuinRun back = runs[2];
            if (back.Length >= 2.8f)
            {
                float nw = Snap(R(rng, 1f, 1.4f)), nd = Snap(R(rng, 0.9f, 1.4f));
                float along = Snap(R(rng, 0.8f, back.Length - 0.8f - nw));
                Vector2 p0 = back.A + back.Dir * along, p1 = back.A + back.Dir * (along + nw), inward = new Vector2(0f, -1f);
                RuinRun n0 = Run(plan, rng, p0, p0 + inward * nd, R(rng, 1.2f, 1.9f), false);
                RuinRun n1 = Run(plan, rng, p1, p1 + inward * nd, R(rng, 1.2f, 1.9f), false);
                n0.JoinStart = n1.JoinStart = true;
                keep[back].Add(along);
                keep[back].Add(along + nw);
            }

            // Windows round the outside.
            foreach (RuinRun r in runs)
            {
                if (r.Length < 2.6f) continue;
                int n = r.Length > 5.5f ? 2 : 1;
                for (int k = 0; k < n; k++) Open(r, Window(r.Length * (k + 1f) / (n + 1f), 0.9f, R(rng, 0.8f, 0.95f), R(rng, 1.7f, 1.9f)), keep[r]);
            }
            foreach (RuinRun r in runs) MaybeBreach(rng, r, 0.3f);
            return plan;
        }
    }
}
