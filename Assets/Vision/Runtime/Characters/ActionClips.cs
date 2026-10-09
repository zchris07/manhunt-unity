using System.Collections.Generic;
using UnityEngine;

namespace Vision.Characters
{
    /// <summary>
    /// Every action clip, authored as key poses (local Euler degrees on the rig: +X pitches the spine and head forward,
    /// -X raises an arm forward or flexes a hip, +Z on a right arm lifts it out to the side, +X on a shin bends the knee).
    /// Survivors' interactions and item use, damage states, and Zach's machete and abilities.
    /// </summary>
    public static class ActionClips
    {
        static ActionPose P() => new ActionPose();

        /// <summary>One knee down, the other foot planted: for work at ground level.</summary>
        static ActionPose Kneel() => P().Lower(0.43f)
            .R(Bone.ThighR, -80f).R(Bone.ShinR, 85f).R(Bone.FootR, -4f)
            .R(Bone.ThighL, -6f).R(Bone.ShinL, 96f).R(Bone.FootL, 0f);

        static ActionPose Stand() => P();

        static ActionPose With(ActionPose a, System.Action<ActionPose> f)
        {
            ActionPose p = a.Copy();
            f(p);
            return p;
        }

        // ---------------------------------------------------------------- survivors' work

        /// <summary>Kneeling at a generator, both hands working (loop).</summary>
        public static readonly ActionClip Repair = new ActionClip("Repair", 1.4f, true)
            .Key(0f, With(Kneel(), p => p.R(Bone.Spine, 25f).R(Bone.Chest, 10f).R(Bone.Head, 15f).R(Bone.UpperArmR, -55f, 0f, 8f).R(Bone.ForearmR, -35f).R(Bone.HandR, 10f).R(Bone.UpperArmL, -50f, 0f, -8f).R(Bone.ForearmL, -45f).R(Bone.HandL, 10f)))
            .Key(0.35f, With(Kneel(), p => p.R(Bone.Spine, 27f).R(Bone.Chest, 12f).R(Bone.Head, 17f).R(Bone.UpperArmR, -47f, 0f, 8f).R(Bone.ForearmR, -48f).R(Bone.HandR, -5f).R(Bone.UpperArmL, -57f, 0f, -8f).R(Bone.ForearmL, -32f).R(Bone.HandL, 15f)))
            .Key(0.7f, With(Kneel(), p => p.R(Bone.Spine, 24f).R(Bone.Chest, 9f).R(Bone.Head, 14f).R(Bone.UpperArmR, -58f, 0f, 9f).R(Bone.ForearmR, -30f).R(Bone.HandR, 15f).R(Bone.UpperArmL, -46f, 0f, -9f).R(Bone.ForearmL, -50f).R(Bone.HandL, -5f)))
            .Key(1.05f, With(Kneel(), p => p.R(Bone.Spine, 26f).R(Bone.Chest, 11f).R(Bone.Head, 16f).R(Bone.UpperArmR, -50f, 0f, 7f).R(Bone.ForearmR, -44f).R(Bone.HandR, 0f).R(Bone.UpperArmL, -54f, 0f, -7f).R(Bone.ForearmL, -38f).R(Bone.HandL, 12f)))
            .Blend(0.35f, 0.3f);

        /// <summary>Hauling on the gate lever: arms up and forward, leaning back into each pull (loop).</summary>
        public static readonly ActionClip Lever = new ActionClip("Lever", 1.6f, true)
            .Key(0f, P().Both(Bone.UpperArmR, -125f, 0f, 6f).Both(Bone.ForearmR, -25f).R(Bone.Spine, -4f).R(Bone.Head, -6f))
            .Key(0.8f, P().Lower(0.07f).Both(Bone.UpperArmR, -95f, 0f, 6f).Both(Bone.ForearmR, -10f).R(Bone.Spine, 12f).R(Bone.Head, 4f).Both(Bone.ThighR, -18f).Both(Bone.ShinR, 30f))
            .Blend(0.3f, 0.3f);

