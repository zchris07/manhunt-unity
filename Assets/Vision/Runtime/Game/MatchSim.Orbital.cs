using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// A K-pop orbital strike called down from Daphne's tablet: locked on one player (anyone playable, Zach or a survivor),
    /// a countdown, then a pink beam straight down onto them from overhead for <see cref="Balance.Items.Orbital.Duration"/>.
    /// </summary>
    public sealed class OrbitalStrike
    {
        public int Id;
        /// <summary>Who called it, and who it's locked on.</summary>
        public int Owner, Target;
        /// <summary>Where the beam comes down (design units), following the target.</summary>
        public Vector2 Pos;
        /// <summary>Seconds of the countdown left (the beam's on once it's done), and how long the beam's been on.</summary>
        public float Countdown, T;
        public float TickAcc;
        /// <summary>Whether it's come down yet.</summary>
        public bool Firing => Countdown <= 0f;
    }

    /// <summary>
    /// The orbital strike (the user's rules): confirmed on the tablet against a player in an explored part of the map, it
    /// is locked on them from then, wherever they go; the tablet stays in the caller's inventory and recharges for two
    /// minutes (dropped where they fall if they're slain). After a 3 s countdown a beam three times Zach's
    /// width comes straight down from overhead, through anything solid, and follows them with a lag: whoever keeps moving
    /// can keep partly out of it but never gets clear. Anyone it's on (players only, never an NPC) takes half the Hemp
    /// Beam's tick ten times a second (about 44 HP over its 8.725 s), is slowed 20% and has every ability silenced; and
    /// everything that can be destroyed (doors, windows, pallets, unlocked gates, gas traps) goes the instant it touches
    /// it. Its track (NewJeans' OMG) plays the whole time; it ends with it.
    /// </summary>
    public sealed partial class MatchSim
    {
        public readonly List<OrbitalStrike> Strikes = new List<OrbitalStrike>();

        /// <summary>Whether a player can be locked on: anyone playable, still in the match and in a body.</summary>
        public static bool Strikeable(SimPlayer q) => q != null && (q.Role == Role.Hunter || q.Role == Role.Survivor) && !q.Ghost
            && q.Health != Health.Eliminated && q.Health != Health.Escaped;

        /// <summary>
        /// Calls the strike down on <paramref name="targetId"/> for <paramref name="ownerId"/> (confirmed on their tablet):
        /// the tablet stays theirs and recharges for two minutes; false if they've none, it's recharging, they can't use
        /// one, or the target can't be struck.
        /// </summary>
        public bool CallStrike(int ownerId, int targetId)
        {
            SimPlayer p = Get(ownerId), q = Get(targetId);
            if (p == null || p.Role != Role.Survivor || !p.CanAct || p.SilencedT > 0f || p.StrikeCd > 0f || !Strikeable(q)) return false;
            if (p.Inv.FirstSlotOf(ItemType.OrbitalStrike) < 0) return false;
            p.StrikeCd = Balance.Items.Orbital.Cooldown;
            LaunchStrike(p, q);
            return true;
        }

        /// <summary>A survivor slain (sacrificed, lost in the woods): their K.P.O.S. drops where they fell, for anyone to take.</summary>
        void DropStrikeTablet(SimPlayer p)
        {
            int slot = p.Inv.FirstSlotOf(ItemType.OrbitalStrike);
            if (slot < 0) return;
            float amount = p.Inv.TakeOne(slot, out _, out _);
            PlaceDrop(new DropItem { Id = AllocEntityId(), Pos = p.Pos, Item = ItemType.OrbitalStrike, Amount = amount }, p.Pos);
            Feed($"{p.Name}'s K.P.O.S. lies where they fell");
        }

        /// <summary>
        /// Everything that can be destroyed within the beam goes at once: doors (not shutters), windows, pallets, the
        /// graveyard's gates once unlocked (locked, nothing breaks them), and gas traps (they burst).
        /// </summary>
        void StrikeDestroys(Vector2 at, float r, SimPlayer owner)
        {
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                SimMap.DoorDef d = Map.Doors[i];
                if (DoorBroken[i] || d.Shutter || DoorLocked[i] || SimMap.SegmentDistance(at, d.A, d.B) > r) continue;
                SetDoor(i, true);
                DoorBroken[i] = true;
                Noise((d.A + d.B) * 0.5f, 900f, d.Gate ? "gate_smash" : "door_smash");
            }
            for (int i = 0; i < Map.Windows.Count; i++)
            {
                if (WindowsBroken[i] || SimMap.SegmentDistance(at, Map.Windows[i].A, Map.Windows[i].B) > r) continue;
                BreakWindow(i);
                Noise((Map.Windows[i].A + Map.Windows[i].B) * 0.5f, 800f, "glass");
            }
            for (int i = 0; i < Map.Barricades.Count; i++)
            {
                if (Barricades[i] == BarricadeState.Broken || SimMap.SegmentDistance(at, Map.Barricades[i].A, Map.Barricades[i].B) > r) continue;
                SetBarricade(i, BarricadeState.Broken);
                Noise(Map.Barricades[i].Pos, 700f, "smash");
            }
            for (int k = Traps.Count - 1; k >= 0; k--)
                if (Vector2.Distance(Traps[k].Pos, at) <= r) BurstTrap(k, owner != null ? owner.Name : "The K.P.O.S.");
        }

        /// <summary>Starts a strike's countdown on <paramref name="q"/>, called by <paramref name="p"/> (yourself, in testing).</summary>
        void LaunchStrike(SimPlayer p, SimPlayer q)
        {
            var s = new OrbitalStrike { Id = AllocEntityId(), Owner = p.Id, Target = q.Id, Pos = q.Pos, Countdown = Balance.Items.Orbital.Countdown };
            Strikes.Add(s);
            Emit(null, new GameEvent { Kind = EventKind.Talk, A = p.Id, B = q.Id, Text = "strike_call", Pos = q.Pos });
            Feed(p == q ? $"{p.Name} called a K-pop orbital strike on themselves" : $"{p.Name} called a K-pop orbital strike on {q.Name}");
        }

        void UpdateStrikes(float dt)
        {
            foreach (SimPlayer p in Order)
            {
                if (p.SilencedT > 0f) p.SilencedT = Mathf.Max(0f, p.SilencedT - dt);
                if (p.StrikeCd > 0f) p.StrikeCd = Mathf.Max(0f, p.StrikeCd - dt);
            }
            float r = R(Balance.Items.Orbital.Radius);
            for (int k = Strikes.Count - 1; k >= 0; k--)
            {
                OrbitalStrike s = Strikes[k];
                SimPlayer target = Get(s.Target);
                // Following the target with a lag, never left further behind than most of its own width.
                if (target != null && Strikeable(target))
                {
                    s.Pos = Vector2.Lerp(s.Pos, target.Pos, 1f - Mathf.Exp(-Balance.Items.Orbital.Follow * dt));
                    Vector2 off = s.Pos - target.Pos;
                    float max = r * Balance.Items.Orbital.MaxLag;
                    if (off.magnitude > max) s.Pos = target.Pos + off.normalized * max;
                }
                if (s.Countdown > 0f)
                {
                    s.Countdown -= dt;
                    if (s.Countdown <= 0f)
                    {
                        s.Countdown = 0f;
                        // Down it comes: its track starts with it.
                        Emit(Near(s.Pos, Balance.Items.Orbital.HearRadius), new GameEvent { Kind = EventKind.Talk, A = s.Owner, B = s.Target, Text = "strike", Pos = s.Pos });
                    }
                    continue;
                }
                s.T += dt;
                s.TickAcc += dt * Balance.Items.Orbital.TickRate;
                int ticks = 0;
                while (s.TickAcc >= 1f) { s.TickAcc -= 1f; ticks++; }
                SimPlayer owner = Get(s.Owner);
                StrikeDestroys(s.Pos, r, owner);
                foreach (SimPlayer q in Order)
                {
                    if (!Strikeable(q) || Vector2.Distance(q.Pos, s.Pos) > r + q.RadiusD) continue;
                    // Slowed and silenced while it's on them.
                    q.SilencedT = Mathf.Max(q.SilencedT, Balance.Items.Orbital.Silence);
                    q.Move.SlowT = Mathf.Max(q.Move.SlowT, 0.2f);
                    q.Move.SlowMul = Mathf.Min(q.Move.SlowMul > 0f ? q.Move.SlowMul : 1f, Balance.Items.Orbital.SlowMul);
                    if (q.Role == Role.Hunter)
                    {
                        q.AbilityLockT = Mathf.Max(q.AbilityLockT, Balance.Items.Orbital.Silence);
                        if (q.HempOn) q.HempOn = false;
                    }
                    for (int i = 0; i < ticks; i++)
                    {
                        if (q.Role == Role.Hunter) HurtHunter(q, Balance.Items.Orbital.ZachTickDamage * Balance.Hunter.Health.Max, owner, "orbital");
                        else HurtSurvivor(q, Balance.Items.Orbital.TickDamage, owner, "orbital");
                    }
                }
                if (s.T >= Balance.Items.Orbital.Duration) Strikes.RemoveAt(k);
            }
        }
    }
}
