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

        bool OneShotPlaying => oneShot != null && Layer.IsPlaying(oneShot);

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
                if (p.Jarvis > 0 && p.JarvisT > 0f) held = PropKind.Tablet;
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
            if (OneShotPlaying) return;

            ActionClip state = zach ? HunterState(p) : SurvivorState(p);
            if (state != null) Layer.Play(state);
            else Layer.Stop();
        }

        static ActionClip SurvivorState(SimPlayer p)
        {
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
            if (p.Jarvis > 0 && p.JarvisT > 0f) return ActionClips.Tablet;
            Inventory.Slot s = p.Selected;
            if (s != null)
            {
                // Each gun held the way it is in real life: a pistol in a two-handed Weaver grip, the shotgun and the
                // 0.50 cal shouldered (the rifle with the cheek down on the scope).
                if (s.item == ItemType.Pistol) return ActionClips.AimPistol;
                if (s.item == ItemType.Shotgun) return ActionClips.AimLong;
                if (s.item == ItemType.Sniper) return ActionClips.AimRifle;
            }
            return null;
        }

        static ActionClip HunterState(SimPlayer p)
        {
            if (p.KnockT > 0f) return null;
            if (p.StunT > 0f) return ActionClips.Stunned;
            switch (p.Action)
            {
                case ActionKind.Search: return ActionClips.Search;
                case ActionKind.DamageGen: return ActionClips.Kick;
                case ActionKind.Talk: return ActionClips.Talk;
            }
            if (p.BeamT > 0f) return ActionClips.Beam;
            if (p.Move.Climbing) return ActionClips.Climb;
            if (p.ChargeT >= 0f) return ActionClips.Charge;
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
                    // From a charge the machete is already raised: skip the windup.
                    if (e.A == self.Id && e.F < 0f)
                    {
                        if (e.B >= 2) PlayOnce(ActionClips.SwingHeavy, 1f, Layer.Current == ActionClips.Charge ? 0.13f : 0f);
                        else if (e.G > 0.5f) PlayOnce(ActionClips.SwingBack);
                        else PlayOnce(ActionClips.Swing, 1f, Layer.Current == ActionClips.Charge ? 0.13f : 0f);
                    }
                    break;
                case EventKind.Throw:
                    if (e.A == self.Id) PlayOnce(ActionClips.Throw);
                    break;
                case EventKind.Shot:
                    // The kick of each gun: the pistol's snap, the shotgun's shove (Zach's pump racked after it), the 0.50's slam.
                    if (e.A == self.Id)
                    {
                        var gun = (Vision.Player.ItemType)e.B;
                        PlayOnce(gun == ItemType.Pistol ? ActionClips.RecoilPistol : gun == ItemType.Sniper ? ActionClips.RecoilRifle
                            : self.Role == Role.Hunter ? ActionClips.RecoilPump : ActionClips.RecoilLong);
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
