using UnityEngine;
using Vision.Audio;
using Vision.Effects;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// Turns what happens in the match into what the local player sees and hears, as the original's GameView does: the
    /// Soundcloud Burst scare (picture and a slice of the song), The Grapes of Wrath and Waz flashes, notes, the vine boom
    /// (louder the nearer), camera shakes, centre messages, the Penjamin gas loop, and stun stars over the player.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public SandboxWorld world;
        public ScreenOverlays overlays;
        public TopDownCamera cameraRig;

        MatchHost host;
        PlayerController player;
        StunStars stars;
        Material starMaterial;

        public AudioManager Audio => AudioManager.Instance;

        float zoomK;

        /// <summary>Who the camera follows while you are out of the match (escaped, sacrificed or spectating), or 0.</summary>
        public int Spectating { get; private set; }

        void Update()
        {
            Bind();
            SimPlayer me = host != null ? host.Local : null;
            if (me == null) return;
            UpdateCamera(me);
            // Hear from where you are.
            Audio.Listener = Scale.ToUnits(1f) * me.Pos;
            // Penjamin: the gas sound loops while a survivor is in the cloud.
            Audio.GasLoop(me.Role == Role.Survivor && me.VapeT > Balance.Hunter.Vape.AfterTime - 0.15f);
            // Stun stars over the player while stunned (Zach: also knocked down); the dizzy spiral in Penjamin's gas.
            if (stars != null)
            {
                bool dazed = me.StunT > 0f || (me.Role == Role.Hunter && me.KnockT > 0f);
                stars.Dizzy = !dazed && me.VapeT > 0f;
                stars.Show(dazed || stars.Dizzy);
            }
        }

        /// <summary>
        /// The camera: the Hemp Battery zooms Zach out (eased in and out), Waz's field of view widens or narrows it, and once
        /// you are out of the match it follows someone still in it (click or the arrow keys switch who).
        /// </summary>
        void UpdateCamera(SimPlayer me)
        {
            if (cameraRig == null || player == null) return;
            float dt = Time.deltaTime;
            zoomK = Mathf.MoveTowards(zoomK, me.Role == Role.Hunter && me.HempOn ? 1f : 0f, dt * Balance.Hunter.Hemp.ZoomRate);
            float e = zoomK * zoomK * (3f - 2f * zoomK);
            float fov = me.FovMul > 0f ? me.FovMul : 1f;
            cameraRig.zoom = (1f + (Balance.Hunter.Hemp.ZoomOut - 1f) * e) / fov;

            bool out_ = me.Role == Role.Spectator || me.Health == Health.Escaped || me.Health == Health.Eliminated;
            Transform target = player.transform;
            Spectating = 0;
            if (out_)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                bool next = (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame && !GameHud.MenuOpen);
                bool prev = kb != null && kb.leftArrowKey.wasPressedThisFrame;
                if (next || prev) host.Sim.CycleSpectate(me, next ? 1 : -1);
                if (host.Sim.Get(me.Spectating) == null) me.Spectating = host.Sim.DefaultSpectateTarget(me.Id);
                var puppets = world.GetComponent<PlayerPuppets>();
                PlayerPuppets.Puppet pup = puppets != null ? puppets.For(me.Spectating) : null;
                if (pup != null && pup.Go != null)
                {
                    target = pup.Go.transform;
                    Spectating = me.Spectating;
                }
            }
            if (cameraRig.target != target) cameraRig.target = target;
        }

        void Bind()
        {
            if (world == null) return;
            if (cameraRig == null) cameraRig = FindAnyObjectByType<TopDownCamera>();
            MatchHost h = MatchHost.For(world);
            if (h != host)
            {
                if (host != null) host.EventRaised -= OnEvent;
                host = h;
                if (host != null) host.EventRaised += OnEvent;
            }
            if (world.Player != player)
            {
                player = world.Player;
                stars = null;
                if (player != null)
                {
                    stars = player.GetComponent<StunStars>() ?? player.gameObject.AddComponent<StunStars>();
                    if (starMaterial == null && world.entityMaterial != null)
                    {
                        starMaterial = new Material(world.entityMaterial) { name = "Stun Stars", hideFlags = HideFlags.HideAndDontSave };
                        if (starMaterial.HasProperty("_Emission")) starMaterial.SetFloat("_Emission", 0.9f);
                    }
                    stars.material = starMaterial;
                }
            }
        }

        void OnDestroy()
        {
            if (host != null) host.EventRaised -= OnEvent;
            if (starMaterial != null) Destroy(starMaterial);
        }

        /// <summary>The original's near(): 1 at the listener, falling to 0 at <paramref name="radius"/> original units.</summary>
        float Near(Vector2 designPos, float radius) => Audio.Near(Scale.ToUnits(1f) * designPos, radius);

        void Shake(float px)
        {
            if (cameraRig != null && px > 0f) cameraRig.Shake(px);
        }

        public void OnEvent(GameEvent e)
        {
            Bind();
            if (host == null || host.Sim == null) return;
            int me = host.LocalId;
            switch (e.Kind)
            {
                case EventKind.Scare:
                {
                    // 2.5 s: the picture and a snippet of the song both fade in and out.
                    overlays?.JumpScare(Balance.Hunter.Burst.ScareTime, Balance.Hunter.Burst.ScareFade);
                    Audio.PlayClip("burst", Balance.Hunter.Burst.ScareVolume, Balance.Hunter.Burst.ScareFade, Balance.Hunter.Burst.ScareFade, Balance.Hunter.Burst.ScareTime);
                    break;
                }
                case EventKind.Talk:
                    // Zach fired a Soundcloud Burst: only he hears it go out.
                    if (e.Text == "burst" && e.A == me) Audio.PlayClip("burst", Balance.Hunter.Burst.ZachVolume);
                    break;
                case EventKind.Book:
                    overlays?.FlashImage(ScreenOverlays.Picture($"book-{Mathf.Clamp(e.A, 0, Balance.Items.Book.Images - 1) + 1}"), Balance.Items.FlashTime, Balance.Items.FlashTime * Balance.Items.FlashFade, true);
                    Shake(16f);
                    break;
                case EventKind.WazSlain:
                    overlays?.FlashImage(ScreenOverlays.Picture("waz-slain"), Balance.Items.FlashTime, Balance.Items.FlashTime * Balance.Items.FlashFade, false);
                    Audio.OneShot("boom", 1f);
                    break;
                case EventKind.Boom:
                    Audio.OneShot("boom", Mathf.Max(0.15f, Near(e.Pos, e.F > 0f ? e.F : 1400f)));
                    break;
                case EventKind.Explosion:
                    Audio.OneShot("boom", Mathf.Max(0.2f, Near(e.Pos, 1600f)));
                    Shake(22f * Near(e.Pos, 900f));
                    break;
                case EventKind.Note:
                    overlays?.ShowNote(e.A);
                    break;
                case EventKind.Hit:
                    if (e.A == me) Shake(14f);
                    else Shake(6f * Near(e.Pos, 300f));
                    break;
                case EventKind.Shot:
                    Shake(9f * Near(e.Pos, 500f));
                    break;
                case EventKind.Noise:
                    if (e.Text == "gen_explode" || e.Text == "barricade" || e.Text == "smash" || e.Text == "door_smash") Shake(7f * Near(e.Pos, 450f));
                    break;
                case EventKind.Downed:
                    if (e.A == me) overlays?.Center("You are down", 4f);
                    break;
                case EventKind.Stun:
                    if (e.A == me)
                    {
                        if (e.Text == "down") overlays?.Center("DOWN", Balance.Hunter.Health.DownTime);
                        else overlays?.Center("STUNNED", 1.5f);
                        Shake(10f);
                    }
                    break;
                case EventKind.Staked:
                    if (e.A == me) overlays?.Center(e.B >= 2 ? "Sacrificed." : "Staked", 4f);
                    break;
                case EventKind.Eliminated:
                    if (e.A == me) overlays?.Center("Sacrificed", 5f);
                    break;
                case EventKind.Escaped:
                    if (e.A == me) overlays?.Center("Escaped", 5f);
                    break;
                case EventKind.GatePowered:
                    overlays?.Center("The exit gate has power", 4f);
                    break;
                case EventKind.GateOpen:
                    overlays?.Center("THE GATE IS OPEN", 4f);
                    break;
                case EventKind.Jarvis:
                    overlays?.Big("JARVIS ONLINE");
                    break;
                case EventKind.Hemp:
                    if (e.A == me) overlays?.Big("HEMP BATTERY ACTIVATED");
                    break;
            }
        }
    }
}
