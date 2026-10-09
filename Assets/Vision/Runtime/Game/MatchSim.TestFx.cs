using UnityEngine;

namespace Vision.Game
{
    /// <summary>The testing-mode effects panel: each button plays a stun or flash on yourself (the original's testFx.ts).</summary>
    public enum TestFx : byte { Scare, Book, Waz, Stun, Blast, Gas, Down, Vape }

    public sealed partial class MatchSim
    {
        public static readonly (TestFx fx, string label)[] TestFxButtons =
        {
            (TestFx.Scare, "Soundcloud Burst scare"),
            (TestFx.Book, "Grapes of Wrath flash"),
            (TestFx.Waz, "Waz slain flash"),
            (TestFx.Stun, "Stun (bottle)"),
            (TestFx.Blast, "Shotgun blast"),
            (TestFx.Gas, "Galaxy gas"),
            (TestFx.Down, "Knocked down"),
            (TestFx.Vape, "Penjamin gas"),
        };

        /// <summary>Puts the NPCs back where they started (testing). The NPC framework sets it.</summary>
        public System.Action<MatchSim> NpcSpawner;

        /// <summary>Testing mode: plays a stun or flash effect on a player, as if it had really happened.</summary>
        public void PlayTestFx(int id, TestFx fx)
        {
            SimPlayer p = Get(id);
            if (!TestMode || p == null || p.Role == Role.Spectator) return;
            bool zach = p.Role == Role.Hunter;
            // Testing is about seeing the effect: stun immunity never gets in the way.
            p.ImmuneT = 0f;
            switch (fx)
            {
                case TestFx.Scare:
                    p.ScareT = Balance.Hunter.Burst.ScareTime;
                    Emit(p.Id, new GameEvent { Kind = EventKind.Scare });
                    break;
                case TestFx.Book:
                    if (zach) BookHit(p, null);
                    else
                    {
                        Emit(p.Id, new GameEvent { Kind = EventKind.Book, A = Rng.Next(Balance.Items.Book.Images) });
                        Emit(p.Id, new GameEvent { Kind = EventKind.Boom, Pos = p.Pos, F = 1400f });
                    }
                    break;
                case TestFx.Waz:
                    Emit(p.Id, new GameEvent { Kind = EventKind.WazSlain });
                    break;
                case TestFx.Stun:
                    if (zach) StunHunter(p, Balance.Items.Bottle.Stun, "bottle");
                    else
                    {
                        p.StunT = Balance.Items.Bottle.Stun;
                        Emit(null, new GameEvent { Kind = EventKind.Stun, A = p.Id, Text = "bottle", F = p.StunT });
                    }
                    break;
                case TestFx.Blast:
                {
                    // A shotgun blast from in front: knocked back (and Zach stunned).
                    p.Move.KbT = Balance.Items.Shotgun.KbDuration;
                    p.Move.KbDur = Balance.Items.Shotgun.KbDuration;
                    p.Move.KbPeak = Balance.Items.Shotgun.KbPeak;
                    p.Move.KbAng = p.Facing + Mathf.PI;
                    p.KnockVersion++;
                    if (zach) StunHunter(p, Balance.Items.Shotgun.Stun, "shotgun");
                    Vector2 from = p.Pos + p.FacingDir * R(120f);
                    var pellets = new string[Balance.Items.Shotgun.Pellets];
                    for (int i = 0; i < pellets.Length; i++) pellets[i] = Mathf.RoundToInt((p.Facing + Mathf.PI + (i - 3.5f) * 0.03f) * 1000f) + ":" + 110;
                    Emit(Near(p.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Shot, A = 0, B = (int)Vision.Player.ItemType.Shotgun, Pos = from, F = p.Facing + Mathf.PI, Text = "hit|" + string.Join(",", pellets) });
                    break;
                }
                case TestFx.Gas:
                    Gases.Add(new GasCloud { Id = AllocEntityId(), Pos = p.Pos });
                    Emit(Near(p.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Gas, Pos = p.Pos });
                    break;
                case TestFx.Vape:
                    // Zach: a cloud from where he stands; a survivor: caught in one, point blank.
                    if (zach) SpawnVape(p.Pos, p.Facing, R(Balance.Hunter.Vape.DefaultView * Balance.Hunter.Vape.ReachMul), p.Id, false);
                    else SpawnVape(p.Pos - p.FacingDir * R(420f), p.Facing, R(900f), 0, false);
                    break;
                case TestFx.Down:
                    // Zach knocked out cold (no lasting slowdown); a survivor flinches.
                    if (zach) DownHunter(p, false);
                    else Emit(null, new GameEvent { Kind = EventKind.Hit, A = p.Id, B = 0, Pos = p.Pos, F = 0f, Text = "punch" });
                    break;
            }
        }

