using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>Prompts, presses and timed interactions (the original's interact.ts).</summary>
    public sealed partial class MatchSim
    {
        /// <summary>The Hemp Beam Sexton drops when Zach slays him (null when there is none on the ground).</summary>
        public Vector2? HempDrop;

        static readonly Prompt[] HoldPrompts = { Prompt.Repair, Prompt.Heal, Prompt.Revive, Prompt.Unstake, Prompt.OpenGate };

        int NearestLoot(Vector2 at, float max)
        {
            int best = -1;
            float bd = max;
            for (int i = 0; i < Map.Loot.Count; i++)
            {
                if (LootTaken[i]) continue;
                float d = Vector2.Distance(Map.Loot[i].Pos, at);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        int NearestDrop(Vector2 at, float max)
        {
            int best = -1;
            float bd = max;
            for (int i = 0; i < Drops.Count; i++)
            {
                float d = Vector2.Distance(Drops[i].Pos, at);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        int NearestOf(System.Collections.Generic.List<Vector2> list, Vector2 at, float max, System.Func<int, bool> ok)
        {
            int best = -1;
            float bd = max;
            for (int i = 0; i < list.Count; i++)
            {
                float d = Vector2.Distance(list[i], at);
                if (d < bd && ok(i)) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Closest door (by distance to its closed panel) within reach.</summary>
        public int NearbyDoor(Vector2 at, float reach = Balance.Reach.Door)
        {
            int best = -1;
            float bd = R(reach);
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                if (DoorBroken[i]) continue;
                float d = SimMap.SegmentDistance(at, Map.Doors[i].A, Map.Doors[i].B);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public int NearbyBarricade(Vector2 at, BarricadeState state, float reach = Balance.Reach.Barricade)
        {
            int best = -1;
            float bd = R(reach);
            for (int i = 0; i < Map.Barricades.Count; i++)
            {
                if (Barricades[i] != state) continue;
                float d = Vector2.Distance(Map.Barricades[i].Pos, at);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        int NearbyHidingSpot(Vector2 at, float reach, bool requireFree)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < Map.HidingSpots.Count; i++)
            {
                SimMap.HideDef h = Map.HidingSpots[i];
                if (requireFree && Hiding[i] != 0) continue;
                float d = Vector2.Distance(h.Pos, at);
                bool grass = h.Kind == Vision.World.HidingSpot.Kind.Grass;
                // Tall grass hides you anywhere inside the patch; furniture from beside its door.
                bool inReach = grass ? d < Mathf.Max(R(reach), h.Reach) : d < R(reach) + Mathf.Max(0f, h.Reach - R(reach)) * 0.5f || Vector2.Distance(h.Exit, at) < R(reach + 10f);
                if (inReach && d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Works out the E and Space prompts for every player.</summary>
        void ComputePrompts()
        {
            foreach (SimPlayer p in Order)
            {
                p.Prompt = Prompt.None;
                p.PromptTarget = -1;
                p.Prompt2 = Prompt.None;
                p.Prompt2Target = -1;
                if (p.Role == Role.Survivor) SurvivorPrompts(p);
                else if (p.Role == Role.Hunter && p.CanAct) HunterPrompts(p);
            }
        }

        void Set(SimPlayer p, Prompt prompt, int target)
        {
            p.Prompt = prompt;
            p.PromptTarget = target;
        }

        Prompt NpcAwaiting(SimPlayer p)
        {
            foreach (Npc n in Npcs)
            {
                Prompt a = n.Awaiting(this, p);
                if (a != Prompt.None) return a;
            }
            return Prompt.None;
        }

        void SurvivorPrompts(SimPlayer p)
        {
            if (p.HideState == 2 || p.HideState == 3)
            {
                Set(p, Prompt.LeaveHiding, p.HideSpot);
                return;
            }
            if (!p.CanAct) return;
            Vector2 at = p.Pos;
            // Njaaron waiting for an answer, or Sexton waiting for you to keep listening, comes first.
            Prompt waiting = NpcAwaiting(p);
            if (waiting != Prompt.None)
            {
                Set(p, waiting, 0);
                return;
            }
            // Teammates: staked first, then downed, then wounded.
            SimPlayer mate = null;
            float mateD = R(Balance.Reach.Teammate);
            int Pri(SimPlayer q) => q.Health == Health.Staked ? 0 : q.Health == Health.Downed ? 1 : 2;
            foreach (SimPlayer q in Order)
            {
                if (q == p || q.Role != Role.Survivor) continue;
                if (q.Health != Health.Staked && q.Health != Health.Downed && !(q.Health == Health.Wounded && q.HideState == 0)) continue;
                float d = Vector2.Distance(q.Pos, at);
                int cur = mate != null ? Pri(mate) : 9;
                if (d < R(Balance.Reach.Teammate) && (Pri(q) < cur || (Pri(q) == cur && d < mateD)))
                {
                    mate = q;
                    mateD = d;
                }
            }
            if (mate != null)
                Set(p, mate.Health == Health.Staked ? Prompt.Unstake : mate.Health == Health.Downed ? Prompt.Revive : Prompt.Heal, mate.Id);

            if (p.Prompt == Prompt.None)
                for (int i = 0; i < Npcs.Count && p.Prompt == Prompt.None; i++)
                {
                    Prompt t = Npcs[i].TalkPrompt(this, p);
                    if (t != Prompt.None) Set(p, t, i);
                }
            if (p.Prompt == Prompt.None)
            {
                int ni = NearestOf(Map.Notes, at, R(Balance.Reach.Note), _ => true);
                if (ni >= 0) Set(p, Prompt.ReadNote, ni);
            }
            if (p.Prompt == Prompt.None)
            {
                int di = NearestDrop(at, R(Balance.Reach.Pickup));
                if (di >= 0) Set(p, Prompt.PickDrop, Drops[di].Id);
            }
            if (p.Prompt == Prompt.None)
            {
                int li = NearestLoot(at, R(Balance.Reach.Loot) + 0.3f);
                if (li >= 0) Set(p, Prompt.Loot, li);
            }
            if (p.Prompt == Prompt.None && !Gate.Powered)
            {
                int gi = NearestOf(Map.Generators, at, R(Balance.Reach.Generator), i => !Gens[i].Repaired);
                if (gi >= 0) Set(p, Prompt.Repair, gi);
            }
            if (p.Prompt == Prompt.None && Map.HasGate && Vector2.Distance(Map.Lever, at) < R(Balance.Reach.Gate) && !Gate.Open)
                Set(p, Gate.Powered ? Prompt.OpenGate : Prompt.GatePowerless, 0);
            if (p.Prompt == Prompt.None)
            {
                int hi = NearbyHidingSpot(at, Balance.Reach.Hide, true);
                if (hi >= 0) Set(p, Prompt.Hide, hi);
            }
            if (p.Prompt == Prompt.None)
            {
                int di = NearbyDoor(at);
                if (di >= 0) Set(p, Doors[di] ? Prompt.CloseDoor : Prompt.OpenDoor, di);
            }
            int bi = NearbyBarricade(at, BarricadeState.Up);
            if (bi >= 0)
            {
                p.Prompt2 = Prompt.DropBarricade;
                p.Prompt2Target = bi;
            }
        }

        void HunterPrompts(SimPlayer p)
        {
            Vector2 at = p.Pos;
            Prompt waiting = NpcAwaiting(p);
            if (waiting != Prompt.None)
            {
                Set(p, waiting, 0);
                return;
            }
            if (p.Carrying != 0)
            {
                int si = NearestOf(Map.Stakes, at, R(Balance.Reach.Stake), i => Stakes[i] == 0);
                if (si >= 0) Set(p, Prompt.Stake, si);
            }
            else
            {
                SimPlayer target = null;
                float bd = R(Balance.Reach.Pickup);
                foreach (SimPlayer q in Order)
                {
                    if (q.Role != Role.Survivor || q.Health != Health.Downed) continue;
                    float d = Vector2.Distance(q.Pos, at);
                    if (d < bd) { bd = d; target = q; }
                }
                if (target != null) Set(p, Prompt.PickUp, target.Id);
                else
                {
                    for (int i = 0; i < Npcs.Count && p.Prompt == Prompt.None; i++)
                    {
                        Prompt t = Npcs[i].TalkPrompt(this, p);
                        if (t != Prompt.None) Set(p, t, i);
                    }
                    if (p.Prompt == Prompt.None)
                    {
                        int ni = NearestOf(Map.Notes, at, R(Balance.Reach.Note), _ => true);
                        if (ni >= 0) Set(p, Prompt.ReadNote, ni);
                    }
                    if (p.Prompt == Prompt.None && HempDrop.HasValue && Vector2.Distance(HempDrop.Value, at) < R(Balance.Reach.Pickup)) Set(p, Prompt.TakeHemp, 0);
                    if (p.Prompt == Prompt.None)
                    {
                        int hi = NearbyHidingSpot(at, Balance.Reach.Hide + 8f, false);
                        if (hi >= 0) Set(p, Prompt.Search, hi);
                        else
                        {
                            int gi = NearestOf(Map.Generators, at, R(Balance.Reach.Generator), i => !Gens[i].Repaired && Gens[i].Progress > 0.01f && !Gens[i].Regressing);
                            if (gi >= 0) Set(p, Prompt.DamageGen, gi);
                        }
                    }
                }
            }
            if (p.Prompt == Prompt.None)
            {
                int di = NearbyDoor(at);
                if (di >= 0) Set(p, Doors[di] ? Prompt.CloseDoor : Prompt.OpenDoor, di);
            }
            // Nothing to do here: he still sees who or what is next to him.
            if (p.Prompt == Prompt.None) NameNear(p);
        }

        /// <summary>Zach next to an NPC or an item gets its name.</summary>
        void NameNear(SimPlayer p)
        {
            int best = -1;
            float bd = R(Balance.Reach.NpcName);
            for (int i = 0; i < Npcs.Count; i++)
            {
                if (!Npcs[i].Solid) continue;
                float d = Vector2.Distance(Npcs[i].Pos, p.Pos);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0)
            {
                Set(p, Prompt.NameNpc, best);
                return;
            }
            int di = NearestDrop(p.Pos, R(Balance.Reach.Pickup));
            if (di >= 0)
            {
                Set(p, Prompt.NameDrop, di);
                return;
            }
            int li = NearestLoot(p.Pos, R(Balance.Reach.Pickup));
            if (li >= 0) Set(p, Prompt.NameLoot, li);
        }

        void HandlePresses(SimPlayer p, InputCmd cmd, Btn pressed)
        {
            // Y and N answer Njaaron, for anyone he asked.
            if ((pressed & Btn.Yes) != 0) foreach (Npc n in Npcs) n.Answer(this, p, true);
            if ((pressed & Btn.No) != 0) foreach (Npc n in Npcs) n.Answer(this, p, false);
            if (p.Role == Role.Survivor)
            {
                // Holding E starts hold-to-act interactions as soon as they become available.
                bool heldStart = cmd.Has(Btn.Interact) && p.Action == ActionKind.None && p.HideState == 0 && System.Array.IndexOf(HoldPrompts, p.Prompt) >= 0;
                if ((pressed & Btn.Interact) != 0 || heldStart) SurvivorInteract(p);
                if ((pressed & Btn.Space) != 0 && p.CanAct && p.Action == ActionKind.None && p.Prompt2 == Prompt.DropBarricade) DropBarricade(p, p.Prompt2Target);
                if ((pressed & Btn.Primary) != 0 && p.CanAct) UseItem(p, cmd);
                if ((pressed & Btn.Drop) != 0) DropItem(p);
                if ((pressed & Btn.Ability) != 0) TryJarvis(p);
                if ((pressed & Btn.Beam) != 0) TryBeam(p, cmd.Aim);
                return;
            }
            if (p.Role != Role.Hunter || !p.CanAct) return;
            // Plasma's golden pump replaces the machete while it has shots.
            if (p.Pump > 0)
            {
                if (cmd.Has(Btn.Primary)) FireZachPump(p, cmd.Aim);
            }
            else if ((pressed & Btn.Primary) != 0 && p.BeamT <= 0f) StartCharge(p);
            if ((pressed & Btn.Secondary) != 0) TryBurst(p, cmd.Aim);
            if ((pressed & Btn.Vape) != 0) TryVape(p, cmd.Aim);
            if ((pressed & Btn.Beam) != 0) TryBeam(p, cmd.Aim);
            if ((pressed & Btn.Ability) != 0) TryHemp(p);
            if (p.Action != ActionKind.None || p.AttackWindup > 0f) return;
            if ((pressed & Btn.Interact) == 0) return;
            switch (p.Prompt)
            {
                case Prompt.PickUp:
                    StartAction(p, ActionKind.PickUp, Balance.Hunter.PickupTime, p.PromptTarget);
                    break;
                case Prompt.Stake:
                    StartAction(p, ActionKind.Stake, Balance.Hunter.StakeTime, p.PromptTarget);
                    break;
                case Prompt.Search:
                {
                    // Searching is instant.
                    SimPlayer occupant = Get(Hiding[p.PromptTarget]);
                    Emit(Near(p.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "search", Pos = Map.HidingSpots[p.PromptTarget].Pos });
                    if (occupant != null && occupant.HideState >= 1)
                    {
                        ExitHiding(occupant, true);
                        DamageSurvivor(occupant, p);
                        Feed($"{p.Name} dragged {occupant.Name} out of hiding");
                    }
                    break;
                }
                case Prompt.DamageGen:
                    StartAction(p, ActionKind.DamageGen, Balance.Hunter.DamageGenTime, p.PromptTarget);
                    break;
                case Prompt.ReadNote:
                    Emit(p.Id, new GameEvent { Kind = EventKind.Note, A = p.PromptTarget });
                    break;
                case Prompt.TakeHemp:
                    if (HempDrop.HasValue)
                    {
                        p.BeamCharges = Balance.Hunter.Beam.Charges;
                        HempDrop = null;
                        Tell(p, $"Got the Hemp Beam: R, {Balance.Hunter.Beam.Charges} charges");
                    }
                    break;
                case Prompt.OpenDoor:
                case Prompt.CloseDoor:
                    ToggleDoor(p.PromptTarget);
                    break;
                default:
                    if (p.PromptTarget >= 0 && p.PromptTarget < Npcs.Count && IsTalk(p.Prompt)) Npcs[p.PromptTarget].Talk(this, p);
                    break;
            }
        }

        static bool IsTalk(Prompt p) => p == Prompt.TalkSexton || p == Prompt.TalkChris || p == Prompt.TalkMarc || p == Prompt.TalkPlasma || p == Prompt.TalkWaz ||
                                         p == Prompt.TalkChacko || p == Prompt.TalkNjaaron || p == Prompt.TalkMonique || p == Prompt.TalkThomas || p == Prompt.TalkSoham ||
                                         p == Prompt.SextonMore;

        void ToggleDoor(int id)
        {
            if (id < 0 || DoorCd[id] > 0f) return;
            SetDoor(id, !Doors[id]);
        }

        void SurvivorInteract(SimPlayer p)
        {
            if (p.HideState == 2)
            {
                p.HideState = 3;
                StartAction(p, ActionKind.HideExit, Balance.Hiding.ExitTime, p.HideSpot);
                return;
            }
            if (p.HideState == 3)
            {
                // Leaving is interruptible: press again to stay hidden.
                p.HideState = 2;
                CancelAction(p);
                return;
            }
            if (!p.CanAct || p.Action != ActionKind.None) return;
            switch (p.Prompt)
            {
                case Prompt.Loot:
                    TakeLoot(p, p.PromptTarget);
                    break;
                case Prompt.Hide:
                    Hiding[p.PromptTarget] = p.Id;
                    p.HideSpot = p.PromptTarget;
                    p.HideState = 1;
                    p.GogglesOn = false;
                    StartAction(p, ActionKind.HideEnter, Balance.Hiding.EnterTime, p.PromptTarget);
                    break;
                case Prompt.Repair:
                    StartAction(p, ActionKind.Repair, 0f, p.PromptTarget);
                    Gens[p.PromptTarget].Workers++;
                    Gens[p.PromptTarget].Regressing = false;
                    break;
                case Prompt.Heal:
                    StartAction(p, ActionKind.Heal, Balance.Survivor.HealTime, p.PromptTarget);
                    break;
                case Prompt.Revive:
                    StartAction(p, ActionKind.Revive, Balance.Survivor.ReviveTime, p.PromptTarget);
                    break;
                case Prompt.Unstake:
                    StartAction(p, ActionKind.Unstake, Balance.Survivor.UnstakeTime, p.PromptTarget);
                    break;
                case Prompt.OpenGate:
                    StartAction(p, ActionKind.OpenGate, Balance.Objectives.GateOpenTime, 0);
                    break;
                case Prompt.ReadNote:
                    Emit(p.Id, new GameEvent { Kind = EventKind.Note, A = p.PromptTarget });
                    break;
                case Prompt.PickDrop:
                    PickUpDrop(p, p.PromptTarget);
                    break;
                case Prompt.OpenDoor:
                case Prompt.CloseDoor:
                    ToggleDoor(p.PromptTarget);
                    break;
                default:
                    if (p.PromptTarget >= 0 && p.PromptTarget < Npcs.Count && IsTalk(p.Prompt)) Npcs[p.PromptTarget].Talk(this, p);
                    break;
            }
        }

        /// <summary>A survivor grabs a loot spawn (a full inventory drops the last slot to make room).</summary>
        void TakeLoot(SimPlayer p, int li)
        {
            if (li < 0 || li >= Map.Loot.Count || LootTaken[li]) return;
            LootTaken[li] = true;
            if (TestMode)
            {
                Tell(p, "Items are infinite");
                return;
            }
            SimMap.LootDef item = Map.Loot[li];
            AddItem(p, item.Item, null, item.Golden);
            Tell(p, $"Picked up: {Items.Name(item.Item, item.Golden)}");
        }

        /// <summary>Adds one item to a survivor's inventory; a full one drops its last stack on the ground.</summary>
        public void AddItem(SimPlayer p, ItemType item, float? amount = null, bool golden = false)
        {
            void Dropped(ItemType it, int n, bool gold, System.Collections.Generic.List<float> amts)
            {
                float ang = p.Facing + Mathf.PI;
                for (int u = 0; u < n; u++)
                {
                    var d = new DropItem { Id = AllocEntityId(), Pos = p.Pos, Item = it, Golden = gold, Amount = u < amts.Count ? amts[u] : Items.FreshAmount(it, gold) };
                    PlaceDrop(d, p.Pos + new Vector2(Mathf.Cos(ang + u * 0.5f), Mathf.Sin(ang + u * 0.5f)) * R(26f));
                }
                Tell(p, $"Dropped: {Items.Name(it, gold)}{(n > 1 ? $" x{n}" : "")}");
            }
            p.Inv.Dropped += Dropped;
            p.Inv.Add(item, 1, amount, golden);
            p.Inv.Dropped -= Dropped;
        }

        /// <summary>Progresses timed interactions.</summary>
        void UpdateInteractions(float dt)
        {
            foreach (SimPlayer p in Order)
            {
                if (p.Action == ActionKind.None || p.Action == ActionKind.Attack || p.Action == ActionKind.Talk) continue;
                bool holding = p.LastCmd.Has(Btn.Interact);
                switch (p.Action)
                {
                    case ActionKind.Repair:
                    {
                        GenState g = Gens[p.ActionTarget];
                        if (!holding || g.Repaired || Gate.Powered || Vector2.Distance(Map.Generators[p.ActionTarget], p.Pos) > R(Balance.Reach.Generator + 10f) || !p.CanAct)
                        {
                            CancelAction(p);
                            continue;
                        }
                        p.Stats.RepairSec += dt;
                        p.ActionT += dt;
                        continue;
                    }
                    case ActionKind.Heal:
                    case ActionKind.Revive:
                    case ActionKind.Unstake:
                    {
                        SimPlayer q = Get(p.ActionTarget);
                        Health want = p.Action == ActionKind.Heal ? Health.Wounded : p.Action == ActionKind.Revive ? Health.Downed : Health.Staked;
                        if (!holding || q == null || q.Health != want || !p.CanAct || Vector2.Distance(q.Pos, p.Pos) > R(Balance.Reach.Teammate + 15f))
                        {
                            CancelAction(p);
                            continue;
                        }
                        p.ActionT += dt;
                        if (p.ActionT >= p.ActionDur)
                        {
                            if (p.Action == ActionKind.Heal)
                            {
                                RestoreSurvivor(q, 1f);
                                p.Stats.Heals++;
                                Emit(new[] { q.Id, p.Id }, new GameEvent { Kind = EventKind.Item, Text = $"{p.Name} patched {q.Name} up" });
                            }
                            else if (p.Action == ActionKind.Revive)
                            {
                                RestoreSurvivor(q, Balance.Survivor.ReviveHp);
                                p.Stats.Revives++;
                                Feed($"{p.Name} got {q.Name} back on their feet");
                            }
                            else
                            {
                                ReleaseFromStake(q);
                                p.Stats.Unstakes++;
                                Feed($"{p.Name} cut {q.Name} down");
                            }
                            p.Action = ActionKind.None;
                        }
                        continue;
                    }
                    case ActionKind.OpenGate:
                        if (!holding || !Gate.Powered || Gate.Open || !p.CanAct || Vector2.Distance(Map.Lever, p.Pos) > R(Balance.Reach.Gate + 10f))
                        {
                            CancelAction(p);
                            continue;
                        }
                        p.ActionT = Gate.Progress * p.ActionDur;
                        continue;
                    case ActionKind.Plant:
                        p.ActionT += dt;
                        if (p.ActionT < p.ActionDur) continue;
                        p.Action = ActionKind.None;
                        PlantTrap(p);
                        continue;
                    case ActionKind.Drink:
                        if (!p.CanAct)
                        {
                            CancelAction(p);
                            continue;
                        }
                        p.ActionT += dt;
                        // Drinking on the move is allowed, at half speed.
                        UseSlow(p, 0.15f);
                        if (p.ActionT < p.ActionDur) continue;
                        p.Action = ActionKind.None;
                        DrinkShield(p);
                        continue;
                    case ActionKind.HideEnter:
                    {
                        p.ActionT += dt;
                        SimMap.HideDef spot = Map.HidingSpots[p.ActionTarget];
                        Vector2 to = spot.Kind == Vision.World.HidingSpot.Kind.Grass ? p.Pos : spot.Pos;
                        Place(p, p.Pos + (to - p.Pos) * Mathf.Min(1f, dt * 8f));
                        if (p.ActionT >= p.ActionDur)
                        {
                            Place(p, to);
                            p.HideState = 2;
                            p.Action = ActionKind.None;
                        }
                        continue;
                    }
                    case ActionKind.HideExit:
                        p.ActionT += dt;
                        if (p.ActionT >= p.ActionDur) ExitHiding(p, false);
                        continue;
                    case ActionKind.PickUp:
                    {
                        p.ActionT += dt;
                        if (p.ActionT < p.ActionDur) continue;
                        SimPlayer q = Get(p.ActionTarget);
                        p.Action = ActionKind.None;
                        if (q != null && q.Health == Health.Downed && Vector2.Distance(q.Pos, p.Pos) < R(Balance.Reach.Pickup + 20f)) CarrySurvivor(p, q);
                        continue;
                    }
                    case ActionKind.Stake:
                    {
                        p.ActionT += dt;
                        if (p.ActionT < p.ActionDur) continue;
                        p.Action = ActionKind.None;
                        SimPlayer q = Get(p.Carrying);
                        if (q != null && Stakes[p.ActionTarget] == 0) StakeSurvivor(p, q, p.ActionTarget);
                        continue;
                    }
                    case ActionKind.DamageGen:
                    {
                        p.ActionT += dt;
                        if (p.ActionT < Balance.Hunter.DamageGenTime) continue;
                        p.Action = ActionKind.None;
                        GenState g = Gens[p.ActionTarget];
                        if (!g.Repaired)
                        {
                            g.Progress = Mathf.Max(0f, g.Progress - Balance.Objectives.DamageRegressInstant);
                            g.Regressing = true;
                            p.Stats.GensDamaged++;
                            Noise(Map.Generators[p.ActionTarget], 700f, "gen_kick");
                        }
                        continue;
                    }
                }
            }
        }

        public void ExitHiding(SimPlayer p, bool forced)
        {
            SimMap.HideDef? spot = p.HideSpot >= 0 && p.HideSpot < Map.HidingSpots.Count ? Map.HidingSpots[p.HideSpot] : (SimMap.HideDef?)null;
            if (p.HideSpot >= 0) Hiding[p.HideSpot] = 0;
            if (spot.HasValue && spot.Value.Kind != Vision.World.HidingSpot.Kind.Grass) Place(p, spot.Value.Exit);
            p.HideSpot = -1;
            p.HideState = 0;
            p.HoldingBreath = false;
            if (p.Action == ActionKind.HideExit || p.Action == ActionKind.HideEnter) p.Action = ActionKind.None;
            Emit(p.Id, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = forced ? "dragged" : "unhide", Pos = p.Pos });
        }

        public void ReleaseFromStake(SimPlayer q)
        {
            Vector2? stake = q.StakeId >= 0 && q.StakeId < Map.Stakes.Count ? Map.Stakes[q.StakeId] : (Vector2?)null;
            if (q.StakeId >= 0) Stakes[q.StakeId] = 0;
            q.StakeId = -1;
            RestoreSurvivor(q, Balance.Survivor.ReviveHp);
            q.Move.HasteT = 0f;
            if (stake.HasValue) Place(q, stake.Value + new Vector2(R(30f), -R(30f)));
            Emit(null, new GameEvent { Kind = EventKind.Unstaked, A = q.Id, Pos = q.Pos });
        }
    }
}
