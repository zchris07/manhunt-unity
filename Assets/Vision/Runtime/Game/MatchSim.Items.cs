using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>A thrown bottle, book or jar of piss in flight (design units).</summary>
    public sealed class ThrownItem
    {
        public int Id;
        public Vector2 Pos, Dir;
        public float Travelled;
        public int Owner;
        public ItemType Item;
    }

    public sealed class Trap
    {
        public int Id;
        public Vector2 Pos;
        public int Owner;
        public float ArmT;
    }

    public sealed class GasCloud
    {
        public int Id;
        public Vector2 Pos;
        public float Age;
    }

    /// <summary>Items: using, throwing, shooting, traps and gas, drops (the original's items.ts).</summary>
    public sealed partial class MatchSim
    {
        public readonly List<ThrownItem> Thrown = new List<ThrownItem>();
        public readonly List<Trap> Traps = new List<Trap>();
        public readonly List<GasCloud> Gases = new List<GasCloud>();
        readonly List<int> bookDeck = new List<int>();
        int lastBookImg = -1;

        float RandRange(float a, float b) => a + (float)Rng.NextDouble() * (b - a);

        /// <summary>The next Grapes of Wrath picture: a shuffled cycle, never the same twice in a row.</summary>
        public int NextBookImage()
        {
            int n = Balance.Items.Book.Images;
            if (bookDeck.Count == 0)
            {
                for (int i = 0; i < n; i++) bookDeck.Add(i);
                for (int i = n - 1; i > 0; i--)
                {
                    int j = Rng.Next(i + 1);
                    (bookDeck[i], bookDeck[j]) = (bookDeck[j], bookDeck[i]);
                }
                if (n > 1 && bookDeck[bookDeck.Count - 1] == lastBookImg)
                {
                    bookDeck.RemoveAt(bookDeck.Count - 1);
                    bookDeck.Insert(0, lastBookImg);
                }
            }
            lastBookImg = bookDeck[bookDeck.Count - 1];
            bookDeck.RemoveAt(bookDeck.Count - 1);
            return lastBookImg;
        }

        void InterruptHunter(SimPlayer h)
        {
            if (h.Action != ActionKind.None) CancelAction(h);
            h.AttackWindup = 0f;
            h.ChargeT = -1f;
            h.Move.LungeT = 0f;
            if (h.Carrying != 0) DropCarried(h);
        }

        /// <summary>
        /// Stuns Zach (he can never be killed). After each stun he is immune for a while so survivors cannot chain-stun him.
        /// Returns false if he is immune. <paramref name="exact"/>: this long whatever the lobby's stun scaling.
        /// </summary>
        public bool StunHunter(SimPlayer h, float seconds, string kind, SimPlayer by = null, bool exact = false)
        {
            if (h.Role != Role.Hunter || h.Health == Health.Eliminated) return false;
            if (by != null && by.Role == Role.Survivor) PlayerAttacked(h, by);
            if (h.ImmuneT > 0f) return false;
            float dur = seconds * (exact ? 1f : Bal.StunMul);
            h.StunT = Mathf.Max(h.StunT, dur);
            h.ImmuneT = dur + Balance.Items.StunImmunity;
            InterruptHunter(h);
            h.Stats.StunnedTimes++;
            if (by != null) by.Stats.Stuns++;
            Emit(null, new GameEvent { Kind = EventKind.Stun, A = h.Id, Text = kind, F = dur });
            return true;
        }

        /// <summary>Slams a standing barricade down across its gap, stunning Zach if he is in it.</summary>
        public void DropBarricade(SimPlayer p, int bi)
        {
            if (bi < 0 || Barricades[bi] != BarricadeState.Up) return;
            SimMap.BarricadeDef b = Map.Barricades[bi];
            var stunned = new List<SimPlayer>();
            foreach (SimPlayer h in Order)
            {
                if (h.Role != Role.Hunter) continue;
                if (SimMap.SegmentDistance(h.Pos, b.A, b.B) < R(Balance.Items.Barricade.SlamRadius) + h.RadiusD) stunned.Add(h);
            }
            SetBarricade(bi, BarricadeState.Down);
            BarricadeHits[bi] = 0;
            Noise(b.Pos, 700f, "barricade");
            Emit(Near(b.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "slam", Pos = b.Pos });
            foreach (SimPlayer h in stunned)
                if (StunHunter(h, Balance.Items.Barricade.Stun, "barricade", p)) Feed($"{p.Name} slammed a barricade on {h.Name}");
        }

        /// <summary>Spends one round of a weapon slot; an empty weapon is gone.</summary>
        void SpendRound(SimPlayer p, int slot)
        {
            if (TestMode) return;
            Inventory.Slot s = p.Inv.SlotAt(slot);
            float left = p.Inv.AmountAt(slot) - 1f;
            p.Inv.SetAmountAt(slot, left);
            if (left > 0f) return;
            Tell(p, $"{Items.Name(s.item, s.golden)} empty");
            p.Inv.Remove(slot);
        }

        /// <summary>Uses up one unit of a slot (testing mode never runs out).</summary>
        void Consume(SimPlayer p, int slot) => p.Inv.Remove(slot);

        /// <summary>Left click: use the item in the selected slot.</summary>
        void UseItem(SimPlayer p, InputCmd cmd) => UseItemAt(p, cmd, p.SelSlot);

        /// <summary>Uses the item in <paramref name="slot"/> (the one in hand, or a quick key's); false if it couldn't be.</summary>
        bool UseItemAt(SimPlayer p, InputCmd cmd, int slot)
        {
            Inventory.Slot s = p.Inv.SlotAt(slot);
            if (s == null || s.Empty || p.Action != ActionKind.None || !p.CanAct) return false;
            switch (s.item)
            {
                case ItemType.Bottle:
                case ItemType.Piss:
                case ItemType.Book:
                {
                    Vector2 d = cmd.AimDir;
                    Thrown.Add(new ThrownItem { Id = AllocEntityId(), Pos = p.Pos + d * (p.RadiusD + R(4f)), Dir = d, Owner = p.Id, Item = s.item });
                    Emit(Near(p.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Throw, A = p.Id, B = (int)s.item, Pos = p.Pos, F = cmd.Aim });
                    Consume(p, slot);
                    break;
                }
                case ItemType.Shotgun:
                    if (p.ReloadT > 0f) return false;
                    FireShotgun(p, cmd.Aim, Balance.Items.Shotgun.PelletDamage, s.golden);
                    p.ReloadT = s.golden ? Balance.Items.Golden.Reload : Balance.Items.Shotgun.Reload;
                    SpendRound(p, slot);
                    break;
                case ItemType.Sniper:
                    if (p.ReloadT > 0f) return false;
                    FireSniper(p, cmd.Aim);
                    p.ReloadT = Balance.Items.Sniper.Reload;
                    SpendRound(p, slot);
                    break;
                case ItemType.HempBeam:
                    FireBeamItem(p, slot, cmd.Aim);
                    break;
                case ItemType.Pistol:
                    if (p.ReloadT > 0f) return false;
                    FirePistol(p, cmd.Aim);
                    p.ReloadT = Balance.Items.Pistol.Reload;
                    SpendRound(p, slot);
                    break;
                case ItemType.DoctorPepper:
                    UseSlow(p);
                    p.Move.BoostT = Balance.Items.Energy.Duration;
                    // The (now longer) sprint meter fills up at once.
                    p.Move.Stamina = MoveState.MaxStamina(Role.Survivor, p.Move.BoostT);
                    p.Move.StaminaLock = 0f;
                    p.Move.SprintBlocked = false;
                    p.BoostVersion++;
                    Consume(p, slot);
                    Tell(p, "Doctor Pepper");
                    Emit(Near(p.Pos, 600f), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "drink", Pos = p.Pos });
                    break;
                case ItemType.Confit:
                    if (p.Hp >= 0.999f)
                    {
                        Tell(p, "Already at full health");
                        return false;
                    }
                    UseSlow(p);
                    RestoreSurvivor(p, 1f);
                    Consume(p, slot);
                    Tell(p, "Duck confit: healed to full");
                    Emit(Near(p.Pos, 600f), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "eat", Pos = p.Pos });
                    break;
                case ItemType.MrBeastBar:
                    if (p.Hp >= 0.999f)
                    {
                        Tell(p, "Already at full health");
                        return false;
                    }
                    UseSlow(p);
                    RestoreSurvivor(p, p.Hp + Balance.Items.BeastBarHeal);
                    Consume(p, slot);
                    Tell(p, "Mr Beast bar: +20% health");
                    Emit(Near(p.Pos, 600f), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "eat", Pos = p.Pos });
                    break;
                case ItemType.MiniShield:
                    if (p.Shield >= Balance.Items.ShieldMax - 1e-6f)
                    {
                        Tell(p, "Shield is full");
                        return false;
                    }
                    StartAction(p, ActionKind.Drink, Balance.Items.ShieldDrinkTime, 0);
                    break;
                case ItemType.Trap:
                    StartAction(p, ActionKind.Plant, Balance.Items.Trap.PlantTime, 0);
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>Healing and boost items can be used on the move, at half speed for a moment.</summary>
        public void UseSlow(SimPlayer p, float seconds = Balance.Items.UseSlowTime)
        {
            p.Move.SlowT = Mathf.Max(p.Move.SlowT, seconds);
            p.Move.SlowMul = Mathf.Min(p.Move.SlowMul > 0f ? p.Move.SlowMul : 1f, Balance.Items.UseSlow);
        }

        /// <summary>The drink finished: a quarter bar of shield (up to a full extra bar).</summary>
        void DrinkShield(SimPlayer p)
        {
            int slot = p.Inv.FirstSlotOf(ItemType.MiniShield);
            if (slot < 0 || p.Role != Role.Survivor) return;
            p.Shield = Mathf.Min(Balance.Items.ShieldMax, p.Shield + Balance.Items.ShieldAmount);
            Consume(p, slot);
            Tell(p, $"Shield {Mathf.RoundToInt(p.Shield * 100f)}%");
        }

        /// <summary>The plant finished: the trap goes down.</summary>
        void PlantTrap(SimPlayer p)
        {
            int slot = p.Inv.FirstSlotOf(ItemType.Trap);
            if (slot < 0) return;
            Traps.Add(new Trap { Id = AllocEntityId(), Pos = p.Pos, Owner = p.Id, ArmT = Balance.Items.Trap.ArmTime });
            Consume(p, slot);
            Tell(p, "Trap planted");
        }

        // ---------------------------------------------------------------- rays

        /// <summary>Distance along a ray (unit dir) to a circle, or +inf.</summary>
        public static float RayCircle(Vector2 o, Vector2 d, Vector2 c, float r)
        {
            Vector2 m = o - c;
            float b = Vector2.Dot(m, d), cc = Vector2.Dot(m, m) - r * r;
            if (cc <= 0f) return 0f;
            if (b > 0f) return float.PositiveInfinity;
            float disc = b * b - cc;
            if (disc < 0f) return float.PositiveInfinity;
            return -b - Mathf.Sqrt(disc);
        }

        /// <summary>Distance along a ray (unit dir) to a segment, or +inf.</summary>
        public static float RaySegment(Vector2 o, Vector2 d, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float denom = d.x * e.y - d.y * e.x;
            if (Mathf.Abs(denom) < 1e-9f) return float.PositiveInfinity;
            Vector2 w = a - o;
            float t = (w.x * e.y - w.y * e.x) / denom, s = (w.x * d.y - w.y * d.x) / denom;
            if (t < 0f || s < -1e-6f || s > 1f + 1e-6f) return float.PositiveInfinity;
            return t;
        }

        /// <summary>Distance along a ray to the nearest unbroken window, and which (or -1).</summary>
        float WindowHit(Vector2 o, Vector2 d, float max, out int which)
        {
            float best = max;
            which = -1;
            for (int i = 0; i < Map.Windows.Count; i++)
            {
                if (WindowsBroken[i]) continue;
                float t = RaySegment(o, d, Map.Windows[i].A, Map.Windows[i].B);
                if (t < best) { best = t; which = i; }
            }
            return best;
        }

        bool Shootable(SimPlayer q, SimPlayer shooter, bool hunters)
        {
            if (q == shooter) return false;
            if (q.Role == Role.Hunter) return hunters && q.Health != Health.Eliminated;
            // A body a ghost is in: only Zach's golden pump reaches it (a single pellet knocks the ghost out).
            if (InBody(q)) return shooter != null && shooter.Role == Role.Hunter;
            return q.Role == Role.Survivor && (q.Health == Health.Healthy || q.Health == Health.Wounded);
        }

        static void Knock(SimPlayer q, float dur, float peak, float ang)
        {
            q.Move.KbT = dur;
            q.Move.KbDur = dur;
            q.Move.KbPeak = peak;
            q.Move.KbAng = ang;
            q.Move.LungeT = 0f;
            q.KnockVersion++;
        }

        /// <summary>Jaden's pistol in a survivor's hands: one bullet, straight down the sights.</summary>
        void FirePistol(SimPlayer p, float aim)
        {
            float a = aim + RandRange(-1f, 1f) * Balance.Items.Pistol.SpreadDeg * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 s = p.Pos + d * (p.RadiusD + R(2f));
            float range = R(Balance.Items.Pistol.Range);
            float best = Mathf.Min(range, Geo.CastSight(s, d, range));
            // The round goes through glass, breaking it (a survivor can climb through the frame afterwards).
            for (int guard = 0; guard < 8; guard++)
            {
                float t = WindowHit(s, d, best, out int win);
                if (win < 0) break;
                BreakWindow(win);
                Noise(s + d * t, 800f, "glass");
            }
            SimPlayer hitP = null;
            Npc hitN = null;
            foreach (SimPlayer q in Order)
            {
                if (!Shootable(q, p, true)) continue;
                float t = RayCircle(s, d, q.Pos, q.RadiusD);
                if (t < best) { best = t; hitP = q; }
            }
            foreach (Npc n in Npcs)
            {
                if (!n.Solid || !CanShoot(n, p)) continue;
                float t = RayCircle(s, d, n.Pos, n.HitRadius);
                if (t < best) { best = t; hitN = n; hitP = null; }
            }
            HitTraps(s, d, best, p.Name);
            Emit(Near(p.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Shot, A = p.Id, B = (int)ItemType.Pistol, Pos = p.Pos, F = a, G = best + p.RadiusD + R(2f), Text = hitP != null ? "hit" : "" });
            Noise(p.Pos, 1000f, "shot");
            if (hitN != null) BulletStruck(hitN.Pos);
            else if (hitP != null) BulletStruck(hitP.Pos);
            if (hitN != null) hitN.ItemHit(this, p, "shot");
            else if (hitP != null && hitP.Role == Role.Hunter)
            {
                HurtHunter(hitP, Balance.Items.Pistol.ZachDamage * MatchState.Current.Tuning.PistolDamage, p, "bullet");
                Feed($"{p.Name} shot {hitP.Name}");
            }
            else if (hitP != null) HurtSurvivor(hitP, Balance.Items.Pistol.Damage * MatchState.Current.Tuning.PistolDamage, p, "bullet");
            if (hitP != null)
            {
                float away = Mathf.Atan2(hitP.Pos.y - p.Pos.y, hitP.Pos.x - p.Pos.x);
                if (hitP.Role == Role.Hunter) StunHunter(hitP, Balance.Items.Pistol.Stun, "bullet", p, true);
                else hitP.StunT = Mathf.Max(hitP.StunT, Balance.Items.Pistol.Stun);
                Knock(hitP, Balance.Items.Pistol.KbDuration, Balance.Items.Pistol.KbPeak, away);
            }
        }

        /// <summary>Zach's golden pump (it replaces his machete until the shots run out).</summary>
        void FireZachPump(SimPlayer h, float aim)
        {
            if (h.Pump <= 0 || h.ReloadT > 0f || !h.CanAct || h.Carrying != 0 || h.Action != ActionKind.None) return;
            FireShotgun(h, aim, Balance.Items.ZachPump.PelletDamage);
            h.ReloadT = Balance.Items.ZachPump.Reload;
            if (!TestMode) h.Pump--;
            if (h.Pump <= 0) Tell(h, "Golden pump empty");
        }

        /// <summary>Eight pellets with random bloom; each flies on until it hits something solid or someone. Glass shatters and lets it through.</summary>
        void FireShotgun(SimPlayer p, float aim, float pelletDamage, bool golden = false)
        {
            bool zachGun = p.Role == Role.Hunter;
            // The lobby's shotgun or golden pump damage slider (Zach's golden pump with the Legendary pump).
            float gunMul = zachGun || golden ? MatchState.Current.Tuning.GoldenDamage : MatchState.Current.Tuning.ShotgunDamage;
            pelletDamage *= gunMul;
            Vector2 o = p.Pos;
            var pellets = new List<float>();
            var onSurvivor = new Dictionary<SimPlayer, int>();
            var npcs = new HashSet<Npc>();
            SimPlayer zach = null;
            int onZach = 0;
            float range = R(Balance.Items.Shotgun.Range);
            for (int i = 0; i < Balance.Items.Shotgun.Pellets; i++)
            {
                float a = aim + RandRange(-1f, 1f) * Balance.Items.Shotgun.SpreadDeg * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 s = o + d * (p.RadiusD + R(2f));
                float wall = Mathf.Min(Geo.CastSight(s, d, range), range);
                for (int guard = 0; guard < 8; guard++)
                {
                    float t = WindowHit(s, d, wall, out int win);
                    if (win < 0) break;
                    BreakWindow(win);
                    Noise(s + d * t, 800f, "glass");
                }
                // A shut door in the way is blown in.
                float best = PelletDoors(s, d, wall);
                SimPlayer hitP = null;
                Npc hitN = null;
                foreach (SimPlayer q in Order)
                {
                    if (!Shootable(q, p, !zachGun)) continue;
                    float t = RayCircle(s, d, q.Pos, q.RadiusD);
                    if (t < best) { best = t; hitP = q; hitN = null; }
                }
                foreach (Npc n in Npcs)
                {
                    if (!n.Solid || !CanShoot(n, p)) continue;
                    float t = RayCircle(s, d, n.Pos, n.HitRadius);
                    if (t < best) { best = t; hitN = n; hitP = null; }
                }
                HitTraps(s, d, best, p.Name);
                pellets.Add(a);
                pellets.Add(best + p.RadiusD + R(2f));
                if (hitN != null) npcs.Add(hitN);
                else if (hitP != null && hitP.Role == Role.Hunter) { zach = hitP; onZach++; }
                else if (hitP != null) onSurvivor[hitP] = (onSurvivor.TryGetValue(hitP, out int n0) ? n0 : 0) + 1;
            }
            bool hit = zach != null || onSurvivor.Count > 0;
            LastPellets = pellets.ToArray();
            var text = new System.Text.StringBuilder(hit ? "hit|" : "|");
            for (int i = 0; i + 1 < pellets.Count; i += 2)
            {
                if (i > 0) text.Append(',');
                text.Append(Mathf.RoundToInt(pellets[i] * 1000f)).Append(':').Append(Mathf.RoundToInt(Scale.ToUnits(pellets[i + 1])));
            }
            Emit(Near(o, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Shot, A = p.Id, B = (int)ItemType.Shotgun, Pos = o, F = aim, G = zachGun || golden ? 1f : 0f, Text = text.ToString() });
            Noise(o, 1100f, "shot");
            foreach (SimPlayer q in onSurvivor.Keys) BulletStruck(q.Pos);
            if (zach != null) BulletStruck(zach.Pos);
            foreach (Npc n in new HashSet<Npc>(npcs)) BulletStruck(n.Pos);
            foreach (var kv in onSurvivor)
            {
                SimPlayer q = kv.Key;
                if (InBody(q))
                {
                    KnockOut(q, p, "pellet");
                    continue;
                }
                if (zachGun)
                {
                    q.StunT = Mathf.Max(q.StunT, Balance.Items.ZachPump.Stun);
                    Knock(q, Balance.Items.ZachPump.KbDuration, Balance.Items.ZachPump.KbPeak, Mathf.Atan2(q.Pos.y - o.y, q.Pos.x - o.x));
                }
                HurtSurvivor(q, kv.Value * pelletDamage, p, "pellet");
            }
            foreach (Npc n in npcs) n.ItemHit(this, p, "shot");
            if (zach == null) return;
            // The blast always shoves Zach back; the stun respects his immunity.
            Knock(zach, Balance.Items.Shotgun.KbDuration, Balance.Items.Shotgun.KbPeak, Mathf.Atan2(zach.Pos.y - o.y, zach.Pos.x - o.x));
            if (StunHunter(zach, Balance.Items.Shotgun.Stun, "shotgun", p)) Feed($"{p.Name} blasted {zach.Name} with a {(golden ? "golden pump" : "shotgun")}");
            HurtHunter(zach, onZach * Balance.Items.Shotgun.ZachBlastDamage * gunMul / Balance.Items.Shotgun.Pellets, p, "pellet");
        }

        /// <summary>
        /// An NPC's shotgun blast along <paramref name="aim"/> (Daphne's, endless shells): a survivor's shotgun, its pellets,
        /// spread, reach and damage, striking players only; Zach is shoved and stunned as by a survivor's blast.
        /// </summary>
        public void NpcShotgun(Npc by, float aim)
        {
            Vector2 o = by.Pos;
            var pellets = new List<float>();
            var onSurvivor = new Dictionary<SimPlayer, int>();
            SimPlayer zach = null;
            int onZach = 0;
            for (int i = 0; i < Balance.Items.Shotgun.Pellets; i++)
            {
                float a = aim + RandRange(-1f, 1f) * Balance.Items.Shotgun.SpreadDeg * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 s = o + d * (by.HitRadius + R(2f));
                float range = R(Balance.Items.Shotgun.Range);
                float best = Mathf.Min(range, Geo.CastSight(s, d, range));
                best = PelletDoors(s, d, best);
                SimPlayer hitP = null;
                foreach (SimPlayer q in Order)
                {
                    bool ok = q.Role == Role.Hunter ? q.Health != Health.Eliminated && q.KnockT <= 0f : q.Role == Role.Survivor && (q.Health == Health.Healthy || q.Health == Health.Wounded);
                    if (!ok) continue;
                    float t = RayCircle(s, d, q.Pos, q.RadiusD);
                    if (t < best) { best = t; hitP = q; }
                }
                pellets.Add(a);
                pellets.Add(best + by.HitRadius + R(2f));
                if (hitP != null && hitP.Role == Role.Hunter) { zach = hitP; onZach++; }
                else if (hitP != null) onSurvivor[hitP] = (onSurvivor.TryGetValue(hitP, out int n0) ? n0 : 0) + 1;
            }
            bool hit = zach != null || onSurvivor.Count > 0;
            var text = new System.Text.StringBuilder(hit ? "hit|" : "|");
            for (int i = 0; i + 1 < pellets.Count; i += 2)
            {
                if (i > 0) text.Append(',');
                text.Append(Mathf.RoundToInt(pellets[i] * 1000f)).Append(':').Append(Mathf.RoundToInt(Scale.ToUnits(pellets[i + 1])));
            }
            Emit(Near(o, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Shot, A = -by.Id, B = (int)ItemType.Shotgun, Pos = o, F = aim, G = 0f, Text = text.ToString() });
            Noise(o, 1100f, "shot");
            foreach (var kv in onSurvivor)
            {
                BulletStruck(kv.Key.Pos);
                HurtSurvivor(kv.Key, kv.Value * Balance.Items.Shotgun.PelletDamage, null, "pellet");
            }
            if (zach == null) return;
            BulletStruck(zach.Pos);
            Knock(zach, Balance.Items.Shotgun.KbDuration, Balance.Items.Shotgun.KbPeak, Mathf.Atan2(zach.Pos.y - o.y, zach.Pos.x - o.x));
            StunHunter(zach, Balance.Items.Shotgun.Stun, "shotgun");
            HurtHunter(zach, onZach * Balance.Items.Shotgun.ZachBlastDamage / Balance.Items.Shotgun.Pellets, null, "pellet");
        }

        /// <summary>A shotgun pellet meeting a shut door (not the graveyard's gates, not locked) blows it in; how far the pellet got.</summary>
        float PelletDoors(Vector2 s, Vector2 d, float best)
        {
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                if (Doors[i] || DoorBroken[i] || Map.Doors[i].Gate || Map.Doors[i].Shutter || DoorLocked[i]) continue;
                float t = RaySegment(s, d, Map.Doors[i].A, Map.Doors[i].B);
                if (float.IsPositiveInfinity(t) || t > best + R(6f)) continue;
                SetDoor(i, true);
                DoorBroken[i] = true;
                Noise(s + d * t, 900f, "door_smash");
                best = Mathf.Min(best, t);
            }
            return best;
        }

        /// <summary>The last shotgun blast's pellets (angle, length pairs), for the view.</summary>
        public float[] LastPellets;

        /// <summary>G: drop one of the selected item on the ground for a teammate.</summary>
        void DropItem(SimPlayer p)
        {
            Inventory.Slot s = p.Selected;
            if (s == null || !p.CanAct || p.Action != ActionKind.None) return;
            ItemType kind = s.item;
            bool golden = s.golden;
            float amount = TestMode ? (s.amounts.Count > 0 ? s.amounts[s.amounts.Count - 1] : 0f) : p.Inv.TakeOne(p.SelSlot, out _, out _);
            var d = new DropItem { Id = AllocEntityId(), Pos = p.Pos, Item = kind, Golden = golden, Amount = amount };
            PlaceDrop(d, p.Pos + p.FacingDir * R(26f));
            Tell(p, $"Dropped: {Items.Name(kind, golden)}");
        }

        /// <summary>
        /// Piss splashed over someone (a thrown jar, or the testing effect): a moment of it in their eyes and, on Zach, +50%
        /// damage taken, falling off linearly to nothing over fifteen seconds, on top of whatever's left of earlier splashes.
        /// </summary>
        public void SoakInPiss(SimPlayer h)
        {
            h.PissBlurT = Balance.Items.Piss.BlurTime;
            if (h.Role != Role.Hunter) return;
            var stacks = new float[h.PissStacks.Length + 1];
            System.Array.Copy(h.PissStacks, stacks, h.PissStacks.Length);
            stacks[stacks.Length - 1] = Balance.Items.Piss.Time;
            h.PissStacks = stacks;
            h.PissT = Balance.Items.Piss.Time;
            Tell(h, $"Soaked: +{Mathf.RoundToInt(PissBonus(h) * 100f)}% damage, wearing off over {Balance.Items.Piss.Time:0} s");
        }

        /// <summary>The extra damage the piss on someone adds (0.5 for a fresh splash, falling off linearly; splashes add up).</summary>
        public static float PissBonus(SimPlayer p)
        {
            float left = 0f;
            if (p.PissStacks != null) foreach (float t in p.PissStacks) left += Mathf.Max(0f, t);
            return (Balance.Items.Piss.Mul - 1f) * left / Balance.Items.Piss.Time;
        }

        /// <summary>Each splash wears off; the spent ones are dropped.</summary>
        static void TickPiss(SimPlayer p, float dt)
        {
            float[] s = p.PissStacks;
            if (s == null || s.Length == 0) { p.PissT = 0f; return; }
            int live = 0;
            float most = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                s[i] -= dt;
                if (s[i] <= 0f) continue;
                live++;
                most = Mathf.Max(most, s[i]);
            }
            if (live < s.Length)
            {
                var keep = new float[live];
                for (int i = 0, k = 0; i < s.Length; i++) if (s[i] > 0f) keep[k++] = s[i];
                p.PissStacks = keep;
            }
            p.PissT = most;
        }

        /// <summary>A Hemp Beam with all its charges, lying on the ground (Sexton's when he falls).</summary>
        public void DropHempBeam(Vector2 at) =>
            Drops.Add(new DropItem { Id = AllocEntityId(), Pos = at, Item = ItemType.HempBeam, Amount = Balance.Hunter.Beam.Charges });

        /// <summary>Puts a dropped item on the ground at the spot, or at the dropper's feet if that's blocked.</summary>
        public void PlaceDrop(DropItem d, Vector2 at)
        {
            if (!Geo.Blocked(at, R(12f)) && Geo.LineOfSight(d.Pos, at)) d.Pos = at;
            Drops.Add(d);
        }

        /// <summary>A survivor picks up a dropped item (a full inventory drops the last slot to make room).</summary>
        void PickUpDrop(SimPlayer p, int id)
        {
            int i = Drops.FindIndex(x => x.Id == id);
            if (i < 0) return;
            DropItem d = Drops[i];
            if (p.Role == Role.Hunter)
            {
                // A golden pump: its shells are his shots (it replaces his machete until they run out).
                if (d.Item == ItemType.Shotgun && d.Golden)
                {
                    Drops.RemoveAt(i);
                    p.Pump += Mathf.Max(1, Mathf.RoundToInt(d.Amount));
                    p.ChargeT = -1f;
                    Tell(p, $"Got a golden pump: {p.Pump} shots");
                    return;
                }
                // Otherwise Zach takes only a Hemp Beam: its charges left go into his own (R), up to a full set.
                if (d.Item != ItemType.HempBeam) return;
                Drops.RemoveAt(i);
                p.BeamCharges = Mathf.Min(Balance.Hunter.Beam.Charges, p.BeamCharges + Mathf.RoundToInt(d.Amount));
                Tell(p, $"Got the Hemp Beam: R, {p.BeamCharges} charges");
                return;
            }
            Drops.RemoveAt(i);
            AddItem(p, d.Item, d.Amount, d.Golden);
            Tell(p, $"Picked up: {Items.Name(d.Item, d.Golden)}");
        }

        void UpdateItems(float dt)
        {
            // Bottles, books and jars fly on until they hit a wall, a tree, a closed door, Zach, another survivor or an NPC;
            // one that meets a window smashes it and drops there (anyone can climb through the frame after).
            float maxFlight = Map.HalfExtent * 2.9f;
            for (int k = Thrown.Count - 1; k >= 0; k--)
            {
                ThrownItem b = Thrown[k];
                bool book = b.Item == ItemType.Book, piss = b.Item == ItemType.Piss;
                float speed = book ? Balance.Items.Book.Speed : piss ? Balance.Items.Piss.Speed : Balance.Items.Bottle.Speed;
                float hitRadius = R(book ? Balance.Items.Book.HitRadius : piss ? Balance.Items.Piss.HitRadius : Balance.Items.Bottle.HitRadius);
                float damage = piss ? 0f : book ? Balance.Items.Book.Damage : Balance.Items.Bottle.Damage;
                float zachDamage = piss ? 0f : book ? Balance.Items.Book.ZachDamage : Balance.Items.Bottle.ZachDamage;
                float step = R(speed) * dt;
                float wallFree = Geo.CastSight(b.Pos, b.Dir, step + R(1f));
                float glass = WindowHit(b.Pos, b.Dir, step + R(1f), out int window);
                float free = Mathf.Min(wallFree, glass);
                float move = Mathf.Min(step, free);
                if (window >= 0 && glass <= wallFree && glass <= step)
                {
                    // Through the glass: it shatters, and the item falls there.
                    b.Pos += b.Dir * glass;
                    BreakWindow(window);
                    Noise(b.Pos, 800f, "glass");
                    Noise(b.Pos, 900f, b.Item == ItemType.Book ? "book" : b.Item == ItemType.Piss ? "piss" : "glass");
                    Thrown.RemoveAt(k);
                    continue;
                }
                float best = move + hitRadius;
                SimPlayer hitP = null;
                Npc hitN = null;
                foreach (SimPlayer q in Order)
                {
                    if (q.Id == b.Owner) continue;
                    bool ok = q.Role == Role.Hunter ? q.Health != Health.Eliminated : q.Role == Role.Survivor && (q.Health == Health.Healthy || q.Health == Health.Wounded);
                    if (!ok) continue;
                    float t = RayCircle(b.Pos, b.Dir, q.Pos, q.RadiusD + hitRadius);
                    if (t < best) { best = t; hitP = q; }
                }
                foreach (Npc n in Npcs)
                {
                    if (!n.Solid) continue;
                    float t = RayCircle(b.Pos, b.Dir, n.Pos, n.HitRadius + hitRadius);
                    if (t < best) { best = t; hitN = n; hitP = null; }
                }
                float adv = hitP != null || hitN != null ? Mathf.Min(move, best) : move;
                SimPlayer owner = Get(b.Owner);
                string sound = book ? "book" : piss ? "piss" : "glass";
                // A bottle breaking on someone (a player or an NPC): a window's glass breaking, softer (MatchEffects).
                string onBody = book || piss ? sound : "glass_body";
                float onTrap = HitTraps(b.Pos, b.Dir, adv, owner?.Name ?? "Someone");
                if (onTrap <= adv)
                {
                    b.Pos += b.Dir * onTrap;
                    Noise(b.Pos, 900f, sound);
                    Thrown.RemoveAt(k);
                    continue;
                }
                b.Pos += b.Dir * adv;
                b.Travelled += adv;
                if (hitN != null)
                {
                    Noise(b.Pos, 900f, onBody);
                    if (owner != null) hitN.ItemHit(this, owner, "bottle");
                    Thrown.RemoveAt(k);
                    continue;
                }
                if (hitP != null && hitP.Role == Role.Hunter)
                {
                    Noise(b.Pos, 900f, onBody);
                    if (book) BookHit(hitP, owner);
                    else if (piss)
                    {
                        SoakInPiss(hitP);
                        Feed($"{owner?.Name ?? "Someone"} threw piss on {hitP.Name}");
                    }
                    else if (StunHunter(hitP, Balance.Items.Bottle.Stun, "bottle", owner)) Feed($"{owner?.Name ?? "Someone"} smashed a bottle on {hitP.Name}");
                    HurtHunter(hitP, zachDamage, owner, "bottle");
                    Thrown.RemoveAt(k);
                    continue;
                }
                if (hitP != null)
                {
                    Noise(b.Pos, 900f, onBody);
                    if (damage > 0f) HurtSurvivor(hitP, damage, owner, "bottle");
                    else Emit(null, new GameEvent { Kind = EventKind.Hit, A = hitP.Id, B = owner?.Id ?? 0, Pos = hitP.Pos, F = 0f, Text = "bottle" });
                    Thrown.RemoveAt(k);
                    continue;
                }
                if (free <= step || b.Travelled >= maxFlight)
                {
                    Noise(b.Pos, 900f, sound);
                    Thrown.RemoveAt(k);
                }
            }

            // Gas traps: arm, then burst into galaxy gas when Zach (or an alerted or raging NPC) comes close.
            float trigger = R(Balance.Items.Trap.TriggerRadius);
            for (int k = Traps.Count - 1; k >= 0; k--)
            {
                Trap t = Traps[k];
                t.ArmT = Mathf.Max(0f, t.ArmT - dt);
                if (t.ArmT > 0f) continue;
                string who = null;
                foreach (SimPlayer q in Order)
                    if (q.Role == Role.Hunter && q.Health != Health.Eliminated && Vector2.Distance(q.Pos, t.Pos) <= trigger) { who = q.Name; break; }
                if (who == null)
                    foreach (Npc n in Npcs)
                        if (n.TripsTraps && Vector2.Distance(n.Pos, t.Pos) <= trigger) { who = n.Name; break; }
                if (who == null) continue;
                BurstTrap(k, who);
            }
            foreach (SimPlayer p in Order) p.Gassed = false;
            for (int k = Gases.Count - 1; k >= 0; k--)
            {
                GasCloud g = Gases[k];
                g.Age += dt;
                float r = R(Balance.Items.Trap.GasRadius) * Mathf.Min(1f, g.Age / Balance.Items.Trap.SpreadTime);
                foreach (SimPlayer h in Order)
                {
                    if (h.Role != Role.Hunter || Vector2.Distance(h.Pos, g.Pos) > r) continue;
                    h.Gassed = true;
                    // None of his abilities while he's in it (his lunge, the Burst, Penjamin, the Hemp Battery and Beam).
                    h.AbilityLockT = Mathf.Max(h.AbilityLockT, Balance.Items.Trap.AbilityLock);
                    h.Move.SlowT = Mathf.Max(h.Move.SlowT, 0.2f);
                    h.Move.SlowMul = Mathf.Min(h.Move.SlowMul, Balance.Items.Trap.SlowMul);
                    HurtHunter(h, Balance.Items.Trap.ZachDps * dt, null, "gas");
                }
                foreach (Npc n in Npcs) if (n.Solid && Vector2.Distance(n.Pos, g.Pos) <= r) n.Gassed(this);
                if (g.Age >= Balance.Items.Trap.GasTime) Gases.RemoveAt(k);
            }

            foreach (SimPlayer p in Order)
            {
                p.PissBlurT = Mathf.Max(0f, p.PissBlurT - dt);
                TickPiss(p, dt);
                if (p.Role == Role.Hunter) p.BookT = Mathf.Max(0f, p.BookT - dt);
                if (p.Role != Role.Survivor) continue;
                p.ReloadT = Mathf.Max(0f, p.ReloadT - dt);
                p.QuickT = Mathf.Max(0f, p.QuickT - dt);
                p.ScareT = Mathf.Max(0f, p.ScareT - dt);
                p.JarvisT = Mathf.Max(0f, p.JarvisT - dt);
                Inventory.Slot s = p.Selected;
                // Right click with the 0.50 cal aims it: the scope up, the laser on, and slower on the feet.
                p.Aiming = p.LastCmd.Has(Btn.Lunge) && s != null && s.item == ItemType.Sniper && p.CanAct && p.Action == ActionKind.None;
                if (p.Aiming)
                {
                    p.Move.SlowT = Mathf.Max(p.Move.SlowT, 0.1f);
                    p.Move.SlowMul = Mathf.Min(p.Move.SlowMul, Balance.Items.Sniper.AimSlowMul);
                }
                // Night vision: an ability, held on right click while there's charge (with the 0.50 cal in hand it aims too),
                // whatever else they're doing; on their feet or down, never past the second ring.
                // (Not while JARVIS runs.)
                bool want = p.Role == Role.Survivor && p.LastCmd.Has(Btn.Lunge) && p.JarvisT <= 0f;
                bool able = (p.Health == Health.Healthy || p.Health == Health.Wounded || p.Health == Health.Downed) && p.Zone < 2;
                p.GogglesOn = want && able && (TestMode || p.Inv.NightVision > 0f);
                if (p.GogglesOn && !TestMode && !p.Inv.Infinite)
                {
                    p.Inv.NightVision = Mathf.Max(0f, p.Inv.NightVision - dt);
                    if (p.Inv.NightVision <= 0f)
                    {
                        p.GogglesOn = false;
                        Tell(p, "Night vision dead");
                    }
                }
            }
        }

        /// <summary>A trap bursts into galaxy gas.</summary>
        void BurstTrap(int k, string who)
        {
            Trap t = Traps[k];
            Gases.Add(new GasCloud { Id = AllocEntityId(), Pos = t.Pos });
            Emit(Near(t.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Gas, Pos = t.Pos });
            Feed($"{who} tripped a galaxy gas trap");
            Traps.RemoveAt(k);
        }

        /// <summary>
        /// A shot or a thrown item from <paramref name="from"/> along <paramref name="dir"/> for <paramref name="len"/>: every
        /// armed trap it passes over (within the trap's own size) bursts. Returns how far along the first one was (+inf if none).
        /// </summary>
        float HitTraps(Vector2 from, Vector2 dir, float len, string who)
        {
            float r = R(Balance.Items.Trap.HitRadius), first = float.PositiveInfinity;
            for (int k = Traps.Count - 1; k >= 0; k--)
            {
                if (Traps[k].ArmT > 0f) continue;
                float t = RayCircle(from, dir, Traps[k].Pos, r);
                if (t > len) continue;
                first = Mathf.Min(first, t);
                BurstTrap(k, who);
            }
            return first;
        }

        /// <summary>The Grapes of Wrath hits Zach: a picture flashes over his screen with the vine boom; stunned; abilities off.</summary>
        void BookHit(SimPlayer h, SimPlayer by)
        {
            h.BookT = Balance.Items.FlashTime;
            StunHunter(h, Balance.Items.Book.Stun, "book", by, true);
            h.AbilityLockT = Balance.Hunter.BookAbilityLock;
            Emit(h.Id, new GameEvent { Kind = EventKind.Book, A = NextBookImage() });
            Emit(Near(h.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Boom, Pos = h.Pos, F = 1400f });
            Feed($"{by?.Name ?? "Someone"} threw The Grapes of Wrath at {h.Name}");
        }
    }
}
