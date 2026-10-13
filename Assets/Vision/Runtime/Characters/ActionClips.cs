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

        /// <summary>
        /// A throw (bottle, book, jar), quick and snappy: the arm whips back behind the head with the chest turned away
        /// and the free arm pointing at the target, snaps forward over the shoulder (release), and follows through
        /// across the body.
        /// </summary>
        public static readonly ActionClip Throw = new ActionClip("Throw", 0.4f)
            .Key(0f, Stand())
            .Key(0.05f, P().R(Bone.UpperArmR, -80f, 0f, 22f).R(Bone.ForearmR, -50f).R(Bone.Spine, -2f, -12f).R(Bone.Chest, 0f, -5f).R(Bone.Head, 0f, 14f).R(Bone.UpperArmL, -40f, 0f, -10f).R(Bone.ForearmL, -10f))
            .Key(0.1f, P().R(Bone.UpperArmR, -150f, 0f, 30f).R(Bone.ForearmR, -88f).R(Bone.HandR, -10f).R(Bone.Spine, -4f, -22f).R(Bone.Chest, 0f, -10f).R(Bone.Head, 0f, 26f).R(Bone.UpperArmL, -62f, 0f, -12f).R(Bone.ForearmL, -10f))
            .Key(0.165f, P().R(Bone.UpperArmR, -88f, 0f, 4f).R(Bone.ForearmR, -10f).R(Bone.HandR, 25f).R(Bone.Spine, 14f, 18f).R(Bone.Chest, 5f, 10f).R(Bone.Head, 0f, -20f).R(Bone.UpperArmL, -12f, 0f, -14f).R(Bone.ForearmL, -30f))
            .Key(0.25f, P().R(Bone.UpperArmR, -48f, -24f, -12f).R(Bone.ForearmR, -18f).R(Bone.HandR, 20f).R(Bone.Spine, 16f, 22f).R(Bone.Chest, 5f, 12f).R(Bone.Head, 0f, -24f).R(Bone.UpperArmL, 6f, 0f, -14f).R(Bone.ForearmL, -36f))
            .Key(0.4f, P().R(Bone.UpperArmR, -24f, -8f, 2f).R(Bone.ForearmR, -18f).R(Bone.Spine, 5f, 6f))
            .Event(0.16f, "release")
            .Blend(0.05f, 0.14f);

        // Guns are held as they are in real life (the props sit in the fist barrel along the forearm, see ActionLayer).
        // Poses solved for where the hands must be on each gun (scratchpad fk4.py): the trigger hand on the grip, the
        // support hand on the forend, the barrel straight down the aim. The legs keep walking underneath.

        /// <summary>
        /// A long gun's hold: the trigger hand on the grip, the trigger elbow out, the stock in the pocket of the right
        /// shoulder, the support hand under the forend; the body bladed (left shoulder forward), the head turned back to
        /// the front and down on the stock. Spine yaw, recoil (pitch back, muzzle up) and the rack are offsets on it.
        /// </summary>
        static ActionPose Shouldered(float sx, float sy, float cx, float cy, float headX, (float x, float y, float z) ur, float fr, (float x, float y, float z) hr,
            (float x, float y, float z) ul, float fl, (float x, float y, float z) hl, float raise = 0f, float flip = 0f) => P()
            .R(Bone.Spine, sx, sy).R(Bone.Chest, cx, cy).R(Bone.Neck, 2f, -(sy + cy) * 0.3f).R(Bone.Head, headX, -(sy + cy) * 0.7f)
            .R(Bone.UpperArmR, ur.x + raise, ur.y, ur.z).R(Bone.ForearmR, fr).R(Bone.HandR, hr.x + flip, hr.y, hr.z)
            .R(Bone.UpperArmL, ul.x + raise, ul.y, ul.z).R(Bone.ForearmL, fl).R(Bone.HandL, hl.x, hl.y, hl.z);

        static ActionPose ShotgunHold(float sx = 2f, float sy = 14f, float raise = 0f, float flip = 0f, float pumpBack = 0f) =>
            Shouldered(sx, sy, 2f, 10f, 8f, (-33.8f, -63.2f, 24.6f), -111.4f, (55.2f, 34.4f, 49.7f), (-50.1f + pumpBack * 0.4f, 8.1f, 8f), -78.4f - pumpBack, (9.9f, -43f, -14f), raise, flip);

        static ActionPose RifleHold(float sx = 6f, float sy = 14f, float raise = 0f, float flip = 0f) =>
            Shouldered(sx, sy, 3f, 10f, 13f, (-35.4f, -65f, 26.9f), -103.9f, (49.2f, 28.1f, 43.6f), (-62.5f, -5.1f, 14.5f), -54.5f, (3.4f, -27.3f, 2.7f), raise, flip);

        /// <summary>
        /// The 0.50 cal at the hip (not aimed): the grip low by the right hip with the elbow tucked back, the stock under the
        /// forearm, the support hand under the forend with a soft elbow, the muzzle level out in front; the body a little
        /// bladed, the head up and looking over the gun (solved in scratchpad poses_hip.py).
        /// </summary>
        static ActionPose HipHold(float sx = 3f, float sy = 10f, float raise = 0f, float flip = 0f) =>
            Shouldered(sx, sy, 1f, 6f, 2f, (9.4f, -54.8f, 8.7f), -76.8f, (-13.7f, 14.8f, 35.2f), (-32f, 47.7f, -13.3f), -20.5f, (-39.9f, 5.7f, -25.8f), raise, flip);

        /// <summary>A pistol in a Weaver grip: the gun arm out with a soft elbow, the support arm bent, its hand cupping the grip.</summary>
        static ActionPose PistolHold(float sx = 2f, float raise = 0f, float flip = 0f, float give = 0f) => P()
            .R(Bone.Spine, sx, 8f).R(Bone.Chest, 1f, 6f).R(Bone.Neck, 1f, -4f).R(Bone.Head, 3f, -10f)
            .R(Bone.UpperArmR, -55.5f + raise, -50.1f, 17.3f).R(Bone.ForearmR, -65f - give).R(Bone.HandR, 39f + flip, 16f, 30.1f)
            .R(Bone.UpperArmL, -37.8f + raise, 15f, 8.4f).R(Bone.ForearmL, -85.2f - give * 0.5f).R(Bone.HandL, 18.4f, -17.9f, 12.1f);

        /// <summary>The shotgun (and the golden pump) at the shoulder, held while it is selected.</summary>
        public static readonly ActionClip AimLong = new ActionClip("AimLong", 1f, true)
            .Key(0f, ShotgunHold())
            .Blend(0.24f, 0.28f);

        /// <summary>The shotgun's kick: the shoulder driven back, the body rocked back, the muzzle up; then back on aim.</summary>
        public static readonly ActionClip RecoilLong = new ActionClip("RecoilLong", 0.42f)
            .Key(0f, ShotgunHold())
            .Key(0.04f, ShotgunHold(-6f, 21f, -7f, -4f))
            .Key(0.14f, ShotgunHold(-3f, 18f, -3f, -2f))
            .Key(0.42f, ShotgunHold())
            .Blend(0.03f, 0.12f);

        /// <summary>Zach's golden pump: the kick, then the support hand racks the pump back and forward.</summary>
        public static readonly ActionClip RecoilPump = new ActionClip("RecoilPump", 0.62f)
            .Key(0f, ShotgunHold())
            .Key(0.04f, ShotgunHold(-6f, 21f, -7f, -4f))
            .Key(0.16f, ShotgunHold(-2f, 17f, -2f, 0f))
            .Key(0.3f, ShotgunHold(0f, 15f, 0f, 0f, 22f))
            .Key(0.42f, ShotgunHold())
            .Key(0.62f, ShotgunHold())
            .Event(0.3f, "rack")
            .Blend(0.03f, 0.12f);

        /// <summary>
        /// Aiming the 0.50 cal (right click): the stock comes up into the shoulder and the cheek down onto it, the eye behind
        /// the scope, the support hand far out; the upper body stays steady over the walking legs, as a rifleman glides
        /// on the move. Eased in and out over about a quarter of a second from the hip.
        /// </summary>
        public static readonly ActionClip AimRifle = new ActionClip("AimRifle", 1f, true)
            .Key(0f, RifleHold())
            .Blend(0.26f, 0.26f);

        /// <summary>The 0.50 cal carried at the hip while it isn't aimed.</summary>
        public static readonly ActionClip HipRifle = new ActionClip("HipRifle", 1f, true)
            .Key(0f, HipHold())
            .Blend(0.26f, 0.26f);

        /// <summary>A 0.50 cal shot from the hip: the gun bucks up and the body rocks back, then settles.</summary>
        public static readonly ActionClip RecoilHip = new ActionClip("RecoilHip", 0.6f)
            .Key(0f, HipHold())
            .Key(0.05f, HipHold(-6f, 18f, -12f, -4f))
            .Key(0.2f, HipHold(-1f, 13f, -5f, -1f))
            .Key(0.6f, HipHold())
            .Blend(0.03f, 0.15f);

        /// <summary>The 0.50 cal's heavy kick: the body shoved back a step's worth, the muzzle climbing; a slow settle.</summary>
        public static readonly ActionClip RecoilRifle = new ActionClip("RecoilRifle", 0.6f)
            .Key(0f, RifleHold())
            .Key(0.05f, RifleHold(-5f, 23f, -9f, -3f))
            .Key(0.2f, RifleHold(-1f, 19f, -4f, -1f))
            .Key(0.6f, RifleHold())
            .Blend(0.03f, 0.15f);

        /// <summary>A pistol held in both hands, Weaver style.</summary>
        public static readonly ActionClip AimPistol = new ActionClip("AimPistol", 1f, true)
            .Key(0f, PistolHold())
            .Blend(0.22f, 0.26f);

        /// <summary>The pistol's snap: the muzzle flips up, the elbows give, the arms come back down onto aim.</summary>
        public static readonly ActionClip RecoilPistol = new ActionClip("RecoilPistol", 0.26f)
            .Key(0f, PistolHold())
            .Key(0.035f, PistolHold(-1f, -6f, -10f, 6f))
            .Key(0.12f, PistolHold(1f, -2f, -3f, 2f))
            .Key(0.26f, PistolHold())
            .Blend(0.03f, 0.1f);

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

        /// <summary>Penjamin's gas in the eyes: both hands clamped over them, head bowed, rocking and shaking it (loop).</summary>
        public static readonly ActionClip CoverEyes = new ActionClip("CoverEyes", 1.3f, true)
            .Key(0f, P().Both(Bone.UpperArmR, -70f, -70f, 22f).Both(Bone.ForearmR, -128f).Both(Bone.HandR, 8f).R(Bone.Spine, 14f).R(Bone.Chest, 6f).R(Bone.Head, 16f, 0f, 0f))
            .Key(0.35f, P().Both(Bone.UpperArmR, -72f, -70f, 21f).Both(Bone.ForearmR, -130f).Both(Bone.HandR, 10f).R(Bone.Spine, 22f, 0f, 4f).R(Bone.Chest, 9f).R(Bone.Head, 22f, 10f, 3f))
            .Key(0.7f, P().Both(Bone.UpperArmR, -69f, -70f, 23f).Both(Bone.ForearmR, -127f).Both(Bone.HandR, 8f).R(Bone.Spine, 12f, 0f, -3f).R(Bone.Chest, 5f).R(Bone.Head, 14f, -10f, -3f))
            .Key(1.0f, P().Both(Bone.UpperArmR, -71f, -70f, 22f).Both(Bone.ForearmR, -129f).Both(Bone.HandR, 9f).R(Bone.Spine, 20f).R(Bone.Chest, 8f).R(Bone.Head, 20f, 4f, 0f))
            .Blend(0.3f, 0.4f);

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

        // Zach's machete cuts in a wide X, back and forth along a figure 8, always in his right hand: the forehand falls from
        // high beside his right shoulder, about 35 degrees below level, across his chest to his left waist, and the blade loops on up over
        // his left shoulder; the backhand falls from there down across to his right hip and loops back up over his right
        // shoulder, ready for the next forehand. The hand's path, the blade's direction (pointing out from him through the
        // cut, the tip trailing the hand) and its edge (leading) are solved key by key in scratchpad poses_x35.py: a flat,
        // side-to-side cut with a wide arc out in front, the torso turning further and the shoulders leading the arm. The torso
        // winds up toward the shoulder the cut starts from and unwinds through it, leaning in as it falls; the free arm
        // reaches forward as a guard while the blade is up, as in the user's reference stance, and swings back against
        // the cut; the head stays on the target. Charged cuts are the same X, wound tighter, low and wide on bent knees,
        // with the weight driving onto the front foot as the blade falls.

        /// <summary>A key of the X: the torso's pitch and yaw (the chest takes 40% and 42%), the right arm, the free arm.</summary>
        static ActionPose Cut(float pitch, float yaw, (float x, float y, float z) u, float f, (float x, float y, float z) h, (float x, float y, float z) free, float freeF) => P()
            .R(Bone.Spine, pitch, yaw).R(Bone.Chest, pitch * 0.4f, yaw * 0.42f).R(Bone.Head, -pitch * 0.5f, -yaw * 1.1f)
            .R(Bone.UpperArmR, u.x, u.y, u.z).R(Bone.ForearmR, f).R(Bone.HandR, h.x, h.y, h.z)
            .R(Bone.UpperArmL, free.x, free.y, free.z).R(Bone.ForearmL, freeF);

        /// <summary>The low, wide, weighted stance of a charged cut: knees bent, the front (left) knee taking the weight as it lands.</summary>
        static ActionPose Low(ActionPose p, float drop, float lunge) => p.Lower(drop)
            .R(Bone.ThighR, -8f + 16f * lunge).R(Bone.ShinR, 22f + 8f * lunge).R(Bone.FootR, -4f * lunge)
            .R(Bone.ThighL, -24f - 12f * lunge).R(Bone.ShinL, 32f + 12f * lunge).R(Bone.FootL, -6f);

        static readonly (float, float, float) Guard = (-56f, 10f, -24f), Tucked = (6f, 0f, -30f);

        // The light X (forehand F, backhand B): cocked, early in the cut, the hit in front, late, the end at the hip, the loop.
        static ActionPose CockF() => Cut(-2f, 38f, (-66.7f, -1.3f, 11.3f), -98.3f, (0f, 42.2f, -66.6f), Guard, -40f);
        static ActionPose F1() => Cut(4f, 22f, (-66.2f, 8.2f, -15f), -75.9f, (90f, 16.5f, -40.9f), (-45f, 5f, -22f), -34f);
        static ActionPose F2() => Cut(8f, 0f, (-33.6f, 31.2f, -70.2f), -70.1f, (90f, -9.4f, -15f), (-25f, 0f, -22f), -30f);
        static ActionPose F3() => Cut(11f, -22f, (-25.5f, 30.8f, -65.2f), -43.2f, (76.7f, -3.6f, -8.2f), (-5f, 0f, -25f), -24f);
        static ActionPose EndF() => Cut(13f, -34f, (-45f, 21.8f, -58.5f), -6f, (78.3f, -17.5f, -27f), (10f, 0f, -28f), -20f);
        static ActionPose LoopF() => Cut(6f, -28f, (-43.6f, -43.9f, -13.5f), -93.2f, (33.9f, -57.5f, -18.3f), (5f, 0f, -28f), -28f);
        static ActionPose CockB() => Cut(-2f, -32f, (-71.7f, -55.5f, -13.3f), -83f, (2.9f, -82.6f, 68.1f), Tucked, -35f);
        static ActionPose B1() => Cut(4f, -18f, (-40.3f, 11.8f, -85.3f), -71.2f, (80.2f, -38.1f, 56.4f), (-10f, 0f, -28f), -32f);
        static ActionPose B2() => Cut(8f, 0f, (-50f, 19.5f, -52.2f), -64.1f, (90f, -26f, 44.3f), (-30f, 0f, -24f), -32f);
        static ActionPose B3() => Cut(11f, 22f, (-39.7f, 24f, -40f), -55.3f, (80.9f, -9.4f, 52.9f), (-45f, 5f, -24f), -36f);
        static ActionPose EndB() => Cut(12f, 34f, (-47.7f, 57.8f, -45.2f), -6f, (40.5f, 40.8f, 83f), Guard, -40f);
        static ActionPose LoopB() => Cut(5f, 34f, (-41f, 12.4f, -32.7f), -114.1f, (44.5f, 11.2f, 42.2f), Guard, -40f);

        // The charged X: wound further, the blade higher, the cut deeper.
        static ActionPose HCockF(float tremble = 0f) => Cut(-2f, 45.6f + tremble, (-76.6f - tremble, 9.3f, -14.1f + tremble * 0.5f), -85.4f, (2.3f, 37.6f, -68.6f), (-60f, 12f, -24f), -42f);
        static ActionPose HF1() => Cut(4f, 26.4f, (-72.7f, 16.1f, -30.6f), -66.6f, (90f, 11.5f, -42.6f), (-45f, 5f, -22f), -34f);
        static ActionPose HF2() => Cut(8f, 0f, (-32.9f, 32.3f, -71.7f), -69.2f, (90f, -13.4f, -17.7f), (-20f, 0f, -24f), -28f);
        static ActionPose HF3() => Cut(11f, -26.4f, (-24.7f, 33.2f, -60.9f), -46f, (76f, -6.6f, -13.5f), (0f, 0f, -26f), -22f);
        static ActionPose HEndF() => Cut(18f, -40.8f, (-51.3f, 23.1f, -53.1f), -6f, (72.2f, -24.2f, -34.9f), (14f, 0f, -30f), -18f);
        static ActionPose HLoopF() => Cut(6f, -33.6f, (-41.7f, -40.4f, -10.6f), -99f, (37.1f, -57.6f, -22.7f), (5f, 0f, -28f), -28f);
        static ActionPose HCockB(float tremble = 0f) => Cut(-2f, -38.4f - tremble, (-78.5f - tremble, -54.2f, -7.3f - tremble * 0.5f), -81.4f, (5.5f, -84.8f, 64.2f), Tucked, -35f);
        static ActionPose HB1() => Cut(4f, -21.6f, (-40.3f, 16.6f, -89.6f), -74.2f, (82.4f, -45.7f, 51f), (-10f, 0f, -28f), -32f);
        static ActionPose HB2() => Cut(8f, 0f, (-49.5f, 20.4f, -53.4f), -64.1f, (90f, -32.8f, 38.1f), (-34f, 0f, -24f), -34f);
        static ActionPose HB3() => Cut(11f, 26.4f, (-44.9f, 20.8f, -40.1f), -40.3f, (73.2f, -2.3f, 58.1f), (-50f, 8f, -24f), -38f);
        static ActionPose HEndB() => Cut(17f, 40.8f, (-49.5f, 51.5f, -49.8f), -6f, (38.2f, 37.2f, 76.2f), (-60f, 12f, -24f), -42f);
        static ActionPose HLoopB() => Cut(5f, 40.8f, (-43.2f, 8.3f, -39.8f), -105.8f, (41.1f, 4.1f, 42.7f), Guard, -40f);

        /// <summary>
        /// Charging a forehand: low and wide on bent knees, the machete cocked high over his right shoulder, the blade
        /// lying back over his head, the torso wound away from the target while the head stays on it, the free arm reaching
        /// out in front as a guard; the whole body trembles with the strain (hold).
        /// </summary>
        public static readonly ActionClip Charge = new ActionClip("Charge", 0.32f, true)
            .Key(0f, Low(HCockF(0f), 0.05f, 0f))
            .Key(0.08f, Low(HCockF(2f), 0.055f, 0f))
            .Key(0.16f, Low(HCockF(-1f), 0.05f, 0f))
            .Key(0.24f, Low(HCockF(2.5f), 0.06f, 0f))
            .Blend(0.3f, 0.25f);

        /// <summary>Charging a backhand: the same, the blade cocked over his left shoulder, the right arm across his face (hold).</summary>
        public static readonly ActionClip ChargeBack = new ActionClip("ChargeBack", 0.32f, true)
            .Key(0f, Low(HCockB(0f), 0.05f, 0f))
            .Key(0.08f, Low(HCockB(2f), 0.055f, 0f))
            .Key(0.16f, Low(HCockB(-1f), 0.05f, 0f))
            .Key(0.24f, Low(HCockB(2.5f), 0.06f, 0f))
            .Blend(0.3f, 0.25f);

        /// <summary>
        /// A light forehand: cocked over the right shoulder, falling diagonally across his chest ("hit" in front) to his
        /// left hip, and looping on up over his left shoulder, ready for the backhand.
        /// </summary>
        public static readonly ActionClip Swing = new ActionClip("Swing", 0.46f)
            .Key(0f, CockF())
            .Key(0.07f, CockF())
            .Key(0.11f, F1())
            .Key(0.15f, F2())
            .Key(0.19f, F3())
            .Key(0.24f, EndF())
            .Key(0.34f, LoopF())
            .Key(0.46f, CockB())
            .Event(0.15f, "hit")
            .Blend(0.08f, 0.22f);

        /// <summary>
        /// A light backhand, the other half of the X: from over his left shoulder, falling diagonally across his chest
        /// ("hit" in front) to his right hip, and looping back up over his right shoulder, ready for the next forehand.
        /// </summary>
        public static readonly ActionClip SwingBack = new ActionClip("SwingBack", 0.46f)
            .Key(0f, CockB())
            .Key(0.07f, CockB())
            .Key(0.11f, B1())
            .Key(0.15f, B2())
            .Key(0.19f, B3())
            .Key(0.24f, EndB())
            .Key(0.34f, LoopB())
            .Key(0.46f, CockF())
            .Event(0.15f, "hit")
            .Blend(0.08f, 0.22f);

        /// <summary>A charged forehand: from the charge's coil, a last wind, then the whole body behind the fall, landing low.</summary>
        public static readonly ActionClip SwingHeavy = new ActionClip("SwingHeavy", 0.75f)
            .Key(0f, Low(HCockF(), 0.05f, 0f))
            .Key(0.05f, Low(HCockF(4f), 0.06f, 0f))
            .Key(0.1f, Low(HF1(), 0.07f, 0.3f))
            .Key(0.15f, Low(HF2(), 0.08f, 0.6f))
            .Key(0.2f, Low(HF3(), 0.09f, 0.85f))
            .Key(0.265f, Low(HEndF(), 0.1f, 1f))
            .Key(0.45f, Low(HEndF(), 0.09f, 0.9f))
            .Key(0.75f, Low(HLoopF(), 0.03f, 0.3f))
            .Event(0.15f, "hit")
            .Blend(0.05f, 0.25f);

        /// <summary>A charged backhand: the same from over his left shoulder, down across to his right hip.</summary>
        public static readonly ActionClip SwingHeavyBack = new ActionClip("SwingHeavyBack", 0.75f)
            .Key(0f, Low(HCockB(), 0.05f, 0f))
            .Key(0.05f, Low(HCockB(4f), 0.06f, 0f))
            .Key(0.1f, Low(HB1(), 0.07f, 0.3f))
            .Key(0.15f, Low(HB2(), 0.08f, 0.6f))
            .Key(0.2f, Low(HB3(), 0.09f, 0.85f))
            .Key(0.265f, Low(HEndB(), 0.1f, 1f))
            .Key(0.45f, Low(HEndB(), 0.09f, 0.9f))
            .Key(0.75f, Low(HLoopB(), 0.03f, 0.3f))
            .Event(0.15f, "hit")
            .Blend(0.05f, 0.25f);

        /// <summary>The swings of the X, light and charged, and the clip time over which each one's blade is cutting.</summary>
        public static bool IsCut(ActionClip c) => c == Swing || c == SwingBack || c == SwingHeavy || c == SwingHeavyBack;
        public const float CutFrom = 0.085f, CutTo = 0.265f;

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

        /// <summary>
        /// The Hemp Beam, like an armoured hero's repulsor: the right arm thrust straight out, the wrist bent back so the
        /// palm faces the target, the right shoulder leading and the body braced behind it, the left arm drawn back at the
        /// side (holding whatever the right hand had) (hold while it charges and fires; it fires from the palm). Only the
        /// arms and torso: the legs keep walking underneath while the player moves.
        /// </summary>
        static ActionPose Repulsor(float shake) => P()
            .R(Bone.UpperArmR, -88f + shake, -4f, 6f).R(Bone.ForearmR, -3f).R(Bone.HandR, -78f - shake * 2f)
            .R(Bone.UpperArmL, 14f, 0f, -14f).R(Bone.ForearmL, -24f).R(Bone.HandL, 6f)
            .R(Bone.Spine, 4f, -10f).R(Bone.Chest, 2f, -6f).R(Bone.Head, 2f, 14f);

        public static readonly ActionClip Beam = new ActionClip("Beam", 0.3f, true)
            .Key(0f, Repulsor(0f))
            .Key(0.1f, Repulsor(-1.5f))
            .Key(0.2f, Repulsor(1f))
            .Blend(0.22f, 0.3f);

        /// <summary>
        /// A vault (through a broken window, over a fallen log or tree): leaning in with both hands planted on top, knees
        /// tucked up to the chest and swung across (loop while vaulting; the body itself is lifted over the top by
        /// <see cref="HumanoidAnimator.Lift"/>).
        /// </summary>
        public static readonly ActionClip Climb = new ActionClip("Climb", 0.6f, true)
            .Key(0f, P().R(Bone.Spine, 30f).R(Bone.Chest, 10f).R(Bone.Head, -14f).Both(Bone.UpperArmR, -42f, 0f, 10f).Both(Bone.ForearmR, -12f).Both(Bone.HandR, 60f)
                .Both(Bone.ThighR, -88f, 0f, 6f).Both(Bone.ShinR, 112f).Both(Bone.FootR, 20f))
            .Key(0.3f, P().R(Bone.Spine, 26f, 6f).R(Bone.Chest, 8f).R(Bone.Head, -12f).Both(Bone.UpperArmR, -38f, 0f, 10f).Both(Bone.ForearmR, -8f).Both(Bone.HandR, 64f)
                .R(Bone.ThighR, -96f, 0f, 14f).R(Bone.ShinR, 118f).R(Bone.ThighL, -80f, 0f, -2f).R(Bone.ShinL, 104f).Both(Bone.FootR, 22f))
            .Blend(0.18f, 0.22f);

        /// <summary>Reading one of the Four Notes: holding the photo at the chest, head bowed over it.</summary>
        public static readonly ActionClip ReadNote = new ActionClip("ReadNote", 1.6f)
            .Key(0f, Stand())
            .Key(0.35f, P().R(Bone.Head, 26f).R(Bone.Spine, 6f).Both(Bone.UpperArmR, -42f, 0f, -6f).Both(Bone.ForearmR, -78f).Both(Bone.HandR, 20f))
            .Key(1.25f, P().R(Bone.Head, 28f).R(Bone.Spine, 7f).Both(Bone.UpperArmR, -44f, 0f, -7f).Both(Bone.ForearmR, -80f).Both(Bone.HandR, 22f))
            .Key(1.6f, Stand())
            .Blend(0.15f, 0.2f);

        // ---------------------------------------------------------------- NPCs

        /// <summary>Chacko on the couch: sat back, a controller in both hands, thumbs busy (loop).</summary>
        public static readonly ActionClip Sit = new ActionClip("Sit", 0.8f, true)
            .Key(0f, P().Lower(0.45f).Both(Bone.ThighR, -88f).Both(Bone.ShinR, 86f).R(Bone.Spine, -10f).R(Bone.Chest, -4f).R(Bone.Head, 9f)
                .Both(Bone.UpperArmR, -28f, 0f, -6f).Both(Bone.ForearmR, -95f).Both(Bone.HandR, 15f))
            .Key(0.4f, P().Lower(0.45f).Both(Bone.ThighR, -88f).Both(Bone.ShinR, 86f).R(Bone.Spine, -9f).R(Bone.Chest, -3f).R(Bone.Head, 11f)
                .Both(Bone.UpperArmR, -30f, 0f, -7f).Both(Bone.ForearmR, -99f).R(Bone.HandR, 22f).R(Bone.HandL, 10f))
            .Blend(0.3f, 0.3f);

        /// <summary>Plasma's GAMER RAGE: hunched, fists out and up, roaring at the sky and back (loop).</summary>
        public static readonly ActionClip Rage = new ActionClip("Rage", 0.6f, true)
            .Key(0f, P().Lower(0.08f).R(Bone.Spine, 8f).R(Bone.Chest, -10f).R(Bone.Head, -18f).Both(Bone.UpperArmR, -25f, 0f, 35f).Both(Bone.ForearmR, -110f).Both(Bone.ThighR, -12f).Both(Bone.ShinR, 20f))
            .Key(0.3f, P().Lower(0.1f).R(Bone.Spine, 10f).R(Bone.Chest, -6f).R(Bone.Head, -10f).Both(Bone.UpperArmR, -32f, 0f, 28f).Both(Bone.ForearmR, -118f).Both(Bone.ThighR, -14f).Both(Bone.ShinR, 24f))
            .Blend(0.3f, 0.3f);

        /// <summary>Chris Zelley's work done: arms spread, face to the sky, as he rises (loop).</summary>
        public static readonly ActionClip Ascend = new ActionClip("Ascend", 1.2f, true)
            .Key(0f, P().R(Bone.Chest, -6f).R(Bone.Head, -20f).Both(Bone.UpperArmR, -10f, 0f, 70f).Both(Bone.ForearmR, -10f).Both(Bone.HandR, -10f))
            .Key(0.6f, P().R(Bone.Chest, -8f).R(Bone.Head, -24f).Both(Bone.UpperArmR, -12f, 0f, 78f).Both(Bone.ForearmR, -6f).Both(Bone.HandR, -15f))
            .Blend(0.4f, 0.3f);

        /// <summary>Every clip, for the animation lab.</summary>
        public static IEnumerable<ActionClip> All => new[]
        {
            Repair, Lever, Tend, PickUp, Crouch, Drink, Swig, Eat, Talk, Plant, Push, Slam, Throw, AimLong, RecoilLong, RecoilPump, AimRifle, RecoilRifle,
            AimPistol, RecoilPistol, HipRifle, RecoilHip, Tablet, Stunned, Cough, Cringe, Staked, Carried, Crawl, Charge, ChargeBack, Swing, SwingHeavy, SwingHeavyBack, Lunge, Carry, LiftBody, StakeBody,
            Search, SearchOnce, Kick, Burst, Vape, Hemp, Beam, Climb, ReadNote, Sit, Rage, Ascend, SwingBack, CoverEyes,
        };
    }
}
