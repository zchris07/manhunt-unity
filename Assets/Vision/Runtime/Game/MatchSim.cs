using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    public enum EventKind : byte
    {
        /// <summary>A line in the feed (everyone).</summary>
        Feed,
        /// <summary>A short personal message ("Picked up: bottle").</summary>
        Item,
        Health,
        GenDone,
        GatePowered,
        GateOpen,
        Escaped,
        Eliminated,
        Unstaked,
        Staked,
        Downed,
        /// <summary>A visual cue at a spot (Text: gen_kick, glass, smash, barricade, door_smash, gen_explode...).</summary>
        Noise,
        Note,
        /// <summary>A hit landed on someone (A victim, B attacker, F damage).</summary>
        Hit,
        Stun,
        Scare,
        Book,
        WazSlain,
        Boom,
        Knockback,
        Jarvis,
        Hemp,
        Swing,
        Shot,
        Throw,
        Shatter,
        Gas,
        Explosion,
        Talk,
        Result,
    }

    /// <summary>Something that happened this tick, for the players in <see cref="To"/> (null: everyone).</summary>
    public struct GameEvent
    {
        public EventKind Kind;
        public int[] To;
        public int A, B;
        public Vector2 Pos;
        public float F, G;
        public string Text;
    }

    public sealed class GenState
    {
        public float Progress;
        public bool Repaired, Regressing;
        public int Workers;
    }

    public sealed class GateState
    {
        public bool Powered, Open;
        public float Progress;
    }

    /// <summary>An item a survivor dropped (G) for a teammate.</summary>
    public sealed class DropItem
    {
        public int Id;
        public Vector2 Pos;
        public ItemType Item;
        public bool Golden;
        public float Amount;
    }

    public sealed class MatchResult
    {
        public Winner Winner;
        public string Reason;
        public float DurationSec;
        public int Escaped, Eliminated, Survivors, Hunters, GeneratorsRepaired, GeneratorsRequired;
        public List<(int id, string name, Role role, MatchStats stats)> Stats = new List<(int, string, Role, MatchStats)>();
    }

    /// <summary>
    /// The authoritative match (the original's World): players, objectives, doors, barricades, items, stakes and the win
    /// check, advanced in fixed ticks. Pure rules: it reads players' inputs and poses, and the level only through
    /// <see cref="SimMap"/> and <see cref="ISimGeometry"/>; views read its state and its events. Split into partial files by
    /// the original's modules (interact, combat, items, objectives, abilities).
    /// </summary>
    public sealed partial class MatchSim
    {
        public const float TickDt = 1f / Balance.Net.TickHz;

        public readonly SimMap Map;
        public readonly ISimGeometry Geo;
        public ResolvedBalance Bal;
        public readonly bool TestMode;
        public readonly System.Random Rng;

        public int Tick;
        public float Time;
        public readonly Dictionary<int, SimPlayer> Players = new Dictionary<int, SimPlayer>();
        public readonly List<SimPlayer> Order = new List<SimPlayer>();
        public readonly GenState[] Gens;
        public readonly GateState Gate = new GateState();
        public readonly BarricadeState[] Barricades;
        public readonly int[] BarricadeHits;
        public readonly bool[] Doors;
        public readonly float[] DoorCd;
        public readonly int[] DoorHits;
        public readonly bool[] DoorBroken;
        public readonly bool[] WindowsBroken;
        public readonly bool[] LootTaken;
        /// <summary>Who is on each stake (0 nobody).</summary>
        public readonly int[] Stakes;
        public readonly List<DropItem> Drops = new List<DropItem>();
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public MatchResult Result;
        int nextEntityId = 32;

        /// <summary>Latest input from each player, and the buttons pressed since the last tick (so short taps are not lost).</summary>
        readonly Dictionary<int, InputCmd> inputs = new Dictionary<int, InputCmd>();
        readonly Dictionary<int, Btn> pendingPressed = new Dictionary<int, Btn>();

        /// <summary>The NPCs the rules talk to (empty until the town is populated).</summary>
        public readonly List<Npc> Npcs = new List<Npc>();

        public MatchSim(SimMap map, ISimGeometry geo, ResolvedBalance balance, bool testMode, int seed)
        {
            Map = map;
            Geo = geo;
            Bal = balance;
            TestMode = testMode;
            Rng = new System.Random(seed ^ 0x5eed);
            Gens = new GenState[map.Generators.Count];
            for (int i = 0; i < Gens.Length; i++) Gens[i] = new GenState();
            Barricades = new BarricadeState[map.Barricades.Count];
            BarricadeHits = new int[map.Barricades.Count];
            Doors = new bool[map.Doors.Count];
            for (int i = 0; i < Doors.Length; i++) Doors[i] = map.Doors[i].StartsOpen;
            DoorCd = new float[map.Doors.Count];
            DoorHits = new int[map.Doors.Count];
            DoorBroken = new bool[map.Doors.Count];
            DoorLocked = new bool[map.Doors.Count];
            for (int i = 0; i < DoorLocked.Length; i++) DoorLocked[i] = map.Doors[i].Gate;
            WindowsBroken = new bool[map.Windows.Count];
            LootTaken = new bool[map.Loot.Count];
            Stakes = new int[map.Stakes.Count];
            doorBeamTicks = new int[map.Doors.Count];
            windowBeamTicks = new int[map.Windows.Count];
            barricadeBeamTicks = new int[map.Barricades.Count];
            // Every generator on the map has to run (the lobby sets how many there are).
            if (Bal.RequiredGenerators != Gens.Length && Gens.Length > 0) Bal = WithGenerators(Bal, Gens.Length);
        }

        static ResolvedBalance WithGenerators(ResolvedBalance b, int n) =>
            new ResolvedBalance(b.Hunters, b.Survivors, b.Pressure, b.Scale, Mathf.Max(1, n), b.RepairTime, b.HunterSpeedMul, b.StunMul, b.EscapeNeeded);

        // ---------------------------------------------------------------- players

        public SimPlayer AddPlayer(int id, string name, Role role, Vector2? at = null)
        {
            Vector2 pos = at ?? SpawnFor(role);
            var p = new SimPlayer(id, name, role, pos) { JoinedTime = Time };
            Players[id] = p;
            Order.Add(p);
            Order.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (role == Role.Spectator) p.Spectating = DefaultSpectateTarget(id);
            if (TestMode) FillTestKit(p);
            return p;
        }

        public void RemovePlayer(int id)
        {
            if (!Players.TryGetValue(id, out SimPlayer p)) return;
            Forfeit(id);
            // A ghost leaving drops the body they were in.
            if (p.Ghost) DropBody(p, BodyOf(p));
            Players.Remove(id);
            Order.Remove(p);
        }

        public SimPlayer Get(int id) => Players.TryGetValue(id, out SimPlayer p) ? p : null;

        /// <summary>Takes a player out without the rules noticing (a client's copy following the host's roster).</summary>
        public void DropPlayer(int id)
        {
            if (!Players.TryGetValue(id, out SimPlayer p)) return;
            Players.Remove(id);
            Order.Remove(p);
            inputs.Remove(id);
            pendingPressed.Remove(id);
        }

        /// <summary>
        /// A client: the local player's latest input with every button pressed since the last call (so a tap between sends
        /// still reaches the host), for sending.
        /// </summary>
        public bool TakeInput(int id, out InputCmd cmd)
        {
            if (!inputs.TryGetValue(id, out cmd)) return false;
            if (pendingPressed.TryGetValue(id, out Btn pressed)) cmd.Buttons |= pressed;
            pendingPressed[id] = Btn.None;
            return true;
        }

        Vector2 SpawnFor(Role role)
        {
            int n = 0;
            foreach (SimPlayer q in Order) if (q.Role == role) n++;
            List<Vector2> spawns = role == Role.Hunter ? Map.HunterSpawns : Map.SurvivorSpawns;
            if (spawns.Count == 0) return Vector2.zero;
            return spawns[n % spawns.Count];
        }

        /// <summary>Testing mode: every item and ability, never used up.</summary>
        public void FillTestKit(SimPlayer p)
        {
            if (p.Role == Role.Survivor)
            {
                p.Inv.Infinite = true;
                p.Inv.Clear();
                p.Inv.Add(ItemType.Shotgun);
                p.Inv.Add(ItemType.Shotgun, 1, null, true);
                foreach (ItemType k in new[] { ItemType.Pistol, ItemType.Sniper, ItemType.Bottle, ItemType.Piss, ItemType.Book, ItemType.Goggles, ItemType.MiniShield, ItemType.MrBeastBar, ItemType.Trap, ItemType.DoctorPepper })
                    p.Inv.Add(k);
                for (int i = 0; i < Inventory.TestingSlots; i++)
                {
                    Inventory.Slot s = p.Inv.SlotAt(i);
                    if (!s.Empty && (s.item == ItemType.Bottle || s.item == ItemType.Piss || s.item == ItemType.Book)) s.count = 9;
                }
                p.Jarvis = 3;
                p.Inv.Add(ItemType.HempBeam);
            }
            else if (p.Role == Role.Hunter)
            {
                p.Hemp = 2;
                p.BeamCharges = Balance.Hunter.Beam.Charges;
            }
        }

        /// <summary>Testing mode: flips a player between Zach and survivor where they stand.</summary>
        public bool SwitchRole(int id) => SwitchRole(id, false);

        /// <summary>
        /// Testing mode: flips a player between Zach and survivor where they stand; <paramref name="cycle"/> (T) puts a
        /// survivor's ghost in between (Zach, a survivor, their ghost with their body at their feet, Zach again).
        /// </summary>
        public bool SwitchRole(int id, bool cycle)
        {
            SimPlayer p = Get(id);
            if (!TestMode || p == null || p.Role == Role.Spectator) return false;
            if (p.Carrying != 0)
            {
                SimPlayer q = Get(p.Carrying);
                if (q != null)
                {
                    RestoreSurvivor(q, Balance.Survivor.ReviveHp);
                    q.CarriedBy = 0;
                }
            }
            if (p.CarriedBy != 0)
            {
                SimPlayer h = Get(p.CarriedBy);
                if (h != null) h.Carrying = 0;
            }
            if (p.StakeId >= 0) Stakes[p.StakeId] = 0;
            CancelAction(p);
            if (p.Ghost) DropBody(p, BodyOf(p));
            if (cycle && p.Role == Role.Survivor && !p.Ghost)
            {
                p.Carrying = p.CarriedBy = 0;
                p.StakeId = -1;
                MakeTestGhost(p);
                return true;
            }
            Role role = p.Role == Role.Hunter ? Role.Survivor : Role.Hunter;
            var fresh = new SimPlayer(p.Id, p.Name, role, p.Pos) { Facing = p.Facing, JoinedTime = p.JoinedTime, IsLocal = p.IsLocal, Connected = p.Connected };
            int i = Order.IndexOf(p);
            Players[id] = fresh;
            Order[i] = fresh;
            FillTestKit(fresh);
            return true;
        }

        /// <summary>Testing mode: jump to a point.</summary>
        public void Teleport(int id, Vector2 at)
        {
            SimPlayer p = Get(id);
            if (p == null || p.Role == Role.Spectator || p.Health == Health.Carried || p.Health == Health.Staked) return;
            CancelAction(p);
            Place(p, at);
        }

        public int DefaultSpectateTarget(int exclude)
        {
            foreach (SimPlayer q in Order)
                if (q.Id != exclude && (q.Role == Role.Hunter || (q.Role == Role.Survivor && q.Health != Health.Escaped && q.Health != Health.Eliminated)))
                    return q.Id;
            return 0;
        }

        /// <summary>The player whose view someone sees (themselves, or whom they spectate).</summary>
        public SimPlayer ViewerFor(SimPlayer p)
        {
            if (p.Ghost) return p;
            if (p.Role == Role.Spectator || p.Health == Health.Escaped || p.Health == Health.Eliminated) return Get(p.Spectating);
            return p;
        }

        /// <summary>Receives a player's input frame (latest wins; presses accumulate until the next tick).</summary>
        public void SubmitInput(int id, InputCmd cmd)
        {
            if (!Players.ContainsKey(id)) return;
            Btn before = inputs.TryGetValue(id, out InputCmd last) ? last.Buttons : Btn.None;
            pendingPressed.TryGetValue(id, out Btn pressed);
            pendingPressed[id] = pressed | (cmd.Buttons & ~before);
            inputs[id] = cmd;
        }

        /// <summary>A player's own movement (the client moves its avatar and reports where it is).</summary>
        public void SetPose(int id, Vector2 pos, float facing, Gait gait)
        {
            SimPlayer p = Get(id);
            if (p == null) return;
            if (p.Health == Health.Carried || p.Health == Health.Staked) return;
            // The outer ring stops Zach while he can't cross it.
            if (p.Role == Role.Hunter && CrossingBlocked(p) && pos.magnitude > Ring3 - Balance.Boundary.WallSkin)
                pos = pos.normalized * (Ring3 - Balance.Boundary.WallSkin);
            // A ghost (in a body or not) keeps to the playable circle.
            if (p.Ghost && pos.magnitude > Ring1) pos = pos.normalized * Ring1;
            p.Pos = pos;
            if (MoveModeFor(p) != MoveMode.Locked) p.Facing = facing;
            p.Gait = gait;
        }

        public MoveMode MoveModeFor(SimPlayer p)
        {
            if (p.Role == Role.Spectator) return MoveMode.Locked;
            // A ghost stands still while it merges with a body.
            if (p.Ghost) return p.PossessT > 0f ? MoveMode.Locked : MoveMode.Normal;
            if (p.Role == Role.Hunter)
            {
                if (p.StunT > 0f || p.KnockT > 0f || p.Health == Health.Eliminated) return MoveMode.Locked;
                if (p.Action == ActionKind.PickUp || p.Action == ActionKind.Stake || p.Action == ActionKind.DamageGen) return MoveMode.Locked;
                return MoveMode.Normal;
            }
            if (p.Health == Health.Downed) return MoveMode.Crawl;
            if (p.Health != Health.Healthy && p.Health != Health.Wounded) return MoveMode.Locked;
            if (p.Action == ActionKind.Talk || p.StunT > 0f) return MoveMode.Locked;
            return MoveMode.Normal;
        }

        /// <summary>What a player's own movement needs from the rules this frame.</summary>
        public MoveContext MoveContextFor(SimPlayer p, bool speedMode) => new MoveContext
        {
            Role = p.Role == Role.Hunter ? Role.Hunter : Role.Survivor,
            HunterSpeedMul = Bal.HunterSpeedMul * (p.Role == Role.Hunter ? MatchRules.HunterHealthMul(p.Hp, p.Downs) * MatchRules.HunterStakeMul(p.StakeBuff) : 1f),
            Carrying = p.Carrying != 0,
            LungeBonus = p.JadenBonus * Balance.Hunter.JadenSlainLunge,
            AbilitiesLocked = p.AbilityLockT > 0f,
            SpeedMode = speedMode,
            // More than halfway into a full charge, Zach can't sprint.
            // No sprint while Zach holds a charge past halfway, nor while the 0.50 cal is aimed.
            NoSprint = (p.Role == Role.Hunter && p.ChargeT > Balance.Hunter.Attack.ChargeMax * 0.5f) || p.Aiming,
            // Out of a body: a sprint that never runs out, through anything (in one, a survivor's).
            Ghost = p.FreeGhost,
        };

        // ---------------------------------------------------------------- the tick

        /// <summary>Advances the match by one fixed tick.</summary>
        public void Step()
        {
            if (Result != null) return;
            Tick++;
            Time += TickDt;
            float dt = TickDt;

            foreach (SimPlayer p in Order)
            {
                inputs.TryGetValue(p.Id, out InputCmd cmd);
                pendingPressed.TryGetValue(p.Id, out Btn pressed);
                pendingPressed[p.Id] = Btn.None;
                ApplyInput(p, cmd, pressed);
            }

            for (int i = 0; i < DoorCd.Length; i++) if (DoorCd[i] > 0f) DoorCd[i] = Mathf.Max(0f, DoorCd[i] - dt);
            ComputePrompts();
            UpdateInteractions(dt);
            UpdateCombat(dt);
            UpdateItems(dt);
            UpdateAbilities(dt);
            UpdateGhosts(dt);
            UpdateBoundary(dt);
            foreach (Npc n in Npcs) n.Update(this, dt);
            UpdateVengeance(dt);
            foreach (SimPlayer p in Order) if (p.ArrowT > 0f) p.ArrowT = Mathf.Max(0f, p.ArrowT - dt);
            UpdateObjectives(dt);
            UpdateSenses(dt);

            // Survivor health states are public (the HUD roster).
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor || p.Health == p.LastHealth) continue;
                p.LastHealth = p.Health;
                Emit(null, new GameEvent { Kind = EventKind.Health, A = p.Id, B = (int)p.Health });
            }
            foreach (SimPlayer p in Order)
                if (p.Role == Role.Hunter || (p.Role == Role.Survivor && p.Health != Health.Escaped && p.Health != Health.Eliminated))
                    p.Stats.TimeAlive = Time - p.JoinedTime;
            if (!TestMode) CheckWin();
        }

        void ApplyInput(SimPlayer p, InputCmd cmd, Btn pressed)
        {
            // Buttons held through the click that finished a lock (or a skill check) do nothing until they're let go: the
            // gun in hand never fires off the last click of the lock.
            if (p.HeldOff != Btn.None)
            {
                p.HeldOff &= cmd.Buttons;
                cmd.Buttons &= ~p.HeldOff;
                pressed &= ~p.HeldOff;
            }
            p.LastCmd = cmd;
            p.PrevButtons = cmd.Buttons;
            if (p.Ghost)
            {
                GhostInput(p, cmd, pressed);
                return;
            }
            if (p.Role == Role.Spectator || p.Health == Health.Escaped || p.Health == Health.Eliminated) return;
            bool locked = MoveModeFor(p) == MoveMode.Locked;
            if (!locked)
            {
                if (cmd.AimDist > 0f || cmd.Aim != 0f) p.Facing = cmd.Aim;
                p.AimDist = cmd.AimDist;
            }
            if (p.Role == Role.Survivor) p.SelSlot = cmd.Item >= 1 && cmd.Item <= p.Inv.Limit ? cmd.Item - 1 : -1;
            // The first time each player takes the 0.50 cal in hand: how to aim it.
            if (p.Role == Role.Survivor && !p.SniperHintShown && p.SelSlot >= 0 && p.Inv.ItemAt(p.SelSlot) == ItemType.Sniper)
            {
                p.SniperHintShown = true;
                Tell(p, "Hold right click to aim");
            }
            p.Crouching = p.Role == Role.Survivor && cmd.Has(Btn.Crouch);
            HandlePresses(p, cmd, pressed);

            // Moving cancels survivor interactions.
            if (cmd.Move.sqrMagnitude > 1e-4f && p.Role == Role.Survivor && p.Action != ActionKind.None && p.Action != ActionKind.Talk && p.Action != ActionKind.Drink)
                CancelAction(p);
            p.Move.Mode = MoveModeFor(p);
        }

        public void StartAction(SimPlayer p, ActionKind action, float dur, int target)
        {
            p.Action = action;
            p.ActionT = 0f;
            p.ActionDur = dur;
            p.ActionTarget = target;
        }

        public void CancelAction(SimPlayer p)
        {
            if (p.Action == ActionKind.Repair && p.ActionTarget >= 0 && p.ActionTarget < Gens.Length) Gens[p.ActionTarget].Workers = Mathf.Max(0, Gens[p.ActionTarget].Workers - 1);
            if (p.Action == ActionKind.Talk) foreach (Npc n in Npcs) n.CancelTalk(this, p);
            // A mini shield drunk off E, stopped: the item in hand is back at once.
            if (p.QuickShield)
            {
                p.QuickShield = false;
                p.QuickT = 0f;
            }
            p.Action = ActionKind.None;
            p.ActionT = 0f;
            p.ActionDur = 0f;
            p.ActionTarget = -1;
        }

        public void SetBarricade(int id, BarricadeState state) => Barricades[id] = state;

        /// <summary>Opens or closes a door (a smashed door stays open).</summary>
        public void SetDoor(int id, bool open)
        {
            if (DoorBroken[id] && !open) return;
            // A locked gate stays shut until a survivor picks it.
            if (open && DoorLocked[id]) return;
            Doors[id] = open;
            DoorCd[id] = 0.35f;
        }

        public void BreakWindow(int i) => WindowsBroken[i] = true;

        public int AllocEntityId() => nextEntityId++;

        // ---------------------------------------------------------------- events

        public void Emit(int[] to, GameEvent e)
        {
            e.To = to;
            Events.Add(e);
        }

        public void Emit(int to, GameEvent e) => Emit(new[] { to }, e);

        public int[] Survivors()
        {
            var ids = new List<int>();
            foreach (SimPlayer p in Order) if (p.Role != Role.Hunter) ids.Add(p.Id);
            return ids.ToArray();
        }

        public int[] Hunters()
        {
            var ids = new List<int>();
            foreach (SimPlayer p in Order) if (p.Role == Role.Hunter || p.Role == Role.Spectator) ids.Add(p.Id);
            return ids.ToArray();
        }

        /// <summary>Everyone whose view (own or spectated) is within r (original units) of a point.</summary>
        public int[] Near(Vector2 at, float r)
        {
            var to = new List<int>();
            float rd = Scale.D(r);
            foreach (SimPlayer p in Order)
            {
                SimPlayer v = ViewerFor(p);
                if (v != null && Vector2.Distance(v.Pos, at) <= rd) to.Add(p.Id);
            }
            return to.ToArray();
        }

        /// <summary>
        /// A cue at a spot (particles for explosions, glass and splinters, and their sounds): sent to everyone within
        /// <paramref name="r"/>, or as far as sounds carry (<see cref="Balance.Net.HearingRadius"/>) if that's further.
        /// </summary>
        public void Noise(Vector2 at, float r, string kind)
        {
            int[] to = Near(at, Mathf.Max(r, Balance.Net.HearingRadius));
            if (to.Length > 0) Emit(to, new GameEvent { Kind = EventKind.Noise, Pos = at, F = r, Text = kind });
        }

        /// <summary>Whether a point is on a player's screen (within <paramref name="margin"/> times what it shows); always, if it isn't known.</summary>
        public static bool OnScreen(SimPlayer p, Vector2 at, float margin = Balance.Net.OffscreenMargin)
        {
            if (p == null || p.ViewHalf.x <= 0f || p.ViewHalf.y <= 0f) return true;
            Vector2 rel = at - (p.Pos + p.ViewOffset);
            float c = Mathf.Cos(p.ViewYaw), s = Mathf.Sin(p.ViewYaw);
            float x = rel.x * c + rel.y * s, y = -rel.x * s + rel.y * c;
            return Mathf.Abs(x) <= p.ViewHalf.x * margin && Mathf.Abs(y) <= p.ViewHalf.y * margin;
        }

        /// <summary>
        /// Whether a player's bullet or pellet can strike an NPC: always once it's roused (alert, armed, angry, chasing),
        /// otherwise only on the shooter's screen (an NPC they can't see is never set on them by a stray round).
        /// </summary>
        public static bool CanShoot(Npc n, SimPlayer by) => by == null || n.Aggravated || OnScreen(by, n.Pos);

        /// <summary>A player landed an attack or ability on another (Zach on a survivor, a survivor on Zach): the NPCs hear of it.</summary>
        public void PlayerAttacked(SimPlayer victim, SimPlayer by)
        {
            if (victim == null || by == null || victim == by) return;
            foreach (Npc n in Npcs) n.PlayerAttacked(this, victim, by);
        }

        /// <summary>A bullet or pellet striking a body (a player's or an NPC's): heard by everyone near.</summary>
        public void BulletStruck(Vector2 at) => Noise(at, 1000f, "flesh");

        public void Feed(string text) => Emit(null, new GameEvent { Kind = EventKind.Feed, Text = text });

        public void Tell(SimPlayer p, string text) => Emit(p.Id, new GameEvent { Kind = EventKind.Item, Text = text });

        /// <summary>Removes a survivor from play (stake stage 2 or a disconnect).</summary>
        public void Eliminate(SimPlayer p, string why, SimPlayer credit = null)
        {
            if (p.StakeId >= 0) Stakes[p.StakeId] = 0;
            if (p.CarriedBy != 0)
            {
                SimPlayer h = Get(p.CarriedBy);
                if (h != null) h.Carrying = 0;
            }
            CancelAction(p);
            p.Health = Health.Eliminated;
            p.StakeId = -1;
            p.CarriedBy = 0;
            p.GogglesOn = false;
            p.Stats.Outcome = "eliminated";
            p.EndedTime = Time;
            p.Spectating = DefaultSpectateTarget(p.Id);
            if (credit != null) credit.Stats.Eliminations++;
            Emit(null, new GameEvent { Kind = EventKind.Eliminated, A = p.Id, Text = why });
            Feed(why == "disconnected" ? $"{p.Name} disconnected" : why == "woods" ? $"{p.Name} wandered into the woods" : $"{p.Name} was sacrificed");
            // Sacrificed: back soon as a ghost.
            ScheduleGhost(p, why);
        }

        /// <summary>Grace period expired: survivors are eliminated, hunters leave the match.</summary>
        public void Forfeit(int id)
        {
            SimPlayer p = Get(id);
            if (p == null) return;
            if (p.Role == Role.Survivor && p.Health != Health.Escaped && p.Health != Health.Eliminated) Eliminate(p, "disconnected");
            else if (p.Role == Role.Hunter)
            {
                if (p.Carrying != 0)
                {
                    SimPlayer s = Get(p.Carrying);
                    if (s != null)
                    {
                        RestoreSurvivor(s, Balance.Survivor.ReviveHp);
                        s.CarriedBy = 0;
                    }
                    p.Carrying = 0;
                }
                p.Health = Health.Eliminated;
                Feed($"{p.Name} left the hunt");
            }
        }

        public void SetConnected(int id, bool connected)
        {
            SimPlayer p = Get(id);
            if (p == null) return;
            p.Connected = connected;
            p.DisconnectedAt = connected ? 0f : Time;
            inputs.Remove(id);
            pendingPressed.Remove(id);
        }

        public static float Dist(Vector2 a, Vector2 b) => Vector2.Distance(a, b);

        /// <summary>A reach in original units as design units.</summary>
        public static float R(float units) => Scale.D(units);
    }
}
