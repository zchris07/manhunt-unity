namespace Vision.Game
{
    /// <summary>
    /// Every tunable number of the original game (<c>shared/src/balance.ts</c>), line for line, in its own units: world units
    /// (u), seconds, degrees where noted. <see cref="Scale"/> turns units into this game's metres. The match time limit is
    /// not used: the night ends when every survivor has escaped or is down.
    /// </summary>
    public static class Balance
    {
        public const float SurvivorRadius = 15f;
        public const float HunterRadius = 19f;
        /// <summary>Zach's body width (diameter). Several ranges are multiples of it.</summary>
        public const float HunterWidth = HunterRadius * 2f;
        const float SpeedUp = 1.2f;
        public const float SurvivorWalk = 120f * SpeedUp;
        public const float SurvivorRun = 190f * SpeedUp;
        /// <summary>Flashlight beams run on until they hit something; this is only a practical cap.</summary>
        public const float BeamRange = 2600f;

        public static class World
        {
            public const float Size = 6000f, WarehouseSize = 1200f;
            /// <summary>The survivors win if at least this fraction escaped.</summary>
            public const float EscapeFraction = 0.5f;
        }

        public static class Net
        {
            public const int TickHz = 30;
            public const float InterpolationDelayMs = 100f;
            public const float ReconnectGraceSec = 30f;
            public const int MaxPlayers = 10;
            public const float MaxSensingRadius = BeamRange + 100f;
        }

        public static class Survivor
        {
            public const float Radius = SurvivorRadius;
            public const float Walk = SurvivorWalk, Run = SurvivorRun, Crouch = 70f * SpeedUp, Crawl = 32f * SpeedUp;
            public const float StaminaMax = 8f, StaminaRefill = 10f;
            public const float HitHasteMul = 1.3f, HitHasteTime = 1.8f;
            public const float ConeHalfAngleDeg = 50f, VisionRange = BeamRange, Proximity = 95f;
            public const float DownedVisionMul = 0.6f;
            public const float AllyLightRadius = 150f, AllyLightIntensity = 0.3f, AllyBody = 40f, AllyBodyIntensity = 0.55f, AllyConeAlpha = 0.11f;
            public const float NoiseIdle = 0f, NoiseCrouch = 45f, NoiseWalk = 170f, NoiseRun = 430f;
            public const float WiggleTime = 16f;
            public const float ReviveHp = 1f / 3f;
            public const float HealTime = 12f, ReviveTime = 8f, UnstakeTime = 1.6f;
            public const float WindowClimbMul = 0.4f;
        }

        public static class Hunter
        {
            public const float Radius = HunterRadius;
            public const float Walk = SurvivorWalk * 0.9f * 0.95f;
            public const float Sprint = SurvivorRun * 1.2f * 0.95f;
            public const float StaminaMax = 6f, StaminaRefill = 10f;
            public const float CarrySpeedMul = 0.9f;
            public const float ConeHalfAngleDeg = 65f, VisionRange = BeamRange, Proximity = 125f;

            public static class Attack
            {
                public const float Range = 124f, ArcDeg = 100f, Windup = 0.15f, SwingTime = 0.32f;
                public const float HitCooldown = 0f, HitSlowMul = 0.45f, MissCooldown = 0f, MissSlowMul = 0.7f;
                public const int BarricadeHits = 2, DoorHits = 2;
                public const float ChargeMax = 0.9f, HeavyAt = 0.85f, ChargeRangeMul = 1.3f, ChargeArcMul = 1.25f, ChargeSlowMul = 0.65f, AutoRelease = 3f;
                public const float DamageBase = 1f / 3f, DamageFull = 2f / 3f, TapGrace = 0.1f;
                /// <summary>Light swings this soon after the last one come back the other way (forehand, backhand...).</summary>
                public const float ComboWindow = 0.9f;
            }

            public static class Lunge
            {
                public const int Charges = 2;
                public const float Recharge = 7f, Duration = 0.5f, Peak = 1150f, HitboxMul = 1.5f, Damage = 1f / 3f;
            }

            public static class Burst
            {
                public const float NpcStun = 2.5f, Cooldown = 12f, Speed = 1700f, Width = HunterWidth * 6f, Thickness = 36f;
                public const float ScareTime = 2.5f, ScareFade = 0.6f, ScareVolume = 0.7f, ZachVolume = 0.25f;
            }

            public static class Health
            {
                public const float Max = 100f, DownTime = 10f, RecoverFraction = 0.5f, RegenTime = 360f;
                public const float SpeedStep = 0.25f, SpeedPerStep = 0.1f, DownPenalty = 0.05f, DownPenaltyMax = 0.2f;
            }

            public const float StakeBuff = 0.05f, StakeRegen = 0.05f;
            public const float ScentRadius = 1300f, ScentSendEvery = 0.5f;

            public static class Vape
            {
                public const int Charges = 2;
                public const float Cooldown = 25f, HalfAngleDeg = 10f, ReachMul = 1.1f, DefaultView = 760f, MinView = 400f, MaxView = 2200f;
                public const float GrowTime = 0.6f, LingerTime = 4f, FadeTime = 1f, Coverage = 0.5f;
                public const float Slow = 0.45f, SlowFar = 0.15f, SlowAfter = 3f, NpcHitEvery = 1.5f;
                public const float Dps = 0.05f, DpsFar = 0.01f, AfterTime = 2f, ConeCut = 0.6f, DarkAfter = 6f, DarkEase = 0.6f;
            }

            public static class Hemp
            {
                public const float RegenBoost = 0.1f, SprintDrainMul = 0.8f, SprintRefillMul = 1.2f, Duration = 10f, Recover = 40f, Lockout = 5f;
                public const float ZoomOut = 1.2f, SpeedMul = 1.1f, ZoomRate = 1.5f, Grace = 0.3f;
            }

            public static class Beam
            {
                public const int Charges = 3;
                public const float Cooldown = 2f, Windup = 1f, ZachTickDamage = 0.01f, NpcHitEvery = 0.5f, TickRate = 10f, TickDamage = 0.03f;
                public const float AudioRadius = 1400f, AudioNear = 200f, AudioCurve = 1.5f, AudioVolume = 1f;
            }

            public const int JadenSlainLunge = 1;
            public const float JadenSlainRangeMul = 1.2f;
            public const float BookAbilityLock = 6f;
            public const float WindowClimbMul = 0.35f;
            public const float BreakBarricadeTime = 2.2f, DamageGenTime = 2f, PickupTime = 1f, StakeTime = 1.2f;
            public const float WindupSlowMul = 0.85f, HitSlowFraction = 0.75f, MissSlowTime = 0.45f, WiggleStun = 1.5f;
            public const float GenKnownRadius = 750f;
        }

        public const float SprintLockout = 1.5f;
        public const float WadeMul = 0.45f;

        public static class Hiding
        {
            public const float EnterTime = 0.6f, ExitTime = 0.5f, BreathMax = 6f, BreathRegen = 0.6f, BreathingHearRadius = 130f;
            public const float PeekHalfAngleDeg = 28f, PeekRange = 420f, PeekProximity = 40f;
            public const float GrassPeekHalfAngleDeg = 180f, GrassPeekRange = 150f, GrassPeekProximity = 150f;
            public const float BreathingIntervalSec = 1.5f, GaspCooldown = 2f;
            /// <summary>Zach within this distance hears a gasp (twice the breathing radius).</summary>
            public const float GaspHearRadius = 260f;
        }

        public static class Items
        {
            public static readonly (Player.ItemType item, int count)[] Counts =
            {
                (Player.ItemType.Piss, 6), (Player.ItemType.Sniper, 2), (Player.ItemType.Bottle, 20), (Player.ItemType.Goggles, 3),
                (Player.ItemType.Confit, 6), (Player.ItemType.Shotgun, 4), (Player.ItemType.DoctorPepper, 8), (Player.ItemType.Trap, 8),
                (Player.ItemType.Book, 4), (Player.ItemType.MrBeastBar, 15), (Player.ItemType.MiniShield, 20),
            };
            public static readonly Player.ItemType[] AmbulanceKit =
                { Player.ItemType.MiniShield, Player.ItemType.MiniShield, Player.ItemType.MrBeastBar, Player.ItemType.MrBeastBar, Player.ItemType.Confit };
            public const float BeastBarHeal = 0.2f;
            public const float ShieldDrinkTime = 2f, ShieldAmount = 0.25f, ShieldMax = 1f;
            public const float FlashTime = 0.8f, FlashFade = 0.3f;

            public static class Bottle { public const float Speed = 760f, Stun = 1.4f, HitRadius = 10f, Damage = 0.2f, ZachDamage = 5f; }
            public static class Book { public const float Speed = 700f, Stun = 2.5f, HitRadius = 12f, Damage = 0.2f, ZachDamage = 5f; public const int Images = 4; }
            public static class Goggles { public const float Meter = 15f, ConeMul = 1.2f; }

            public static class Shotgun
            {
                public const int Shells = 6, Pellets = 8;
                public const float Reload = 2f, Range = BeamRange, SpreadDeg = 9f, PelletDamage = 0.15f, Stun = 2.1f, KbPeak = 520f, KbDuration = 0.3f, ZachBlastDamage = 25f;
            }

            public static class Pistol
            {
                public const int Shots = 10;
                public const float Reload = 0.35f, Range = BeamRange, SpreadDeg = 1.5f, Damage = 0.1f, ZachDamage = 10f, Stun = 0.1f, KbPeak = 300f, KbDuration = 0.2f;
            }

            public static class Piss { public const float Speed = 760f, HitRadius = 10f, Mul = 1.5f, Time = 5f; }

            public const float AnywhereShare = 0.3f;
            public const float UseSlow = 0.5f, UseSlowTime = 0.8f;

            public static class Golden { public const int Shells = 5; public const float Reload = 1f; }

            public static class Sniper
            {
                public const int Shots = 3;
                public const float Reload = 1.2f, PelletSpeed = 4000f, Speed = 8000f, ZachHp = 25f, KbPeak = 1100f, KbDuration = 0.5f, SprintLock = 3f, GenDamage = 0.3f, HitRadius = 5f, LaserMax = 6000f;
            }

            public static class ZachPump
            {
                public const int Shots = 10;
                public const float Reload = 1f, PelletDamage = 0.09f, Stun = 0.1f, KbPeak = 380f, KbDuration = 0.2f;
            }

            public static class Energy { public const float Duration = 20f, RefillMul = 1.5f, BonusSec = 2f, SpeedMul = 0.15f; }

            public static class Trap
            {
                public const float PlantTime = 2f, TriggerRadius = HunterWidth * 5f, GasRadius = HunterWidth * 10f * 0.65f, ArmTime = 1f, GasTime = 7f, SpreadTime = 0.5f, SlowMul = 0.5f, ZachDps = 2f;
            }

            public const float StunImmunity = 2.5f;

            public static class Barricade { public const float Stun = 3f, SlamRadius = 60f, DropTime = 0.2f; }
        }

        public static class Xray { public const float Range = BeamRange, Brightness = 0.7f, FadeIn = 0.75f, FadeOut = 1f / 6f; }

        public static class Sexton
        {
            public const float LightRadius = 110f, LightIntensity = 0.28f;
            public const float Radius = 15f, Walk = 70f, Flee = 200f, FleeTime = 6f;
            public const int Hp = 3;
            public const float TalkTime = 1.6f, HandTime = 0.45f, Reach = 72f;
            public const float AudioNear = 60f, AudioFar = 950f, AudioCurve = 3f;
            public const float JarvisRadarSec = 10f;
            public const string SecondLine = "This is powerful tech, NAME. Be careful with it type shi";
            public const float SecondTalkTime = 2.4f, LeaveTime = 7f, JarvisMinimapMul = 1.2f;

            public static class Defense
            {
                public const float BeamTime = 3f, Cooldown = 3f, ApproachTime = 0.9f, BeamRange = 950f, BeamWidth = 10f, BeamDamage = 1f / 3f;
                public const int Attacks = 3;
                public const float TurnRate = 1.3f, Walk = 85f, Vicinity = 750f, ResetAfter = 10f, BottleStun = 0.1f, ShotStun = 0.3f, GasSlowMul = 0.5f;
                public const float LightRadius = 170f, LightIntensity = 0.85f;
            }
        }

        public static class Shane
        {
            public const float Radius = 15f, Walk = 65f, Chase = 200f, AlertRadius = 130f, ProxAlertSec = 2f, FlashAlertSec = 3.5f, AlertDecay = 0.2f;
            public const float ChaseTime = 20f, HunterBreakRadius = 260f, LoseRadius = 1100f;
            public const int BottlesToShake = 2;
            public const float FleeTime = 4f, Cooldown = 10f, LightRadius = 150f, LightIntensity = 0.4f;
            public const float StepsVolume = 0.55f, StepsNear = 70f, StepsFar = 750f;
        }

        public static class Jaden
        {
            public const float Radius = 15f, Walk = 62f, Chase = 175f, AlertRadius = 130f, ProxAlertSec = 2f, FlashAlertSec = 3.5f, AlertDecay = 0.2f;
            public const float ChaseTime = 20f, HunterBreakRadius = 260f, LoseRadius = 600f, Stun = 1.2f;
            public const int Hp = 3, ZachHp = 6, BottlesToShake = 2;
            public const float Kb = 46f, MeleeStun = 0.2f, FleeTime = 4f, Cooldown = 12f, LightRadius = 150f, LightIntensity = 0.4f;

            public static class Gun
            {
                public const float Range = 420f, Keep = 220f, Cooldown = 0.9f, Damage = 0.125f, ZachDamage = 0.08f, SpreadDeg = 5f, StopAfter = 0.5f;
            }
        }

        public static class Marc { public const float Radius = 15f, Walk = 62f, Reach = 72f, TalkCooldown = 3f, LightRadius = 110f, LightIntensity = 0.28f; }

        public static class Plasma
        {
            public const float LightRadius = 110f, LightIntensity = 0.28f, Radius = 15f, BeastRadius = 24f, Walk = 64f, Chase = 212f;
            public const float TransformTime = 2f, PunchRange = 34f, PunchCooldown = 0.85f, PunchDamage = 0.25f, ZachPunchDamage = 20f, EscapeTime = 10f, RageTime = 10f;
            public const int SurvivorHits = 6, ZachHits = 6;
            public const float LoseRadius = 900f, BottleStun = 0.1f, ShotStun = 0.3f, SlashStun = 0.1f, GasSlowMul = 0.45f, Reach = 72f;
        }

        public static class Waz
        {
            public const float Radius = 15f, Walk = 60f, Flee = 200f, FleeTime = 6f, Reach = 72f, FovBonus = 0.1f, FovPenalty = 0.1f;
            public const int Hp = 3;
            public const string Line = "lemme take a looksie";
        }

        public static class Chacko
        {
            public const float Radius = 14f, Reach = 70f, Explosion = 0.5f, NicRangeMul = 1.1f, VengeanceSec = 30f, LightRadius = 130f, LightIntensity = 0.3f;
            public const string LineSurvivor = "Take a Dr Pepper, bro. Madden is on.", LineZach = "Here. 50 Nic. Don't tell anyone.", LineHit = "Not during the game!";
        }

        public static class Njaaron
        {
            public const float Radius = 14f, Reach = 70f, Walk = 70f, Run = 175f;
            public const int Hp = 4, StunHits = 3;
            public const float Stun = 1.2f, AngrySec = 10f, PunchHp = 10f, SurvivorPunch = 0.1f, PunchRange = 40f, PunchCooldown = 0.9f, KbPeak = 300f, KbDuration = 0.25f;
            public const float ZachClose = 380f, ZachLose = 800f, RegenMul = 1.2f, AskSec = 8f, ZachBlast = 20f, SurvivorBlast = 0.5f, BlastRadius = 240f;
            public const string LineAsk = "you wanna go to the Y later?", LineYes = "Let's go!", LineNo = "cmon man";
        }

        public static class Soham { public const float Radius = 14f, Reach = 70f, Walk = 60f, Fuse = 2f; public const string Line = "Hi"; }

        public static class Monique
        {
            public const float Radius = 14f, Reach = 70f, Walk = 60f, FleeMul = 2f, FleeTime = 6f, ArrowSec = 40f, AttackSec = 10f, ShotDamage = 0.4f, Reload = 1.2f;
            public const string LineArrow = "spoiler alert", LineHi = "Hi there";
        }

        public static class Thomas
        {
            public const float Radius = 14f, Reach = 70f, Walk = 60f, FleeMul = 2f, FleeTime = 6f;
            public const string Line = "this is neat!", LineAfter = "I'm all out";
        }

        public const float NpcLightRadius = 120f, NpcLightIntensity = 0.3f;

        public static class Chris
        {
            public const float Radius = 15f, Walk = 60f, Wander = 70f, Run = SurvivorRun * 1.2f, Flee = 125f, FleeTime = 5f, Reach = 72f, Pace = 42f;
            public const int Hp = 2;
            public const float DownedAfter = 4f, StakedAfter = 4f, AscendTime = 3f, LightRadius = 110f, LightIntensity = 0.28f;
            public const float AmbulanceLength = 300f, AmbulanceWidth = 150f, AmbulanceLightRadius = 230f, AmbulanceLightIntensity = 0.35f;
        }

        public static class Objectives
        {
            public const float RepairTime = 70f, CoopStep = 0.25f, RepairNoise = 520f, RegressPerSec = 0.25f / 60f, DamageRegressInstant = 0.08f;
            public const float GateOpenTime = 20f, StakeStageTime = 60f;
        }

        public static class Notes { public const int Count = 4; public const float Spacing = 900f; }

        public static class Reach
        {
            public const float Note = 64f, Teammate = 70f, Generator = 78f, Gate = 58f, Loot = 48f, Hide = 58f, Barricade = 80f, Door = 62f, Stake = 78f, Pickup = 68f, NpcName = 110f;
        }

        public static class Trails { public const float ScentEvery = 0.12f, WalkHeadStart = 4f, BloodEvery = 0.4f, MaxAgeSec = 10f; }

        public static class Lights
        {
            public const int MaxPolygonsPerFrame = 6;
            public const float CampfireRadius = 360f, GeneratorRadius = 260f, LampRadius = 300f, FlickerSpeed = 7f;
        }

        public static class Scaling
        {
            public const float P0 = 4f, MinScale = 0.5f, MaxScale = 2f;
            public const float RepairTimeMin = 0.75f, RepairTimeMax = 1.35f;
            public const float HunterSpeedSlope = 0.06f, HunterSpeedMin = 0.95f, HunterSpeedMax = 1.08f;
            public const float StunMin = 0.75f, StunMax = 1.3f;
            public const int RequiredGenMin = 3, RequiredGenMax = 7;
        }
    }
}
