using UnityEngine;

namespace Vision.Game
{
    /// <summary>What movement needs to know about the ground under a point (design units).</summary>
    public interface IMoveEnv
    {
        bool InWater(Vector2 p);
        bool InBrokenWindow(Vector2 p, float radius);
    }

    public struct MoveContext
    {
        public Role Role;
        public float HunterSpeedMul;
        public bool Carrying;
        /// <summary>Zach: extra lunge charges (slaying Jaden), and whether the Grapes of Wrath switched his abilities off.</summary>
        public int LungeBonus;
        public bool AbilitiesLocked;
        /// <summary>Testing mode's speed mode: six times the speed, a sprint meter that never drains.</summary>
        public bool SpeedMode;
    }

    /// <summary>
    /// One step of a player's own movement (the original's stepMovement): sprint meter and lockout, crouch and crawl, Zach's
    /// lunge dash and its charges, knockback, haste, slows, the energy drink, the Hemp Battery, wading and window climbing.
    /// It returns the displacement (design units) for the avatar's controller to carry out against the level's colliders.
    /// </summary>
    public static class Movement
    {
        /// <summary>Dash speed curve shared by the lunge and knockback: instant peak, then a fast ease-out.</summary>
        public static float DashSpeed(float peak, float elapsed, float duration)
        {
            float k = Mathf.Max(0f, 1f - elapsed / duration);
            return peak * k * k;
        }

        /// <summary>Distance a full dash covers (original units).</summary>
        public static float DashDistance(float peak, float duration) => peak * duration / 3f;

