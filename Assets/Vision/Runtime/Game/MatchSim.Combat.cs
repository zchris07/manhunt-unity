using UnityEngine;

namespace Vision.Game
{
    /// <summary>Zach's machete and lunge, damage, carrying and stakes (the original's combat.ts).</summary>
    public sealed partial class MatchSim
    {
        const float HitTolerance = 8f;   // the original's hunterHitTolerance (units)

        /// <summary>Moves a player and tells their client (the client then puts its avatar there).</summary>
        public void Place(SimPlayer p, Vector2 pos)
        {
            p.Pos = pos;
            p.PlaceVersion++;
        }

        static float HempRegen(SimPlayer p) =>
            p.Hemp == 0 ? 1f : MatchRules.HempRegenMul(p.Hemp == 2 ? 1f : p.HempLeft / Balance.Hunter.Hemp.Duration);

        static bool CanSwing(SimPlayer h) =>
            h.Carrying == 0 && h.AttackCd <= 0f && h.AttackWindup <= 0f && h.Action == ActionKind.None && h.StunT <= 0f && h.KnockT <= 0f && h.Pump <= 0;

        static bool CanKeepCharging(SimPlayer h) =>
            h.Carrying == 0 && h.AttackWindup <= 0f && h.Action == ActionKind.None && h.StunT <= 0f && h.KnockT <= 0f;

        /// <summary>Left click pressed: start charging a swing (released in UpdateCombat).</summary>
        public void StartCharge(SimPlayer h)
        {
            if (h.ChargeT < 0f && CanSwing(h))
            {
                h.ChargeT = 0f;
                h.ChargeHeld = 0f;
            }
        }

        /// <summary>Health a swipe charged for <paramref name="chargeT"/> s takes: a third, up to two thirds fully charged.</summary>
        public static float SwipeDamage(float chargeT)
        {
            float k = Mathf.Clamp01((chargeT - Balance.Hunter.Attack.TapGrace) / (Balance.Hunter.Attack.HeavyAt - Balance.Hunter.Attack.TapGrace));
            return Balance.Hunter.Attack.DamageBase + (Balance.Hunter.Attack.DamageFull - Balance.Hunter.Attack.DamageBase) * k;
        }

        void AttemptAttack(SimPlayer h, bool heavy, float chargeT)
        {
            if (!CanSwing(h)) return;
            h.Heavy = heavy;
            h.SwingDamage = SwipeDamage(heavy ? Balance.Hunter.Attack.ChargeMax : chargeT);
            h.AttackWindup = Balance.Hunter.Attack.Windup;
            h.SwingT = Balance.Hunter.Attack.SwingTime;
            h.Move.SlowT = Mathf.Max(h.Move.SlowT, Balance.Hunter.Attack.Windup);
            h.Move.SlowMul = Mathf.Min(h.Move.SlowMul, Balance.Hunter.WindupSlowMul);
            // Swings in quick succession go back and forth; a charged one always comes from over the shoulder.
            h.SwingSide = !heavy && Time - h.LastSwingAt < Balance.Hunter.Attack.ComboWindow ? 1 - h.SwingSide : 0;
            h.LastSwingAt = Time;
            Emit(Near(h.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Swing, A = h.Id, B = heavy ? 2 : 1, F = -1f, G = h.SwingSide });
        }

        static bool Hittable(SimPlayer q) => q.Role == Role.Survivor && (q.Health == Health.Healthy || q.Health == Health.Wounded) && q.HideState != 2;

