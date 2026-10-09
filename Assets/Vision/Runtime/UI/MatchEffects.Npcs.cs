using System.Collections.Generic;
using UnityEngine;
using Vision.Game;
using Vision.Visibility;

namespace Vision.UI
{
    /// <summary>
    /// The story NPCs' effects: Sexton's reel playing around him and his Hemp Beam (the same white-green beam as Zach's,
    /// with the repulsor's hum and three lights along it), JARVIS passing from his hands to a survivor's, Chris Zelley's
    /// halo and wings as he ascends, Plasma.TTV's RGB aura while he rages, and the ambulance's red and blue light bar.
    /// </summary>
    public sealed partial class MatchEffects
    {
        struct Tablet { public Vector2 From; public int To; public float Born; }

        readonly List<Tablet> tablets = new List<Tablet>();
        readonly List<VisionLight> beamLights = new List<VisionLight>();
        bool reelOn, npcBeamOn;

        /// <summary>NPC events this partial handles (true: handled).</summary>
        bool NpcEvent(GameEvent e)
        {
            if (e.Kind != EventKind.Talk || e.A >= 0) return false;
            if (e.Text == "tablet") tablets.Add(new Tablet { From = e.Pos, To = e.B, Born = Now });
            return true;
        }

        void DrawNpcs(MatchSim sim)
        {
            Sexton sexton = null;
            foreach (Npc n in sim.Npcs)
                switch (n)
                {
                    case Sexton s:
                        sexton = s;
                        break;
                    case Chris c when c.State == Chris.Mode.Ascend:
                        Wings(c);
                        break;
                    case Plasma p when p.Alive && p.Beast:
                        Aura(p);
                        break;
                }
            SextonBeam(sexton);
            Reel(sexton);
            DrawTablets(sim);
            AmbulanceBar(sim);
        }

        void Reel(Sexton s)
        {
            bool on = s != null && s.Alive;
            if (on)
                Audio.Loop("sexton.reel", "sexton.reel", Units(s.Pos), 1f, Balance.Sexton.AudioNear, Balance.Sexton.AudioFar, Balance.Sexton.AudioCurve);
            else if (reelOn) Audio.Loop("sexton.reel", null);
            reelOn = on;
        }

        /// <summary>Sexton's beam: as Zach's, from his hands, lighting its whole length.</summary>
        void SextonBeam(Sexton s)
        {
            bool on = s != null && s.Beaming && s.BeamLen > 0f;
            if (on)
            {
                Vector3 hands = Along(s.Pos, s.BeamAng, Balance.Sexton.Radius * 1.6f, 0f, 1.15f);
                Vector3 end = Along(s.Pos, s.BeamAng, Scale.ToUnits(s.BeamLen), 0f, 1.15f);
                float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 60f);
                foreach (var (w, c) in new[] { (0.5f, new Color(0.35f, 1f, 0.4f, 0.25f)), (0.24f, new Color(0.7f, 1f, 0.72f, 0.6f)), (0.08f, new Color(1f, 1f, 1f, 0.95f)) })
                {
                    pts.Clear(); cols.Clear();
                    pts.Add(hands); pts.Add(end);
                    cols.Add(c * flicker); cols.Add(c * flicker);
                    Fx.Ribbon(pts, cols, w * S, false, true);
                }
                Fx.Dot(end, 0.5f * S * flicker, new Color(0.7f, 1f, 0.72f, 0.6f), false, true);
                Fx.Dot(hands, 0.3f * S, new Color(0.9f, 1f, 0.9f, 0.8f), false, true);
                Audio.Loop("beam.sexton", "repulsor", Units(s.Pos), Balance.Hunter.Beam.AudioVolume, Balance.Hunter.Beam.AudioNear, Balance.Hunter.Beam.AudioRadius, Balance.Hunter.Beam.AudioCurve, true);
            }
            else if (npcBeamOn) Audio.Loop("beam.sexton", null);
            npcBeamOn = on;
            // Three lights along it, as in the original (made again after a New map takes the old ones with the level).
            beamLights.RemoveAll(l => l == null);
            while (beamLights.Count < 3)
            {
                var go = new GameObject("Sexton Beam Light");
                go.transform.SetParent(world.transform, false);
                var l = go.AddComponent<VisionLight>();
                l.isStatic = false;
                l.range = Scale.D(Balance.Sexton.Defense.LightRadius);
                l.intensity = Balance.Sexton.Defense.LightIntensity;
                l.height = 1.2f;
                l.flickerAmount = 0.1f;
                l.enabled = false;
                beamLights.Add(l);
            }
            float[] along = { 0.15f, 0.55f, 0.97f };
            for (int i = 0; i < 3; i++)
            {
                beamLights[i].enabled = on;
                if (!on) continue;
                Vector2 at = s.Pos + new Vector2(Mathf.Cos(s.BeamAng), Mathf.Sin(s.BeamAng)) * s.BeamLen * along[i];
                float h = world.GroundHeight(at);
                beamLights[i].transform.localPosition = new Vector3(at.x, h, at.y);
            }
        }