        /// <summary>Kneeling over a teammate: healing, reviving or cutting them down (loop).</summary>
        public static readonly ActionClip Tend = new ActionClip("Tend", 1.2f, true)
            .Key(0f, With(Kneel(), p => p.R(Bone.Spine, 33f).R(Bone.Chest, 8f).R(Bone.Head, 20f).Both(Bone.UpperArmR, -66f, 0f, 6f).Both(Bone.ForearmR, -20f).Both(Bone.HandR, 25f)))
            .Key(0.6f, With(Kneel(), p => p.Lower(0.46f).R(Bone.Spine, 38f).R(Bone.Chest, 10f).R(Bone.Head, 22f).Both(Bone.UpperArmR, -72f, 0f, 6f).Both(Bone.ForearmR, -12f).Both(Bone.HandR, 30f)))
            .Blend(0.35f, 0.3f);

        /// <summary>Bending to pick something up (one-shot; "grab" at the bottom).</summary>
        public static readonly ActionClip PickUp = new ActionClip("PickUp", 0.75f)
            .Key(0f, Stand())
            .Key(0.32f, P().Lower(0.14f).R(Bone.Spine, 42f).R(Bone.Chest, 10f).R(Bone.Head, 12f).R(Bone.UpperArmR, -45f, 0f, 6f).R(Bone.ForearmR, -12f).Both(Bone.ThighR, -32f).Both(Bone.ShinR, 48f))
            .Key(0.75f, Stand())
            .Event(0.32f, "grab")
            .Blend(0.08f, 0.12f);

        /// <summary>Crouched low (going into or out of a hiding spot).</summary>
        public static readonly ActionClip Crouch = new ActionClip("Crouch", 0.5f)
            .Key(0f, P().Lower(0.32f).Both(Bone.ThighR, -62f).Both(Bone.ShinR, 98f).Both(Bone.FootR, -20f).R(Bone.Spine, 22f).R(Bone.Head, -12f).Both(Bone.UpperArmR, -30f, 0f, 8f).Both(Bone.ForearmR, -40f))
            .Blend(0.2f, 0.25f);

        /// <summary>Drinking (a mini shield, Doctor Pepper): the can to the mouth, head tipped back, sipping (loop).</summary>
        public static readonly ActionClip Drink = new ActionClip("Drink", 1.6f, true)
            .Key(0f, P().R(Bone.UpperArmR, -78f, -58f, -5f).R(Bone.ForearmR, -122f).R(Bone.HandR, 20f).R(Bone.Head, -10f).R(Bone.Neck, -4f))
            .Key(0.8f, P().R(Bone.UpperArmR, -82f, -60f, -5f).R(Bone.ForearmR, -128f).R(Bone.HandR, 30f).R(Bone.Head, -18f).R(Bone.Neck, -6f))
            .Blend(0.32f, 0.3f);

        /// <summary>A swig (Doctor Pepper): the can up, head back, down again.</summary>
        public static readonly ActionClip Swig = new ActionClip("Swig", 1.0f)
            .Key(0f, Stand())
            .Key(0.35f, Drink.Keys[0].pose)
            .Key(0.65f, Drink.Keys[1].pose)
            .Key(1.0f, Stand())
            .Blend(0.15f, 0.2f);

        /// <summary>Eating (duck confit, a Mr Beast bar): quick bites (one-shot).</summary>
        public static readonly ActionClip Eat = new ActionClip("Eat", 1.05f)
            .Key(0f, Stand())
            .Key(0.32f, P().R(Bone.UpperArmR, -80f, -60f, -5f).R(Bone.ForearmR, -126f).R(Bone.HandR, 15f).R(Bone.Head, 6f))
            .Key(0.52f, P().R(Bone.UpperArmR, -72f, -55f, -5f).R(Bone.ForearmR, -116f).R(Bone.HandR, 10f).R(Bone.Head, 2f))
            .Key(0.72f, P().R(Bone.UpperArmR, -80f, -60f, -5f).R(Bone.ForearmR, -126f).R(Bone.HandR, 15f).R(Bone.Head, 6f))
            .Key(1.05f, Stand())
            .Blend(0.15f, 0.15f);

