using System;
using System.IO;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// The lobby's balance and difficulty sliders (anyone in the lobby can move them; only the host can put them back to
    /// the defaults): how many generators there are, each side's speed, sprint and sprint recovery, the damage each side
    /// deals, each gun's damage and the rounds it starts with. The match being played reads them from
    /// <see cref="MatchState.Tuning"/>; the defaults are the game's own numbers, so an untouched lobby plays as before.
    /// </summary>
    public sealed class Tuning
    {
        public int Generators = DefaultGenerators;
        /// <summary>Multipliers on walking and running (and Zach's sprint).</summary>
        public float SurvivorSpeed = 1f, ZachSpeed = 1f;
        /// <summary>Seconds of sprint a full meter holds.</summary>
        public float SurvivorSprint = Balance.Survivor.StaminaMax, ZachSprint = Balance.Hunter.StaminaMax;
        /// <summary>Seconds an empty meter takes to fill again.</summary>
        public float SurvivorRecover = Balance.Survivor.StaminaRefill, ZachRecover = Balance.Hunter.StaminaRefill;
        /// <summary>Multipliers on all the damage each side deals to the other (and survivors to each other).</summary>
        public float SurvivorDamage = 1f, ZachDamage = 1f;
        /// <summary>Multipliers on each gun's damage (the golden pump's covers the Legendary pump and Zach's golden pump).</summary>
        public float ShotgunDamage = 1f, GoldenDamage = 1f, PistolDamage = 1f, SniperDamage = 1f;
        /// <summary>The rounds each gun starts with.</summary>
        public int ShotgunAmmo = Balance.Items.Shotgun.Shells, GoldenAmmo = Balance.Items.Golden.Shells, PistolAmmo = Balance.Items.Pistol.Shots, SniperAmmo = Balance.Items.Sniper.Shots;

        /// <summary>How many generators a map has unless the lobby says otherwise (two woods, two in the building, the graveyard's).</summary>
        public const int DefaultGenerators = 5;
        public const int MinGenerators = 2, MaxGenerators = 8;

        /// <summary>One slider: its label, range, step and how it reads and sets its number.</summary>
        public sealed class Knob
        {
            public string Label, Unit;
            public float Min, Max, Step;
            public Func<Tuning, float> Get;
            public Action<Tuning, float> Set;
            public float Default => Get(new Tuning());

            public string Format(float v) => Unit == "x" ? $"{v:0.00}x" : Unit == "s" ? $"{v:0.#} s" : Mathf.RoundToInt(v).ToString();
        }

        static Knob K(string label, string unit, float min, float max, float step, Func<Tuning, float> get, Action<Tuning, float> set) =>
            new Knob { Label = label, Unit = unit, Min = min, Max = max, Step = step, Get = get, Set = set };

        /// <summary>The sliders, in the order the lobby shows them (and they go on the wire).</summary>
        public static readonly Knob[] Knobs =
        {
            K("Generators", "", MinGenerators, MaxGenerators, 1f, t => t.Generators, (t, v) => t.Generators = Mathf.RoundToInt(v)),
            K("Survivor move speed", "x", 0.5f, 2f, 0.05f, t => t.SurvivorSpeed, (t, v) => t.SurvivorSpeed = v),
            K("Zach move speed", "x", 0.5f, 2f, 0.05f, t => t.ZachSpeed, (t, v) => t.ZachSpeed = v),
            K("Survivor sprint duration", "s", 2f, 20f, 0.5f, t => t.SurvivorSprint, (t, v) => t.SurvivorSprint = v),
            K("Zach sprint duration", "s", 2f, 20f, 0.5f, t => t.ZachSprint, (t, v) => t.ZachSprint = v),
            K("Survivor sprint recovery", "s", 2f, 30f, 0.5f, t => t.SurvivorRecover, (t, v) => t.SurvivorRecover = v),
            K("Zach sprint recovery", "s", 2f, 30f, 0.5f, t => t.ZachRecover, (t, v) => t.ZachRecover = v),
            K("Survivor damage", "x", 0.25f, 3f, 0.05f, t => t.SurvivorDamage, (t, v) => t.SurvivorDamage = v),
            K("Zach damage", "x", 0.25f, 3f, 0.05f, t => t.ZachDamage, (t, v) => t.ZachDamage = v),
            K("Shotgun damage", "x", 0.25f, 3f, 0.05f, t => t.ShotgunDamage, (t, v) => t.ShotgunDamage = v),
            K("Golden pump damage", "x", 0.25f, 3f, 0.05f, t => t.GoldenDamage, (t, v) => t.GoldenDamage = v),
            K("P250 damage", "x", 0.25f, 3f, 0.05f, t => t.PistolDamage, (t, v) => t.PistolDamage = v),
            K("0.50 cal damage", "x", 0.25f, 3f, 0.05f, t => t.SniperDamage, (t, v) => t.SniperDamage = v),
            K("Shotgun starting shells", "", 1f, 30f, 1f, t => t.ShotgunAmmo, (t, v) => t.ShotgunAmmo = Mathf.RoundToInt(v)),
            K("Golden pump starting shells", "", 1f, 40f, 1f, t => t.GoldenAmmo, (t, v) => t.GoldenAmmo = Mathf.RoundToInt(v)),
            K("P250 starting rounds", "", 1f, 50f, 1f, t => t.PistolAmmo, (t, v) => t.PistolAmmo = Mathf.RoundToInt(v)),
            K("0.50 cal starting rounds", "", 1f, 20f, 1f, t => t.SniperAmmo, (t, v) => t.SniperAmmo = Mathf.RoundToInt(v)),
        };

        /// <summary>A value as the slider takes it: within its range, on its step.</summary>
        public static float Snap(Knob k, float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = k.Default;
            v = Mathf.Clamp(v, k.Min, k.Max);
            return k.Step > 0f ? Mathf.Clamp(k.Min + Mathf.Round((v - k.Min) / k.Step) * k.Step, k.Min, k.Max) : v;
        }

        /// <summary>Moves one slider (out-of-range indices and values are ignored or pulled into range).</summary>
        public void SetKnob(int index, float value)
        {
            if (index < 0 || index >= Knobs.Length) return;
            Knobs[index].Set(this, Snap(Knobs[index], value));
        }

        public Tuning Copy()
        {
            var t = new Tuning();
            for (int i = 0; i < Knobs.Length; i++) Knobs[i].Set(t, Knobs[i].Get(this));
            return t;
        }

        /// <summary>Every slider in range and on its step.</summary>
        public Tuning Clamped()
        {
            var t = new Tuning();
            for (int i = 0; i < Knobs.Length; i++) t.SetKnob(i, Knobs[i].Get(this));
            return t;
        }

        public bool IsDefault
        {
            get
            {
                var d = new Tuning();
                for (int i = 0; i < Knobs.Length; i++) if (Mathf.Abs(Knobs[i].Get(this) - Knobs[i].Get(d)) > 1e-4f) return false;
                return true;
            }
        }

        public void Write(BinaryWriter w)
        {
            w.Write((byte)Knobs.Length);
            foreach (Knob k in Knobs) w.Write(k.Get(this));
        }

        public static Tuning Read(BinaryReader r)
        {
            var t = new Tuning();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                float v = r.ReadSingle();
                t.SetKnob(i, v);
            }
            return t;
        }

        // ---------------------------------------------------------------- what the match reads

        public float SpeedMul(Role role) => role == Role.Hunter ? ZachSpeed : SurvivorSpeed;
        public float SprintMax(Role role) => role == Role.Hunter ? ZachSprint : SurvivorSprint;
        public float SprintRefill(Role role) => role == Role.Hunter ? ZachRecover : SurvivorRecover;
        /// <summary>The damage a player of <paramref name="role"/> deals is multiplied by this.</summary>
        public float DamageBy(Role role) => role == Role.Hunter ? ZachDamage : SurvivorDamage;
    }
}