        /// <summary>JARVIS flying from Sexton's hands to the survivor's in a little arc, glowing.</summary>
        void DrawTablets(MatchSim sim)
        {
            for (int i = tablets.Count - 1; i >= 0; i--)
            {
                Tablet t = tablets[i];
                float k = (Now - t.Born) / Balance.Sexton.HandTime;
                SimPlayer to = sim.Get(t.To);
                if (k >= 1f || to == null)
                {
                    tablets.RemoveAt(i);
                    continue;
                }
                Vector2 at = Vector2.Lerp(t.From, to.Pos, k);
                Vector3 p = Ground(at, 1.1f + Mathf.Sin(k * Mathf.PI) * 0.5f);
                Fx.Dot(p, 0.16f * S, new Color(0.55f, 0.9f, 1f, 0.95f));
                Fx.Dot(p, 0.4f * S, new Color(0.3f, 0.7f, 1f, 0.35f));
            }
        }

        /// <summary>Chris Zelley's halo and wings as he rises.</summary>
        void Wings(Chris c)
        {
            float k = Mathf.Clamp01(c.AscendT / Balance.Chris.AscendTime);
            float rise = Mathf.SmoothStep(0f, 1f, k) * 4f;
            float alpha = Mathf.Clamp01(k * 4f) * (1f - Mathf.Clamp01((k - 0.85f) / 0.15f));
            Vector3 head = Ground(c.Pos, 2.0f + rise);
            Fx.Circle(head, 0.2f * S, 0.035f * S, new Color(1f, 0.92f, 0.55f, 0.95f * alpha));
            Fx.Dot(head, 0.35f * S, new Color(1f, 0.9f, 0.5f, 0.25f * alpha));
            float flap = Mathf.Sin(Time.time * 7f) * 0.25f;
            float back = c.Facing + Mathf.PI;
            foreach (float side in new[] { -1f, 1f })
                for (int f = 0; f < 4; f++)
                {
                    pts.Clear(); cols.Clear();
                    pts.Add(Along(c.Pos, back, 6f, side * 8f, 1.45f + rise));
                    float spread = 22f + f * 12f;
                    pts.Add(Along(c.Pos, back, 10f + f * 2f, side * spread, 1.75f + rise + flap * (1f + f * 0.3f) - f * 0.12f));
                    cols.Add(new Color(1f, 1f, 1f, 0.9f * alpha)); cols.Add(new Color(1f, 1f, 1f, 0.35f * alpha));
                    Fx.Ribbon(pts, cols, (0.14f - f * 0.02f) * S);
                }
        }

        /// <summary>Plasma's RGB glow: rings at his feet cycling through the colours of his headset.</summary>
        void Aura(Plasma p)
        {
            float hue = Mathf.Repeat(Time.time * 0.6f, 1f);
            float r = Scale.D(Balance.Plasma.BeastRadius * 1.8f) * S;
            Vector3 at = Ground(p.Pos, 0.05f);
            Color a = Color.HSVToRGB(hue, 0.85f, 1f), b = Color.HSVToRGB(Mathf.Repeat(hue + 0.33f, 1f), 0.85f, 1f);
            a.a = 0.55f;
            b.a = 0.3f;
            Fx.Circle(at, r, 0.05f * S, a);
            Fx.Circle(at, r * 1.25f, 0.035f * S, b);
            Fx.Disc(at, r, new Color(a.r, a.g, a.b, 0.08f));
        }

        /// <summary>The ambulance's light bar, flashing red then blue.</summary>
        void AmbulanceBar(MatchSim sim)
        {
            if (world.Ambulance == null) return;
            bool red = Mathf.Repeat(Time.time, 1f) < 0.5f;
            Vector3 bar = world.Ambulance.TransformPoint(new Vector3(red ? -0.6f : 0.6f, 2.8f, 1.88f));
            Color c = red ? new Color(1f, 0.15f, 0.12f, 0.9f) : new Color(0.2f, 0.4f, 1f, 0.9f);
            Fx.Dot(bar, 0.22f * S, c, false, true);
            Fx.Dot(bar, 0.7f * S, new Color(c.r, c.g, c.b, 0.18f), false, true);
        }
    }
}