        /// <summary>Talking: hands out, gesturing (loop).</summary>
        public static readonly ActionClip Talk = new ActionClip("Talk", 1.8f, true)
            .Key(0f, P().R(Bone.UpperArmR, -22f, 0f, 8f).R(Bone.ForearmR, -70f).R(Bone.HandR, 10f).R(Bone.UpperArmL, -18f, 0f, -6f).R(Bone.ForearmL, -45f))
            .Key(0.6f, P().R(Bone.UpperArmR, -16f, 0f, 6f).R(Bone.ForearmR, -48f).R(Bone.UpperArmL, -26f, 0f, -10f).R(Bone.ForearmL, -72f).R(Bone.HandL, 12f).R(Bone.Head, 4f, 6f))
            .Key(1.2f, P().R(Bone.UpperArmR, -26f, 0f, 12f).R(Bone.ForearmR, -62f).R(Bone.HandR, -8f).R(Bone.UpperArmL, -14f, 0f, -6f).R(Bone.ForearmL, -40f).R(Bone.Head, 2f, -5f))
            .Blend(0.3f, 0.3f);

        /// <summary>Kneeling to set a gas trap on the ground ("plant" when it is down).</summary>
        public static readonly ActionClip Plant = new ActionClip("Plant", 1.0f)
            .Key(0f, Kneel())
            .Key(0.55f, With(Kneel(), p => p.R(Bone.Spine, 40f).R(Bone.Head, 22f).R(Bone.UpperArmR, -52f, 0f, 4f).R(Bone.ForearmR, -8f).R(Bone.HandR, 20f)))
            .Key(1.0f, With(Kneel(), p => p.R(Bone.Spine, 30f).R(Bone.Head, 15f).R(Bone.UpperArmR, -40f).R(Bone.ForearmR, -20f)))
            .Event(0.6f, "plant")
            .Blend(0.25f, 0.3f);

        /// <summary>Opening or closing a door: a quick push.</summary>
        public static readonly ActionClip Push = new ActionClip("Push", 0.45f)
            .Key(0f, Stand())
            .Key(0.18f, P().R(Bone.UpperArmR, -82f, 0f, 6f).R(Bone.ForearmR, -12f).R(Bone.HandR, 70f).R(Bone.Spine, 8f))
            .Key(0.45f, Stand())
            .Blend(0.06f, 0.12f);

        /// <summary>Slamming a pallet down: both arms haul it over (Space).</summary>
        public static readonly ActionClip Slam = new ActionClip("Slam", 0.55f)
            .Key(0f, Stand())
            .Key(0.15f, P().Both(Bone.UpperArmR, -130f, 0f, 10f).Both(Bone.ForearmR, -30f).R(Bone.Spine, -6f))
            .Key(0.32f, P().Lower(0.12f).Both(Bone.UpperArmR, -60f, 0f, 10f).Both(Bone.ForearmR, -10f).R(Bone.Spine, 30f).Both(Bone.ThighR, -25f).Both(Bone.ShinR, 40f))
            .Key(0.6f, Stand())
            .Event(0.32f, "slam")
            .Blend(0.06f, 0.15f);

        // ---------------------------------------------------------------- items

