using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// Converts the original game's world units into this game's design units (metres; the level root scales them by
    /// <see cref="Vision.World.WorldScale.S"/> into world units). Distances always convert at 3 cm per unit. Speeds also
    /// carry the <see cref="MatchState.Pace"/> setting: 1 is the original's pace, about 0.37 this game's earlier human pace.
    /// </summary>
    public static class Scale
    {
        /// <summary>Design units per original unit.</summary>
        public const float Unit = Vision.World.MapLayout.Unit;
        /// <summary>
        /// Every character moves at 70% of the speed the pace gives (walking, running, Zach's lunge, knockbacks, the NPCs;
        /// thrown items, rounds and the Burst keep theirs), slowed by 30% at the user's request.
        /// </summary>
        public const float GlobalMove = 0.7f;
        /// <summary>The pace at which the survivor walks 3.2 world units a second (this game's earlier human pace).</summary>
        public const float HumanPace = 3.2f / (Vision.World.WorldScale.S * Unit * Balance.SurvivorWalk * GlobalMove);
        public const float MinPace = 0.3f, MaxPace = 1.25f;

        /// <summary>A distance in design units.</summary>
        public static float D(float units) => units * Unit;
        /// <summary>A position or offset in design units.</summary>
        public static Vector2 D(Vector2 units) => units * Unit;
        /// <summary>Back from design units to original units.</summary>
        public static float ToUnits(float design) => design / Unit;
        /// <summary>A movement speed in design units per second, at the match's pace (and the global 70%).</summary>
        public static float Speed(float unitsPerSecond) => unitsPerSecond * Unit * Pace * GlobalMove;
        public static float Pace => MatchState.Current != null ? MatchState.Current.Pace : 1f;
    }

    public enum Role : byte { Survivor, Hunter, Spectator }

    /// <summary>A survivor's state (the original's Health): Zach uses Healthy and Eliminated only.</summary>
    public enum Health : byte { Healthy = 0, Wounded = 1, Downed = 2, Carried = 3, Staked = 4, Escaped = 5, Eliminated = 6 }

    public enum MoveMode : byte { Normal = 0, Locked = 1, Crawl = 2 }

    public enum Gait : byte { Idle = 0, Walk = 1, Run = 2, Crouch = 3 }

    /// <summary>The timed thing a player is doing (the original's Action).</summary>
    public enum ActionKind : byte
    {
        None = 0, Repair = 1, Heal = 3, Revive = 4, Unstake = 5, OpenGate = 6, Loot = 7, HideEnter = 8, HideExit = 9,
        PickUp = 11, Stake = 12, Search = 13, DamageGen = 15, Attack = 17, Talk = 18, Plant = 19, Drink = 20,
    }

    public enum BarricadeState : byte { Up = 0, Down = 1, Broken = 2 }

    public enum Prompt : byte
    {
        None = 0, Repair = 1, Heal = 5, Revive = 6, Unstake = 7, OpenGate = 8, Loot = 9, Hide = 10, LeaveHiding = 11,
        DropBarricade = 13, PickUp = 14, Stake = 15, Search = 16, DamageGen = 18, GatePowerless = 19, InventoryFull = 20,
        OpenDoor = 21, CloseDoor = 22, TalkSexton = 23, TakeHemp = 24, TalkChris = 27, TalkMarc = 28, TalkPlasma = 29,
        SextonMore = 30, PickDrop = 31, NameNpc = 32, NameLoot = 33, NameDrop = 34, TalkWaz = 35, ReadNote = 36,
        TalkChacko = 37, TalkNjaaron = 38, NjaaronAsk = 39, TalkMonique = 40, TalkThomas = 41, TalkSoham = 42,
    }

    /// <summary>Buttons of one input frame (the original's Btn, with this game's key mapping).</summary>
    [System.Flags]
    public enum Btn : int
    {
        None = 0,
        /// <summary>Shift: sprint.</summary>
        Run = 1 << 0,
        /// <summary>C / Ctrl: crouch (survivors).</summary>
        Crouch = 1 << 1,
        /// <summary>E: interact.</summary>
        Interact = 1 << 2,
        /// <summary>Left mouse: Zach swings; survivors use the selected item.</summary>
        Primary = 1 << 3,
        /// <summary>F: Zach's Soundcloud Burst.</summary>
        Secondary = 1 << 4,
        /// <summary>Space: slam a barricade, hold breath while hidden.</summary>
        Space = 1 << 5,
        /// <summary>Q: JARVIS (survivors) or the Hemp Battery (Zach).</summary>
        Ability = 1 << 6,
        /// <summary>Right mouse: Zach's lunge.</summary>
        Lunge = 1 << 7,
        /// <summary>G: drop one of the selected item.</summary>
        Drop = 1 << 8,
        /// <summary>Space (Zach): Penjamin.</summary>
        Vape = 1 << 9,
        /// <summary>R: the Hemp Beam.</summary>
        Beam = 1 << 10,
        /// <summary>Y / N: answer Njaaron.</summary>
        Yes = 1 << 11,
        No = 1 << 12,
    }

    /// <summary>One frame of a player's input, sent to the authority (locally, or to the host online).</summary>
    public struct InputCmd
    {
        public uint Seq;
        public Btn Buttons;
        /// <summary>Movement direction on the ground plane (x east, y north), length at most 1.</summary>
        public Vector2 Move;
        /// <summary>Aim angle in radians (atan2 of the aim direction on the plane), and the distance to the cursor in original units.</summary>
        public float Aim;
        public float AimDist;
        /// <summary>Survivors: the selected inventory slot, 1-based (0 = none).</summary>
        public int Item;

        public bool Has(Btn b) => (Buttons & b) != 0;
        public Vector2 AimDir => new Vector2(Mathf.Cos(Aim), Mathf.Sin(Aim));
    }

    public static class Texts
    {
        public static readonly string[] NpcNames =
            { "Sexton Science", "Shane Jeans", "Chris Zelley", "Marc Cortez", "Plasma.TTV", "Jaden Nguyen", "Waz", "Chacko", "Njaaron", "Monique Bourgeois", "Thomas Bourgeois", "Soham" };

        public static string ActionLabel(ActionKind a) => a switch
        {
            ActionKind.Repair => "Repairing",
            ActionKind.Heal => "Healing",
            ActionKind.Revive => "Reviving",
            ActionKind.Unstake => "Unstaking",
            ActionKind.OpenGate => "Opening gate",
            ActionKind.Loot => "Picking up",
            ActionKind.HideEnter => "Hiding",
            ActionKind.HideExit => "Leaving",
            ActionKind.PickUp => "Picking up",
            ActionKind.Stake => "Staking",
            ActionKind.Search => "Searching",
            ActionKind.DamageGen => "Damaging",
            ActionKind.Attack => "Swinging",
            ActionKind.Talk => "Listening to Sexton",
            ActionKind.Plant => "Planting a galaxy gas trap",
            ActionKind.Drink => "Drinking a mini shield",
            _ => "",
        };

        /// <summary>The original's prompt text.</summary>
        public static string PromptLabel(Prompt p) => p switch
        {
            Prompt.Repair => "Hold E to start the generator",
            Prompt.Heal => "Hold E to heal",
            Prompt.Revive => "Hold E to revive",
            Prompt.Unstake => "Hold E to unstake",
            Prompt.OpenGate => "Hold E to open the gate",
            Prompt.Loot => "Press E to pick up",
            Prompt.Hide => "Press E to hide",
            Prompt.LeaveHiding => "Press E to leave",
            Prompt.DropBarricade => "Press Space to slam the barricade",
            Prompt.PickUp => "Press E to pick up",
            Prompt.Stake => "Press E to stake",
            Prompt.Search => "Press E to search",
            Prompt.DamageGen => "Hold E to damage generator",
            Prompt.GatePowerless => "The gate has no power",
            Prompt.InventoryFull => "Full",
            Prompt.OpenDoor => "Press E to open the door",
            Prompt.CloseDoor => "Press E to close the door",
            Prompt.TalkSexton => "Press E to talk to Sexton Science",
            Prompt.TakeHemp => "Press E to take the Hemp Beam",
            Prompt.TalkChris => "Press E to talk to Chris Zelley",
            Prompt.TalkMarc => "Press E to talk to Marc Cortez",
            Prompt.TalkPlasma => "Press E to talk to Plasma.TTV",
            Prompt.SextonMore => "Press E to keep listening",
            Prompt.PickDrop => "Press E to pick up",
            Prompt.TalkWaz => "Press E to talk to Waz",
            Prompt.ReadNote => "Press E to read the note",
            Prompt.TalkChacko => "Press E to talk to Chacko",
            Prompt.TalkNjaaron => "Press E to talk to Njaaron",
            Prompt.NjaaronAsk => "Y: yes     N: no",
            Prompt.TalkMonique => "Press E to talk to Monique Bourgeois",
            Prompt.TalkThomas => "Press E to talk to Thomas Bourgeois",
            Prompt.TalkSoham => "Press E to talk to Soham",
            _ => null,
        };
    }
}
