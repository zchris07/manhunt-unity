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
        /// <summary>Slows that fade as they wear off (a 0.50 cal round, 50 Nic), multiplied in afresh each tick by the match.</summary>
        public float FadeMul = 1f;
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

        /// <summary>A full sprint meter (seconds): the lobby's sprint duration for the side (the original's by default).</summary>
        public static float MaxStaminaBase(Role role) => MatchState.Current.Tuning.SprintMax(role);

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
        /// <summary>
        /// The top of Zach's health bar, as a fraction of his original 100 (his <see cref="Hp"/> is out of the same 100, so his
        /// speed goes by it): each down takes 10 off it for good, each stake adds 5; at nothing he's dead.
        /// </summary>
        public float MaxHp = 1f;
        /// <summary>Zach was killed (his bar worn away to nothing).</summary>
        public bool Killed;
        public float KnockT;
        public int Downs;
        public int StakeBuff;
        public float BookT;
        public float AbilityLockT;
        /// <summary>Soaked in piss (Zach): the seconds left on the freshest splash.</summary>
        public float PissT;
        /// <summary>Every splash still on him, by its seconds left (each +50% damage taken, falling off linearly with it).</summary>
        public float[] PissStacks = new float[0];
        /// <summary>Piss in the eyes: the view blurred and yellowed, falling off linearly over <see cref="Balance.Items.Piss.BlurTime"/> s.</summary>
        public float PissBlurT;
        /// <summary>Hit by a 0.50 cal round: slowed, falling off linearly over <see cref="Balance.Items.Sniper.SlowTime"/> s.</summary>
        public float SniperSlowT;
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
        /// <summary>They've been told how to aim the 0.50 cal (shown once, the first time they take it in hand).</summary>
        public bool SniperHintShown;
        /// <summary>The slot in hand when JARVIS came on: the tablet is held up until they take something else in hand.</summary>
        public int JarvisSlot = -2;
        /// <summary>
        /// Picking a graveyard lock: checks passed (0-3) and misses on this run, how long the tick has spun (negative while
        /// it waits to start), where its mark is (degrees clockwise from the top) and which way it turns; the last press
        /// (1 on the mark, -1 a miss, -2 a miss that started it over) and when.
        /// </summary>
        public int PickStage, PickMiss, PickDir = 1, PickResult;
        public float PickT, PickZone, PickResultT;
        /// <summary>
        /// A generator's skill check: seconds repaired since the last one, whether one is up, how long its tick has swept
        /// (negative while it waits to start), where its mark is, which way it turns, and the last result (1 hit, -1 miss) and when.
        /// </summary>
        public bool CheckOn;
        public int CheckDir = 1, CheckResult;
        public float RepairClock, CheckT, CheckZone, CheckResultT;
        /// <summary>Holding up JARVIS's tablet: it's running and they haven't switched to anything else since (it keeps running when they do).</summary>
        public bool HoldingTablet => Jarvis > 0 && JarvisT > 0f && SelSlot == JarvisSlot;
        public bool GogglesOn;
        /// <summary>Aiming the 0.50 cal (right click held with it selected): the scope at the eye and its laser on.</summary>
        public bool Aiming;
        /// <summary>
        /// The edge of the woods (MatchSim.Boundary): the zone (0 inside, 1 past the first ring, 2 past the second), how long
        /// the light has been dying, the countdown to the next bite of the dark, how many bites so far (the red pulse), and
        /// Zach's lockout before he can cross the outer ring again.
        /// </summary>
        public int Zone, BoundaryHits;
        public float ShutdownT, BoundaryTickT, CrossCd;
        public float ReloadT;
        /// <summary>JARVIS: 0 none, 1 the tablet (used again after its cooldown), 3 infinite (testing mode).</summary>
        public int Jarvis;
        /// <summary>Seconds left of JARVIS running for this survivor (paused past the second ring), and the tablet's cooldown.</summary>
        public float JarvisT, JarvisCd;
        public float ScareT;
        public bool Gassed;
        /// <summary>Penjamin: seconds the slow and burn last, their strength, seconds of darkness.</summary>
        public float VapeT, VapeSlow, VapeDps, DarkT, VapeSlowT;
        public float ViewReach;
        /// <summary>
        /// The ground this player's screen shows (design units): a rectangle half <see cref="ViewHalf"/> across its own axes,
        /// turned <see cref="ViewYaw"/> (radians), its middle <see cref="ViewOffset"/> from the player. Zero: not known
        /// (bots, tests), and nothing is off their screen.
        /// </summary>
        public Vector2 ViewHalf, ViewOffset;
        public float ViewYaw;

        // Zach's attack.
        public float AttackCd, AttackWindup, SwingT;
        public float ChargeT = -1f, ChargeHeld;
        public bool Heavy;
        public float SwingDamage = 1f / 3f;
        /// <summary>The swing's side: 0 forehand (high right down to low left), 1 backhand (high left down to low right).</summary>
        public int SwingSide;
        /// <summary>The side the swing being charged will cut from (the machete is cocked over that shoulder).</summary>
        public int NextSide;
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
        /// <summary>The graveyard gate the beam is burning into (-1: none), and for how long it's been on it: one beam's whole firing breaks it.</summary>
        public int BeamGate = -1;
        public float BeamGateT;
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
        public float LastScent, NextBlood;
        /// <summary>How far their scent trail has run (design units), and where its last point was.</summary>
        public float ScentDist;
        public Vector2 ScentAt;
        /// <summary>Where blood last started dripping from them, and how many drops have landed there since.</summary>
        public Vector2 BloodAt;
        /// <summary>Eliminated by wandering into the woods (their light stays out where they fell).</summary>
        public bool LostInWoods;
        /// <summary>
        /// Sacrificed and back as a ghost (MatchSim.Ghosts): when they come back (0: not waiting) and where; the body they
        /// took (<see cref="MatchSim.Bodies"/>' id, 0: none), how long taking it has left to play, the look they wear in it
        /// ("Survivor", or the NPC's name) and whose it was; and whether they've been knocked out of one (never another).
        /// </summary>
        public bool Ghost;
        public float GhostAt;
        public Vector2 GhostSpawn;
        public int Body;
        public float PossessT;
        public string BodyLook = "", BodyName = "";
        public bool Spent;
        /// <summary>A ghost in a body, walking about as its owner did.</summary>
        public bool Reanimated => Ghost && Body != 0;
        /// <summary>A ghost out of any body (sees through it all once knocked out of one).</summary>
        public bool FreeGhost => Ghost && Body == 0;
        /// <summary>Buttons that do nothing until they're let go (held through the click that picked a lock or hit a skill check).</summary>
        public Btn HeldOff;
        public int BloodPool;
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