        /// <summary>True if a body at <paramref name="at"/> of radius r (design units) is inside the swipe in front of the hunter.</summary>
        bool InSwipe(SimPlayer h, Vector2 at, float r)
        {
            float reachU = Balance.Hunter.Attack.Range * (h.Heavy ? Balance.Hunter.Attack.ChargeRangeMul : 1f) * (h.JadenBonus > 0 ? Balance.Hunter.JadenSlainRangeMul : 1f) + HitTolerance;
            float d = Vector2.Distance(at, h.Pos) - r;
            if (d > R(reachU)) return false;
            if (d <= h.RadiusD) return true;
            float a = Mathf.Atan2(at.y - h.Pos.y, at.x - h.Pos.x);
            float half = Balance.Hunter.Attack.ArcDeg / 2f * Mathf.Deg2Rad * (h.Heavy ? Balance.Hunter.Attack.ChargeArcMul : 1f);
            return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, h.Facing * Mathf.Rad2Deg)) * Mathf.Deg2Rad <= half + Mathf.Atan2(r, Mathf.Max(R(1f), d));
        }

        void ResolveAttack(SimPlayer h)
        {
            SimPlayer best = null;
            float bestD = float.MaxValue;
            foreach (SimPlayer q in Order)
            {
                if (!Hittable(q)) continue;
                float d = Vector2.Distance(q.Pos, h.Pos);
                if (d >= bestD || !InSwipe(h, q.Pos, q.RadiusD) || !Geo.LineOfSight(h.Pos, q.Pos)) continue;
                best = q;
                bestD = d;
            }
            bool hit = false;
            int power = h.Heavy ? 2 : 1;
            if (best != null)
            {
                DamageSurvivor(best, h, h.SwingDamage);
                hit = true;
            }
            else
            {
                foreach (Npc n in Npcs)
                {
                    if (!n.Slashable || !n.Solid || !InSwipe(h, n.Pos, n.HitRadius) || !Geo.LineOfSight(h.Pos, n.Pos)) continue;
                    n.Slash(this, h, power);
                    hit = true;
                    break;
                }
            }
            if (!hit)
            {
                // Two swings break a dropped barricade.
                for (int i = 0; i < Map.Barricades.Count && !hit; i++)
                {
                    if (Barricades[i] != BarricadeState.Down) continue;
                    Vector2 c = ClosestOnSegment(h.Pos, Map.Barricades[i].A, Map.Barricades[i].B);
                    if (!InSwipe(h, c, R(6f))) continue;
                    hit = true;
                    BarricadeHits[i] += power;
                    Noise(c, 700f, "smash");
                    if (BarricadeHits[i] >= Balance.Hunter.Attack.BarricadeHits) SetBarricade(i, BarricadeState.Broken);
                }
                // Two swings smash a closed door (it stays open for good).
                for (int i = 0; i < Map.Doors.Count && !hit; i++)
                {
                    if (Doors[i] || DoorBroken[i] || Map.Doors[i].Shutter) continue;
                    Vector2 c = ClosestOnSegment(h.Pos, Map.Doors[i].A, Map.Doors[i].B);
                    if (!InSwipe(h, c, R(6f))) continue;
                    hit = true;
                    DoorHits[i] += power;
                    Noise(c, 700f, "smash");
                    if (DoorHits[i] >= Balance.Hunter.Attack.DoorHits)
                    {
                        SetDoor(i, true);
                        DoorBroken[i] = true;
                        Noise(c, 900f, "door_smash");
                    }
                }
            }
            // One swipe smashes a window; Zach can climb through the frame afterwards.
            if (!hit)
                for (int i = 0; i < Map.Windows.Count && !hit; i++)
                {
                    if (WindowsBroken[i]) continue;
                    Vector2 c = ClosestOnSegment(h.Pos, Map.Windows[i].A, Map.Windows[i].B);
                    if (!InSwipe(h, c, R(6f))) continue;
                    hit = true;
                    BreakWindow(i);
                    Noise(c, 800f, "glass");
                }
            h.Heavy = false;
            Emit(Near(h.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Swing, A = h.Id, B = power, F = hit ? 1f : 0f, Text = h.SwingSide == 1 ? "back" : null });
            if (hit)
            {
                h.AttackCd = Balance.Hunter.Attack.HitCooldown;
                h.Move.SlowT = Balance.Hunter.Attack.HitCooldown * Balance.Hunter.HitSlowFraction;
                h.Move.SlowMul = Balance.Hunter.Attack.HitSlowMul;
            }
            else
            {
                h.AttackCd = Balance.Hunter.Attack.MissCooldown;
                h.Move.SlowT = Mathf.Max(h.Move.SlowT, Balance.Hunter.MissSlowTime);
                h.Move.SlowMul = Mathf.Min(h.Move.SlowMul, Balance.Hunter.Attack.MissSlowMul);
            }
        }

        public static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        /// <summary>
        /// Lunge contact: during the dash Zach's hitbox is 50% larger than his body, and touching a survivor is enough to hit
        /// them. Tested along the path moved since the last tick.
        /// </summary>
        public void LungeContact(SimPlayer h, Vector2 from)
        {
            float reach = h.RadiusD * Balance.Hunter.Lunge.HitboxMul;
            foreach (SimPlayer q in Order)
            {
                if (!Hittable(q)) continue;
                float r = reach + q.RadiusD + R(HitTolerance * 0.5f);
                if (SimMap.SegmentDistance(q.Pos, from, h.Pos) > r || !Geo.LineOfSight(h.Pos, q.Pos)) continue;
                DamageSurvivor(q, h, Balance.Hunter.Lunge.Damage);
                LungeLanded(h);
                Emit(Near(h.Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Swing, A = h.Id, B = 1, F = 1f, G = 1f });
                return;
            }
            foreach (Npc n in Npcs)
            {
                if (!n.Slashable || !n.Solid) continue;
                if (SimMap.SegmentDistance(n.Pos, from, h.Pos) > reach + n.HitRadius) continue;
                n.Slash(this, h, 1);
                LungeLanded(h);
                return;
            }
        }

        /// <summary>The dash stops on contact. A swipe already charging or winding up still goes off (the combo).</summary>
        static void LungeLanded(SimPlayer h)
        {
            h.LungeHit = true;
            h.Move.LungeT = 0f;
            h.LungeStopVersion++;
            if (h.ChargeT >= 0f || h.AttackWindup > 0f) return;
            h.AttackCd = Mathf.Max(h.AttackCd, Balance.Hunter.Attack.HitCooldown);
            h.Move.SlowT = Balance.Hunter.Attack.HitCooldown * Balance.Hunter.HitSlowFraction;
            h.Move.SlowMul = Balance.Hunter.Attack.HitSlowMul;
        }

        /// <summary>
        /// Takes <paramref name="amount"/> hp (out of his 100) off Zach. At zero he goes down for a while; Plasma's punches and
        /// explosions put him down without the lasting slowdown. Gas burns quietly (no flinch).
        /// </summary>
        public void HurtHunter(SimPlayer h, float amount, SimPlayer by, string kind)
        {
            if (h.Role != Role.Hunter || h.Health == Health.Eliminated || h.KnockT > 0f || amount <= 0f) return;
            if (h.PissT > 0f && kind != "gas") amount *= Balance.Items.Piss.Mul;
            h.Hp = Mathf.Max(0f, h.Hp - amount / Balance.Hunter.Health.Max);
            if (kind != "gas")
            {
                h.HurtT = 0.4f;
                Emit(null, new GameEvent { Kind = EventKind.Hit, A = h.Id, B = by?.Id ?? 0, Pos = h.Pos, F = amount / Balance.Hunter.Health.Max, Text = kind });
            }
            if (h.Hp > 1e-4f) return;
            DownHunter(h, kind != "punch" && kind != "blast");
            Feed(kind == "blast" ? $"{h.Name} was blown apart" : kind == "punch" ? $"Plasma.TTV knocked {h.Name} out" : by != null ? $"{by.Name} put {h.Name} down" : $"{h.Name} went down");
        }

        /// <summary>Zach is down: everything he was doing stops, and he drops whoever he carried.</summary>
        public void DownHunter(SimPlayer h, bool counts)
        {
            h.Hp = 0f;
            h.KnockT = Balance.Hunter.Health.DownTime;
            h.ChargeT = -1f;
            h.AttackWindup = 0f;
            h.SwingT = 0f;
            h.Move.LungeT = 0f;
            if (h.Action != ActionKind.None) CancelAction(h);
            if (h.Carrying != 0) DropCarried(h);
            if (counts) h.Downs++;
            Emit(null, new GameEvent { Kind = EventKind.Stun, A = h.Id, Text = "down", F = Balance.Hunter.Health.DownTime });
        }

        /// <summary>
        /// Takes <paramref name="amount"/> (a fraction of full health) off a survivor: the shield first; they flinch, and at zero
        /// they are down. Zach's hits also give them a burst of speed.
        /// </summary>
        public void HurtSurvivor(SimPlayer q, float amount, SimPlayer by, string kind)
        {
            if (q.Role != Role.Survivor || (q.Health != Health.Healthy && q.Health != Health.Wounded) || q.HideState == 2) return;
            bool zach = by != null && by.Role == Role.Hunter;
            bool quiet = kind == "gas";
            if (!quiet) CancelAction(q);
            if (zach && !quiet) by.Stats.Hits++;
            float soaked = Mathf.Min(q.Shield, amount);
            q.Shield -= soaked;
            q.Hp = Mathf.Max(0f, q.Hp - (amount - soaked));
            if (!quiet)
            {
                q.HurtT = 0.4f;
                Emit(null, new GameEvent { Kind = EventKind.Hit, A = q.Id, B = by?.Id ?? 0, Pos = q.Pos, F = amount, Text = kind });
            }
            if (q.Hp > 0.001f)
            {
                q.Health = Health.Wounded;
                if (zach && !quiet) q.Move.HasteT = Balance.Survivor.HitHasteTime;
                return;
            }
            if (quiet) CancelAction(q);
            q.Hp = 0f;
            q.Health = Health.Downed;
            q.Move.HasteT = 0f;
            q.GogglesOn = false;
            if (zach) by.Stats.Downs++;
            Emit(null, new GameEvent { Kind = EventKind.Downed, A = q.Id, B = by?.Id ?? 0, Pos = q.Pos });
            Feed($"{q.Name} is down");
        }

        /// <summary>Zach's machete (a third of their health unless charged) or lunge.</summary>
        public void DamageSurvivor(SimPlayer q, SimPlayer h, float amount = Balance.Hunter.Attack.DamageBase) => HurtSurvivor(q, amount, h, "slash");

        /// <summary>Sets a survivor's health (back on their feet): full is Healthy, anything less Wounded.</summary>
        public static void RestoreSurvivor(SimPlayer q, float hp)
        {
            q.Hp = Mathf.Clamp(hp, 0.001f, 1f);
            q.Health = q.Hp >= 0.999f ? Health.Healthy : Health.Wounded;
        }

        public void CarrySurvivor(SimPlayer h, SimPlayer q)
        {
            CancelAction(q);
            q.Health = Health.Carried;
            q.CarriedBy = h.Id;
            q.Wiggle = 0f;
            h.Carrying = q.Id;
        }

        /// <summary>Drops the carried survivor (wiggle free or stun). They get a burst of speed.</summary>
        public void DropCarried(SimPlayer h)
        {
            SimPlayer q = Get(h.Carrying);
            h.Carrying = 0;
            if (q == null) return;
            q.CarriedBy = 0;
            RestoreSurvivor(q, Balance.Survivor.ReviveHp);
            q.Wiggle = 0f;
            Vector2 back = h.Pos - h.FacingDir * R(30f);
            Place(q, Geo.Blocked(back, q.RadiusD) ? h.Pos : back);
            q.Move.HasteT = Balance.Survivor.HitHasteTime;
            Feed($"{q.Name} broke free");
        }

        /// <summary>Stage 1 on the first staking; a second staking (or the stage timer) is stage 2: eliminated.</summary>
        public void StakeSurvivor(SimPlayer h, SimPlayer q, int stakeId)
        {
            h.Carrying = 0;
            q.CarriedBy = 0;
            q.StakeCount++;
            q.StakedBy = h.Id;
            h.Stats.Stakes++;
            // Every survivor staked buffs Zach for good: faster, and he sees further.
            h.StakeBuff++;
            h.FovMul *= 1f + Balance.Hunter.StakeBuff;
            Tell(h, $"Staked {q.Name}: +{Mathf.RoundToInt(Balance.Hunter.StakeBuff * 100f)}% speed and view");
            Vector2 stake = Map.Stakes[stakeId];
            if (q.StakeCount >= 2)
            {
                Place(q, stake);
                Emit(null, new GameEvent { Kind = EventKind.Staked, A = q.Id, B = 2, Pos = stake });
                Eliminate(q, "stake", h);
                return;
            }
            q.Health = Health.Staked;
            q.StakeId = stakeId;
            q.StakeStage = 1;
            q.StakeT = Balance.Objectives.StakeStageTime;
            Place(q, stake);
            Stakes[stakeId] = q.Id;
            Emit(null, new GameEvent { Kind = EventKind.Staked, A = q.Id, B = 1, Pos = stake });
            Feed($"{q.Name} is on a stake");
        }

        void UpdateCombat(float dt)
        {
            foreach (SimPlayer p in Order)
            {
                if (p.StunT > 0f) p.StunT = Mathf.Max(0f, p.StunT - dt);
                if (p.ImmuneT > 0f) p.ImmuneT = Mathf.Max(0f, p.ImmuneT - dt);
                if (p.AttackCd > 0f) p.AttackCd = Mathf.Max(0f, p.AttackCd - dt);
                if (p.SwingT > 0f) p.SwingT = Mathf.Max(0f, p.SwingT - dt);
                p.HurtT = Mathf.Max(0f, p.HurtT - dt);

                if (p.Role == Role.Hunter)
                {
                    p.ReloadT = Mathf.Max(0f, p.ReloadT - dt);
                    // Down: back up after a while at half health. Up: the bar slowly fills again.
                    if (p.KnockT > 0f)
                    {
                        p.KnockT = Mathf.Max(0f, p.KnockT - dt);
                        if (p.KnockT == 0f)
                        {
                            p.Hp = Balance.Hunter.Health.RecoverFraction;
                            Feed($"{p.Name} got back up");
                        }
                    }
                    else if (p.Health != Health.Eliminated)
                        p.Hp = Mathf.Min(1f, p.Hp + dt / Balance.Hunter.Health.RegenTime * (1f + Balance.Hunter.StakeRegen * p.StakeBuff) *
                                              (p.NjaaronRegen ? Balance.Njaaron.RegenMul : 1f) * HempRegen(p));
                    if (p.ChargeT >= 0f)
                    {
                        if (!CanKeepCharging(p)) p.ChargeT = -1f;
                        else if (p.LastCmd.Has(Btn.Primary) && p.ChargeHeld + dt < Balance.Hunter.Attack.AutoRelease)
                        {
                            p.ChargeHeld += dt;
                            p.ChargeT = Mathf.Min(Balance.Hunter.Attack.ChargeMax, p.ChargeT + dt);
                            p.Move.SlowT = Mathf.Max(p.Move.SlowT, 0.1f);
                            p.Move.SlowMul = Mathf.Min(p.Move.SlowMul, 1f - (1f - Balance.Hunter.Attack.ChargeSlowMul) * (p.ChargeT / Balance.Hunter.Attack.ChargeMax));
                        }
                        else
                        {
                            // Released (or held too long): strike.
                            bool heavy = p.ChargeT >= Balance.Hunter.Attack.HeavyAt;
                            float charged = p.ChargeT;
                            p.ChargeT = -1f;
                            AttemptAttack(p, heavy, charged);
                        }
                    }
                    if (p.AttackWindup > 0f)
                    {
                        p.AttackWindup -= dt;
                        if (p.AttackWindup <= 0f)
                        {
                            p.AttackWindup = 0f;
                            ResolveAttack(p);
                        }
                    }
                    if (p.Carrying != 0)
                    {
                        SimPlayer q = Get(p.Carrying);
                        if (q == null || q.Health != Health.Carried) p.Carrying = 0;
                    }
                    // The lunge's contact is tested along the path moved this tick.
                    bool lunging = p.Move.LungeT > 0f || (p.WasLunging && Vector2.Distance(p.Pos, p.LastTickPos) > 0f);
                    if (p.Move.LungeT > 0f && !p.WasLunging) p.LungeHit = false;
                    if (lunging && !p.LungeHit) LungeContact(p, p.LastTickPos);
                    p.WasLunging = p.Move.LungeT > 0f;
                    p.LastTickPos = p.Pos;
                    continue;
                }

                if (p.Role != Role.Survivor) continue;
                p.LastTickPos = p.Pos;
                if (p.Health == Health.Staked)
                {
                    p.StakeT -= dt;
                    if (p.StakeT <= 0f && !TestMode)
                    {
                        Emit(null, new GameEvent { Kind = EventKind.Staked, A = p.Id, B = 2, Pos = p.Pos });
                        Eliminate(p, "stake", Get(p.StakedBy));
                    }
                }
                else if (p.Health == Health.Carried)
                {
                    SimPlayer h = Get(p.CarriedBy);
                    if (h == null || h.Role != Role.Hunter)
                    {
                        RestoreSurvivor(p, Balance.Survivor.ReviveHp);
                        p.CarriedBy = 0;
                        continue;
                    }
                    p.Pos = h.Pos;
                    if (p.LastCmd.Move.sqrMagnitude > 1e-4f) p.Wiggle += dt / Balance.Survivor.WiggleTime;
                    if (p.Wiggle >= 1f)
                    {
                        DropCarried(h);
                        h.StunT = Mathf.Max(h.StunT, Balance.Hunter.WiggleStun);
                        h.Stats.StunnedTimes++;
                        Emit(null, new GameEvent { Kind = EventKind.Stun, A = h.Id, Text = "wiggle", F = Balance.Hunter.WiggleStun });
                    }
                }
            }
        }
    }
}
