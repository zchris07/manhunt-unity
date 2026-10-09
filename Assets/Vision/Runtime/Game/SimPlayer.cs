using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>Per-player match statistics for the results screen (the original's PlayerStats).</summary>
    public sealed class MatchStats
    {
        public string Outcome = "survived";
        public float RepairSec, TimeAlive;
        public int Heals, Revives, Unstakes, Stuns, Hits, Downs, Stakes, Eliminations, StunnedTimes, GensDamaged;
    }

    /// <summary>
    /// The movement state a player's own client advances (the original's MoveState). Speeds and timers in the original's
    /// units; the position lives on the <see cref="SimPlayer"/> in design units.
    /// </summary>
    public sealed class MoveState
    {
        public MoveMode Mode;
        public float LungeT, LungeAng, LungeRecharge;
        public int LungeCharges = Balance.Hunter.Lunge.Charges;
        /// <summary>Knockback: seconds left, total, peak speed (units/s) and direction.</summary>
        public float KbT, KbDur, KbPeak, KbAng;
        public float HasteT, SlowT, SlowMul = 1f;
        /// <summary>Sprint meter in seconds of sprint left; lockout after it ran dry; blocked until Shift is released.</summary>
        public float Stamina, StaminaLock;
        public bool SprintBlocked;
        /// <summary>Doctor Pepper's fading boost, and the Hemp Battery's speed bonus.</summary>
        public float BoostT, HempT;
        public Btn PrevButtons;
        public bool Sprinting;
        /// <summary>Climbing through a broken window this step (slowed; the view plays the climb).</summary>
        public bool Climbing;

        public MoveState(Role role) => Stamina = MaxStaminaBase(role);

        public static float MaxStaminaBase(Role role) => role == Role.Hunter ? Balance.Hunter.StaminaMax : Balance.Survivor.StaminaMax;

        /// <summary>Current sprint meter capacity (the energy drink adds a fading bonus).</summary>
        public static float MaxStamina(Role role, float boostT) =>
            MaxStaminaBase(role) + Balance.Items.Energy.BonusSec * Mathf.Clamp01(boostT / Balance.Items.Energy.Duration);
    }

    /// <summary>
    /// Everything the match keeps about one player (the original's SimPlayer), for both roles. Positions are design units on
    /// the ground plane (x east, y north); facing is an angle in radians on that plane. Health is a 0-1 bar for survivors and
    /// a 0-1 fraction of Zach's 100 hp for him.
    /// </summary>
    public sealed class SimPlayer
    {
        public int Id;
        public string Name;
        public Role Role;
        public int Tint;
        public bool Connected = true;
        public float DisconnectedAt;
        public bool IsLocal;
        /// <summary>Testing: an inert stand-in (no one plays it; the host moves it only when the rules push it).</summary>
        public bool IsDummy;

        public Vector2 Pos;
        public float Facing = Mathf.PI / 2f;
        /// <summary>Distance to the cursor, original units.</summary>
        public float AimDist;
        public Gait Gait;
        public MoveState Move;
        /// <summary>Collision radius, original units.</summary>
        public float Radius;

        public InputCmd LastCmd;
        public Btn PrevButtons;

        public Health Health, LastHealth;
        public float Hp = 1f;
        public float Shield;

        // Zach.
        public float KnockT;
        public int Downs;
        public int StakeBuff;
        public float BookT;
        public float AbilityLockT;
        public float PissT;
        public int JadenBonus;
        public bool NjaaronRegen;
        public bool Nic;
        public int Pump;
        public int Carrying;

        public float FovMul = 1f;
        public bool WazLooked;

        public ActionKind Action;
        public float ActionT, ActionDur;
        public int ActionTarget = -1;

        public float StunT, ImmuneT;
        public int CarriedBy;
        public int StakeId = -1, StakeStage, StakeCount, StakedBy;
        public float StakeT;
        public float Wiggle;

        public int HideSpot = -1;
        /// <summary>0 none, 1 entering, 2 hidden, 3 leaving.</summary>
        public int HideState;
        public float HideT;
        public float Breath = 1f;
        public bool HoldingBreath;
        public float GaspCd, BreathCueT;

        public Inventory Inv = new Inventory();
        /// <summary>Selected slot, 0-based (-1 none).</summary>
        public int SelSlot = -1;
        public bool GogglesOn;
        public float ReloadT;
        /// <summary>JARVIS: 0 none, 1 tablet in hand, 2 used, 3 infinite (testing mode).</summary>
        public int Jarvis;
        public float JarvisT;
        public float ScareT;
        public bool Gassed;
        /// <summary>Penjamin: seconds the slow and burn last, their strength, seconds of darkness.</summary>
        public float VapeT, VapeSlow, VapeDps, DarkT, VapeSlowT;
        public float ViewReach;

        // Zach's attack.
        public float AttackCd, AttackWindup, SwingT;
        public float ChargeT = -1f, ChargeHeld;
        public bool Heavy;
        public float SwingDamage = 1f / 3f;
        /// <summary>The swing's side: 0 forehand (right to left), 1 backhand; quick light swings alternate.</summary>
        public int SwingSide;
        public float LastSwingAt = -9f;
        public bool LungeHit, WasLunging;
        public float BurstCd;
        /// <summary>Hemp Battery: 0 none, 1 carried, 2 infinite (testing mode).</summary>
        public int Hemp;
        public float HempLeft = Balance.Hunter.Hemp.Duration;
        public bool HempOn;
        public float HempLock;
        public int BeamCharges;
        public float BeamCd, BeamT, BeamAng, BeamLen, BeamTick, BeamFlinch;
        public int BeamId;
        public readonly Dictionary<string, float> BeamHit = new Dictionary<string, float>();
        public int VapeCharges = Balance.Hunter.Vape.Charges;
        public float VapeCd;
        public float ArrowT;
        public float HitHasteT;
        /// <summary>Seconds of hurt flash left (client effect).</summary>
        public float HurtT;

        public float Noise;
        public Prompt Prompt;
        public int PromptTarget = -1;
        public Prompt Prompt2;
        public int Prompt2Target = -1;
        public float LastScent, LastBlood;
        public float Terror;

        public int Spectating;
        public MatchStats Stats = new MatchStats();
        public float JoinedTime, EndedTime;
        public bool Crouching;
        /// <summary>Bumped when the rules move the player (the avatar is put there) or change their movement (knockback, a stopped lunge, a drink).</summary>
        public int PlaceVersion, KnockVersion, LungeStopVersion, BoostVersion;
        /// <summary>Where the player was at the last tick (the lunge's contact sweeps from here).</summary>
        public Vector2 LastTickPos;

        public SimPlayer(int id, string name, Role role, Vector2 pos)
        {
            Id = id;
            Name = name;
            Role = role;
            Pos = pos;
            Move = new MoveState(role);
            Radius = role == Role.Hunter ? Balance.HunterRadius : Balance.SurvivorRadius;
            Health = LastHealth = role == Role.Spectator ? Health.Eliminated : Health.Healthy;
            Stats.Outcome = role == Role.Hunter ? "hunter" : "survived";
        }

        public bool IsSurvivor => Role == Role.Survivor;
        public bool IsHunter => Role == Role.Hunter;

        /// <summary>Still in the match (a survivor neither escaped nor eliminated).</summary>
        public bool InPlay => Role == Role.Survivor && Health != Health.Escaped && Health != Health.Eliminated;

        /// <summary>Can walk around and interact (not downed, carried, staked, hidden; Zach not stunned or down).</summary>
        public bool CanAct
        {
            get
            {
                if (Role == Role.Spectator) return false;
                if (Role == Role.Hunter) return StunT <= 0f && KnockT <= 0f && Health != Health.Eliminated;
                return (Health == Health.Healthy || Health == Health.Wounded) && HideState == 0;
            }
        }

        /// <summary>Standing survivor (still on their feet or hiding).</summary>
        public bool Standing => Role == Role.Survivor && (Health == Health.Healthy || Health == Health.Wounded);

        /// <summary>World units of collision radius (design).</summary>
        public float RadiusD => Scale.D(Radius);

        /// <summary>The facing as a unit vector on the plane.</summary>
        public Vector2 FacingDir => new Vector2(Mathf.Cos(Facing), Mathf.Sin(Facing));
        /// <summary>Where the body faces: the aim, or while the Hemp Beam charges and fires, the beam (which turns slower than the aim).</summary>
        public float BodyFacing => BeamT > 0f ? BeamAng : Facing;

        /// <summary>The selected slot, if it holds anything.</summary>
        public Inventory.Slot Selected
        {
            get
            {
                Inventory.Slot s = Inv.SlotAt(SelSlot);
                return s != null && !s.Empty ? s : null;
            }
        }
    }
}
