using System.Collections.Generic;
using UnityEngine;

namespace Vision.Audio
{
    /// <summary>
    /// The game's sound cues and the files behind them (the fetched CC0 packs in Resources/Audio/Fx, see CREDITS.md):
    /// each cue picks one of its variants at random, with a little pitch and volume jitter so repeats don't sound
    /// mechanical. Some cues are layered or pitched to build sounds no pack has (a heavy machete swing).
    /// </summary>
    public static class SoundBank
    {
        public sealed class Cue
        {
            public string[] Files;
            /// <summary>
            /// A local file played instead when it's there (Resources/Audio/Fx/local_*, kept out of git: sounds from other
            /// games, used only in the user's own builds); without it the cue falls back to <see cref="Files"/>, or is silent.
            /// </summary>
            public string[] Prefer;
            public float Volume = 1f, Pitch = 1f, PitchJitter = 0.06f, VolumeJitter = 0.1f;
            /// <summary>Original units: full volume within Near, silent past Radius.</summary>
            public float Near = 150f, Radius = 1400f;
        }

        static string[] Range(string prefix, int from, int to, string fmt = "D3")
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++) list.Add(prefix + i.ToString(fmt));
            return list.ToArray();
        }

        public static readonly Dictionary<string, Cue> Cues = new Dictionary<string, Cue>
        {
            // Zach.
            ["swing"] = new Cue { Files = new[] { "swishes_swish-1", "swishes_swish-2", "swishes_swish-3", "swishes_swish-4" }, Volume = 0.8f, Radius = 900f },
            // The full charged swing: the heavy swish pitched well down, with a deep, slow whoosh layered under it.
            ["swing_heavy"] = new Cue { Files = new[] { "swishes_swish-9", "swishes_swish-10", "swishes_swish-11", "swishes_swish-12", "swishes_swish-13" }, Volume = 1f, Pitch = 0.64f, PitchJitter = 0.04f, Radius = 1200f },
            ["swing_heavy_low"] = new Cue { Files = new[] { "swishes_swish-7", "swishes_swish-8" }, Volume = 0.85f, Pitch = 0.42f, PitchJitter = 0.03f, Radius = 1200f },
            ["slash"] = new Cue { Prefer = new[] { "local_axe_flesh" }, Files = new[] { "rpg_knifeslice", "rpg_knifeslice2", "rpg_chop" }, Volume = 0.9f, Radius = 1000f },
            ["body_hit"] = new Cue { Files = Range("impact_impactpunch_heavy_", 0, 4), Volume = 0.8f, Radius = 900f },
            ["lunge"] = new Cue { Files = new[] { "swishes_swish-7", "swishes_swish-8" }, Volume = 0.8f, Pitch = 0.7f, Radius = 900f },
            ["kick"] = new Cue { Files = Range("impact_impactmetal_heavy_", 0, 4), Volume = 0.9f, Radius = 1200f },
            ["smash"] = new Cue { Files = Range("impact_impactwood_heavy_", 0, 4), Volume = 1f, Radius = 1300f },
            ["splinter"] = new Cue { Files = Range("impact_impactplank_medium_", 0, 4), Volume = 0.8f, Radius = 1000f },
            // A window breaking.
            ["glass"] = new Cue { Prefer = new[] { "local_glass_break" }, Files = new[] { "impact_impactglass_heavy_000", "impact_impactglass_heavy_001", "impact_impactglass_heavy_002", "sfx100_glass_01", "sfx100_glass_02" }, Volume = 0.8f, Radius = 1300f },
            // Guns. The 0.50 cal's crack, synthesised for the game in the character of a Noita rifle (tools/synth_sniper.py);
            // the other guns stay silent.
            ["shotgun_shot"] = new Cue { Prefer = new[] { "local_tactical_shotgun" }, Files = new string[0], Volume = 0.5f, PitchJitter = 0.04f, Near = 250f, Radius = 2600f },
            ["pistol_shot"] = new Cue { Prefer = new[] { "local_p250" }, Files = new string[0], Volume = 0.9f, PitchJitter = 0.05f, Near = 200f, Radius = 2200f },
            // A survivor sacrificed (staked to the end, or gone into the woods), gone from the match for good: heard by all.
            ["sacrificed"] = new Cue { Prefer = new[] { "local_noita_game_over" }, Files = new string[0], Volume = 0.9f, PitchJitter = 0f, VolumeJitter = 0f },
            // The machete biting into wood (a tree, a pole, a door, a crate): one of five cracks, each fading out linearly
            // to nothing over its length.
            ["machete_wood"] = new Cue { Prefer = new[] { "local_wood_hit_1", "local_wood_hit_2", "local_wood_hit_3", "local_wood_hit_4", "local_wood_hit_5" }, Files = new string[0], Volume = 0.425f, PitchJitter = 0.06f, Near = 150f, Radius = 1300f },
            // Into sturdy metal (the building's doors, a car, a generator, a light tower, a locker): one of three clangs,
            // as loud as the wood and fading out the same way.
            ["metal_heavy"] = new Cue { Prefer = new[] { "local_metal_heavy_1", "local_metal_heavy_2", "local_metal_heavy_3" }, Files = new string[0], Volume = 0.425f, PitchJitter = 0.05f, Near = 150f, Radius = 1300f },
            // Into lighter metal (fences, the graveyard's gates, lampposts, barrels, racks): one of nine, as loud, held to the end.
            ["metal_light"] = new Cue { Prefer = new[] { "local_metal_1", "local_metal_2", "local_metal_3", "local_metal_4", "local_metal_5", "local_metal_6", "local_metal_7", "local_metal_8", "local_metal_9" }, Files = new string[0], Volume = 0.425f, PitchJitter = 0.05f, Near = 150f, Radius = 1300f },
            // Into a wall of a building, a cabin or a shack: one of three slams.
            ["wall_slam"] = new Cue { Prefer = new[] { "local_wall_slam_1", "local_wall_slam_2", "local_wall_slam_3" }, Files = new string[0], Volume = 0.5f, PitchJitter = 0.05f, Near = 150f, Radius = 1300f },
            // A bullet or pellet striking a body (a player or an NPC): one of three, quiet, held to the end.
            ["bullet_flesh"] = new Cue { Prefer = new[] { "local_headshot_1", "local_headshot_2", "local_headshot_3" }, Files = new string[0], Volume = 0.3f, PitchJitter = 0.04f, VolumeJitter = 0f, Near = 150f, Radius = 1200f },
            // The 0.50 cal: the bolt-action rifle's whole clip (the shot and the bolt worked), as loud as the shotgun and
            // held to the end; without it, the synthesised crack.
            ["sniper_shot"] = new Cue { Prefer = new[] { "local_bolt_sniper" }, Files = new[] { "synth_sniper_1", "synth_sniper_2", "synth_sniper_3" }, Volume = 0.5f, PitchJitter = 0.02f, VolumeJitter = 0f, Near = 300f, Radius = 3000f },
            // Things.
            ["door_open"] = new Cue { Files = new[] { "rpg_dooropen_1", "rpg_dooropen_2" }, Volume = 0.7f, Radius = 900f },
            ["door_close"] = new Cue { Files = new[] { "rpg_doorclose_1", "rpg_doorclose_2", "rpg_doorclose_3", "rpg_doorclose_4" }, Volume = 0.7f, Radius = 900f },
            ["slam"] = new Cue { Files = new[] { "sfx100_slam_01", "sfx100_slam_02", "sfx100_slam_03", "sfx100_slam_04" }, Volume = 1f, Radius = 1300f },
            ["pickup"] = new Cue { Files = new[] { "rpg_handlesmallleather", "rpg_handlesmallleather2", "rpg_cloth1", "rpg_cloth2" }, Volume = 0.6f, Radius = 400f },
            ["throw"] = new Cue { Files = new[] { "swishes_swish-2", "swishes_swish-3" }, Volume = 0.6f, Pitch = 1.15f, Radius = 600f },
            ["bottle_break"] = new Cue { Files = Range("impact_impactglass_light_", 0, 4), Volume = 0.9f, Radius = 1100f },
            ["book_hit"] = new Cue { Files = new[] { "rpg_bookplace1", "rpg_bookplace2", "rpg_bookplace3" }, Volume = 1f, Pitch = 0.8f, Radius = 900f },
            ["splash"] = new Cue { Files = new[] { "sfx100_splash_01", "sfx100_splash_02" }, Volume = 0.8f, Radius = 900f },
            ["drink"] = new Cue { Files = new[] { "sfx100_plop_01", "sfx100_plop_02" }, Volume = 0.6f, Radius = 400f },
            ["eat"] = new Cue { Files = new[] { "rpg_cloth3", "rpg_cloth4" }, Volume = 0.6f, Radius = 400f },
            ["trap"] = new Cue { Files = new[] { "rpg_metallatch", "rpg_metalclick" }, Volume = 0.8f, Radius = 700f },
            ["gas"] = new Cue { Files = new[] { "sfx100_noise_01", "sfx100_noise_02" }, Volume = 0.7f, Radius = 1200f },
            ["stun"] = new Cue { Files = Range("impact_impactplate_heavy_", 0, 4), Volume = 0.8f, Radius = 900f },
            ["generator_done"] = new Cue { Files = new[] { "sfx100_machine_01", "sfx100_machine_02" }, Volume = 0.9f, Radius = 2400f },
            ["gate"] = new Cue { Files = new[] { "sfx100_metal_03", "sfx100_metal_07" }, Volume = 1f, Radius = 3000f },
            ["note"] = new Cue { Files = new[] { "sfx100_paper_01", "sfx100_paper_02", "sfx100_paper_03" }, Volume = 0.8f, Radius = 300f },
            ["hide"] = new Cue { Files = new[] { "rpg_creak1", "rpg_creak2", "rpg_creak3" }, Volume = 0.7f, Radius = 700f },
            // The graveyard's lock and gates.
            ["pick_hit"] = new Cue { Files = new[] { "rpg_metalclick" }, Volume = 0.8f, Pitch = 1.25f, Radius = 300f },
            ["pick_miss"] = new Cue { Files = new[] { "rpg_metallatch" }, Volume = 0.6f, Pitch = 0.7f, Radius = 300f },
            ["pick_reset"] = new Cue { Files = Range("impact_impactmetal_light_", 0, 4), Volume = 0.5f, Pitch = 0.8f, Radius = 400f },
            ["gate_unlock"] = new Cue { Files = new[] { "rpg_metallatch" }, Volume = 1f, Pitch = 0.85f, Radius = 900f },
            ["gate_swing"] = new Cue { Files = new[] { "rpg_creak1", "rpg_creak2", "rpg_creak3" }, Volume = 0.9f, Pitch = 0.7f, Radius = 1000f },
            // Footsteps (by ground).
            ["step_grass"] = new Cue { Files = Range("impact_footstep_grass_", 0, 4), Volume = 0.35f, Radius = 600f, PitchJitter = 0.08f },
            ["step_concrete"] = new Cue { Files = Range("impact_footstep_concrete_", 0, 4), Volume = 0.35f, Radius = 600f, PitchJitter = 0.08f },
            ["step_wood"] = new Cue { Files = Range("impact_footstep_wood_", 0, 4), Volume = 0.35f, Radius = 600f, PitchJitter = 0.08f },
            ["step_shane"] = new Cue { Files = new[] { "steps_gravel", "steps_leaves01", "steps_leaves02", "steps_mud02" }, Volume = 0.8f, Near = 70f, Radius = 750f, PitchJitter = 0.1f },
        };

        static readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>();

        public static AudioClip Load(string file)
        {
            if (loaded.TryGetValue(file, out AudioClip c)) return c;
            c = Resources.Load<AudioClip>("Audio/Fx/" + file);
            loaded[file] = c;
            return c;
        }
    }
}
