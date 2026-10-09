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
        public readonly List<Vector2> Generators = new List<Vector2>();
        public Vector2 Lever, GatePos;
        public bool HasGate;
        /// <summary>The fenced yard beyond the gate: walking into it escapes.</summary>
        public Rect ExitZone;
        public readonly List<LootDef> Loot = new List<LootDef>();
        public readonly List<HideDef> HidingSpots = new List<HideDef>();
        public readonly List<DoorDef> Doors = new List<DoorDef>();
        public readonly List<BarricadeDef> Barricades = new List<BarricadeDef>();
        public readonly List<WindowDef> Windows = new List<WindowDef>();
        public readonly List<Vector2> Stakes = new List<Vector2>();
        public readonly List<Vector2> Notes = new List<Vector2>();
        public readonly List<Vector2> SurvivorSpawns = new List<Vector2>();
        public readonly List<Vector2> HunterSpawns = new List<Vector2>();

        public struct LootDef { public Vector2 Pos; public ItemType Item; public bool Golden; }

        public struct HideDef
        {
            public Vector2 Pos, Exit;
            public World.HidingSpot.Kind Kind;
            /// <summary>Tall grass: the patch radius (anywhere inside it hides you).</summary>
            public float Reach;
        }

        /// <summary>A door's closed panel runs A to B.</summary>
        public struct DoorDef { public Vector2 A, B; public bool StartsOpen; public bool Shutter; }

        public struct BarricadeDef { public Vector2 Pos, A, B; }

        public struct WindowDef { public Vector2 A, B; }

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
        /// <summary>True if nothing that blocks sight (walls, closed doors, trees, rocks) lies between a and b.</summary>
        bool LineOfSight(Vector2 a, Vector2 b);
        /// <summary>How far a ray of sight goes from <paramref name="from"/> along <paramref name="dir"/> (up to max).</summary>
        float CastSight(Vector2 from, Vector2 dir, float max);
        /// <summary>How far a body of <paramref name="radius"/> moves from <paramref name="from"/> along <paramref name="dir"/> before it hits something solid.</summary>
        float CastBody(Vector2 from, Vector2 dir, float max, float radius);
        /// <summary>True if a body of <paramref name="radius"/> at p overlaps anything solid.</summary>
        bool Blocked(Vector2 p, float radius);
        bool InExitZone(Vector2 p);
    }

    /// <summary>Open ground everywhere: for tests and tools without a level.</summary>
    public sealed class OpenGeometry : ISimGeometry
    {
        public Rect ExitZone;
        public bool LineOfSight(Vector2 a, Vector2 b) => true;
        public float CastSight(Vector2 from, Vector2 dir, float max) => max;
        public float CastBody(Vector2 from, Vector2 dir, float max, float radius) => max;
        public bool Blocked(Vector2 p, float radius) => false;
        public bool InExitZone(Vector2 p) => ExitZone.Contains(p);
        public bool InWater(Vector2 p) => false;
        public bool InBrokenWindow(Vector2 p, float radius) => false;
    }
}
