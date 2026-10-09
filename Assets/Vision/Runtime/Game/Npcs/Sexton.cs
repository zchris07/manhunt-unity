using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// Sexton Science (the original's sexton.ts): wanders with his reel playing around him. A survivor who talks to him,
    /// and presses E again to keep listening, gets JARVIS (a glowing tablet), and he walks off mysteriously. Zach slays him
    /// in three hits (he runs after each) and he drops his Hemp Beam. Survivors can't hurt him: hit him and he defends
    /// himself with Hemp Beams against every survivor nearby until they've all left him alone for ten seconds.
    /// </summary>
    public sealed class Sexton : Walker
    {
        public enum Mode { Idle, Walk, Flee, Talk, Leave, Defend }
        public enum TalkStage { First, Await, Second, Hand }
        public enum Phase { Retreat, Approach, Beam, Flee }

        public Mode State = Mode.Idle;
        public TalkStage Stage = TalkStage.First;
        public Phase DefensePhase = Phase.Retreat;
        public int TalkTo;
        /// <summary>His Hemp Beam: where it points, how far it reaches (design units), how long it has fired.</summary>
        public float BeamAng, BeamLen, BeamAge;
        public int Attacks;
        int hp = Balance.Sexton.Hp, target;
        float talkT, handT, phaseT, awayT, turnT;
        Vector2 leaveFrom;
        readonly HashSet<int> beamHit = new HashSet<int>();
        readonly HashSet<int> given = new HashSet<int>();

        public override string Name => "Sexton Science";
        public override int Kind => 0;
        public override float RadiusUnits => Balance.Sexton.Radius;
        public override bool Slashable => alive;
        /// <summary>Slain, he lies where he fell.</summary>
        public override bool Gone => false;
        public bool Defending => State == Mode.Defend;
        public bool Beaming => alive && State == Mode.Defend && DefensePhase == Phase.Beam && StunT <= 0f;
        /// <summary>How far his beam has charged while he steps in to fire (0-1; 0 when he isn't).</summary>
        public float ChargeK => alive && State == Mode.Defend && DefensePhase == Phase.Approach ? Mathf.Clamp01(1f - phaseT / Balance.Sexton.Defense.ApproachTime) : 0f;
        public override bool Aggressive => alive && State == Mode.Defend;
        public override NpcFlags Flags => base.Flags | (State == Mode.Flee || (State == Mode.Defend && DefensePhase == Phase.Flee) ? NpcFlags.Fleeing : 0)
            | (State == Mode.Defend ? NpcFlags.Defending : 0) | (State == Mode.Talk ? NpcFlags.Talking : 0);

        public Sexton(MatchSim sim) : base(sim, 101) => SpawnAnywhere(4f, 500f);

        bool CanTalk(SimPlayer p) =>
            alive && (State == Mode.Idle || State == Mode.Walk) && p.Role == Role.Survivor && OnFeet(p) && p.HideState == 0 && !given.Contains(p.Id) && Near(p, Balance.Sexton.Reach);

        bool IsAwaiting(SimPlayer p) =>
            alive && State == Mode.Talk && Stage == TalkStage.Await && TalkTo == p.Id && Vector2.Distance(p.Pos, Pos) < Scale.D(Balance.Sexton.Reach + 30f);

        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkSexton : Prompt.None;
        public override Prompt Awaiting(MatchSim sim, SimPlayer p) => IsAwaiting(p) ? Prompt.SextonMore : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (IsAwaiting(p))
            {
                // E again: the second line, then the tablet.
                Stage = TalkStage.Second;
                talkT = Balance.Sexton.SecondTalkTime;
                Face(p.Pos);
                Sim.StartAction(p, ActionKind.Talk, Balance.Sexton.SecondTalkTime + Balance.Sexton.HandTime, 0);
                Speak(Balance.Sexton.SecondLine.Replace("NAME", p.Name), Balance.Sexton.SecondTalkTime + 1f);
                return;
            }
            if (!CanTalk(p)) return;
            State = Mode.Talk;
            Stage = TalkStage.First;
            TalkTo = p.Id;
            talkT = Balance.Sexton.TalkTime;
            Moving = false;
            Face(p.Pos);
            Sim.StartAction(p, ActionKind.Talk, Balance.Sexton.TalkTime, 0);
            Speak("I'm working on something big", Balance.Sexton.TalkTime + 1f);
        }

        public override void CancelTalk(MatchSim sim, SimPlayer p)
        {
            if (TalkTo != p.Id || State != Mode.Talk) return;
            // Walking off while he waits for you is fine; he keeps waiting a moment.
            if (Stage == TalkStage.Await) return;
            EndTalk();
        }

        void EndTalk()
        {
            TalkTo = 0;
            if (State == Mode.Talk) SetMode(Mode.Walk, 2f);
        }

        void DropTalk()
        {
            if (TalkTo == 0) return;
            SimPlayer p = Sim.Get(TalkTo);
            TalkTo = 0;
            if (p != null && p.Action == ActionKind.Talk)
            {
                p.Action = ActionKind.None;
                p.ActionT = p.ActionDur = 0f;
            }
        }

        void SetMode(Mode m, float t)
        {
            State = m;
            ModeT = t;
        }

        /// <summary>Zach's machete (harm false: only scared, by Penjamin): he flees; three hits slay him.</summary>
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (!alive) return;
            if (harm)
            {
                hp--;
                HurtT = 0.35f;
                h.Stats.Hits++;
            }
            DropTalk();
            if (hp <= 0)
            {
                alive = false;
                State = Mode.Idle;
                Moving = false;
                Sim.HempDrop = Pos + new Vector2(Mathf.Cos(Facing), Mathf.Sin(Facing)) * Scale.D(22f);
                Sim.Feed($"{h.Name} slayed Sexton Science");
                return;
            }
            SetMode(Mode.Flee, 0f);
            FleeT = Balance.Sexton.FleeTime;
            turnT = 0f;
            Speak(hp == 2 ? "AGH! Not the face!" : "Somebody help!");
        }

        /// <summary>
        /// A bottle or pellets. From Zach (his golden pump) it's like a machete hit. From a survivor it can't hurt him: he
        /// turns to self-defense, or, already defending, is stunned.
        /// </summary>
        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (!alive) return;
            if (by.Role == Role.Hunter)
            {
                Slash(sim, by, 1);
                return;
            }
            HurtT = 0.35f;
            if (State == Mode.Defend)
            {
                StunT = Mathf.Max(StunT, kind == "bottle" ? Balance.Sexton.Defense.BottleStun : Balance.Sexton.Defense.ShotStun);
                return;
            }
            DropTalk();
            State = Mode.Defend;
            DefensePhase = Phase.Retreat;
            phaseT = 1f;
            Attacks = 0;
            awayT = 0f;
            target = by.Id;
            Speak("Oh, you want to play? HEMP BEAM!");
            Sim.Feed("Sexton Science is defending himself");
        }

        public override void Update(MatchSim sim, float dt)
        {
            TickTimers(dt);
            TalkT = Mathf.Max(0f, TalkT - dt);
            if (!alive) return;
            if (StunT > 0f)
            {
                Moving = false;
                Gait = 0;
                return;
            }
            if (State == Mode.Talk)
            {
                UpdateTalk(dt);
                Moving = false;
                Gait = 0;
                return;
            }
            float speed = 0f;
            if (State == Mode.Defend) speed = UpdateDefense(dt);
            else if (State == Mode.Flee)
            {
                FleeT -= dt;
                speed = Erratic(dt, NearestHunter());
                if (FleeT <= 0f) SetMode(Mode.Walk, Range(2f, 5f));
            }
            else if (State == Mode.Leave)
            {
                // Mysteriously away, never looking back.
                ModeT -= dt;
                Heading = Mathf.Atan2(Pos.y - leaveFrom.y, Pos.x - leaveFrom.x) + Mathf.Sin(Sim.Time * 0.7f) * 0.25f;
                speed = Balance.Sexton.Walk;
                if (ModeT <= 0f) SetMode(Mode.Walk, Range(2f, 4f));
            }
            else
            {
                ModeT -= dt;
                if (State == Mode.Idle)
                {
                    Facing += Mathf.Sin(Sim.Time * 1.3f + Id) * dt * 0.8f;
                    if (ModeT <= 0f)
                    {
                        Heading = Facing + Range(-1.5f, 1.5f);
                        SetMode(Mode.Walk, Range(3f, 8f));
                    }
                }
                else
                {
                    Heading += Range(-1f, 1f) * dt * 1.6f;
                    speed = Balance.Sexton.Walk;
                    if (ModeT <= 0f) SetMode(Mode.Idle, Range(1f, 3.5f));
                }
            }
            if (GasT > 0f) speed *= Balance.Sexton.Defense.GasSlowMul;
            Moving = speed > 0f;
            Gait = !Moving ? 0 : speed > Balance.Sexton.Walk * 1.5f ? 2 : 1;
            if (!Moving) return;
            Step(speed, dt);
            if (!(State == Mode.Defend && DefensePhase == Phase.Approach)) Facing = Heading;
            if (StuckT > 0f)
            {
                // Open a closed door in the way, otherwise turn away from the obstacle.
                Vector2 ahead = Pos + new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * Scale.D(30f);
                int di = Sim.NearbyDoor(ahead, 40f);
                if (di >= 0 && !Sim.Doors[di] && Sim.DoorCd[di] <= 0f) Sim.SetDoor(di, true);
                else if (StuckT > 0.25f)
                {
                    Heading += Mathf.PI * (0.5f + Next());
                    StuckT = 0f;
                }
            }
        }

        /// <summary>A new direction every few tenths of a second, mostly away from <paramref name="from"/>.</summary>
        float Erratic(float dt, SimPlayer from)
        {
            turnT -= dt;
            if (turnT <= 0f)
            {
                float away = from != null ? Mathf.Atan2(Pos.y - from.Pos.y, Pos.x - from.Pos.x) : Heading;
                Heading = away + Range(-1.3f, 1.3f);
                turnT = Range(0.2f, 0.45f);
            }
            return Balance.Sexton.Flee;
        }

        /// <summary>Survivors he defends himself from: anyone up (or crawling) near him.</summary>
        List<SimPlayer> Threats()
        {
            var near = new List<SimPlayer>();
            foreach (SimPlayer p in Sim.Order)
                if (p.Role == Role.Survivor && (p.Health == Health.Healthy || p.Health == Health.Wounded || p.Health == Health.Downed) && p.HideState != 2
                    && Vector2.Distance(p.Pos, Pos) < Scale.D(Balance.Sexton.Defense.Vicinity))
                    near.Add(p);
            return near;
        }

        /// <summary>
        /// Self-defense: walk away from the survivors, turn and step toward one to fire a Hemp Beam (3 s), walk away again
        /// for the cooldown (3 s); after three beams just flee. Once nobody has been near him for ten seconds he calms down.
        /// </summary>
        float UpdateDefense(float dt)
        {
            List<SimPlayer> near = Threats();
            awayT = near.Count > 0 ? 0f : awayT + dt;
            if (awayT >= Balance.Sexton.Defense.ResetAfter)
            {
                SetMode(Mode.Walk, 2f);
                Sim.Feed("Sexton Science calmed down");
                return 0f;
            }
            SimPlayer closest = null;
            foreach (SimPlayer p in near)
                if (closest == null || Vector2.Distance(p.Pos, Pos) < Vector2.Distance(closest.Pos, Pos)) closest = p;
            phaseT -= dt;
            switch (DefensePhase)
            {
                case Phase.Retreat:
                    if (closest != null) Heading = Mathf.Atan2(Pos.y - closest.Pos.y, Pos.x - closest.Pos.x) + Mathf.Sin(Sim.Time * 1.7f) * 0.4f;
                    if (phaseT <= 0f)
                    {
                        if (Attacks >= Balance.Sexton.Defense.Attacks)
                        {
                            DefensePhase = Phase.Flee;
                            Speak("I did not sign up for this!");
                        }
                        else
                        {
                            SimPlayer t = PickTarget(near);
                            if (t != null)
                            {
                                target = t.Id;
                                DefensePhase = Phase.Approach;
                                phaseT = Balance.Sexton.Defense.ApproachTime;
                            }
                        }
                    }
                    return closest != null ? Balance.Sexton.Defense.Walk : 0f;
                case Phase.Approach:
                {
                    SimPlayer t = Sim.Get(target);
                    if (t == null || !near.Contains(t))
                    {
                        DefensePhase = Phase.Retreat;
                        phaseT = 0.5f;
                        return 0f;
                    }
                    float a = Mathf.Atan2(t.Pos.y - Pos.y, t.Pos.x - Pos.x);
                    Heading = a;
                    Facing = a;
                    if (phaseT <= 0f)
                    {
                        DefensePhase = Phase.Beam;
                        phaseT = Balance.Sexton.Defense.BeamTime;
                        BeamAng = a;
                        BeamAge = 0f;
                        BeamLen = 0f;
                        beamHit.Clear();
                        Speak("HEMP BEAM!");
                        return 0f;
                    }
                    return Balance.Sexton.Defense.Walk;
                }
                case Phase.Beam:
                {
                    BeamAge += dt;
                    SimPlayer t = Sim.Get(target);
                    if (t != null)
                    {
                        // The beam swings toward its target, slowly enough to dodge.
                        float da = Mathf.DeltaAngle(BeamAng * Mathf.Rad2Deg, Mathf.Atan2(t.Pos.y - Pos.y, t.Pos.x - Pos.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        BeamAng += Mathf.Clamp(da, -Balance.Sexton.Defense.TurnRate * dt, Balance.Sexton.Defense.TurnRate * dt);
                    }
                    Facing = BeamAng;
                    FireBeam();
                    if (phaseT <= 0f)
                    {
                        Attacks++;
                        DefensePhase = Phase.Retreat;
                        phaseT = Balance.Sexton.Defense.Cooldown;
                        BeamLen = 0f;
                    }
                    return 0f;
                }
                default:
                    return Erratic(dt, closest);
            }
        }

        SimPlayer PickTarget(List<SimPlayer> near)
        {
            var seen = near.FindAll(p => Sim.Geo.LineOfSight(Pos, p.Pos));
            List<SimPlayer> pool = seen.Count > 0 ? seen : near;
            SimPlayer first = pool.Find(p => p.Id == target);
            return first ?? (pool.Count > 0 ? pool[0] : null);
        }

        /// <summary>The beam runs until it meets something that blocks sight, or a survivor (one hit each per beam).</summary>
        void FireBeam()
        {
            var dir = new Vector2(Mathf.Cos(BeamAng), Mathf.Sin(BeamAng));
            Vector2 s = Pos + dir * (HitRadius + Scale.D(2f));
            float max = Scale.D(Balance.Sexton.Defense.BeamRange);
            float len = Mathf.Min(max, Sim.Geo.CastSight(s, dir, max));
            for (int i = 0; i < Sim.Map.Windows.Count; i++)
            {
                if (Sim.WindowsBroken[i]) continue;
                len = Mathf.Min(len, MatchSim.RaySegment(s, dir, Sim.Map.Windows[i].A, Sim.Map.Windows[i].B));
            }
            SimPlayer victim = null;
            float vt = len;
            float halfW = Scale.D(Balance.Sexton.Defense.BeamWidth) * 0.5f;
            foreach (SimPlayer p in Sim.Order)
            {
                if (p.Role != Role.Survivor || !OnFeet(p) || p.HideState == 2) continue;
                float along = Vector2.Dot(p.Pos - s, dir);
                if (along < 0f || along > vt) continue;
                if (SimMap.SegmentDistance(p.Pos, s, s + dir * len) > p.RadiusD + halfW) continue;
                vt = Mathf.Max(0f, along - p.RadiusD * 0.6f);
                victim = p;
            }
            BeamLen = vt + HitRadius + Scale.D(2f);
            if (victim != null && beamHit.Add(victim.Id))
                // One hit per beam: three beams take a survivor down.
                Sim.HurtSurvivor(victim, Balance.Sexton.Defense.BeamDamage, null, "beam");
        }

        void UpdateTalk(float dt)
        {
            SimPlayer p = Sim.Get(TalkTo);
            if (p == null || !OnFeet(p))
            {
                EndTalk();
                return;
            }
            Face(p.Pos);
            switch (Stage)
            {
                case TalkStage.First:
                    if (p.Action != ActionKind.Talk) { EndTalk(); return; }
                    p.ActionT += dt;
                    talkT -= dt;
                    if (talkT <= 0f)
                    {
                        // He waits for them to press E again to keep listening.
                        Stage = TalkStage.Await;
                        talkT = 8f;
                        p.Action = ActionKind.None;
                        p.ActionT = p.ActionDur = 0f;
                        Sim.Tell(p, "Press E");
                    }
                    return;
                case TalkStage.Await:
                    talkT -= dt;
                    if (talkT <= 0f || Vector2.Distance(p.Pos, Pos) > Scale.D(Balance.Sexton.Reach + 60f)) EndTalk();
                    return;
                case TalkStage.Second:
                    if (p.Action != ActionKind.Talk) { EndTalk(); return; }
                    p.ActionT += dt;
                    talkT -= dt;
                    if (talkT <= 0f)
                    {
                        Stage = TalkStage.Hand;
                        handT = Balance.Sexton.HandTime;
                        // The tablet passes from his hands to theirs.
                        Sim.Emit(Sim.Near(Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Talk, A = -Id, B = p.Id, Text = "tablet", Pos = Pos });
                    }
                    return;
                default:
                    p.ActionT += dt;
                    handT -= dt;
                    if (handT > 0f) return;
                    given.Add(p.Id);
                    if (p.Jarvis == 0) p.Jarvis = 1;
                    p.Action = ActionKind.None;
                    p.ActionT = p.ActionDur = 0f;
                    TalkTo = 0;
                    // Then he walks away, mysteriously.
                    leaveFrom = p.Pos;
                    SetMode(Mode.Leave, Balance.Sexton.LeaveTime);
                    Sim.Tell(p, "Got JARVIS");
                    return;
            }
        }

        SimPlayer NearestHunter()
        {
            SimPlayer best = null;
            float bd = float.MaxValue;
            foreach (SimPlayer h in Sim.Order)
            {
                if (h.Role != Role.Hunter) continue;
                float d = Vector2.Distance(h.Pos, Pos);
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }
    }
}