        public static Vector2 Step(MoveState s, Vector2 at, in InputCmd cmd, in MoveContext ctx, IMoveEnv env, float dt, out Gait gait)
        {
            Btn pressed = cmd.Buttons & ~s.PrevButtons;
            s.PrevButtons = cmd.Buttons;
            float radius = Scale.D(ctx.Role == Role.Hunter ? Balance.HunterRadius : Balance.SurvivorRadius);
            Vector2 delta = Vector2.zero;
            s.HasteT = Mathf.Max(0f, s.HasteT - dt);
            s.HempT = Mathf.Max(0f, s.HempT - dt);
            if (s.SlowT > 0f)
            {
                s.SlowT = Mathf.Max(0f, s.SlowT - dt);
                if (s.SlowT == 0f) s.SlowMul = 1f;
            }

            // Lunge charges come back one at a time.
            int maxCharges = Balance.Hunter.Lunge.Charges + ctx.LungeBonus;
            if (s.LungeCharges < maxCharges)
            {
                s.LungeRecharge -= dt;
                if (s.LungeRecharge <= 0f)
                {
                    s.LungeCharges++;
                    s.LungeRecharge = s.LungeCharges < maxCharges ? s.LungeRecharge + Balance.Hunter.Lunge.Recharge : 0f;
                }
            }
            else s.LungeRecharge = 0f;

            // Knockback moves you even while stunned.
            if (s.KbT > 0f)
            {
                float elapsed = s.KbDur - s.KbT, step = Mathf.Min(dt, s.KbT);
                float v = Scale.Speed(DashSpeed(s.KbPeak, elapsed + step / 2f, s.KbDur));
                delta += new Vector2(Mathf.Cos(s.KbAng), Mathf.Sin(s.KbAng)) * (v * step);
                s.KbT = Mathf.Max(0f, s.KbT - dt);
            }

            // Sprint meter.
            Role role = ctx.Role;
            float cfgMax = role == Role.Hunter ? Balance.Hunter.StaminaMax : Balance.Survivor.StaminaMax;
            float cfgRefill = role == Role.Hunter ? Balance.Hunter.StaminaRefill : Balance.Survivor.StaminaRefill;
            float boostK = Mathf.Clamp01(s.BoostT / Balance.Items.Energy.Duration);
            float cap = MoveState.MaxStamina(role, s.BoostT);
            bool hemp = role == Role.Hunter && s.HempT > 0f;
            float refill = cfgMax / cfgRefill * (1f + (Balance.Items.Energy.RefillMul - 1f) * boostK) * (hemp ? Balance.Hunter.Hemp.SprintRefillMul : 1f);
            s.BoostT = Mathf.Max(0f, s.BoostT - dt);
            if (s.StaminaLock > 0f) s.StaminaLock = Mathf.Max(0f, s.StaminaLock - dt);
            bool runHeld = cmd.Has(Btn.Run);
            if (!runHeld && s.StaminaLock <= 0f) s.SprintBlocked = false;

            if (s.Mode == MoveMode.Locked)
            {
                s.LungeT = 0f;
                s.Sprinting = false;
                s.Stamina = Mathf.Min(cap, s.Stamina + refill * dt);
                gait = Gait.Idle;
                return delta;
            }

            Vector2 dir = Vector2.ClampMagnitude(cmd.Move, 1f);
            bool moving = dir.sqrMagnitude > 1e-4f;
            bool crouching = role == Role.Survivor && cmd.Has(Btn.Crouch);
            bool wantsSprint = runHeld && moving && !crouching && s.Mode == MoveMode.Normal;
            bool sprint = wantsSprint && !s.SprintBlocked && s.StaminaLock <= 0f && s.Stamina > 0f;
            if (ctx.SpeedMode) sprint = wantsSprint;
            if (sprint && !ctx.SpeedMode)
            {
                s.Stamina -= dt * (hemp ? Balance.Hunter.Hemp.SprintDrainMul : 1f);
                if (s.Stamina <= 0f)
                {
                    s.Stamina = 0f;
                    s.StaminaLock = Balance.SprintLockout;
                    s.SprintBlocked = true;
                }
            }
            else s.Stamina = Mathf.Min(cap, s.Stamina + refill * dt);   // the meter refills even while sprint is locked out
            if (ctx.SpeedMode) s.Stamina = cap;
            s.Stamina = Mathf.Min(s.Stamina, cap);
            s.Sprinting = sprint;

            float speed;
            bool inWindow = env != null && env.InBrokenWindow(at, radius + Scale.D(4f));
            s.Climbing = inWindow;
            if (role == Role.Hunter)
            {
                if ((pressed & Btn.Lunge) != 0 && !ctx.AbilitiesLocked && s.LungeCharges > 0 && s.LungeT <= 0f && !ctx.Carrying && s.Mode == MoveMode.Normal)
                {
                    s.LungeCharges--;
                    if (s.LungeRecharge <= 0f) s.LungeRecharge = Balance.Hunter.Lunge.Recharge;
                    s.LungeT = Balance.Hunter.Lunge.Duration;
                    s.LungeAng = cmd.Aim;
                }
                if (s.LungeT > 0f)
                {
                    float elapsed = Balance.Hunter.Lunge.Duration - s.LungeT, step = Mathf.Min(dt, s.LungeT);
                    float v = Scale.Speed(DashSpeed(Balance.Hunter.Lunge.Peak, elapsed + step / 2f, Balance.Hunter.Lunge.Duration));
                    delta += new Vector2(Mathf.Cos(s.LungeAng), Mathf.Sin(s.LungeAng)) * (v * step);
                    s.LungeT = Mathf.Max(0f, s.LungeT - dt);
                    gait = Gait.Run;
                    return delta;
                }
                speed = (sprint ? Balance.Hunter.Sprint : Balance.Hunter.Walk) * ctx.HunterSpeedMul;
                if (ctx.Carrying) speed *= Balance.Hunter.CarrySpeedMul;
                if (s.HempT > 0f) speed *= Balance.Hunter.Hemp.SpeedMul;
                if (s.SlowT > 0f) speed *= s.SlowMul;
                if (inWindow) speed *= Balance.Hunter.WindowClimbMul;
                gait = moving ? (sprint ? Gait.Run : Gait.Walk) : Gait.Idle;
            }
            else
            {
                if (s.Mode == MoveMode.Crawl)
                {
                    speed = Balance.Survivor.Crawl;
                    gait = moving ? Gait.Crouch : Gait.Idle;
                }
                else if (crouching)
                {
                    speed = Balance.Survivor.Crouch;
                    gait = moving ? Gait.Crouch : Gait.Idle;
                }
                else if (sprint)
                {
                    speed = Balance.Survivor.Run;
                    gait = Gait.Run;
                }
                else
                {
                    speed = Balance.Survivor.Walk;
                    gait = moving ? Gait.Walk : Gait.Idle;
                }
                if (s.HasteT > 0f) speed *= Balance.Survivor.HitHasteMul;
                // Energy drink: walking and running up to 15% faster, fading with the drink.
                if (boostK > 0f && (gait == Gait.Walk || gait == Gait.Run)) speed *= 1f + Balance.Items.Energy.SpeedMul * boostK;
                if (s.SlowT > 0f) speed *= s.SlowMul;
                if (inWindow) speed *= Balance.Survivor.WindowClimbMul;
            }
            if (env != null && env.InWater(at)) speed *= Balance.WadeMul;
            if (ctx.SpeedMode) speed *= Vision.Player.GameSession.SpeedMultiplier;
            if (moving) delta += dir * (Scale.Speed(speed) * dt);
            return delta;
        }
    }
}