        /// <summary>A throw (bottle, book, jar): wind up over the shoulder, release, follow through.</summary>
        public static readonly ActionClip Throw = new ActionClip("Throw", 0.6f)
            .Key(0f, Stand())
            .Key(0.2f, P().R(Bone.UpperArmR, -165f, 0f, 25f).R(Bone.ForearmR, -85f).R(Bone.Spine, -4f, -24f).R(Bone.Chest, 0f, -10f).R(Bone.UpperArmL, -40f, 0f, -10f))
            .Key(0.32f, P().R(Bone.UpperArmR, -82f, 0f, 2f).R(Bone.ForearmR, -12f).R(Bone.HandR, 20f).R(Bone.Spine, 15f, 18f).R(Bone.Chest, 5f, 10f).R(Bone.UpperArmL, -10f, 0f, -12f))
            .Key(0.6f, P().R(Bone.UpperArmR, -28f, 0f, 4f).R(Bone.ForearmR, -16f).R(Bone.Spine, 5f, 4f))
            .Event(0.3f, "release")
            .Blend(0.06f, 0.18f);

        /// <summary>A long gun at the shoulder (shotgun, golden pump, 0.50 cal), held while it is selected.</summary>
        public static readonly ActionClip AimLong = new ActionClip("AimLong", 1f, true)
            .Key(0f, P().R(Bone.UpperArmR, -35f, 0f, 25f).R(Bone.ForearmR, -85f).R(Bone.HandR, 112f).R(Bone.UpperArmL, -78f, 0f, -6f).R(Bone.ForearmL, -28f).R(Bone.HandL, 30f).R(Bone.Spine, 4f, 8f).R(Bone.Head, 4f, -6f))
            .Blend(0.3f, 0.3f);

        /// <summary>The kick of a long gun, back to the aim.</summary>
        public static readonly ActionClip RecoilLong = new ActionClip("RecoilLong", 0.35f)
            .Key(0f, AimLong.Keys[0].pose)
            .Key(0.05f, With(AimLong.Keys[0].pose, p => p.R(Bone.UpperArmR, -48f, 0f, 25f).R(Bone.HandR, 122f).R(Bone.UpperArmL, -92f, 0f, -6f).R(Bone.Spine, -8f, 12f).R(Bone.Head, -6f, -6f)))
            .Key(0.35f, AimLong.Keys[0].pose)
            .Blend(0.03f, 0.1f);

        /// <summary>A pistol held out in both hands.</summary>
        public static readonly ActionClip AimPistol = new ActionClip("AimPistol", 1f, true)
            .Key(0f, P().R(Bone.UpperArmR, -84f, 0f, 4f).R(Bone.ForearmR, -6f).R(Bone.HandR, 90f).R(Bone.UpperArmL, -78f, 0f, 14f).R(Bone.ForearmL, -24f).R(Bone.HandL, 40f).R(Bone.Spine, 2f, 6f))
            .Blend(0.28f, 0.28f);

        public static readonly ActionClip RecoilPistol = new ActionClip("RecoilPistol", 0.25f)
            .Key(0f, AimPistol.Keys[0].pose)
            .Key(0.04f, With(AimPistol.Keys[0].pose, p => p.R(Bone.UpperArmR, -100f, 0f, 4f).R(Bone.HandR, 100f).R(Bone.UpperArmL, -92f, 0f, 14f)))
            .Key(0.25f, AimPistol.Keys[0].pose)
            .Blend(0.03f, 0.1f);

        /// <summary>Night vision goggles: a hand up at the eyes.</summary>
        public static readonly ActionClip Goggles = new ActionClip("Goggles", 1f, true)
            .Key(0f, P().R(Bone.UpperArmL, -110f, 0f, -30f).R(Bone.ForearmL, -120f).R(Bone.HandL, 10f).R(Bone.Head, 2f))
            .Blend(0.32f, 0.3f);

        /// <summary>JARVIS: holding up the tablet, looking at it.</summary>
        public static readonly ActionClip Tablet = new ActionClip("Tablet", 1f, true)
            .Key(0f, P().Both(Bone.UpperArmR, -40f, 0f, 10f).Both(Bone.ForearmR, -85f).Both(Bone.HandR, 30f).R(Bone.Head, 22f))
            .Blend(0.28f, 0.28f);

        // ---------------------------------------------------------------- damage and states

