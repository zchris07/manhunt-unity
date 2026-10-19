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
            /// <summary>
            /// How far (original units) a sound carries to other players: every event that makes one goes to everyone within
            /// this (as far as the loudest sound is heard), each hearing it fall off with distance.
            /// </summary>
            public const float HearingRadius = 3000f;
            /// <summary>
            /// A player's bullets and pellets only strike an NPC that isn't already roused within this much of what their
            /// screen shows (1.1 times its half width and half height about its middle).
            /// </summary>
            public const float OffscreenMargin = 1.1f;
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
                // The swipe reaches 15% less far than the original's 124 (at the user's request); the same arc.
                public const float Range = 124f * 0.85f, ArcDeg = 100f, Windup = 0.15f, SwingTime = 0.32f;
                public const float HitCooldown = 0f, HitSlowMul = 0.45f, MissCooldown = 0f, MissSlowMul = 0.7f;
                public const int BarricadeHits = 2, DoorHits = 2;
                // The charge takes a second longer than the original's (heavy at 1.85 s, full at 1.9 s), and a full
                // charge can still be held as long before it lets go on its own (4 s from the press).
                public const float ChargeMax = 0.9f + 1f, HeavyAt = 0.85f + 1f, ChargeRangeMul = 1.3f, ChargeArcMul = 1.25f, ChargeSlowMul = 0.65f, AutoRelease = 3f + 1f;
                /// <summary>
                /// An uncharged swing takes 10 of a survivor's 100 (the user's rule; the original's a third), growing evenly over
                /// the charge to two thirds fully charged. Pulling a survivor out of hiding still takes a third (<see cref="FoundDamage"/>).
                /// </summary>
                public const float DamageBase = 0.1f, DamageFull = 2f / 3f, TapGrace = 0.1f, FoundDamage = 1f / 3f;
                /// <summary>
                /// Swings, charged or not, go back and forth in an X (forehand, backhand...) while each is started within this
                /// long of the last one; after a longer pause the next starts again with the forehand.
                /// </summary>
                public const float ComboWindow = 1f;
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
                /// <summary>
                /// Each time Zach is put down his bar loses this much of its top for good (his speed still goes by his health
                /// out of the original 100, so he's slower too); each survivor he stakes adds <see cref="StakeMaxGain"/> to it
                /// (and to his health). He gets up with <see cref="RecoverFraction"/> of what's left; with nothing left, he's dead.
                /// </summary>
                public const float DownMaxLoss = 10f, StakeMaxGain = 5f;
                public const float SpeedStep = 0.25f, SpeedPerStep = 0.1f, DownPenalty = 0.05f, DownPenaltyMax = 0.2f;
            }

            public const float StakeBuff = 0.05f, StakeRegen = 0.05f;
            public const float ScentRadius = 1300f, ScentSendEvery = 0.5f;

            public static class Vape
            {
                public const int Charges = 2;
                // The cloud reaches 30% less far than the original's, then 20% further than that, then 10% further again
                // (ReachMul 1.1 x 0.7 x 1.2 x 1.1, at the user's requests); its cone 20% narrower than the original's 10 deg.
                // Then 40% narrower again, then 30% narrower again (3.36 deg either side), reaching 15% further.
                // Then 50% wider (5.04 deg either side).
                public const float Cooldown = 25f, HalfAngleDeg = 10f * 0.8f * 0.6f * 0.7f * 1.5f, ReachMul = 1.1f * 0.7f * 1.2f * 1.1f * 1.15f, DefaultView = 760f, MinView = 400f, MaxView = 2200f;
                public const float GrowTime = 0.6f, LingerTime = 4f, FadeTime = 1f, Coverage = 0.5f;
                /// <summary>
                /// The cloud is seen this much longer before it's fully gone, thinning away over its fade and these extra
                /// seconds; it does nothing in that time (its effect ends when it always did).
                /// </summary>
                public const float SeenExtra = 2f;
                /// <summary>
                /// 50 Nic's cone is 20% wider than Penjamin's (its reach is Chacko's <see cref="Chacko.NicRangeMul"/>). Out of
                /// either cloud, its slow, blur and narrowed (and darkened) sight all fall off together, linearly, over
                /// <see cref="Linger"/> seconds, all gone at once at the end of it.
                /// </summary>
                public const float NicAngleMul = 1.2f, Linger = 5f;
                /// <summary>How wide the cloud is either side of the aim right at his mouth (design units), as it's drawn.</summary>
                public const float MouthHalfWidth = 10f;
                /// <summary>(The strongest slow 40% less than the original's 0.45, at the user's request.)</summary>
                public const float Slow = 0.45f * 0.6f, SlowFar = 0.15f, SlowAfter = 3f, NpcHitEvery = 1.5f;
                public const float Dps = 0.05f, DpsFar = 0.01f, AfterTime = 2f, ConeCut = 0.6f, DarkAfter = 6f, DarkEase = 0.6f;
                /// <summary>The blur on a gassed survivor clears over this long once they're out (a second longer than the after-effect).</summary>
                public const float BlurTime = AfterTime + 1f;
            }

            public static class Hemp
            {
                /// <summary>(Recharges fully over 50 s, ten more than the original's 40, at the user's request.)</summary>
                public const float RegenBoost = 0.1f, SprintDrainMul = 0.8f, SprintRefillMul = 1.2f, Duration = 10f, Recover = 50f, Lockout = 5f;
                // The zoom out (and back in) takes 0.5 s: the original's is 1/1.5 s, quickened at the user's request.
                public const float ZoomOut = 1.2f, SpeedMul = 1.1f, ZoomRate = 2f, Grace = 0.3f;
            }

            public static class Beam
            {
                /// <summary>(Five charges, the original's three and two more, at the user's request.)</summary>
                public const int Charges = 5;
                // Every beam, whoever fires it, takes 1 HP of anyone it touches ten times a second (the user's rule; the
                // original's Zach beam took 3% of a survivor's health a tick). Anything it can destroy goes the moment it touches it.
                public const float Cooldown = 2f, Windup = 1f, ZachTickDamage = 0.01f, NpcHitEvery = 0.5f, TickRate = 10f, TickDamage = 0.01f;
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
                /// <summary>(Reloads swapped with the 0.50 cal's: the shotgun now 1.2 s, the 0.50 cal 2 s.)</summary>
                public const float Reload = 1.2f, Range = BeamRange, SpreadDeg = 9f, PelletDamage = 0.15f, Stun = 2.1f * 0.5f, KbPeak = 520f, KbDuration = 0.3f, ZachBlastDamage = 25f;
            }

            public static class Pistol
            {
                public const int Shots = 10;
                // Each shot strays up to 7.5 degrees either side of the aim (the original's 1.5, then 2.5, then 5 more at the user's request).
                /// <summary>(Its bloom 5 degrees wider than at first, 2.5 to 7.5; firing 40% faster, a shot every 0.25 s.)</summary>
                public const float Reload = 0.35f / 1.4f, Range = BeamRange, SpreadDeg = 2.5f + 5f, Damage = 0.1f, ZachDamage = 10f, Stun = 0.1f, KbPeak = 300f, KbDuration = 0.2f;
            }

            public static class Piss { public const float Speed = 760f, HitRadius = 10f, Mul = 1.5f, Time = 5f, BlurTime = 2f; }

            public const float AnywhereShare = 0.3f;
            public const float UseSlow = 0.5f, UseSlowTime = 0.8f;

            /// <summary>The Legendary pump: twelve shells in all (the user's rule).</summary>
            public static class Golden { public const int Shells = 12; public const float Reload = 1f; }

            public static class Sniper
            {
                /// <summary>Five rounds (shown as 5/5, 4/5...); another 0.50 cal picked up adds its rounds and five more to the count (10/10).</summary>
                public const int Shots = 5;
                /// <summary>Hit, Zach is slowed by this much, falling off linearly to nothing over <see cref="SlowTime"/> seconds.</summary>
                public const float Slow = 0.5f, SlowTime = 1f;
                public const float Reload = 2f, PelletSpeed = 4000f, Speed = 8000f, ZachHp = 25f, KbPeak = 1100f * 0.75f, KbDuration = 0.5f, SprintLock = 3f, GenDamage = 0.3f, HitRadius = 5f, LaserMax = 6000f;
                /// <summary>
                /// Right click aims it (the user's rule): the scope at the eye, the laser on, the view 15% wider, movement 30%
                /// slower, and the round goes exactly where it's aimed; fired from the hip it strays up to 5 degrees either side.
                /// </summary>
                public const float AimSlowMul = 0.7f, AimZoom = 1.15f, HipSpreadDeg = 5f;
            }

            public static class ZachPump
            {
                /// <summary>(Twelve, as the survivors' golden pump: the lobby's golden pump shells set both.)</summary>
                public const int Shots = 12;
                public const float Reload = 1f, PelletDamage = 0.09f, Stun = 0.1f, KbPeak = 380f, KbDuration = 0.2f;
            }

            public static class Energy { public const float Duration = 20f, RefillMul = 1.5f, BonusSec = 2f, SpeedMul = 0.15f; }

            public static class Trap
            {
                // The trigger 80% smaller than it was; the cloud 15% smaller.
                public const float PlantTime = 2f, TriggerRadius = HunterWidth * 5f * 0.2f, GasRadius = HunterWidth * 10f * 0.65f * 0.85f * 0.85f, ArmTime = 1f, GasTime = 7f, SpreadTime = 0.5f, SlowMul = 0.5f, ZachDps = 2f;
                /// <summary>Zach's vision cone in the gas is this much narrower (the user's rule); the blur that follows clears over BlurAfter s (3: a second longer than at first).</summary>
                public const float ZachConeCut = 0.3f, BlurAfter = 2f + 1f;
                /// <summary>An armed trap a shot or a thrown item passes over (within the trap's own size) bursts.</summary>
                public const float HitRadius = 20f;
            }

            public const float StunImmunity = 2.5f;

            public static class Barricade { public const float Stun = 3f, SlamRadius = 60f, DropTime = 0.2f; }
        }

        /// <summary>
        /// The edge of the woods (the user's rule, not the original's): three rings round the map's centre, the first at the
        /// playable radius and each Gap metres further out. Past the second the light dies over ShutdownTime s; once it's
        /// out a survivor loses TickDamage of health (straight through the shield) every TickEvery s. Zach crossing the third
        /// costs CrossCost health and can't be done again for CrossLockout s; a blocked ring stops him WallSkin inside it,
        /// and he arrives ArriveInside inside it on the far side.
        /// </summary>
        public static class Boundary
        {
            /// <summary>
            /// The second ring 15.6 m past the first (30% further than its first 12 m), and the end of the world 18 m past
            /// the second (50% further than its first 12 m; <see cref="Outer"/> from the first ring in all, and the world is
            /// made that much bigger).
            /// </summary>
            public const float Gap = 12f * 1.3f, KillGap = 12f * 1.5f, Outer = Gap + KillGap;
            /// <summary>
            /// The first ring stands this far past the map's edge (30% of <see cref="Gap"/>, 4.68 m), so the stretch from it to
            /// the shutdown ring (still <see cref="Gap"/> past the edge) is 30% shorter: <see cref="InnerGap"/>.
            /// </summary>
            public const float FirstRing = Gap * 0.3f, InnerGap = Gap - FirstRing;
            /// <summary>Zach's halo once his light has died past the second ring: its reach (m) and brightness.</summary>
            public const float HaloRadius = 4f, HaloIntensity = 0.8f;
            public const float ShutdownTime = 3f, TickEvery = 3f, TickDamage = 0.2f, CrossCost = 20f, CrossLockout = 10f;
            /// <summary>Gone into the woods: the screen stays black this long while "Sacrificed" is written out, then lifts over a second.</summary>
            public const float SacrificedHold = 5.2f;
            public const float WallSkin = 0.05f, ArriveInside = 1.5f;
        }

        /// <summary>
        /// See-through light (night vision goggles, the Hemp Battery): it grows out of the torch to its full reach while it
        /// fades in, over <see cref="FadeIn"/> s (the original's 0.75 s, quickened to 0.5 s at the user's request), and
        /// fades out in <see cref="FadeOut"/> s, or as slowly as it came for the Hemp Battery; it starts at
        /// <see cref="StartReach"/> of its reach.
        /// </summary>
        public static class Xray { public const float Range = BeamRange, Brightness = 0.7f, FadeIn = 0.5f, FadeOut = 1f / 6f, StartReach = 0.35f; }

        /// <summary>
        /// Ghosts (the user's rules): a sacrificed survivor is back as one after <see cref="RespawnDelay"/> s (out of the woods,
        /// after the black screen). Taking a body plays for <see cref="PossessTime"/> s (it levitates upright and the ghost
        /// merges with it), from no further than <see cref="Reach"/> units; a body dropped falls over <see cref="FallTime"/> s.
        /// In a body they help a living survivor's repair or gate at <see cref="HelpShare"/> of the speed-up another living
        /// survivor would add.
        /// </summary>
        /// <summary>
        /// NPCs on a chase (the user's rules): they climb over logs and fences and through windows (smashing any glass) at a
        /// survivor's climbing pace; Jaden, Njaaron and Plasma knock a shut door down with <see cref="DoorPunches"/> punches,
        /// one every <see cref="DoorPunchEvery"/> s.
        /// </summary>
        public static class NpcChase { public const int DoorPunches = 3; public const float DoorPunchEvery = 0.6f, ClimbMul = Survivor.WindowClimbMul; }

        public static class Ghost { public const float RespawnDelay = 4f, PossessTime = 3.6f, FallTime = 0.6f, Reach = 70f, HelpShare = 2f / 3f; }

        public static class Sexton
        {
            public const float LightRadius = 110f, LightIntensity = 0.28f;
            public const float Radius = 15f, Walk = 70f, Flee = 200f, FleeTime = 6f;
            public const int Hp = 3;
            public const float TalkTime = 1.6f, HandTime = 0.45f, Reach = 72f;
            public const float AudioNear = 60f, AudioFar = 950f, AudioCurve = 3f;
            /// <summary>
            /// How long JARVIS runs (20 s, the user's rule): its boot, its screen, the map and every survivor's whole screen lit,
            /// for every survivor in the match each time anyone with the tablet uses it. It's used again after
            /// <see cref="JarvisCooldown"/> s, counted from when it ends.
            /// </summary>
            public const float JarvisUserSec = 20f, JarvisCooldown = 90f;
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
            public const float Radius = 14f, Reach = 70f, Explosion = 0.5f, NicRangeMul = 1.1f, LightRadius = 130f, LightIntensity = 0.3f;
            public const string LineSurvivor = "Take a Dr Pepper, bro. Madden is on.", LineZach = "Here. 50 Nic. Don't tell anyone.", LineHit = "Not during the game!";
        }

        public static class Njaaron
        {
            /// <summary>(Runs 20% faster than the original's 175.)</summary>
            public const float Radius = 14f, Reach = 70f, Walk = 70f, Run = 175f * 1.2f;
            public const int Hp = 4, StunHits = 3;
            public const float Stun = 1.2f, AngrySec = 10f, PunchHp = 10f, SurvivorPunch = 0.1f, PunchRange = 40f, PunchCooldown = 0.9f, KbPeak = 300f, KbDuration = 0.25f;
            public const float AllyHuntSec = 20f;
            public const float ZachClose = 380f, ZachLose = 800f, RegenMul = 1.1f, AskSec = 8f, ZachBlast = 20f, SurvivorBlast = 0.5f, BlastRadius = 240f;
            public const string LineAsk = "you wanna go to the Y later?", LineYes = "Let's go!", LineNo = "cmon man";
        }

        public static class Soham { public const float Radius = 14f, Reach = 70f, Walk = 60f, Fuse = 2f; public const string Line = "Hi"; }

        public static class Monique
        {
            public const float Radius = 14f, Reach = 70f, Walk = 60f, FleeMul = 2f, FleeTime = 6f, ArrowSec = 40f, AttackSec = 10f, ShotDamage = 0.4f;
            /// <summary>The most 0.50 cal shots she fires before putting it away (an attack after that starts her again).</summary>
            public const int Shots = 3;
            /// <summary>Monique's 0.50 cal reloads as a survivor's does.</summary>
            public const float Reload = Items.Sniper.Reload;
            /// <summary>The most shotgun blasts Daphne fires before she goes back to passive (an attack after that starts her again).</summary>
            public const int DaphneShots = 5;
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
            /// <summary>
            /// His health (out of Zach's damage to a survivor's 100): nobody can harm him, except Zach while he's reviving
            /// someone or cutting them down off a stake.
            /// </summary>
            public const float Health = 50f;
            public const float DownedAfter = 4f, StakedAfter = 4f, AscendTime = 3f, LightRadius = 110f, LightIntensity = 0.28f;
            public const float AmbulanceLength = 300f, AmbulanceWidth = 150f, AmbulanceLightRadius = 230f, AmbulanceLightIntensity = 0.35f;
            /// <summary>The light bar's own light: it lights the roof and just round the van (metres, brightness), no further.</summary>
            public const float BarLightRange = 6.5f, BarLightIntensity = 0.42f;
        }

        public static class Objectives
        {
            public const float RepairTime = 70f * 0.75f, CoopStep = 0.25f, RepairNoise = 520f, RegressPerSec = 0.25f / 60f, DamageRegressInstant = 0.08f;
            /// <summary>(Opening a gate takes 10 s, half the original's 20, at the user's request.)</summary>
            public const float GateOpenTime = 10f, StakeStageTime = 60f;
            /// <summary>
            /// Once a gate opens (or Zach slams the hatch), anyone still on the map after this many seconds is taken by the
            /// Entity (Dead by Daylight's end game collapse). A survivor this close to the open hatch (units) jumps in.
            /// </summary>
            public const float EndGameTime = 120f, HatchRadius = 34f;
            /// <summary>How far (units) a started generator's sound carries: ten feet (3.05 m).</summary>
            public const float GeneratorHearRadius = 3.048f / 0.03f;
        }

        /// <summary>
        /// The paper notes: <see cref="Images"/> photos, each found once. One lies beside every generator (the graveyard's
        /// gets a random one); the rest lie off the paths, as before.
        /// </summary>
        public static class Notes { public const int Count = 4, Images = 6; public const float Spacing = 900f, BesideGenerator = 2.2f; }

        /// <summary>
        /// The graveyard: one way in, through ornate iron gates that start locked. A survivor picks the lock with
        /// <see cref="PickChecks"/> timing checks (stop the spinning tick on its mark; each spins faster with a narrower mark);
        /// <see cref="PickMisses"/> misses start it over. Unlocked, the gates swing open and open and close freely. Closed
        /// again, Zach can only smash them in (<see cref="GateHits"/>: four full charged swings, two hits each, or eight
        /// light ones). No one climbs the fence, Zach included.
        /// </summary>
        public static class Graveyard
        {
            public const int PickChecks = 5, PickMisses = 2, GateHits = 4 * 2;
            public static readonly float[] PickSpeeds = { 200f, 230f, 260f, 290f, 320f };
            /// <summary>(10% narrower than they were, at the user's request: the lock 10% harder.)</summary>
            public static readonly float[] PickWidths = { 36f * 0.9f, 32f * 0.9f, 29f * 0.9f, 27f * 0.9f, 25f * 0.9f };
            public const float PickPause = 0.45f;
        }

        /// <summary>
        /// A generator's skill checks: while repairing, every <see cref="Every"/> seconds a dial flashes up and a tick sweeps
        /// round it once; stopping it on the mark (Space or a click) takes <see cref="Bonus"/> seconds off the repair. Faster
        /// than the lock pick's fastest tick and on a narrower mark than its narrowest. A miss costs nothing.
        /// </summary>
        public static class SkillCheck
        {
            public const float Every = 8f, Bonus = 4f, Speed = 350f, Width = 14f, Lead = 0.55f;
        }

        public static class Reach
        {
            public const float Hatch = 80f, Note = 64f, Teammate = 70f, Generator = 78f, Gate = 58f, Loot = 48f, Hide = 58f, Barricade = 80f, Door = 62f, Stake = 78f, Pickup = 68f, NpcName = 110f;
        }

        public static class Trails
        {
            public const float ScentEvery = 0.12f, WalkHeadStart = 4f, MaxAgeSec = 10f;
            /// <summary>
            /// The scent trail swells and thins on a strict repeat along its length: from its thickest, most solid point to its
            /// thinnest, faintest is as far as a survivor sprints in this many seconds, and back again the same.
            /// </summary>
            public const float ScentWaveSprintSec = 2f;
            /// <summary>
            /// Blood drips at random moments (every 0.15-0.75 s, 0.45 s on average, about the original's 0.4 s) and lands up to
            /// BloodSpread units about them. Standing still, no more than BloodPoolMax drops land within BloodPoolRadius of
            /// where they stand; moving on that far starts them again.
            /// </summary>
            public const float BloodEveryMin = 0.15f, BloodEveryMax = 0.75f, BloodSpread = 8f, BloodPoolRadius = 16f;
            public const int BloodPoolMax = 6;
        }

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
