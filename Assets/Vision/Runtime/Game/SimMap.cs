using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// The fixed layout the match rules need, in design units on the level's ground plane (x east, y north), indexed in the
    /// level's deterministic build order so every machine gives every object the same id.
    /// </summary>
    public sealed class SimMap
    {
        public float HalfExtent = 90f;
        /// <summary>The warehouse's footprint (Marc starts inside it).</summary>
        public Rect Building = new Rect(-18f, -18f, 36f, 36f);
        public readonly List<Vector2> Generators = new List<Vector2>();
        public Vector2 Lever, GatePos;
        public bool HasGate;
        /// <summary>The fenced yard beyond the gate: walking into it escapes.</summary>
        public Rect ExitZone;
        /// <summary>The second exit gate beside Miga Sushi: its lever, its gate and the yard behind it.</summary>
        public Vector2 Lever2, GatePos2;
        public bool HasGate2;
        public Rect ExitZone2;
        /// <summary>The hatch by the lake's dock.</summary>
        public Vector2 Hatch;
        public bool HasHatch;
        public readonly List<LootDef> Loot = new List<LootDef>();
        public readonly List<DoorDef> Doors = new List<DoorDef>();
        public readonly List<BarricadeDef> Barricades = new List<BarricadeDef>();
        public readonly List<WindowDef> Windows = new List<WindowDef>();
        public readonly List<Vector2> Stakes = new List<Vector2>();
        public readonly List<Vector2> Notes = new List<Vector2>();
        public readonly List<Vector2> SurvivorSpawns = new List<Vector2>();
        /// <summary>Where a sacrificed survivor's ghost comes back: inside Miga Sushi and the Diddy Chamber.</summary>
        public readonly List<Vector2> GhostSpawns = new List<Vector2>();
        /// <summary>Chris Zelley's ambulance: its centre, the direction its length runs (radians), and its length and width.</summary>
        public Vector2 AmbulanceAt = new Vector2(0f, -27f);
        public float AmbulanceAngle;
        public Vector2 AmbulanceSize = new Vector2(300f, 150f) * Scale.Unit;
        /// <summary>The lounge: Chacko's seat on the couch, and the TV he watches.</summary>
        public Vector2 LoungeSeat, LoungeTv = new Vector2(0f, 2f);
        public readonly List<Vector2> HunterSpawns = new List<Vector2>();

        public struct LootDef { public Vector2 Pos; public ItemType Item; public bool Golden; }

        /// <summary>A door's closed panel runs A to B.</summary>
        /// <summary>A door; <see cref="Gate"/>: the graveyard's gates (locked at the start, survivors only).</summary>
        public struct DoorDef { public Vector2 A, B; public bool StartsOpen; public bool Shutter; public bool Gate; }
        /// <summary>Which photo each of <see cref="Notes"/> shows (0-based; missing ones go in order).</summary>
        public readonly List<int> NoteImages = new List<int>();

        public struct BarricadeDef { public Vector2 Pos, A, B; }

        public struct WindowDef { public Vector2 A, B; }

        /// <summary>
        /// A fingerprint of everything the rules use (positions to 1 cm), so a client can tell its level is the host's.
        /// </summary>
        public int Hash()
        {
            unchecked
            {
                int h = 17;
                void V(Vector2 v) { h = h * 31 + Mathf.RoundToInt(v.x * 100f); h = h * 31 + Mathf.RoundToInt(v.y * 100f); }
                void L(List<Vector2> l) { h = h * 31 + l.Count; foreach (Vector2 v in l) V(v); }
                h = h * 31 + Mathf.RoundToInt(HalfExtent * 100f);
                L(Generators); L(Stakes); L(Notes); L(SurvivorSpawns); L(HunterSpawns);
                V(Lever); V(GatePos); V(AmbulanceAt); V(LoungeSeat); V(LoungeTv); V(Lever2); V(GatePos2); V(Hatch);
                h = h * 31 + Mathf.RoundToInt(AmbulanceAngle * 1000f);
                h = h * 31 + Loot.Count;
                foreach (LootDef l in Loot) { V(l.Pos); h = h * 31 + (int)l.Item; }
                h = h * 31 + Doors.Count;
                foreach (DoorDef d in Doors) { V(d.A); V(d.B); }
                h = h * 31 + Barricades.Count;
                foreach (BarricadeDef b in Barricades) V(b.Pos);
                h = h * 31 + Windows.Count;
                foreach (WindowDef w in Windows) { V(w.A); V(w.B); }
                return h;
            }
        }

        /// <summary>Distance from p to the segment a-b.</summary>
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }

    /// <summary>The level's geometry as the rules see it (design units on the ground plane).</summary>
    public interface ISimGeometry : IMoveEnv
    {
        /// <summary>
        /// The walkable grid for a body of this radius (original units), 25-unit cells. With <paramref name="doorsOpen"/>
        /// every door counts as passable (NPCs who open doors on the way); otherwise doors stand as they are now.
        /// </summary>
        NavGrid Nav(float radiusUnits, bool doorsOpen = true, bool climbs = false);

        /// <summary>True if nothing that blocks sight (walls, closed doors, trees, rocks) lies between a and b.</summary>
        bool LineOfSight(Vector2 a, Vector2 b);
        /// <summary>How far a ray of sight goes from <paramref name="from"/> along <paramref name="dir"/> (up to max).</summary>
        float CastSight(Vector2 from, Vector2 dir, float max);
        /// <summary>How far a body of <paramref name="radius"/> moves from <paramref name="from"/> along <paramref name="dir"/> before it hits something solid.</summary>
        float CastBody(Vector2 from, Vector2 dir, float max, float radius);
        /// <summary>True if a body of <paramref name="radius"/> at p overlaps anything solid.</summary>
        bool Blocked(Vector2 p, float radius);
        /// <summary>
        /// As <see cref="Blocked"/>, for a body that climbs (an NPC on a chase): fallen logs and trees, fences and windows
        /// (their sills, glass and boards) never stop it; it goes over or through them.
        /// </summary>
        bool BlockedClimbing(Vector2 p, float radius);
        bool InExitZone(Vector2 p);
    }

    /// <summary>Open ground everywhere: for tests and tools without a level.</summary>
    public sealed class OpenGeometry : ISimGeometry
    {
        public Rect ExitZone;
        public float Half = 90f;
        /// <summary>Optional walls for tests: a body hits these discs (centre, radius).</summary>
        public readonly List<(Vector2 c, float r)> Pillars = new List<(Vector2, float)>();
        /// <summary>Optional climbable walls for tests (a, b, half width): they stop a body unless it climbs (a fence, a window).</summary>
        public readonly List<(Vector2 a, Vector2 b, float half)> Climbables = new List<(Vector2, Vector2, float)>();
        readonly Dictionary<(int, bool), NavGrid> navs = new Dictionary<(int, bool), NavGrid>();

        public NavGrid Nav(float radiusUnits, bool doorsOpen = true, bool climbs = false)
        {
            var key = (Mathf.RoundToInt(radiusUnits), climbs);
            if (!navs.TryGetValue(key, out NavGrid n))
                navs[key] = n = new NavGrid(Half, Scale.D(25f), p => climbs ? BlockedClimbing(p, Scale.D(radiusUnits)) : Blocked(p, Scale.D(radiusUnits)));
            return n;
        }
        public bool LineOfSight(Vector2 a, Vector2 b) => true;
        /// <summary>Optional sight for tests (from, direction, max: distance); open ground without it.</summary>
        public System.Func<Vector2, Vector2, float, float> Sight;
        /// <summary>Optional fallen logs for tests (a, b, half width): vaulted over.</summary>
        public readonly List<(Vector2 a, Vector2 b, float half)> Lows = new List<(Vector2, Vector2, float)>();
        public float CastSight(Vector2 from, Vector2 dir, float max) => Sight != null ? Sight(from, dir, max) : max;
        public float CastBody(Vector2 from, Vector2 dir, float max, float radius) => max;
        public bool Blocked(Vector2 p, float radius)
        {
            foreach (var (a, b, half) in Climbables) if (SimMap.SegmentDistance(p, a, b) < half + radius) return true;
            return BlockedClimbing(p, radius);
        }

        public bool BlockedClimbing(Vector2 p, float radius)
        {
            foreach (var (c, r) in Pillars) if (Vector2.Distance(p, c) < r + radius) return true;
            return false;
        }
        public bool InExitZone(Vector2 p) => ExitZone.Contains(p);
        public bool InWater(Vector2 p) => false;
        public bool InBrokenWindow(Vector2 p, float radius) => false;
        public bool OverLow(Vector2 p, float radius)
        {
            foreach (var (a, b, half) in Lows) if (SimMap.SegmentDistance(p, a, b) < half + radius) return true;
            return false;
        }
        /// <summary>Optional: how much longer vaulting each of <see cref="Lows"/> takes (by index).</summary>
        public readonly List<float> LowSlow = new List<float>();
        public float Vault(Vector2 p, float radius, bool hunter)
        {
            float v = 0f;
            for (int i = 0; i < Lows.Count; i++)
                if (SimMap.SegmentDistance(p, Lows[i].a, Lows[i].b) < Lows[i].half + radius)
                    v = Mathf.Max(v, MatchSim.VaultFor(i < LowSlow.Count ? LowSlow[i] : 1f));
            foreach (var (a, b, half) in Climbables) if (SimMap.SegmentDistance(p, a, b) < half + radius) v = Mathf.Max(v, 1f);
            return v;
        }
    }
}
