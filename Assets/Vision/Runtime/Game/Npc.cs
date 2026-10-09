using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// An NPC as the match rules see it (the original's NpcTarget plus the per-NPC hooks combat, items, gas and the beam
    /// call). Each NPC overrides what applies to it; the defaults do nothing. Positions are design units on the ground plane.
    /// </summary>
    [System.Flags]
    public enum NpcFlags
    {
        None = 0, Hurt = 1, Stunned = 2, Dead = 4, Fleeing = 8, Chasing = 16, Firing = 32, Armed = 64, Angry = 128, Talking = 256,
        Punching = 512, Fuse = 1024, Following = 2048, Seated = 4096, Raging = 8192, Defending = 16384,
    }

    public abstract class Npc
    {
        public abstract string Name { get; }
        /// <summary>Index into <see cref="Texts.NpcNames"/> (the minimap and name tags).</summary>
        public abstract int Kind { get; }
        public Vector2 Pos;
        public float Facing = -Mathf.PI / 2f;
        /// <summary>Animation state for the view: 0 idle, 1 walk, 2 run.</summary>
        public int Gait;
        /// <summary>Walking this tick (the view animates the legs).</summary>
        public bool Moving;
        /// <summary>How close the NPC is to being alerted (0-1), shown over its head (Shane, Jaden).</summary>
        public virtual float AlertLevel => 0f;
        /// <summary>What the view shows: hurt, stunned, dead, fleeing, chasing, firing, armed, angry...</summary>
        public virtual NpcFlags Flags => (HurtT > 0f ? NpcFlags.Hurt : 0) | (StunT > 0f ? NpcFlags.Stunned : 0) | (!Alive ? NpcFlags.Dead : 0) | (SayT > 0f ? NpcFlags.Talking : 0);
        public float StunT, GasT, FlinchT, HurtT;
        /// <summary>Penjamin's slow on this NPC.</summary>
        public float VapeSlow, VapeSlowT;
        /// <summary>A line of speech and how long it shows.</summary>
        public string Say;
        public float SayT;
        public int Id;

        /// <summary>Collision radius in original units.</summary>
        public virtual float RadiusUnits => 15f;
        public float HitRadius => Scale.D(RadiusUnits);
        /// <summary>Alive and present in the world (thrown items and pellets can hit it).</summary>
        public virtual bool Solid => true;
        public virtual bool Alive => true;
        /// <summary>Gone for good (Chris after ascending, Waz slain): not drawn.</summary>
        public virtual bool Gone => false;

        public virtual void Update(MatchSim sim, float dt) { }
        public virtual Prompt TalkPrompt(MatchSim sim, SimPlayer p) => Prompt.None;
        public virtual void Talk(MatchSim sim, SimPlayer p) { }
        /// <summary>The prompt shown while this NPC waits on the player (Sexton's second line, Njaaron's question).</summary>
        public virtual Prompt Awaiting(MatchSim sim, SimPlayer p) => Prompt.None;
        public virtual void CancelTalk(MatchSim sim, SimPlayer p) { }
        public virtual void Answer(MatchSim sim, SimPlayer p, bool yes) { }

        /// <summary>A survivor's thrown item or shot (kind "bottle" or "shot").</summary>
        public virtual void ItemHit(MatchSim sim, SimPlayer by, string kind) { }
        /// <summary>Zach's machete, lunge, beam or Penjamin can hit it (in this priority order after survivors).</summary>
        public virtual bool Slashable => false;
        /// <summary>A machete-like hit from Zach: power 1 light, 2 heavy; harm false is only a scare (Penjamin's first touch).</summary>
        public virtual void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true) { }
        /// <summary>Penjamin's gas reaches it: it reacts once as if attacked.</summary>
        public virtual void Provoke(MatchSim sim, SimPlayer by) => Slash(sim, by, 1, false);
        /// <summary>Penjamin hurts it (a light hit every 1.5 s) as well as slowing it.</summary>
        public virtual bool VapeHurts => Slashable;
        /// <summary>Galaxy gas covers it this tick.</summary>
        public virtual void Gassed(MatchSim sim) => GasT = 0.25f;
        /// <summary>Sets off a galaxy gas trap (an alerted Shane or Jaden, a raging Plasma).</summary>
        public virtual bool TripsTraps => false;
        /// <summary>Aggressive right now: a Soundcloud Burst wave stuns it.</summary>
        public virtual bool Aggressive => false;
        /// <summary>A 0.50 cal round passes through it.</summary>
        public virtual void Snipe(MatchSim sim, SimPlayer shooter) => ItemHit(sim, shooter, "shot");
        /// <summary>Chacko's vengeance: hunt this survivor.</summary>
        public virtual void Avenge(MatchSim sim, SimPlayer target) { }
        public virtual void StopAvenging(MatchSim sim) { }
        /// <summary>A door closed on it: move clear.</summary>
        public virtual void Unstick(MatchSim sim) { }

        public void Speak(string line, float seconds = 0f)
        {
            Say = line;
            SayT = seconds > 0f ? seconds : Mathf.Max(2.6f, line.Length * 0.075f);
        }

        protected void TickTimers(float dt)
        {
            StunT = Mathf.Max(0f, StunT - dt);
            GasT = Mathf.Max(0f, GasT - dt);
            FlinchT = Mathf.Max(0f, FlinchT - dt);
            HurtT = Mathf.Max(0f, HurtT - dt);
            SayT = Mathf.Max(0f, SayT - dt);
            if (SayT <= 0f) Say = null;
            VapeSlowT = Mathf.Max(0f, VapeSlowT - dt);
            if (VapeSlowT <= 0f) VapeSlow = 0f;
        }
    }
}