        /// <summary>Stunned: hands to the head, swaying (loop).</summary>
        public static readonly ActionClip Stunned = new ActionClip("Stunned", 1.0f, true)
            .Key(0f, P().Both(Bone.UpperArmR, -115f, -60f, -10f).Both(Bone.ForearmR, -120f).R(Bone.Head, 10f, 0f, 10f).R(Bone.Spine, 6f, 0f, 5f))
            .Key(0.5f, P().Both(Bone.UpperArmR, -118f, -56f, -8f).Both(Bone.ForearmR, -116f).R(Bone.Head, 14f, 0f, -12f).R(Bone.Spine, 8f, 0f, -6f))
            .Blend(0.25f, 0.3f);

        /// <summary>Penjamin's gas: coughing, rubbing an eye (loop).</summary>
        public static readonly ActionClip Cough = new ActionClip("Cough", 1.4f, true)
            .Key(0f, P().R(Bone.UpperArmR, -128f, 0f, 22f).R(Bone.ForearmR, -132f).R(Bone.Spine, 14f).R(Bone.Head, 18f))
            .Key(0.25f, P().R(Bone.UpperArmR, -122f, 0f, 22f).R(Bone.ForearmR, -138f).R(Bone.Spine, 30f).R(Bone.Chest, 10f).R(Bone.Head, 26f))
            .Key(0.55f, P().R(Bone.UpperArmR, -130f, 0f, 22f).R(Bone.ForearmR, -128f).R(Bone.Spine, 12f).R(Bone.Head, 14f))
            .Key(0.95f, P().R(Bone.UpperArmR, -125f, 0f, 20f).R(Bone.ForearmR, -134f).R(Bone.Spine, 26f).R(Bone.Chest, 8f).R(Bone.Head, 24f))
            .Blend(0.3f, 0.35f);

        /// <summary>The Soundcloud Burst's jump scare: a flinch back, arms up.</summary>
        public static readonly ActionClip Cringe = new ActionClip("Cringe", 1.2f)
            .Key(0f, Stand())
            .Key(0.12f, P().Both(Bone.UpperArmR, -120f, 0f, 25f).Both(Bone.ForearmR, -110f).R(Bone.Spine, -12f).R(Bone.Head, -15f).R(Bone.Chest, -6f))
            .Key(0.8f, P().Both(Bone.UpperArmR, -110f, 0f, 25f).Both(Bone.ForearmR, -105f).R(Bone.Spine, -6f).R(Bone.Head, -6f))
            .Key(1.2f, Stand())
            .Blend(0.05f, 0.25f);

        /// <summary>On a stake: arms pulled up overhead, head down, struggling (loop).</summary>
        public static readonly ActionClip Staked = new ActionClip("Staked", 3f, true)
            .Key(0f, P().Both(Bone.UpperArmR, -168f, 0f, 12f).Both(Bone.ForearmR, -20f).R(Bone.Head, 22f).R(Bone.Spine, -5f, 0f, 4f))
            .Key(1.5f, P().Both(Bone.UpperArmR, -164f, 0f, 16f).Both(Bone.ForearmR, -28f).R(Bone.Head, 26f, 0f, 6f).R(Bone.Spine, -3f, 0f, -5f))
            .Blend(0.3f, 0.4f);

        /// <summary>Carried over Zach's shoulder: arms hanging (the body already lies forward).</summary>
        public static readonly ActionClip Carried = new ActionClip("Carried", 2f, true)
            .Key(0f, P().Both(Bone.UpperArmR, -70f, 0f, 6f).Both(Bone.ForearmR, -5f).R(Bone.Head, 30f))
            .Key(1f, P().Both(Bone.UpperArmR, -78f, 0f, 9f).Both(Bone.ForearmR, -10f).R(Bone.Head, 34f))
            .Blend(0.3f, 0.3f);

