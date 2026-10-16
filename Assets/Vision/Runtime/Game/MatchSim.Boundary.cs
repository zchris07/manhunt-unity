using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// The edge of the woods (the user's rule; the map is a circle with no wall round it). Three invisible rings round the
    /// centre:
    /// past the first (the playable radius) a player's light flickers, and night vision, JARVIS and the Hemp Battery stop
    /// working (a JARVIS running ends), as does Zach's Hemp Beam (a survivor is told not to wander off, with an arrow back in);
    /// past the second (15.6 m further out) the light dies over 3 s until the screen is black, and then a survivor takes 20 damage straight
    /// through the shield every 3 s, the screen's edges pulsing red with each (at 0 they are gone at once, not downed:
    /// "NAME wandered into the woods");
    /// the third is where the world ends: a survivor who reaches it is gone the same way, while Zach walks through it
    /// and comes out on the far side of the map (mirrored through the centre), 20 health poorer ("Zach crossed over"), at
    /// most once every 10 s; while he can't (locked out, or 20 health or less) it stops him like a wall. Zach takes no
    /// damage from the dark.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>The rings' radii (design units) about the map's centre: flicker, shutdown, the end of the world.</summary>
        public float Ring1 => Map.HalfExtent;
        public float Ring2 => Map.HalfExtent + Balance.Boundary.Gap;
        public float Ring3 => Map.HalfExtent + Balance.Boundary.Outer;

        /// <summary>Which zone a point is in: 0 inside, 1 past the first ring, 2 past the second.</summary>
        public int ZoneAt(Vector2 at)
        {
            float r = at.magnitude;
            return r > Ring2 ? 2 : r > Ring1 ? 1 : 0;
        }

        /// <summary>Zach can't cross the outer ring right now (locked out, or too hurt): it stops him like a wall.</summary>
        public static bool CrossingBlocked(SimPlayer h) =>
            h.CrossCd > 0f || Mathf.CeilToInt(h.Hp * Balance.Hunter.Health.Max - 0.01f) <= Balance.Boundary.CrossCost;

        void UpdateBoundary(float dt)
        {
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor && p.Role != Role.Hunter) continue;
                p.CrossCd = Mathf.Max(0f, p.CrossCd - dt);
                bool gone = p.Health == Health.Eliminated || p.Health == Health.Escaped;
                // Carried, staked or hidden, a survivor is where Zach or the hiding spot is; nothing applies.
                bool pinned = p.Health == Health.Carried || p.Health == Health.Staked || p.HideState != 0;
                p.Zone = gone || pinned ? 0 : ZoneAt(p.Pos);
                if (p.Zone == 2) p.ShutdownT = Mathf.Min(Balance.Boundary.ShutdownTime, p.ShutdownT + dt);
                // Back inside the second ring, the light comes back about twice as fast as it went.
                else p.ShutdownT = Mathf.Max(0f, p.ShutdownT - 2f * dt);
                if (p.Zone >= 2) VisionItemsOff(p);
                // Zach's Hemp Beam stops at the first ring.
                if (p.Zone >= 1 && p.Role == Role.Hunter && p.BeamT > 0f) p.BeamT = 0f;
                if (gone || pinned) continue;

                if (p.Role == Role.Survivor)
                {
                    if (p.Pos.magnitude >= Ring3) { Woods(p); continue; }
                    if (p.ShutdownT >= Balance.Boundary.ShutdownTime)
                    {
                        // Fully dark: the first bite now, then every 3 s.
                        p.BoundaryTickT -= dt;
                        if (p.BoundaryTickT <= 0f)
                        {
                            p.BoundaryTickT = Balance.Boundary.TickEvery;
                            p.BoundaryHits++;
                            p.Hp = Mathf.Max(0f, p.Hp - Balance.Boundary.TickDamage);
                            Emit(null, new GameEvent { Kind = EventKind.Hit, A = p.Id, Pos = p.Pos, F = 0f, Text = "woods" });
                            if (p.Hp <= 1e-4f) Woods(p);
                        }
                    }
                    else p.BoundaryTickT = 0f;
                }
                else if (p.Pos.magnitude >= Ring3)
                {
                    if (CrossingBlocked(p)) Place(p, p.Pos.normalized * (Ring3 - Balance.Boundary.WallSkin));
                    else
                    {
                        // Out through the end of the world and in on the far side, mirrored through the centre.
                        Place(p, -p.Pos.normalized * (Ring3 - Balance.Boundary.ArriveInside));
                        p.Hp = Mathf.Max(0f, p.Hp - Balance.Boundary.CrossCost / Balance.Hunter.Health.Max);
                        p.CrossCd = Balance.Boundary.CrossLockout;
                        Feed("Zach crossed over");
                    }
                }
            }
        }

        /// <summary>Night vision, JARVIS and the Hemp Battery work up to the second ring, not past it.</summary>
        void VisionItemsOff(SimPlayer p)
        {
            p.GogglesOn = false;
            if (p.JarvisT > 0f)
            {
                p.JarvisT = 0f;
                RevealT = 0f;
            }
            if (p.Role == Role.Hunter) p.HempOn = false;
        }

        /// <summary>Gone into the dark: eliminated at once, without being downed.</summary>
        void Woods(SimPlayer p)
        {
            p.Hp = 0f;
            p.LostInWoods = true;
            Eliminate(p, "woods");
        }
    }
}
