using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Characters
{
    /// <summary>
    /// How a character looks and moves beyond walking: its model (from a <see cref="CharacterSpec"/>), what it holds,
    /// and which action clip plays, chosen from its state in the match (working a generator, drinking, stunned, carrying,
    /// charging a swing...) and from match events (a throw, a shot, a swing, a hit). The same for the local player and
    /// for everyone else.
    /// </summary>
    [RequireComponent(typeof(ActionLayer))]
    public sealed class CharacterView : MonoBehaviour
    {
        public CharacterSpec Spec { get; private set; }
        public ActionLayer Layer { get; private set; }
        public HumanoidAnimator Animator { get; private set; }
        SkinnedMeshRenderer skin;
        ActionClip oneShot;
        ActionKind lastAction;
        float lastScare;

        void Awake() => Wire();

        void Wire()
        {
            if (Layer == null) Layer = GetComponent<ActionLayer>();
            if (Animator == null) Animator = GetComponent<HumanoidAnimator>();
            if (skin == null) skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (Layer != null) Layer.Wire();
        }

        /// <summary>Puts on a character's model and size (a role change swaps the whole look).</summary>
        public void SetSpec(CharacterSpec spec)
        {
            Wire();
            if (spec == null || (Spec != null && Spec.Name == spec.Name)) return;
            Spec = spec;
            if (skin != null) skin.sharedMesh = CharacterBuilder.Shared(spec);
            transform.localScale = Vector3.one * spec.RootScale;
            Layer.Hold(PropKind.None);
            Layer.Hold(PropKind.None, true);
        }

        /// <summary>A one-shot still playing (a gun's kick that has played through and holds the aim pose doesn't count).</summary>
        bool OneShotPlaying => oneShot != null && Layer.IsPlaying(oneShot) && !(oneShot.HoldEnd && Layer.AtEnd(oneShot));

        void PlayOnce(ActionClip clip, float speed = 1f, float startAt = 0f)
        {
            oneShot = clip;
            Layer.Play(clip, speed, true, startAt);
        }

        /// <summary>Chooses the held prop and the clip from the player's state. Call every frame.</summary>
        public void Present(SimPlayer p)
        {
            Wire();
            if (p == null || Layer == null) return;
            bool zach = p.Role == Role.Hunter;
            if (Animator != null)
            {
                Animator.Prone = zach ? p.KnockT > 0f : p.Health == Game.Health.Downed || p.Health == Game.Health.Carried;
                Animator.Crouch = !zach && p.Crouching && (p.Health == Game.Health.Healthy || p.Health == Game.Health.Wounded) ? 1f : 0f;
            }

            // Props: Zach's machete; a survivor's selected item, or the flashlight.
            PropKind right;
            if (zach) right = p.Pump > 0 ? PropKind.GoldenPump : PropKind.Machete;
            else
            {
                Inventory.Slot s = p.Selected;
                PropKind held = s != null ? PropModels.ForItem(s.item, s.golden) : PropKind.None;
                bool hands = p.Health == Game.Health.Healthy || p.Health == Game.Health.Wounded;
                right = hands ? (held != PropKind.None ? held : PropKind.Flashlight) : PropKind.None;
            }
            // Night vision goggles go on over the eyes while they are on (the hands stay free: the light in the right).
            bool goggles = !zach && p.GogglesOn && p.Health != Game.Health.Downed && p.Health != Game.Health.Carried;
            if (goggles && right == PropKind.Goggles) right = PropKind.Flashlight;
            Layer.Wear(goggles ? PropKind.Goggles : PropKind.None);
            // The Hemp Beam fires from the open right palm: whatever was in it goes to the left hand meanwhile.
            bool beaming = p.BeamT > 0f;
            Layer.Hold(beaming ? PropKind.None : right);
            Layer.Hold(beaming ? right : PropKind.None, true);

            // One-shots on entering an action.
            if (p.Action != lastAction)
            {
                if (p.Action == ActionKind.Loot) PlayOnce(ActionClips.PickUp);
                else if (p.Action == ActionKind.PickUp) PlayOnce(ActionClips.LiftBody, ActionClips.LiftBody.Length / Mathf.Max(0.2f, p.ActionDur > 0f ? p.ActionDur : 1f));
                else if (p.Action == ActionKind.Stake) PlayOnce(ActionClips.StakeBody, ActionClips.StakeBody.Length / Mathf.Max(0.2f, p.ActionDur > 0f ? p.ActionDur : 1.2f));
                else if (p.Action == ActionKind.Plant) PlayOnce(ActionClips.Plant, ActionClips.Plant.Length / Mathf.Max(0.2f, p.ActionDur > 0f ? p.ActionDur : 1f));
                lastAction = p.Action;
            }
            if (p.ScareT > 0f && lastScare <= 0f) PlayOnce(ActionClips.Cringe);
            lastScare = p.ScareT;
            // Working the pump stops once the shotgun's out of their hands.
            if (OneShotPlaying && (oneShot == ActionClips.PumpReloadShotgun || oneShot == ActionClips.PumpReloadGolden)
                && right != PropKind.Shotgun && right != PropKind.GoldenPump) oneShot = null;
            if (OneShotPlaying) return;

            ActionClip state = zach ? HunterState(p) : SurvivorState(p);
            // A gun's kick played through hands straight over to holding the gun (it ends in that pose): no fade through
            // the walk in between, so the support hand never leaves the gun.
            bool handOver = oneShot != null && oneShot.HoldEnd && Layer.AtEnd(oneShot);
            if (state != null) Layer.Play(state, 1f, false, 0f, handOver);
            else Layer.Stop();
            if (handOver) oneShot = null;
        }

        static ActionClip SurvivorState(SimPlayer p)
        {
            // A ghost taking a body: the A pose, hovering, as the body before it.
            if (p.Ghost && p.Body != 0 && p.PossessT > 0f) return ActionClips.APose;
            // (A ghost is out of the match, but in a body it still works a generator, cowers in gas...)
            if (!p.Ghost)
                switch (p.Health)
                {
                    case Game.Health.Downed: return ActionClips.Crawl;
                    case Game.Health.Carried: return ActionClips.Carried;
                    case Game.Health.Staked: return ActionClips.Staked;
                    case Game.Health.Escaped:
                    case Game.Health.Eliminated: return null;
                }
            if (p.StunT > 0f) return ActionClips.Stunned;
            // A survivor Thomas armed fires the Hemp Beam from the palm too.
            if (p.BeamT > 0f) return ActionClips.Beam;
            switch (p.Action)
            {
                case ActionKind.Repair: return ActionClips.Repair;
                case ActionKind.OpenGate: return ActionClips.Lever;
                case ActionKind.Heal:
                case ActionKind.Revive:
                case ActionKind.Unstake: return ActionClips.Tend;
                case ActionKind.HideEnter:
                case ActionKind.HideExit: return ActionClips.Crouch;
                case ActionKind.Drink: return ActionClips.Drink;
                case ActionKind.Talk: return ActionClips.Talk;
            }
            if (p.Move.Climbing) return ActionClips.Climb;
            if (p.VapeT > 0f) return ActionClips.CoverEyes;
            Inventory.Slot s = p.Selected;
            if (s != null)
            {
                // Each gun held the way it is in real life: a pistol in a two-handed Weaver grip, the shotgun shouldered,
                // the 0.50 cal at the hip until right click brings the scope up to the eye.
                if (s.item == ItemType.Pistol) return ActionClips.AimPistol;
                if (s.item == ItemType.Shotgun) return ActionClips.AimLong;
                if (s.item == ItemType.Sniper) return p.Aiming ? ActionClips.AimRifle : ActionClips.HipRifle;
            }
            return null;
        }

        static ActionClip HunterState(SimPlayer p)
        {
            if (p.KnockT > 0f) return null;
            // Stunned, he reels on the spot.
            if (p.StunT > 0f) return ActionClips.Reel;
            switch (p.Action)
            {
                case ActionKind.Search: return ActionClips.Search;
                case ActionKind.DamageGen: return ActionClips.Kick;
                case ActionKind.Talk: return ActionClips.Talk;
            }
            if (p.BeamT > 0f) return ActionClips.Beam;
            if (p.Move.Climbing) return ActionClips.Climb;
            // Charging, the machete is cocked over the shoulder the next cut of the X falls from.
            if (p.ChargeT >= 0f) return p.NextSide == 1 ? ActionClips.ChargeBack : ActionClips.Charge;
            if (p.Move.LungeT > 0f) return ActionClips.Lunge;
            if (p.Carrying != 0) return ActionClips.Carry;
            if (p.VapeT > 0f) return ActionClips.CoverEyes;
            // Plasma's golden pump, shouldered until its shots run out.
            if (p.Pump > 0) return ActionClips.AimLong;
            return null;
        }

        /// <summary>One-shot clips and reactions from what happened in the match.</summary>
        public void OnEvent(GameEvent e, SimPlayer self, MatchSim sim)
        {
            Wire();
            if (self == null || Layer == null) return;
            switch (e.Kind)
            {
                case EventKind.Swing:
                    // The X: forehand or backhand (e.G), light or charged (e.B). From a charge the machete is already
                    // cocked over that shoulder: a light cut skips its windup.
                    if (e.A == self.Id && e.F < 0f)
                    {
                        bool back = e.G > 0.5f;
                        bool cocked = Layer.Current == ActionClips.Charge || Layer.Current == ActionClips.ChargeBack;
                        if (e.B >= 2) PlayOnce(back ? ActionClips.SwingHeavyBack : ActionClips.SwingHeavy);
                        else PlayOnce(back ? ActionClips.SwingBack : ActionClips.Swing, 1f, cocked ? 0.07f : 0f);
                    }
                    break;
                case EventKind.Throw:
                    if (e.A == self.Id) PlayOnce(ActionClips.Throw);
                    break;
                case EventKind.Shot:
                    // The kick of each gun: the pistol's snap, the shotgun's shove and the pump worked over its reload (Zach's
                    // golden pump racked after it); the 0.50 cal is simply held on, both hands on it, no change at all.
                    if (e.A == self.Id)
                    {
                        var gun = (Vision.Player.ItemType)e.B;
                        if (gun == ItemType.Sniper) break;
                        bool golden = e.G > 0f || (e.Text != null && e.Text.Contains("gold"));
                        PlayOnce(gun == ItemType.Pistol ? ActionClips.RecoilPistol
                            : self.Role == Role.Hunter ? ActionClips.RecoilPump
                            : golden ? ActionClips.PumpReloadGolden : ActionClips.PumpReloadShotgun);
                    }
                    break;
                case EventKind.Talk:
                    if (e.A == self.Id && e.Text == "burst") PlayOnce(ActionClips.Burst);
                    else if (e.A == self.Id && e.Text == "search") PlayOnce(ActionClips.SearchOnce);
                    else if (e.A == self.Id && e.Text == "slam") PlayOnce(ActionClips.Slam);
                    else if (e.A == self.Id && e.Text == "door") PlayOnce(ActionClips.Push);
                    else if (e.A == self.Id && e.Text == "drink") PlayOnce(ActionClips.Swig);
                    else if (e.A == self.Id && e.Text == "eat") PlayOnce(ActionClips.Eat);
                    break;
                case EventKind.Gas:
                    if (e.A == self.Id && e.Text != null && (e.Text == "vape" || e.Text == "nic")) PlayOnce(ActionClips.Vape);
                    break;
                case EventKind.Hemp:
                    if (e.A == self.Id) PlayOnce(ActionClips.Hemp);
                    break;
                case EventKind.Note:
                    // Only the reader is told (A is the note), so it's the local player reading.
                    if (self.IsLocal) PlayOnce(ActionClips.ReadNote);
                    break;
                case EventKind.Hit:
                    if (e.A == self.Id)
                    {
                        // Flinch away from the attacker (or the blow's position), harder for heavy hits.
                        SimPlayer by = sim?.Get(e.B);
                        Vector2 from = by != null ? by.Pos - self.Pos : self.FacingDir;
                        float yaw = Animator != null ? Animator.LegsYaw * Mathf.Deg2Rad : 0f;
                        Vector2 local = new Vector2(from.x * Mathf.Cos(yaw) - from.y * Mathf.Sin(yaw), from.x * Mathf.Sin(yaw) + from.y * Mathf.Cos(yaw));
                        Layer.React(local, Mathf.Clamp(0.5f + e.F * 1.5f, 0.4f, 1.6f));
                    }
                    break;
            }
        }
    }
}
