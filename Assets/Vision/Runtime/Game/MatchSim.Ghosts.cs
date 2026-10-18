using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// A body lying where someone died (the user's rules): a sacrificed survivor's (at the stake's foot, or at the edge of
    /// the trees), or a slain NPC's (one that blew apart leaves none). A ghost can take it over.
    /// </summary>
    public sealed class Body
    {
        public int Id;
        public Vector2 Pos;
        public float Facing;
        /// <summary>Whose it was: a survivor's player id, or (<see cref="OfNpc"/>) an NPC's entity id.</summary>
        public int Of;
        public bool OfNpc;
        /// <summary>Their name, and the look a ghost wears in it ("Survivor", or the NPC's name, for its model).</summary>
        public string Name, Look;
        /// <summary>The ghost in it (0: lying there), and when it last fell or was taken (its fall or its rise plays from then).</summary>
        public int Ghost;
        public float Since;
    }

    /// <summary>
    /// Ghosts (the user's rules): a survivor sacrificed (on a stake, or gone into the woods) comes back a little later as a
    /// glowing white ghost of themselves in Miga Sushi or the Diddy Chamber, with a sprint that never runs out, passing
    /// through walls and anything solid. Survivors never see them. They can't be hurt and count as sacrificed for the
    /// night's result; nothing they do touches an NPC. What they can do is find a body (a sacrificed survivor's, a slain
    /// NPC's) and take it over (E): it levitates upright and the ghost merges with it, and they walk about in it looking
    /// as its owner did, moving and sprinting as a survivor does (none of an NPC's abilities), seen by everyone. In a body
    /// they can't pick anything up, talk to an NPC, use an item or a weapon or hurt anyone; the Soundcloud Burst and
    /// Penjamin's gas still reach them, hurting nothing; and all they can do is lend a living survivor a hand with a
    /// generator or a gate they've started, at two thirds of the speed-up another living survivor would add (never a
    /// skill check). Any blow of Zach's (a machete hit at any charge, a lunge, one tick of the Hemp Beam, a single golden
    /// pump pellet) drops the body and throws the ghost out of it, as a 0.50 cal round throws a body back; never to take
    /// another, they see everything from then on (the whole map lit and revealed, through walls, the view zoomed with the
    /// wheel), sprinting forever again.
    /// </summary>
    public sealed partial class MatchSim
    {
        public readonly List<Body> Bodies = new List<Body>();
        /// <summary>The NPCs whose bodies have been laid (each only once).</summary>
        readonly HashSet<int> npcBodies = new HashSet<int>();

        public Body BodyById(int id)
        {
            if (id == 0) return null;
            foreach (Body b in Bodies) if (b.Id == id) return b;
            return null;
        }

        /// <summary>The body this player is in, or taking.</summary>
        public Body BodyOf(SimPlayer p) => p != null && p.Ghost ? BodyById(p.Body) : null;

        /// <summary>
        /// Survivors (on their feet or down, and those back in a body) never see a ghost out of a body; Zach, the other ghosts
        /// and anyone only watching do.
        /// </summary>
        public static bool SeesGhosts(SimPlayer viewer) =>
            viewer == null || viewer.Role != Role.Survivor || viewer.FreeGhost || viewer.Health == Health.Escaped || (viewer.Health == Health.Eliminated && !viewer.Ghost);

        Body LayBody(Vector2 at, float facing, int of, bool npc, string name, string look)
        {
            var b = new Body { Id = AllocEntityId(), Pos = at, Facing = facing, Of = of, OfNpc = npc, Name = name, Look = look, Since = Time };
            Bodies.Add(b);
            return b;
        }

        /// <summary>
        /// A sacrificed survivor: their body left where they died (at the stake's foot; out of the woods, at the edge of the
        /// trees), and back after a moment as a ghost in Miga Sushi or the Diddy Chamber.
        /// </summary>
        void ScheduleGhost(SimPlayer p, string why)
        {
            if (p.Role != Role.Survivor || (why != "stake" && why != "woods")) return;
            p.GhostAt = Time + (why == "woods" ? Balance.Boundary.SacrificedHold + 1f : Balance.Ghost.RespawnDelay);
            p.GhostSpawn = Map.GhostSpawns.Count > 0 ? Map.GhostSpawns[Rng.Next(Map.GhostSpawns.Count)]
                : Map.SurvivorSpawns.Count > 0 ? Map.SurvivorSpawns[0] : Vector2.zero;
            Vector2 at = p.Pos;
            float edge = Ring1 - R(60f);
            if (at.magnitude > edge) at = at.normalized * edge;
            LayBody(at, p.Facing, p.Id, false, p.Name, "Survivor");
        }

        void UpdateGhosts(float dt)
        {
            // A slain NPC's body (those blown apart leave none).
            foreach (Npc n in Npcs)
                if (!n.Alive && !n.Gone && npcBodies.Add(n.Id)) LayBody(n.Pos, n.Facing, n.Id, true, n.Name, n.Name);
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor || p.Health != Health.Eliminated) continue;
                if (!p.Ghost)
                {
                    if (p.GhostAt > 0f && Time >= p.GhostAt) MakeGhost(p);
                    continue;
                }
                // Out of a body: endless sprint.
                if (p.Body == 0)
                {
                    p.Move.Stamina = MoveState.MaxStamina(Role.Survivor, 0f);
                    continue;
                }
                if (p.PossessT <= 0f) continue;
                p.PossessT = Mathf.Max(0f, p.PossessT - dt);
                if (p.PossessT <= 0f) Reanimate(p);
            }
        }

        /// <summary>Back as a ghost: the light comes back on, in Miga Sushi or the Diddy Chamber (where they stand, testing).</summary>
        public void MakeGhost(SimPlayer p)
        {
            p.Ghost = true;
            p.GhostAt = 0f;
            p.Body = 0;
            p.PossessT = 0f;
            p.Spent = false;
            p.BodyLook = p.BodyName = "";
            p.LostInWoods = false;
            p.ShutdownT = 0f;
            p.BoundaryTickT = 0f;
            p.Zone = 0;
            p.Hp = 1f;
            p.Shield = 0f;
            p.StunT = 0f;
            p.ScareT = 0f;
            p.GogglesOn = false;
            p.JarvisT = 0f;
            p.SelSlot = -1;
            p.Crouching = false;
            p.VapeT = p.VapeSlowT = p.DarkT = 0f;
            p.ArrowT = 0f;
            p.Jarvis = 0;
            p.JarvisCd = 0f;
            // No inventory: whatever they carried went with them.
            p.Inv.Clear();
            Place(p, p.GhostSpawn);
            Emit(null, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "ghost", Pos = p.Pos });
            Feed($"{p.Name} came back as a ghost");
            Tell(p, "find a body, help your team");
        }

        /// <summary>The body a ghost could take from where they stand (the nearest lying within reach), or null.</summary>
        public Body BodyInReach(SimPlayer p)
        {
            if (p == null || !p.FreeGhost || p.Spent) return null;
            Body best = null;
            float bd = R(Balance.Ghost.Reach);
            foreach (Body b in Bodies)
            {
                if (b.Ghost != 0) continue;
                float d = Vector2.Distance(b.Pos, p.Pos);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>A ghost takes a body (E): it rises upright and they merge over the next few seconds.</summary>
        public bool Possess(SimPlayer p, Body b)
        {
            if (p == null || b == null || !p.FreeGhost || p.Spent || b.Ghost != 0) return false;
            if (Vector2.Distance(b.Pos, p.Pos) > R(Balance.Ghost.Reach) * 1.5f) return false;
            p.Body = b.Id;
            p.PossessT = Balance.Ghost.PossessTime;
            p.BodyLook = b.Look ?? "Survivor";
            p.BodyName = b.Name ?? "";
            p.Move.KbT = 0f;
            b.Ghost = p.Id;
            b.Since = Time;
            Emit(Near(b.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Talk, A = p.Id, B = b.Id, Text = "possess", Pos = b.Pos });
            return true;
        }

        /// <summary>The merge done: the ghost walks about in the body, as its owner looked.</summary>
        void Reanimate(SimPlayer p)
        {
            Body b = BodyOf(p);
            if (b == null)
            {
                p.Body = 0;
                return;
            }
            Place(p, b.Pos);
            p.Move.Stamina = MoveState.MaxStamina(Role.Survivor, 0f);
            p.Move.StaminaLock = 0f;
            p.Move.SprintBlocked = false;
            // Everyone is told whose body walks again (the flash where it stands, a banner on every screen).
            Emit(null, new GameEvent { Kind = EventKind.Talk, A = p.Id, B = b.Id, Text = "reanimated", Pos = b.Pos });
            Feed($"{b.Name} has been reanimated");
            Tell(p, "you cannot use items or fight, but you can help your team repair generators");
        }

        /// <summary>
        /// Zach struck a body a ghost is in (any machete hit, a lunge, one tick of the Hemp Beam, a golden pump pellet): the
        /// body drops where it stands and the ghost is thrown out of it, as a 0.50 cal round throws a body back; they never
        /// take another.
        /// </summary>
        public void KnockOut(SimPlayer p, SimPlayer by, string kind)
        {
            if (p == null || !p.Reanimated) return;
            Body b = BodyOf(p);
            CancelAction(p);
            DropBody(p, b);
            p.Spent = true;
            p.Crouching = false;
            p.ScareT = 0f;
            p.VapeT = p.VapeSlowT = p.DarkT = 0f;
            float ang = by != null && (p.Pos - by.Pos).sqrMagnitude > 1e-6f ? Mathf.Atan2(p.Pos.y - by.Pos.y, p.Pos.x - by.Pos.x) : p.Facing + Mathf.PI;
            Knock(p, Balance.Items.Sniper.KbDuration, Balance.Items.Sniper.KbPeak, ang);
            if (by != null) by.Stats.Hits++;
            Emit(null, new GameEvent { Kind = EventKind.Hit, A = p.Id, B = by?.Id ?? 0, Pos = p.Pos, F = 1f, Text = kind });
            Emit(Near(p.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "ghost_out", Pos = p.Pos, F = ang });
            Feed($"{p.Name} was knocked out of {b?.Name ?? "a"}{(b != null ? "'s" : "")} body");
            Tell(p, "Knocked out of the body: you can't take another, but now you see everything");
        }

        /// <summary>A body dropped where its ghost stands (knocked out, or the ghost gone): it falls and lies there.</summary>
        void DropBody(SimPlayer p, Body b)
        {
            if (b != null)
            {
                b.Ghost = 0;
                b.Pos = p.Pos;
                b.Facing = p.Facing;
                b.Since = Time;
                if (b.OfNpc) foreach (Npc n in Npcs) if (n.Id == b.Of) n.Pos = b.Pos;
            }
            p.Body = 0;
            p.PossessT = 0f;
            p.BodyLook = p.BodyName = "";
        }

        /// <summary>Whether Zach's blows reach this player as a body a ghost is in (not while it's still rising).</summary>
        public static bool InBody(SimPlayer q) => q.Reanimated && q.PossessT <= 0f;

        /// <summary>A ghost can do this: out of a body, nothing; in one (after the merge), lend a hand.</summary>
        static bool Able(SimPlayer p) => p.Ghost ? InBody(p) : p.CanAct;

        /// <summary>A living survivor (not a ghost in a body) is doing this: repairing generator i, or opening gate i.</summary>
        bool LivingAt(ActionKind a, int target)
        {
            foreach (SimPlayer q in Order)
                if (q.Role == Role.Survivor && !q.Ghost && q.Action == a && q.ActionTarget == target) return true;
            return false;
        }

        /// <summary>A ghost's prompt: a body to take; in one, a generator or a gate a living survivor is working.</summary>
        void GhostPrompts(SimPlayer p)
        {
            if (p.Body == 0)
            {
                Body b = BodyInReach(p);
                if (b != null) Set(p, Prompt.Possess, b.Id);
                return;
            }
            if (p.PossessT > 0f) return;
            if (!Gate.Powered)
            {
                int gi = NearestOf(Map.Generators, p.Pos, R(Balance.Reach.Generator), i => !Gens[i].Repaired && LivingAt(ActionKind.Repair, i));
                if (gi >= 0)
                {
                    Set(p, Prompt.Repair, gi);
                    return;
                }
            }
            for (int gi = 0; gi < 2; gi++)
                if (HasGateAt(gi) && GateAt(gi).Powered && !GateAt(gi).Open && Vector2.Distance(LeverAt(gi), p.Pos) < R(Balance.Reach.Gate) && LivingAt(ActionKind.OpenGate, gi))
                {
                    Set(p, Prompt.OpenGate, gi);
                    return;
                }
        }

        /// <summary>A ghost's input: out of a body, E takes the one in reach; in one, holding E lends a hand (moving lets go).</summary>
        void GhostInput(SimPlayer p, InputCmd cmd, Btn pressed)
        {
            if (cmd.AimDist > 0f || cmd.Aim != 0f) p.Facing = cmd.Aim;
            p.AimDist = cmd.AimDist;
            p.SelSlot = -1;
            p.Crouching = InBody(p) && cmd.Has(Btn.Crouch);
            if (p.Body == 0)
            {
                if ((pressed & Btn.Interact) != 0 && p.Prompt == Prompt.Possess) Possess(p, BodyById(p.PromptTarget));
            }
            else if (InBody(p))
            {
                if (cmd.Has(Btn.Interact) && p.Action == ActionKind.None && p.PromptTarget >= 0)
                {
                    if (p.Prompt == Prompt.Repair)
                    {
                        StartAction(p, ActionKind.Repair, 0f, p.PromptTarget);
                        p.CheckOn = false;
                        Gens[p.PromptTarget].Workers++;
                    }
                    else if (p.Prompt == Prompt.OpenGate) StartAction(p, ActionKind.OpenGate, Balance.Objectives.GateOpenTime, p.PromptTarget);
                }
                if (cmd.Move.sqrMagnitude > 1e-4f && p.Action != ActionKind.None) CancelAction(p);
            }
            p.Move.Mode = MoveModeFor(p);
        }

        /// <summary>Testing (T): a survivor becomes their own ghost where they stand, their body left at their feet.</summary>
        void MakeTestGhost(SimPlayer p)
        {
            Bodies.RemoveAll(b => !b.OfNpc && b.Of == p.Id && b.Ghost == 0);
            p.Health = Health.Eliminated;
            p.Stats.Outcome = "eliminated";
            p.Inv.Clear();
            LayBody(p.Pos, p.Facing, p.Id, false, p.Name, "Survivor");
            p.GhostSpawn = p.Pos + new Vector2(Mathf.Cos(p.Facing + Mathf.PI), Mathf.Sin(p.Facing + Mathf.PI)) * R(40f);
            MakeGhost(p);
        }
    }
}
