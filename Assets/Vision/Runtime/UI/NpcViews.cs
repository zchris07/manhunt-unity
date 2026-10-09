using System.Collections.Generic;
using UnityEngine;
using Vision.Audio;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// Draws the match's NPCs: each in their own look (<see cref="CharacterSpec.Npc"/>), walking or running as they move,
    /// holding what they hold (Jaden's pistol when he's after someone, Monique's 0.50 cal when she's armed, Waz's
    /// magnifier), talking with their hands, punching, aiming and recoiling, stunned with stars, lying where they fell.
    /// The exploded are gone. Shane's chase comes with his footsteps. Like any entity, they show only in the viewer's light.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class NpcViews : MonoBehaviour
    {
        public SandboxWorld world;

        public sealed class View
        {
            public Npc Npc;
            public GameObject Go;
            public CharacterView Character;
            public ActionLayer Layer;
            public HumanoidAnimator Animator;
            public StunStars Stars;
            public Renderer[] Renderers;
            public bool Visible = true;
            public float StepT;
        }

        readonly Dictionary<Npc, View> views = new Dictionary<Npc, View>();
        readonly List<Npc> stale = new List<Npc>();
        MatchHost subscribed;
        Material starMaterial;

        public IReadOnlyDictionary<Npc, View> All => views;
        public View For(Npc n) => n != null && views.TryGetValue(n, out View v) ? v : null;

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void LateUpdate()
        {
            MatchHost h = world != null ? MatchHost.For(world) : null;
            if (h != subscribed)
            {
                if (subscribed != null) subscribed.EventRaised -= OnEvent;
                subscribed = h;
                if (h != null) h.EventRaised += OnEvent;
            }
            Sync(Time.deltaTime);
        }

        void OnEvent(GameEvent e)
        {
            if (e.Kind != EventKind.Shot || e.A >= 0) return;
            foreach (View v in views.Values)
                if (v.Npc.Id == -e.A && v.Layer != null)
                    v.Layer.Play(e.B == (int)Vision.Player.ItemType.Pistol ? ActionClips.RecoilPistol : ActionClips.RecoilLong, 1f, true);
        }

        Vector3 Ground(Vector2 at)
        {
            float h = world.Terrain != null ? world.Terrain.Height(at.x, at.y) : 0f;
            return new Vector3(at.x, h, at.y);
        }

        public void Sync(float dt)
        {
            if (world == null) world = GetComponent<SandboxWorld>();
            MatchHost host = world != null ? MatchHost.For(world) : null;
            MatchSim sim = host != null ? host.Sim : null;
            stale.Clear();
            foreach (var kv in views)
                if (sim == null || kv.Value.Go == null || !sim.Npcs.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (Npc n in stale)
            {
                Kill(views[n].Go);
                views.Remove(n);
            }
            if (sim == null) return;
            foreach (Npc n in sim.Npcs)
            {
                if (!views.TryGetValue(n, out View v)) v = Create(n);
                Drive(v, sim, dt);
            }
        }

        View Create(Npc n)
        {
            GameObject go = PropFactory.CreateCharacter(n.Name, world.entityMaterial, null);
            go.transform.SetParent(world.transform, false);
            go.layer = SandboxWorld.CharacterLayer;
            go.AddComponent<CharacterShadow>().isEntity = true;
            var character = go.GetComponent<CharacterView>();
            character.SetSpec(CharacterSpec.Npc(n.Name));
            var stars = go.AddComponent<StunStars>();
            if (starMaterial == null && world.entityMaterial != null)
            {
                starMaterial = new Material(world.entityMaterial) { name = "Stun Stars", hideFlags = HideFlags.HideAndDontSave };
                if (starMaterial.HasProperty("_Emission")) starMaterial.SetFloat("_Emission", 0.9f);
            }
            stars.material = starMaterial;
            var v = new View
            {
                Npc = n,
                Go = go,
                Character = character,
                Layer = go.GetComponent<ActionLayer>(),
                Animator = go.GetComponent<HumanoidAnimator>(),
                Stars = stars,
                Renderers = go.GetComponentsInChildren<Renderer>(),
            };
            go.transform.localPosition = Ground(n.Pos);
            views[n] = v;
            return v;
        }

        void Drive(View v, MatchSim sim, float dt)
        {
            Npc n = v.Npc;
            bool visible = !n.Gone;
            if (visible != v.Visible)
            {
                v.Visible = visible;
                foreach (Renderer r in v.Renderers) if (r != null) r.enabled = visible;
            }
            if (!visible) return;
            Vector3 target = Ground(n.Pos);
            Vector3 before = v.Go.transform.localPosition;
            Vector3 now = (target - before).sqrMagnitude > 9f ? target : Vector3.Lerp(before, target, 1f - Mathf.Exp(-dt * 18f));
            v.Go.transform.localPosition = now;
            NpcFlags f = n.Flags;
            bool dead = !n.Alive;
            v.Animator.Prone = dead;
            Vector3 vLocal = dt > 0f ? (now - before) / dt : Vector3.zero;
            v.Animator.Drive(world.transform.TransformVector(new Vector3(vLocal.x, 0f, vLocal.z)), new Vector2(Mathf.Cos(n.Facing), Mathf.Sin(n.Facing)));

            // What they hold.
            PropKind prop = PropKind.None;
            if (n is Jaden && (f & NpcFlags.Armed) != 0) prop = PropKind.Pistol;
            else if (n is Monique && (f & NpcFlags.Armed) != 0) prop = PropKind.Sniper;
            else if (n is Waz) prop = PropKind.Magnifier;
            if (dead) prop = PropKind.None;
            v.Layer.Hold(prop);

            // What they're doing.
            ActionClip clip = null;
            if (!dead)
            {
                if ((f & NpcFlags.Stunned) != 0) clip = ActionClips.Stunned;
                else if ((f & NpcFlags.Punching) != 0) clip = ActionClips.Push;
                else if (prop == PropKind.Pistol) clip = ActionClips.AimPistol;
                else if (prop == PropKind.Sniper) clip = ActionClips.AimLong;
                else if ((f & NpcFlags.Talking) != 0 && !n.Moving) clip = ActionClips.Talk;
            }
            bool oneShot = v.Layer.Current == ActionClips.RecoilPistol || v.Layer.Current == ActionClips.RecoilLong;
            if (!oneShot)
            {
                if (clip != null) v.Layer.Play(clip);
                else v.Layer.Stop();
            }
            if ((f & NpcFlags.Hurt) != 0 && v.Layer.Reaction.sqrMagnitude < 4f) v.Layer.React(Vector2.up, 0.5f);
            v.Stars.Show((f & NpcFlags.Stunned) != 0 && !dead);

            // Shane's footsteps while he chases, six a second like the original's pitter-patter.
            if (n is Shane && !(n is Jaden) && (f & NpcFlags.Chasing) != 0 && n.Moving)
            {
                v.StepT -= dt;
                if (v.StepT <= 0f)
                {
                    v.StepT = 1f / 6f;
                    AudioManager.Instance.PlayCue("step_shane", n.Pos / Scale.Unit, Balance.Shane.StepsVolume);
                }
            }
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.EventRaised -= OnEvent;
            foreach (View v in views.Values) Kill(v.Go);
            views.Clear();
            Kill(starMaterial);
        }
    }
}