        /// <summary>Downed: reaching forward as they crawl (the body lies forward).</summary>
        public static readonly ActionClip Crawl = new ActionClip("Crawl", 1.3f, true)
            .Key(0f, P().R(Bone.UpperArmR, -150f, 0f, 20f).R(Bone.ForearmR, -30f).R(Bone.UpperArmL, -60f, 0f, -20f).R(Bone.ForearmL, -60f).R(Bone.Head, -30f))
            .Key(0.65f, P().R(Bone.UpperArmR, -60f, 0f, 20f).R(Bone.ForearmR, -60f).R(Bone.UpperArmL, -150f, 0f, -20f).R(Bone.ForearmL, -30f).R(Bone.Head, -30f))
            .Blend(0.4f, 0.3f);

        // ---------------------------------------------------------------- Zach

        static ActionPose Raised() => P().R(Bone.UpperArmR, -158f, 0f, 45f).R(Bone.ForearmR, -95f).R(Bone.HandR, 25f).R(Bone.Spine, -2f, -26f).R(Bone.Chest, 0f, -10f).R(Bone.UpperArmL, -30f, 0f, -18f).R(Bone.ForearmL, -30f);

        /// <summary>Charging the machete: raised back over the shoulder, trembling as it fills (hold).</summary>
        public static readonly ActionClip Charge = new ActionClip("Charge", 0.24f, true)
            .Key(0f, Raised())
            .Key(0.12f, With(Raised(), p => p.R(Bone.UpperArmR, -161f, 0f, 47f).R(Bone.Spine, -3f, -28f)))
            .Blend(0.3f, 0.3f);

        /// <summary>A light machete swipe: a short windup, across the body ("hit" at the strike), recover.</summary>
        public static readonly ActionClip Swing = new ActionClip("Swing", 0.5f)
            .Key(0f, Stand())
            .Key(0.12f, Raised())
            .Key(0.15f, Raised())
            .Key(0.24f, P().R(Bone.UpperArmR, -82f, -35f, -22f).R(Bone.ForearmR, -14f).R(Bone.HandR, 30f).R(Bone.Spine, 10f, 30f).R(Bone.Chest, 4f, 12f).R(Bone.UpperArmL, -10f, 0f, -16f))
            .Key(0.5f, P().R(Bone.UpperArmR, -40f, -10f, -4f).R(Bone.ForearmR, -30f).R(Bone.Spine, 4f, 8f))
            .Event(0.2f, "hit")
            .Blend(0.04f, 0.18f);

        /// <summary>A heavy swipe: everything behind it, a long follow-through.</summary>
        public static readonly ActionClip SwingHeavy = new ActionClip("SwingHeavy", 0.7f)
            .Key(0f, Stand())
            .Key(0.12f, With(Raised(), p => p.R(Bone.Spine, -6f, -34f).R(Bone.UpperArmR, -165f, 0f, 50f)))
            .Key(0.15f, With(Raised(), p => p.R(Bone.Spine, -6f, -34f).R(Bone.UpperArmR, -165f, 0f, 50f)))
            .Key(0.27f, P().Lower(0.06f).R(Bone.UpperArmR, -64f, -45f, -38f).R(Bone.ForearmR, -8f).R(Bone.HandR, 30f).R(Bone.Spine, 18f, 40f).R(Bone.Chest, 6f, 16f).Both(Bone.ThighR, -12f).Both(Bone.ShinR, 18f))
            .Key(0.7f, P().R(Bone.UpperArmR, -36f, -12f, -6f).R(Bone.ForearmR, -30f).R(Bone.Spine, 6f, 10f))
            .Event(0.22f, "hit")
            .Blend(0.04f, 0.22f);

        /// <summary>The lunge: leaning in, machete out ahead.</summary>
        public static readonly ActionClip Lunge = new ActionClip("Lunge", 0.5f)
            .Key(0f, P().R(Bone.Spine, 22f).R(Bone.Chest, 6f).R(Bone.Head, -14f).R(Bone.UpperArmR, -100f, 0f, 12f).R(Bone.ForearmR, -18f).R(Bone.HandR, 70f).R(Bone.UpperArmL, 20f, 0f, -14f))
            .Key(0.5f, P().R(Bone.Spine, 14f).R(Bone.Head, -8f).R(Bone.UpperArmR, -70f, 0f, 10f).R(Bone.ForearmR, -20f).R(Bone.HandR, 40f))
            .Blend(0.1f, 0.2f);

