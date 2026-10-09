using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// Draws every player the local machine doesn't control (testing dummies now, remote players online): an animated
    /// figure at the match's position, smoothed between ticks, walking at the speed it moves, lying down when downed,
    /// carried over Zach's shoulder, hidden inside a hiding spot or once out of the match, with stun stars when stunned.
    /// Like any entity it shows only inside the viewer's own light.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PlayerPuppets : MonoBehaviour
    {
        public SandboxWorld world;

        public sealed class Puppet
        {
            public int Id;
            public Role Role;
            public GameObject Go;
            public HumanoidAnimator Animator;
            public CharacterView View;
            public StunStars Stars;
            public Renderer[] Renderers;
            public Vector3 Shown;
            public bool Visible = true;
        }

        readonly Dictionary<int, Puppet> puppets = new Dictionary<int, Puppet>();
        readonly List<int> gone = new List<int>();
        Material starMaterial;

        public IReadOnlyDictionary<int, Puppet> All => puppets;

        public Puppet For(int id) => puppets.TryGetValue(id, out Puppet p) ? p : null;

        MatchHost subscribed;

        void LateUpdate()
        {
            MatchHost h = world != null ? MatchHost.For(world) : null;
            if (h != subscribed)
            {
                if (subscribed != null) subscribed.EventRaised -= OnMatchEvent;
                subscribed = h;
                if (h != null) h.EventRaised += OnMatchEvent;
            }
            Sync(Time.deltaTime);
        }

        void OnMatchEvent(GameEvent e)
        {
            MatchSim sim = subscribed != null ? subscribed.Sim : null;
            if (sim == null) return;
            foreach (Puppet pup in puppets.Values)
                if (pup.View != null) pup.View.OnEvent(e, sim.Get(pup.Id), sim);
        }

        /// <summary>Creates, moves and removes the figures to match the players in the match.</summary>
        public void Sync(float dt)
        {
            if (world == null) world = GetComponent<SandboxWorld>();
            MatchHost host = world != null ? MatchHost.For(world) : null;
            MatchSim sim = host != null ? host.Sim : null;
            gone.Clear();
            foreach (int id in puppets.Keys)
                if (sim == null || puppets[id].Go == null || sim.Get(id) == null || sim.Get(id).IsLocal || sim.Get(id).Role != puppets[id].Role) gone.Add(id);
            foreach (int id in gone)
            {
                Kill(puppets[id].Go);
                puppets.Remove(id);
            }
            if (sim == null) return;
            foreach (SimPlayer p in sim.Order)
            {
                if (p.IsLocal || p.Role == Role.Spectator) continue;
                if (!puppets.TryGetValue(p.Id, out Puppet pup)) pup = Create(p);
                Drive(pup, p, dt);
            }
        }

        Puppet Create(SimPlayer p)
        {
            GameObject go = PropFactory.CreateCharacter(p.Role == Role.Hunter ? $"Zach ({p.Name})" : p.Name, world.entityMaterial, null);
            go.transform.SetParent(world.transform, false);
            go.layer = SandboxWorld.CharacterLayer;
            go.AddComponent<CharacterShadow>().isEntity = true;
            var view = go.GetComponent<CharacterView>();
            view.SetSpec(p.Role == Role.Hunter ? CharacterSpec.Zach() : CharacterSpec.Survivor());
            var stars = go.AddComponent<StunStars>();
            if (starMaterial == null && world.entityMaterial != null)
            {
                starMaterial = new Material(world.entityMaterial) { name = "Stun Stars", hideFlags = HideFlags.HideAndDontSave };
                if (starMaterial.HasProperty("_Emission")) starMaterial.SetFloat("_Emission", 0.9f);
            }
            stars.material = starMaterial;
            var pup = new Puppet
            {
                Id = p.Id,
                Role = p.Role,
                Go = go,
                Animator = go.GetComponent<HumanoidAnimator>(),
                View = view,
                Stars = stars,
                Renderers = go.GetComponentsInChildren<Renderer>(),
                Shown = new Vector3(p.Pos.x, 0f, p.Pos.y),
            };
            go.transform.localPosition = Ground(p.Pos);
            puppets[p.Id] = pup;
            return pup;
        }

        /// <summary>A staked survivor stands just in front of the post (the match puts them on it).</summary>
        public static Vector3 StakeOffset(SimPlayer p)
        {
            if (p.Health != Game.Health.Staked) return Vector3.zero;
            Vector2 d = p.FacingDir * 0.2f;
            return new Vector3(d.x, 0f, d.y);
        }

        Vector3 Ground(Vector2 at)
        {
            float h = world.GroundHeight(at);
            return new Vector3(at.x, h, at.y);
        }

        void Drive(Puppet pup, SimPlayer p, float dt)
        {
            bool visible = p.HideState != 2 && p.Health != Game.Health.Escaped && p.Health != Game.Health.Eliminated;
            if (visible != pup.Visible)
            {
                pup.Visible = visible;
                foreach (Renderer r in pup.Renderers) if (r != null) r.enabled = visible;
            }
            Vector3 target = Ground(p.Pos);
            // Carried over Zach's shoulder; tied to the front of a stake.
            if (p.Health == Game.Health.Carried) target += Vector3.up * 1.05f;
            target += StakeOffset(p);
            Vector3 before = pup.Go.transform.localPosition;
            // Ticks come at 30 Hz: ease toward the latest position (snap on long jumps, such as a teleport).
            Vector3 now = (target - before).sqrMagnitude > 9f ? target : Vector3.Lerp(before, target, 1f - Mathf.Exp(-dt * 18f));
            pup.Go.transform.localPosition = now;
            pup.View.Present(p);
            if (pup.Animator != null)
            {
                Vector3 vLocal = dt > 0f ? (now - before) / dt : Vector3.zero;
                Vector3 vWorld = world.transform.TransformVector(new Vector3(vLocal.x, 0f, vLocal.z));
                pup.Animator.Drive(vWorld, new Vector2(Mathf.Cos(p.Facing), Mathf.Sin(p.Facing)));
            }
            bool dazed = p.StunT > 0f || (p.Role == Role.Hunter && p.KnockT > 0f);
            pup.Stars.Dizzy = !dazed && p.VapeT > 0f;
            pup.Stars.Show(visible && (dazed || pup.Stars.Dizzy));
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.EventRaised -= OnMatchEvent;
            foreach (Puppet p in puppets.Values) Kill(p.Go);
            puppets.Clear();
            Kill(starMaterial);
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
