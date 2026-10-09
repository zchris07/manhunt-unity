using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// Chris Zelley, the paramedic (the original's chris.ts). Until a survivor talks to him he paces round his ambulance
    /// (and never strays from it). Enlisted, he wanders the map; the first time a survivor stays downed or staked too long
    /// he sprints to them, revives or cuts them down, then sprouts wings and flies to the heavens. Zach kills him in two
    /// hits at any point; a survivor's item only makes him flinch.
    /// </summary>
    public sealed class Chris : Walker
    {
        public enum Mode { Pace, Idle, Wander, Rescue, Work, Flee, Ascend, Gone }

        public Mode State = Mode.Pace;
        /// <summary>A survivor talked to him: he's left the ambulance.</summary>
        public bool Active;
        /// <summary>Who he's rescuing (0 nobody).</summary>
        public int Target;
        public float AscendT, WorkT, WorkDur;
        int hp = Balance.Chris.Hp, wp, dir = 1;
        float turnT, pathT;
        readonly Vector2[] beat = new Vector2[4];
        readonly Dictionary<int, float> downFor = new Dictionary<int, float>();
        List<Vector2> path;

        public override string Name => "Chris Zelley";
        public override int Kind => 2;
        public override float RadiusUnits => Balance.Chris.Radius;
        bool Hittable => alive && State != Mode.Ascend && State != Mode.Gone;
        public override bool Slashable => Hittable;
        public override bool Solid => Hittable;
        public override bool Gone => State == Mode.Gone;
        public override NpcFlags Flags => base.Flags | (State == Mode.Flee ? NpcFlags.Fleeing : 0) | (State == Mode.Work ? NpcFlags.Working : 0)
            | (State == Mode.Rescue ? NpcFlags.Chasing : 0) | (State == Mode.Ascend ? NpcFlags.Ascending : 0);

        public Chris(MatchSim sim) : base(sim, 102)
        {
            SimMap m = sim.Map;
            var u = new Vector2(Mathf.Cos(m.AmbulanceAngle), Mathf.Sin(m.AmbulanceAngle));
            var n = new Vector2(-u.y, u.x);
            float hl = m.AmbulanceSize.x * 0.5f + Scale.D(Balance.Chris.Pace), hw = m.AmbulanceSize.y * 0.5f + Scale.D(Balance.Chris.Pace);
            beat[0] = m.AmbulanceAt + u * hl + n * hw;
            beat[1] = m.AmbulanceAt + u * hl - n * hw;
            beat[2] = m.AmbulanceAt - u * hl - n * hw;
            beat[3] = m.AmbulanceAt - u * hl + n * hw;
            wp = Rnd.Next(4);
            Pos = beat[wp];
            dir = Next() < 0.5f ? 1 : -1;
            wp = (wp + dir + 4) % 4;
            Unstick(sim);
        }

        bool CanTalk(SimPlayer p) => alive && !Active && State != Mode.Flee && p.Role == Role.Survivor && OnFeet(p) && Near(p, Balance.Chris.Reach);
        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkChris : Prompt.None;

        /// <summary>A survivor talks to him: he promises to come when needed, then heads off.</summary>
        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            Active = true;
            State = Mode.Idle;
            ModeT = 2.2f;
            Moving = false;
            Face(p.Pos);
            Heading = Facing + Mathf.PI;
            TalkT = 2f;
            Speak("I'll be there when you need me.");
            Sim.Feed($"{p.Name} enlisted Chris Zelley");
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (by.Role == Role.Hunter) { Slash(sim, by, 1); return; }
            if (Hittable) HurtT = 0.35f;
        }

        /// <summary>Zach hits him: he runs (slowly); the second hit kills him. Harm false: only scared (Penjamin).</summary>
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (!Hittable) return;
            if (harm)
            {
                hp--;
                HurtT = 0.35f;
                h.Stats.Hits++;
            }
            if (hp <= 0)
            {
                alive = false;
                Moving = false;
                Target = 0;
                Sim.Feed($"{h.Name} killed Chris Zelley");
                return;
            }
            Target = 0;
            path = null;
            State = Mode.Flee;
            FleeT = Balance.Chris.FleeTime;
            turnT = 0f;
            if (!Active)
            {
                // Round the ambulance, the way that takes him away from Zach.
                Vector2 next = beat[wp], other = beat[(wp - dir + 4) % 4];
                if (Vector2.Distance(other, h.Pos) > Vector2.Distance(next, h.Pos))
                {
                    dir = -dir;
                    wp = (wp + dir + 4) % 4;
                }
            }
            Speak("Whoa, whoa! I'm a medic!");
        }

        public override void Unstick(MatchSim sim)
        {
            if (Hittable) base.Unstick(sim);
        }

        void Track(float dt)
        {
            foreach (SimPlayer p in Sim.Order)
            {
                if (p.Role == Role.Survivor && p.Health == Health.Downed) downFor[p.Id] = (downFor.TryGetValue(p.Id, out float t) ? t : 0f) + dt;
                else downFor.Remove(p.Id);
            }
        }

        bool NeedsHelp(SimPlayer p)
        {
            if (p.Role != Role.Survivor) return false;
            if (p.Health == Health.Downed) return downFor.TryGetValue(p.Id, out float t) && t >= Balance.Chris.DownedAfter;
            if (p.Health == Health.Staked) return Balance.Objectives.StakeStageTime - p.StakeT >= Balance.Chris.StakedAfter;
            return false;
        }

        static bool StillDown(SimPlayer p) => p != null && (p.Health == Health.Downed || p.Health == Health.Staked);

        SimPlayer PickPatient()
        {
            SimPlayer best = null;
            float bd = float.MaxValue;
            foreach (SimPlayer p in Sim.Order)
            {
                if (!NeedsHelp(p)) continue;
                float d = Vector2.Distance(p.Pos, Pos);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public override void Update(MatchSim sim, float dt)
        {
            Tick(dt);
            if (!alive || State == Mode.Gone)
            {
                Moving = false;
                Gait = 0;
                return;
            }
            if (State == Mode.Ascend)
            {
                Moving = false;
                Gait = 0;
                AscendT += dt;
                if (AscendT >= Balance.Chris.AscendTime) State = Mode.Gone;
                return;
            }
            Track(dt);
            if (Active && State != Mode.Flee && State != Mode.Rescue && State != Mode.Work)
            {
                SimPlayer p = PickPatient();
                if (p != null)
                {
                    State = Mode.Rescue;
                    Target = p.Id;
                    path = null;
                    pathT = 0f;
                    Speak("Hang on, I'm coming!");
                    Sim.Feed($"Chris Zelley is running to {p.Name}");
                }
            }
            float speed = 0f;
            switch (State)
            {
                case Mode.Pace:
                case Mode.Idle:
                    speed = UpdateCalm(dt);
                    break;
                case Mode.Wander:
                    speed = UpdateWander(dt);
                    break;
                case Mode.Flee:
                    speed = UpdateFlee(dt);
                    break;
                case Mode.Rescue:
                    speed = UpdateRescue(dt);
                    break;
                case Mode.Work:
                    UpdateWork(dt);
                    break;
            }
            Moving = speed > 0f;
            Gait = !Moving ? 0 : speed > Balance.Chris.Wander * 1.5f ? 2 : 1;
            if (!Moving) return;
            Step(speed, dt);
            if (State != Mode.Work) Facing = Heading;
            if (StuckT > 0f)
            {
                Vector2 ahead = Pos + new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * Scale.D(30f);
                int di = Sim.NearbyDoor(ahead, 40f);
                if (Active && di >= 0 && !Sim.Doors[di] && Sim.DoorCd[di] <= 0f) Sim.SetDoor(di, true);
                else if (StuckT > 0.3f && (State == Mode.Pace || (State == Mode.Flee && !Active)))
                {
                    dir = -dir;
                    wp = (wp + dir + 4) % 4;
                    StuckT = 0f;
                }
                else if (StuckT > 0.25f && (State == Mode.Wander || (State == Mode.Flee && Active)))
                {
                    Heading += Mathf.PI * (0.5f + Next());
                    StuckT = 0f;
                }
            }
        }

        /// <summary>Before he's enlisted he walks his beat round the ambulance, pausing now and then, and stops to face any survivor who walks up.</summary>
        float UpdateCalm(float dt)
        {
            if (!Active)
                foreach (SimPlayer q in Sim.Order)
                    if (q.Role == Role.Survivor && OnFeet(q) && q.HideState == 0 && Vector2.Distance(q.Pos, Pos) < Scale.D(Balance.Chris.Reach + 30f))
                    {
                        Face(q.Pos);
                        return 0f;
                    }
            if (State == Mode.Idle)
            {
                ModeT -= dt;
                if (ModeT > 0f)
                {
                    if (!Active) Facing += Mathf.Sin(Sim.Time * 1.2f + Id) * dt * 0.6f;
                    return 0f;
                }
                if (Active)
                {
                    State = Mode.Wander;
                    ModeT = Range(3f, 7f);
                    return 0f;
                }
                State = Mode.Pace;
            }
            return WalkBeat(Balance.Chris.Walk, true);
        }

        float WalkBeat(float speed, bool mayPause)
        {
            if (Vector2.Distance(beat[wp], Pos) < Scale.D(8f))
            {
                if (Next() < 0.2f) dir = -dir;
                wp = (wp + dir + 4) % 4;
                if (mayPause && Next() < 0.45f)
                {
                    State = Mode.Idle;
                    ModeT = Range(0.8f, 3f);
                    return 0f;
                }
            }
            Vector2 n = beat[wp];
            Heading = Mathf.Atan2(n.y - Pos.y, n.x - Pos.x);
            return speed;
        }

        float UpdateWander(float dt)
        {
            ModeT -= dt;
            Heading += Range(-1f, 1f) * dt * 1.5f;
            if (ModeT <= 0f)
            {
                State = Mode.Idle;
                ModeT = Range(1f, 3f);
            }
            return Balance.Chris.Wander;
        }

        float UpdateFlee(float dt)
        {
            FleeT -= dt;
            if (FleeT <= 0f)
            {
                State = Active ? Mode.Wander : Mode.Pace;
                ModeT = Range(2f, 4f);
                return 0f;
            }
            if (!Active) return WalkBeat(Balance.Chris.Flee, false);
            turnT -= dt;
            if (turnT <= 0f)
            {
                SimPlayer h = NearestHunter();
                float away = h != null ? Mathf.Atan2(Pos.y - h.Pos.y, Pos.x - h.Pos.x) : Heading;
                Heading = away + Range(-0.9f, 0.9f);
                turnT = Range(0.3f, 0.6f);
            }
            return Balance.Chris.Flee;
        }

        float UpdateRescue(float dt)
        {
            SimPlayer p = Sim.Get(Target);
            if (!StillDown(p))
            {
                p = PickPatient();
                if (p == null)
                {
                    Target = 0;
                    State = Mode.Wander;
                    ModeT = Range(2f, 4f);
                    return 0f;
                }
                Target = p.Id;
                path = null;
            }
            float d = Vector2.Distance(p.Pos, Pos);
            if (d <= Scale.D(Balance.Reach.Teammate - 8f))
            {
                State = Mode.Work;
                WorkT = 0f;
                WorkDur = p.Health == Health.Downed ? Balance.Survivor.ReviveTime : Balance.Survivor.UnstakeTime;
                Face(p.Pos);
                return 0f;
            }
            Heading = Steer(p.Pos, dt);
            return Balance.Chris.Run;
        }

        /// <summary>Reviving or cutting down takes him as long as it would take a survivor.</summary>
        void UpdateWork(float dt)
        {
            SimPlayer p = Sim.Get(Target);
            Health want = WorkDur == Balance.Survivor.ReviveTime ? Health.Downed : Health.Staked;
            if (p == null || p.Health != want || Vector2.Distance(p.Pos, Pos) > Scale.D(Balance.Reach.Teammate + 15f))
            {
                State = Mode.Rescue;
                WorkT = 0f;
                return;
            }
            Face(p.Pos);
            WorkT += dt;
            if (WorkT < WorkDur) return;
            if (p.Health == Health.Downed)
            {
                MatchSim.RestoreSurvivor(p, Balance.Survivor.ReviveHp);
                Sim.Feed($"Chris Zelley got {p.Name} back on their feet");
            }
            else
            {
                Sim.ReleaseFromStake(p);
                Sim.Feed($"Chris Zelley cut {p.Name} down");
            }
            Target = 0;
            State = Mode.Ascend;
            AscendT = 0f;
            Moving = false;
            Speak("My work here is done.", Balance.Chris.AscendTime);
            Sim.Feed("Chris Zelley ascended");
        }

        /// <summary>Along a walkable path (he opens doors on the way); straight at the patient only for the last few steps.</summary>
        float Steer(Vector2 t, float dt)
        {
            if (Vector2.Distance(t, Pos) < Scale.D(90f) && Sim.Geo.LineOfSight(Pos, t) && StuckT < 0.3f)
            {
                path = null;
                return Mathf.Atan2(t.y - Pos.y, t.x - Pos.x);
            }
            pathT -= dt;
            if (pathT <= 0f || path == null || path.Count < 1)
            {
                pathT = 1f;
                path = Sim.Geo.Nav(Balance.Chris.Radius, true).FindPath(Pos, t, 40000) ?? new List<Vector2> { t };
            }
            while (path.Count > 1 && Vector2.Distance(path[0], Pos) < Scale.D(20f)) path.RemoveAt(0);
            return Mathf.Atan2(path[0].y - Pos.y, path[0].x - Pos.x);
        }

        SimPlayer NearestHunter()
        {
            SimPlayer best = null;
            float bd = float.MaxValue;
            foreach (SimPlayer h in Sim.Order)
            {
                if (h.Role != Role.Hunter || h.Health == Health.Eliminated) continue;
                float d = Vector2.Distance(h.Pos, Pos);
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }
    }
}