        /// <summary>Carrying a survivor over the left shoulder (hold).</summary>
        public static readonly ActionClip Carry = new ActionClip("Carry", 1f, true)
            .Key(0f, P().R(Bone.UpperArmL, -150f, 0f, -24f).R(Bone.ForearmL, -118f).R(Bone.HandL, 10f).R(Bone.Spine, 6f, 0f, 4f).R(Bone.Head, -4f))
            .Blend(0.3f, 0.3f);

        /// <summary>Picking a downed survivor up: bend, grab, heave onto the shoulder ("lift").</summary>
        public static readonly ActionClip LiftBody = new ActionClip("LiftBody", 1.0f)
            .Key(0f, Stand())
            .Key(0.45f, P().Lower(0.25f).R(Bone.Spine, 55f).R(Bone.Chest, 10f).Both(Bone.UpperArmR, -70f, 0f, 10f).Both(Bone.ForearmR, -20f).Both(Bone.ThighR, -45f).Both(Bone.ShinR, 70f))
            .Key(1.0f, Carry.Keys[0].pose)
            .Event(0.6f, "lift")
            .Blend(0.1f, 0.35f);

        /// <summary>Hanging a survivor on a stake: up overhead, then down hard ("stake").</summary>
        public static readonly ActionClip StakeBody = new ActionClip("StakeBody", 1.2f)
            .Key(0f, Carry.Keys[0].pose)
            .Key(0.5f, P().Both(Bone.UpperArmR, -172f, 0f, 14f).Both(Bone.ForearmR, -40f).R(Bone.Spine, -8f).R(Bone.Head, -14f))
            .Key(0.9f, P().Both(Bone.UpperArmR, -120f, 0f, 14f).Both(Bone.ForearmR, -20f).R(Bone.Spine, 14f))
            .Key(1.2f, Stand())
            .Event(0.9f, "stake")
            .Blend(0.3f, 0.2f);

        /// <summary>Searching a hiding spot: rummaging with both hands (loop).</summary>
        public static readonly ActionClip Search = new ActionClip("Search", 0.9f, true)
            .Key(0f, P().R(Bone.Spine, 22f).R(Bone.Head, 12f).R(Bone.UpperArmR, -92f, 0f, 8f).R(Bone.ForearmR, -16f).R(Bone.UpperArmL, -72f, 0f, -8f).R(Bone.ForearmL, -30f))
            .Key(0.45f, P().R(Bone.Spine, 26f).R(Bone.Head, 16f).R(Bone.UpperArmR, -74f, 0f, 8f).R(Bone.ForearmR, -30f).R(Bone.UpperArmL, -94f, 0f, -8f).R(Bone.ForearmL, -14f))
            .Blend(0.2f, 0.25f);

        /// <summary>Searching a hiding spot (the original's search is instant): one rummage.</summary>
        public static readonly ActionClip SearchOnce = new ActionClip("SearchOnce", 0.9f)
            .Key(0f, Stand())
            .Key(0.28f, Search.Keys[0].pose)
            .Key(0.55f, Search.Keys[1].pose)
            .Key(0.9f, Stand())
            .Event(0.4f, "search")
            .Blend(0.12f, 0.2f);

