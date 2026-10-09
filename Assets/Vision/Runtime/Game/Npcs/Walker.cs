using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// What the walking NPCs share (the original's Folk, and the wandering in Shane, Marc and Waz): a spawn on open
    /// ground, their own random stream, walking with wall sliding (and getting unstuck by turning away), idling and
    /// strolling about, running away from someone, and walking up to a point. Speeds and distances are in the original's
    /// units; positions in design units.
    /// </summary>
    public abstract class Walker : Npc
    {
        protected readonly MatchSim Sim;
        protected readonly System.Random Rnd;
        protected float Heading, ModeT = 1f, StuckT, FleeT, TalkT;
        protected bool Idle = true;
        protected Vector2 FleeFrom;
        protected bool alive = true;

        public override bool Alive => alive;
        public override bool Solid => alive;
        public override bool Gone => !alive;

        protected Walker(MatchSim sim, int salt)
        {
            Sim = sim;
            Id = sim.AllocEntityId();
            // Its own random stream, so adding an NPC doesn't change anything else.
            Rnd = new System.Random(unchecked(sim.Rng.Next() ^ (salt * 7919)));
        }

        protected float Range(float a, float b) => a + (float)Rnd.NextDouble() * (b - a);
        protected float Next() => (float)Rnd.NextDouble();

        /// <summary>Anywhere open on the map, at least <paramref name="awayUnits"/> from the spawns.</summary>
        protected void SpawnAnywhere(float clearUnits, float awayUnits)
        {
            MatchSim s = Sim;
            float half = s.Map.HalfExtent - Scale.D(300f);
            Vector2 a = s.Map.SurvivorSpawns.Count > 0 ? s.Map.SurvivorSpawns[0] : Vector2.zero;
            Vector2 b = s.Map.HunterSpawns.Count > 0 ? s.Map.HunterSpawns[0] : Vector2.zero;
            for (int i = 0; i < 500; i++)
            {
                var p = new Vector2(Range(-half, half), Range(-half, half));
                if (s.Geo.Blocked(p, HitRadius + Scale.D(clearUnits)) || s.Geo.InWater(p)) continue;
                if (Vector2.Distance(p, a) < Scale.D(awayUnits) || Vector2.Distance(p, b) < Scale.D(awayUnits)) continue;
                Pos = p;
                break;
            }
            Heading = Range(-Mathf.PI, Mathf.PI);
            Facing = Heading;
        }

        /// <summary>Somewhere open inside a rectangle (Marc in the warehouse).</summary>
        protected void SpawnIn(Rect r, float clearUnits)
        {
            float m = Scale.D(60f);
            for (int i = 0; i < 400; i++)
            {
                var p = new Vector2(Range(r.xMin + m, r.xMax - m), Range(r.yMin + m, r.yMax - m));
                if (Sim.Geo.Blocked(p, HitRadius + Scale.D(clearUnits))) continue;
                Pos = p;
                Heading = Range(-Mathf.PI, Mathf.PI);
                Facing = Heading;
                return;
            }
            Pos = r.center;
            Unstick(Sim);
        }

        protected bool Near(SimPlayer p, float reachUnits) => Vector2.Distance(p.Pos, Pos) < Scale.D(reachUnits);

        protected void Face(Vector2 at) => Facing = Mathf.Atan2(at.y - Pos.y, at.x - Pos.x);

        protected static bool OnFeet(SimPlayer p) => p.Health == Health.Healthy || p.Health == Health.Wounded;

        /// <summary>Who can talk to an NPC: a survivor on their feet, or Zach able to act; never a spectator.</summary>
        protected static bool CanChat(SimPlayer p)
        {
            if (p.Role == Role.Spectator) return false;
            if (p.Role == Role.Survivor) return OnFeet(p) && p.HideState == 0;
            return p.CanAct;
        }

        /// <summary>One step along the heading, sliding on walls; it notes when it is stuck.</summary>
        protected void Step(float speedUnits, float dt)
        {
            float speed = Scale.Speed(speedUnits);
            if (VapeSlowT > 0f) speed *= 1f - VapeSlow;
            if (Sim.Geo.InWater(Pos)) speed *= Balance.WadeMul;
            Vector2 before = Pos;
            Pos = Sim.MoveCircle(Pos, HitRadius, new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * speed * dt);
            Moving = true;
            StuckT = Vector2.Distance(Pos, before) < speed * dt * 0.35f ? StuckT + dt : 0f;
        }

        /// <summary>A step toward a point; true once within <paramref name="stopUnits"/>.</summary>
        protected bool WalkTo(Vector2 target, float speedUnits, float stopUnits, float dt)
        {
            if (Vector2.Distance(target, Pos) <= Scale.D(stopUnits))
            {
                Moving = false;
                return true;
            }
            // Round whatever is in the way: sidestep while stuck.
            Heading = Mathf.Atan2(target.y - Pos.y, target.x - Pos.x) + (StuckT > 0.3f ? Mathf.Sin(Sim.Time * 3f) * 1.2f : 0f);
            Step(speedUnits, dt);
            Face(target);
            return false;
        }

        /// <summary>Idle a while, then stroll about.</summary>
        protected void Wander(float speedUnits, float dt, float turn = 1.6f)
        {
            ModeT -= dt;
            if (Idle)
            {
                Moving = false;
                if (TalkT <= 0f) Facing += Mathf.Sin(Sim.Time * 1.2f + Id) * dt * 0.7f;
                if (ModeT <= 0f)
                {
                    Idle = false;
                    ModeT = Range(3f, 8f);
                    Heading = Facing + Range(-1.5f, 1.5f);
                }
                return;
            }
            Heading += Range(-1f, 1f) * dt * turn;
            Step(speedUnits, dt);
            Facing = Heading;
            if (StuckT > 0.25f)
            {
                Heading += Mathf.PI * (0.5f + Next());
                StuckT = 0f;
            }
            if (ModeT <= 0f)
            {
                Idle = true;
                ModeT = Range(1f, 3.5f);
            }
        }

        protected void StartFlee(Vector2 from, float seconds)
        {
            FleeFrom = from;
            FleeT = seconds;
        }

        /// <summary>Running away: erratic, mostly away from <see cref="FleeFrom"/>.</summary>
        protected void RunAway(float speedUnits, float dt)
        {
            FleeT -= dt;
            Heading = Mathf.Atan2(Pos.y - FleeFrom.y, Pos.x - FleeFrom.x) + Mathf.Sin(Sim.Time * 4f + Id) * 0.8f;
            Step(speedUnits, dt);
            Facing = Heading;
            if (FleeT <= 0f)
            {
                Idle = false;
                ModeT = Range(2f, 4f);
            }
        }

        /// <summary>Pushed into something (a door closed on it): move to the nearest open spot.</summary>
        public override void Unstick(MatchSim sim)
        {
            if (!alive || !sim.Geo.Blocked(Pos, HitRadius)) return;
            for (int ring = 1; ring <= 12; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    Vector2 p = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Scale.D(ring * 8f);
                    if (!sim.Geo.Blocked(p, HitRadius)) { Pos = p; return; }
                }
        }

        protected void Tick(float dt)
        {
            TickTimers(dt);
            TalkT = Mathf.Max(0f, TalkT - dt);
        }

        public override NpcFlags Flags => base.Flags | (FleeT > 0f ? NpcFlags.Fleeing : 0) | (TalkT > 0f ? NpcFlags.Talking : 0);
    }
}