        /// <summary>The inventory editor (Tab): swaps two slots of a survivor's inventory.</summary>
        public void MoveSlot(int id, int from, int to)
        {
            SimPlayer p = Get(id);
            if (p == null || p.Role != Role.Survivor) return;
            int lim = p.Inv.Limit;
            if (from < 0 || to < 0 || from >= lim || to >= lim || from == to) return;
            p.Inv.Move(from, to);
        }

        /// <summary>Dummies take ids from here up (players are 1-99).</summary>
        public const int FirstDummyId = 100;

        /// <summary>
        /// Testing: an inert player to practise on (a survivor to down, carry, stake, heal or revive; a Zach to stun). It
        /// stands where it is put and never acts.
        /// </summary>
        public SimPlayer AddDummy(Role role, Vector2 at)
        {
            if (!TestMode || role == Role.Spectator) return null;
            int id = FirstDummyId;
            while (Players.ContainsKey(id)) id++;
            SimPlayer p = AddPlayer(id, role == Role.Hunter ? "Dummy Zach" : $"Dummy {id - FirstDummyId + 1}", role, at);
            p.IsDummy = true;
            return p;
        }

        public void ClearDummies()
        {
            foreach (SimPlayer p in Order.ToArray())
                if (p.IsDummy)
                {
                    foreach (SimPlayer q in Order)
                    {
                        if (q.Carrying == p.Id) q.Carrying = 0;
                        if (q.CarriedBy == p.Id) { q.CarriedBy = 0; RestoreSurvivor(q, Balance.Survivor.ReviveHp); }
                    }
                    if (p.StakeId >= 0 && p.StakeId < Stakes.Length && Stakes[p.StakeId] == p.Id) Stakes[p.StakeId] = 0;
                    if (p.HideSpot >= 0 && p.HideSpot < Hiding.Length && Hiding[p.HideSpot] == p.Id) Hiding[p.HideSpot] = 0;
                    Players.Remove(p.Id);
                    Order.Remove(p);
                }
        }

        /// <summary>
        /// Moves the players nobody plays (dummies) as their client would: no input, so they only move when the rules push
        /// them (knockback). Runs before each tick.
        /// </summary>
        public void StepDummies()
        {
            foreach (SimPlayer p in Order)
            {
                if (!p.IsDummy || p.Health == Health.Carried || p.Health == Health.Staked || p.HideState != 0) continue;
                p.Move.Mode = MoveModeFor(p);
                var cmd = new InputCmd { Aim = p.Facing };
                Vector2 d = Movement.Step(p.Move, p.Pos, cmd, MoveContextFor(p, false), Geo, TickDt, out Gait gait);
                if (d.sqrMagnitude > 0f && !Geo.Blocked(p.Pos + d, p.RadiusD * 0.8f)) p.Pos += d;
                p.Gait = gait;
            }
        }

        /// <summary>Testing: every NPC back to its start.</summary>
        public void RespawnNpcs()
        {
            if (!TestMode) return;
            Npcs.Clear();
            NpcSpawner?.Invoke(this);
        }
    }
}