        /// <summary>Damaging a generator: stamping kicks ("kick" each time, loop).</summary>
        public static readonly ActionClip Kick = new ActionClip("Kick", 1.0f, true)
            .Key(0f, P().R(Bone.Spine, -4f).R(Bone.ThighR, -10f).R(Bone.ShinR, 20f))
            .Key(0.35f, P().R(Bone.Spine, -10f).R(Bone.ThighR, -72f).R(Bone.ShinR, 60f).Both(Bone.UpperArmR, -20f, 0f, 14f))
            .Key(0.5f, P().R(Bone.Spine, -14f).R(Bone.ThighR, -62f).R(Bone.ShinR, 8f).Both(Bone.UpperArmR, -24f, 0f, 16f))
            .Key(0.75f, P().R(Bone.Spine, -4f).R(Bone.ThighR, -14f).R(Bone.ShinR, 24f))
            .Event(0.5f, "kick")
            .Blend(0.15f, 0.2f);

        /// <summary>The Soundcloud Burst: a palm thrust forward.</summary>
        public static readonly ActionClip Burst = new ActionClip("Burst", 0.55f)
            .Key(0f, P().R(Bone.UpperArmL, -60f, 0f, -10f).R(Bone.ForearmL, -90f).R(Bone.Spine, -4f, 10f))
            .Key(0.14f, P().R(Bone.UpperArmL, -92f, 0f, -2f).R(Bone.ForearmL, -2f).R(Bone.HandL, 80f).R(Bone.Spine, 8f, -12f))
            .Key(0.55f, Stand())
            .Event(0.14f, "burst")
            .Blend(0.06f, 0.2f);

        /// <summary>Penjamin: the vape to the mask, then a long exhale.</summary>
        public static readonly ActionClip Vape = new ActionClip("Vape", 1.0f)
            .Key(0f, Stand())
            .Key(0.32f, P().R(Bone.UpperArmL, -62f, 20f, -18f).R(Bone.ForearmL, -125f).R(Bone.Head, -8f).R(Bone.Chest, -6f))
            .Key(0.62f, P().R(Bone.UpperArmL, -40f, 10f, -14f).R(Bone.ForearmL, -70f).R(Bone.Head, 6f).R(Bone.Chest, 6f).R(Bone.Spine, 8f))
            .Key(1.0f, Stand())
            .Event(0.58f, "exhale")
            .Blend(0.08f, 0.2f);

        /// <summary>Toggling the Hemp Battery: a fist to the chest.</summary>
        public static readonly ActionClip Hemp = new ActionClip("Hemp", 0.75f)
            .Key(0f, Stand())
            .Key(0.3f, P().R(Bone.UpperArmL, -40f, 30f, -10f).R(Bone.ForearmL, -130f).R(Bone.Chest, -6f).R(Bone.Head, -10f))
            .Key(0.75f, Stand())
            .Blend(0.08f, 0.2f);

        /// <summary>The Hemp Beam: both arms out, palms forward, braced (hold while it charges and fires).</summary>
        public static readonly ActionClip Beam = new ActionClip("Beam", 0.3f, true)
            .Key(0f, P().Lower(0.06f).Both(Bone.UpperArmR, -88f, 0f, 4f).Both(Bone.ForearmR, -4f).Both(Bone.HandR, 70f).R(Bone.Spine, 4f).Both(Bone.ThighR, -14f).Both(Bone.ShinR, 22f))
            .Key(0.15f, P().Lower(0.07f).Both(Bone.UpperArmR, -90f, 0f, 3f).Both(Bone.ForearmR, -3f).Both(Bone.HandR, 72f).R(Bone.Spine, 5f).Both(Bone.ThighR, -15f).Both(Bone.ShinR, 24f))
            .Blend(0.28f, 0.3f);

        /// <summary>Every clip, for the animation lab.</summary>
        public static IEnumerable<ActionClip> All => new[]
        {
            Repair, Lever, Tend, PickUp, Crouch, Drink, Swig, Eat, Talk, Plant, Push, Slam, Throw, AimLong, RecoilLong, AimPistol, RecoilPistol,
            Goggles, Tablet, Stunned, Cough, Cringe, Staked, Carried, Crawl, Charge, Swing, SwingHeavy, Lunge, Carry, LiftBody, StakeBody,
            Search, SearchOnce, Kick, Burst, Vape, Hemp, Beam,
        };
    }
}
