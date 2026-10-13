using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vision.UI
{
    /// <summary>
    /// Writes a word out letter by letter, after the boss lines in the user's game Stroop (strooply.html): each letter, 68 ms
    /// after the last, is stroked on over 1.25 s (a bright pen line round its shape, drawn on in the direction of writing on a
    /// fast-then-settling ease), and half a second in the ink floods it, the outline thinning as it fills; in italic. Here,
    /// on top, each letter morphs into place as it's written: it starts larger, leaning further and set off down and to the
    /// right, and settles into its slot. When it has been read it lifts a little and fades. A mesh effect on a UI Text.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class WrittenText : BaseMeshEffect
    {
        /// <summary>Stroop's timings: the stagger between letters, the stroke's length, when the ink starts and how long it takes.</summary>
        public const float Stagger = 0.068f, StrokeTime = 1.25f, InkDelay = 0.52f, InkTime = 0.8f, FadeTime = 1.1f;
        const float Italic = 0.22f;

        float start = -1f, fadeAt = -1f;
        readonly List<UIVertex> stream = new List<UIVertex>(256), output = new List<UIVertex>(2048);

        /// <summary>When the last letter is fully inked (seconds after the start).</summary>
        public float WrittenAfter(string text) => Stagger * Mathf.Max(0, text.Length - 1) + StrokeTime + 0.1f;

        public bool Showing => start >= 0f;

        /// <summary>Writes <paramref name="text"/> out, holds it <paramref name="holdSeconds"/>, then fades it.</summary>
        public void Write(string text, float holdSeconds)
        {
            var t = GetComponent<Text>();
            t.text = text;
            start = Time.unscaledTime;

            fadeAt = start + WrittenAfter(text) + holdSeconds;
            gameObject.SetActive(true);
            graphic.SetVerticesDirty();
        }

        void Update()
        {
            if (start < 0f) return;
            if (Time.unscaledTime > fadeAt + FadeTime)
            {
                start = -1f;
                gameObject.SetActive(false);
                return;
            }
            graphic.SetVerticesDirty();
        }

        /// <summary>Stroop's cubic-bezier(.5,.1,.3,1): slow off the mark, fast through the middle, settling.</summary>
        static float Pen(float x)
        {
            x = Mathf.Clamp01(x);
            // Solve the bezier's x(t) = x by a few Newton steps, then return y(t).
            float t = x;
            for (int i = 0; i < 6; i++)
            {
                float u = 1f - t;
                float bx = 3f * u * u * t * 0.5f + 3f * u * t * t * 0.3f + t * t * t - x;
                float dx = 3f * u * u * 0.5f + 6f * u * t * (0.3f - 0.5f) + 3f * t * t * (1f - 0.3f);
                if (Mathf.Abs(dx) < 1e-5f) break;
                t = Mathf.Clamp01(t - bx / dx);
            }
            float w = 1f - t;
            return 3f * w * w * t * 0.1f + 3f * w * t * t + t * t * t;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || start < 0f) return;
            stream.Clear();
            vh.GetUIVertexStream(stream);
            output.Clear();
            float now = Time.unscaledTime - start;
            float size = ((Text)graphic).fontSize;
            float fade = fadeAt < 0f ? 1f : 1f - Mathf.Clamp01((Time.unscaledTime - fadeAt) / FadeTime);
            float lift = (1f - fade) * size * 0.08f;
            int letters = stream.Count / 6;
            for (int g = 0; g < letters; g++)
            {
                UIVertex tl = stream[g * 6], tr = stream[g * 6 + 1], br = stream[g * 6 + 2], bl = stream[g * 6 + 4];
                float t = now - g * Stagger;
                if (t <= 0f) continue;
                float s = Pen(t / StrokeTime);
                float ink = Mathf.SmoothStep(0f, 1f, (t - InkDelay) / InkTime);
                float baseY = bl.position.y;
                Vector3 centre = (tl.position + br.position) * 0.5f;

                // Morphing into place: from larger, leaning further and set off down and right, to its slot.
                float m = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (StrokeTime * 0.7f)));
                float scale = 1f + 0.28f * m, lean = Italic + 0.25f * m;
                Vector3 off = new Vector3(size * 0.16f * m, -size * 0.12f * m + lift, 0f);
                Vector3 Place(Vector3 p)
                {
                    Vector3 d = (p - centre) * scale;
                    p = centre + d;
                    p.x += (p.y - baseY) * lean;
                    return p + off;
                }

                // Written on in the direction of writing: the letter shows up to the pen's front.
                float right = Mathf.Lerp(tl.position.x, tr.position.x, s);
                float k = Mathf.InverseLerp(tl.position.x, tr.position.x, right);
                UIVertex ctr = Lerp(tl, tr, k), cbr = Lerp(bl, br, k);

                // The stroke: a bright line round the shape (copies set round it), thinning as the ink comes in.
                float o = Mathf.Lerp(size * 0.028f, size * 0.009f, ink);
                Color32 line = new Color(1f, 1f, 1f, fade * Mathf.Lerp(1f, 0.85f, ink));
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * Mathf.PI * 0.25f;
                    Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * o;
                    Quad(Moved(tl, d, Place, line), Moved(ctr, d, Place, line), Moved(cbr, d, Place, line), Moved(bl, d, Place, line));
                }
                // The face: dark until the ink floods it white.
                Color face = Color.Lerp(new Color(0.02f, 0.02f, 0.025f, 1f), Color.white, ink);
                face.a *= fade;
                Quad(Moved(tl, Vector3.zero, Place, face), Moved(ctr, Vector3.zero, Place, face), Moved(cbr, Vector3.zero, Place, face), Moved(bl, Vector3.zero, Place, face));
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }

        static UIVertex Lerp(UIVertex a, UIVertex b, float k)
        {
            UIVertex v = a;
            v.position = Vector3.Lerp(a.position, b.position, k);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, k);
            return v;
        }

        static UIVertex Moved(UIVertex v, Vector3 by, System.Func<Vector3, Vector3> place, Color32 c)
        {
            v.position = place(v.position) + by;
            v.color = c;
            return v;
        }

        void Quad(UIVertex a, UIVertex b, UIVertex c, UIVertex d)
        {
            output.Add(a); output.Add(b); output.Add(c);
            output.Add(c); output.Add(d); output.Add(a);
        }
    }
}
