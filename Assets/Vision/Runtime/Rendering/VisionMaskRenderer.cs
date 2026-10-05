using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vision.Characters;
using Vision.Visibility;

namespace Vision.Rendering
{
    /// <summary>
    /// Builds every visibility polygon each frame and draws them into a world-space mask texture
    /// (a top-down square of the ground plane around the camera focus), then blurs it for soft edges.
    ///
    /// Channels:  B = viewer's own light (cone, proximity, see-through cone), with distance falloff.
    ///            G = 360° line of sight (no falloff, lights nothing by itself).
    ///            R = light sources, with distance falloff.
    ///            A = soft character shadows cast away from the flashlight (cosmetic: they only darken
    ///                already-lit ground). Light sources cast no character shadows.
    ///
    /// The mask lives in world space rather than screen space because the camera is pitched and the
    /// world has height: every shader samples it at a fragment's world X,Z.
    /// Publishes _VisionMask, _VisionMaskRect, _VisionEntityThreshold and _VisionViewerPos as shader globals.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class VisionMaskRenderer : MonoBehaviour
    {
        public Camera viewCamera;
        public VisionViewer viewer;
        public Shader maskShader;
        public Shader blurShader;

        [Header("Mask")]
        [Tooltip("Minimum side of the square of ground covered by the mask, in metres. It grows to cover a larger view; the cone and line-of-sight caps stay tied to this value.")]
        public float worldSize = 48f;

        /// <summary>Side of the mask square this frame: at least worldSize, and enough to cover the whole view.</summary>
        public float CoverSize { get; private set; } = 48f;
        [Tooltip("Mask texels per screen pixel along the visible ground (0.5 = half resolution).")]
        [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
        [Range(0, 4)] public int blurIterations = 3;
        [Range(0.5f, 4f)] public float blurRadius = 2.5f;
        [Header("Penumbra (shadow edges soften away from the light)")]
        [Tooltip("Width of the shadow edge's blur per world unit of distance from the light casting it.")]
        [Range(0f, 0.2f)] public float penumbraGrowth = 0.055f;
        [Tooltip("Widest shadow-edge blur in world units (keeps light from seeping through walls).")]
        [Range(0f, 3f)] public float maxPenumbra = 1.1f;
        [Tooltip("Light sources that also cast soft character shadows (nearest first).")]
        [Range(0, 6)] public int shadowLights = 4;
        [Tooltip("Only this many nearest light sources get a polygon each frame.")]
        public int maxLights = 6;


        [Header("Entities")]
        [Tooltip("Hard cut-off on channel B for dynamic objects.")]
        [Range(0.01f, 0.9f)] public float entityThreshold = 0.3f;

        [Header("Debug")]
        public bool drawPolygons;

        public static readonly int MaskId = Shader.PropertyToID("_VisionMask");
        public static readonly int MaskRectId = Shader.PropertyToID("_VisionMaskRect");
        public static readonly int EntityThresholdId = Shader.PropertyToID("_VisionEntityThreshold");
        static readonly int BlurSourceId = Shader.PropertyToID("_VisionBlurSource");
        public static readonly int ViewerPosId = Shader.PropertyToID("_VisionViewerPos");
        static readonly int BlurStepId = Shader.PropertyToID("_VisionBlurStep");
        static readonly int BlurPenumbraId = Shader.PropertyToID("_VisionBlurPenumbra");
        static readonly int BlurOriginsId = Shader.PropertyToID("_VisionBlurOrigins");
        const int MaxBlurLights = 8;
        readonly Vector4[] blurOrigins = new Vector4[MaxBlurLights + 1];

        VisibilityComputer computer;
        Material maskMaterial;
        Material blurMaterial;
        RenderTexture maskTexture;
        RenderTexture blurTexture;
        CommandBuffer cmd;
        Mesh mesh;

        readonly List<Vector2> polygon = new List<Vector2>(1024);
        readonly List<Vector3> vertices = new List<Vector3>(8192);
        readonly List<Color> colors = new List<Color>(8192);
        readonly List<Vector2> uv0 = new List<Vector2>(8192);
        readonly List<Vector2> uv1 = new List<Vector2>(8192);
        readonly List<Vector4> uv2 = new List<Vector4>(8192);
        readonly List<int> indices = new List<int>(16384);
        readonly List<VisionLight> lightOrder = new List<VisionLight>(32);
        readonly List<Vector2> conePolygon = new List<Vector2>(1024);
        readonly List<Vector2> proximityPolygon = new List<Vector2>(256);
        readonly List<Vector2> seeThroughPolygon = new List<Vector2>(256);
        readonly List<Vector2> shadowPolygon = new List<Vector2>(8);

        // Debug: last polygons as line strips for the overlay.
        readonly List<List<Vector2>> debugPolygons = new List<List<Vector2>>();
        int debugCount;

        public VisibilityComputer Computer => computer ??= new VisibilityComputer(VisionWorld.Occluders);
        public RenderTexture MaskTexture => maskTexture;
        public Rect MaskRect { get; private set; }
        public int LastPolygonCount { get; private set; }
        public int LastLightPolygonCount { get; private set; }
        public int LastRayCount { get; private set; }
        public int LastShadowCount { get; private set; }

        void OnEnable()
        {
            maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
            blurMaterial = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            cmd = new CommandBuffer { name = "Vision Mask" };
            mesh = new Mesh { name = "Vision Polygons", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            RenderPipelineManager.endCameraRendering += DrawDebug;
        }

        void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= DrawDebug;
            Release(ref maskTexture);
            Release(ref blurTexture);
            cmd?.Release();
            cmd = null;
            DestroyImmediate(maskMaterial);
            DestroyImmediate(blurMaterial);
            DestroyImmediate(mesh);
        }

        static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            DestroyImmediate(rt);
            rt = null;
        }

        void LateUpdate()
        {
            if (viewCamera == null || viewer == null) return;

            CoverSize = CoverSizeFor(viewCamera, worldSize);
            int resolution = ComputeResolution();
            EnsureTextures(resolution);

            // Centre on the camera's ground focus, snapped to whole texels so the mask doesn't shimmer.
            Vector2 focus = GroundFocus(viewCamera, viewer.transform.position.y);
            float size = CoverSize;
            float texel = size / resolution;
            focus.x = Mathf.Round(focus.x / texel) * texel;
            focus.y = Mathf.Round(focus.y / texel) * texel;
            Vector2 min = focus - Vector2.one * (size * 0.5f);
            MaskRect = new Rect(min, Vector2.one * size);
            var rect = new Vector4(min.x, min.y, 1f / size, 1f / size);
            Shader.SetGlobalVector(MaskRectId, rect);
            Shader.SetGlobalFloat(EntityThresholdId, entityThreshold);

            BuildPolygons();

            cmd.Clear();
            cmd.SetGlobalVector(MaskRectId, rect);
            cmd.SetRenderTarget(maskTexture);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.DrawMesh(mesh, Matrix4x4.identity, maskMaterial, 0, 0);

            // Penumbra: the blur widens with distance from the viewer (B, G, A) and from the nearest light source (R).
            int blurLights = Mathf.Min(MaxBlurLights, Mathf.Min(lightOrder.Count, maxLights));
            blurOrigins[0] = origin;
            for (int i = 0; i < blurLights; i++) blurOrigins[i + 1] = lightOrder[i].PlanePosition;
            cmd.SetGlobalVectorArray(BlurOriginsId, blurOrigins);
            float texelWorld = size / resolution;
            float tapsPerRadius = 1f / (4f * Mathf.Sqrt(Mathf.Max(1, blurIterations)));
            cmd.SetGlobalVector(BlurPenumbraId, new Vector4(penumbraGrowth, maxPenumbra, tapsPerRadius, blurLights));
            for (int i = 0; i < blurIterations; i++)
            {
                float step = 1f / resolution;
                cmd.SetGlobalTexture(BlurSourceId, maskTexture);
                cmd.SetGlobalVector(BlurStepId, new Vector4(step, 0f, blurRadius, texelWorld));
                cmd.SetRenderTarget(blurTexture);
                cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3);
                cmd.SetGlobalTexture(BlurSourceId, blurTexture);
                cmd.SetGlobalVector(BlurStepId, new Vector4(0f, step, blurRadius, texelWorld));
                cmd.SetRenderTarget(maskTexture);
                cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            Shader.SetGlobalTexture(MaskId, maskTexture);
        }

        int ComputeResolution()
        {
            // Visible ground height ~ 2 * orthoSize / sin(pitch). Match resolutionScale texels per pixel.
            float pitch = Mathf.Max(10f, viewCamera.transform.eulerAngles.x) * Mathf.Deg2Rad;
            float visibleHeight = 2f * viewCamera.orthographicSize / Mathf.Sin(pitch);
            float texelsPerMetre = Screen.height * resolutionScale / Mathf.Max(1f, visibleHeight);
            int res = Mathf.CeilToInt(CoverSize * texelsPerMetre / 64f) * 64;
            return Mathf.Clamp(res, 256, 2048);
        }

        void EnsureTextures(int resolution)
        {
            if (maskTexture != null && maskTexture.width == resolution) return;
            Release(ref maskTexture);
            Release(ref blurTexture);
            maskTexture = CreateMask(resolution, "Vision Mask");
            blurTexture = CreateMask(resolution, "Vision Mask Blur");
        }

        static RenderTexture CreateMask(int resolution, string name)
        {
            var rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        /// <summary>The point on the ground plane at the centre of the camera's view.</summary>
        /// <summary>Ground square that covers everything the orthographic camera sees, plus a margin for tall geometry.</summary>
        public static float CoverSizeFor(Camera cam, float minimum)
        {
            float pitch = Mathf.Max(10f, cam.transform.eulerAngles.x) * Mathf.Deg2Rad;
            float width = 2f * cam.orthographicSize * cam.aspect;
            float depth = 2f * cam.orthographicSize / Mathf.Sin(pitch);
            // The margin also covers hills and hollows: ground 4 m above or below the player shifts by 4 / tan(pitch).
            return Mathf.Max(minimum, Mathf.Max(width, depth) + 8f + 2f * 4f * Vision.World.WorldScale.S / Mathf.Tan(pitch));
        }

        public static Vector2 GroundFocus(Camera cam, float groundY = 0f)
        {
            Transform t = cam.transform;
            Vector3 fwd = t.forward;
            float dist = Mathf.Abs(fwd.y) > 1e-4f ? (groundY - t.position.y) / fwd.y : 0f;
            return VisionWorld.ToPlane(t.position + fwd * dist);
        }

        void BuildPolygons()
        {
            vertices.Clear();
            colors.Clear();
            uv0.Clear();
            uv1.Clear();
            uv2.Clear();
            indices.Clear();
            debugCount = 0;
            int polygons = 0, rays = 0;

            VisibilityComputer vc = Computer;
            Vector2 origin = viewer.PlanePosition;
            float dir = viewer.FacingAngle;
            float halfSize = CoverSize * 0.5f;
            var blue = new Color(0f, 0f, 1f, 0f);

            // B: cone (capped near the screen edge), proximity circle, optional see-through cone.
            float k = viewer.Scale;
            float coneRange, coneStart;
            if (viewer.reachScreenEdge)
            {
                Vector2 axis = new Vector2(Mathf.Cos(dir), Mathf.Sin(dir));
                coneRange = Mathf.Min(BeamReach(viewCamera, origin, axis, viewer.transform.position.y) * EdgeMargin, halfSize * 0.98f);
                coneStart = 2f;   // no distance falloff: full strength to the screen edge
            }
            else
            {
                coneRange = Mathf.Min(viewer.coneRange * k, halfSize * 0.95f);
                coneStart = viewer.coneFalloffStart;
            }
            coneRange *= viewer.visionMultiplier;
            this.origin = origin;
            coneRangeW = coneRange;
            coneStartW = coneStart;
            float halfAngle = viewer.coneHalfAngleDeg * Mathf.Deg2Rad;
            vc.Compute(ViewQuery.Cone(origin, dir, halfAngle, coneRange), polygon);
            // The beam fades toward its sides as well as with distance (soft cone edge).
            beam = new Vector4(Mathf.Cos(dir), Mathf.Sin(dir), halfAngle, viewer.coneEdgeSoftness);
            AddPolygon(polygon, false, origin, blue, coneRange, coneStart);
            beam = Vector4.zero;
            conePolygon.Clear();
            conePolygon.AddRange(polygon);
            rays += vc.LastRayCount; polygons++;

            float proximity = viewer.proximityRadius * k;
            proximityW = proximity;
            vc.Compute(ViewQuery.Circle(origin, proximity), polygon);
            AddPolygon(polygon, true, origin, blue, proximity, viewer.proximityFalloffStart);
            proximityPolygon.Clear();
            proximityPolygon.AddRange(polygon);
            seeThroughPolygon.Clear();
            rays += vc.LastRayCount; polygons++;

            if (viewer.seeThroughEnabled)
            {
                float seeThrough = viewer.seeThroughRange * k;
                seeThroughW = seeThrough;
                vc.Compute(ViewQuery.Cone(origin, dir, viewer.seeThroughHalfAngleDeg * Mathf.Deg2Rad, seeThrough, false), polygon);
                AddPolygon(polygon, false, origin, blue * viewer.seeThroughStrength, seeThrough, 0.6f);
                seeThroughPolygon.AddRange(polygon);
                rays += vc.LastRayCount; polygons++;
            }

            // G: long-range 360° line of sight. No falloff (falloffStart >= 1 disables it).
            float losRange = Mathf.Min(viewer.lineOfSightRange * k * viewer.visionMultiplier, halfSize * 1.42f);
            vc.Compute(ViewQuery.Circle(origin, losRange), polygon);
            AddPolygon(polygon, true, origin, new Color(0f, 1f, 0f, 0f), losRange, 2f);
            rays += vc.LastRayCount; polygons++;

            // R: the nearest light sources, each with its own (cached when static) polygon.
            lightOrder.Clear();
            lightOrder.AddRange(VisionWorld.Lights);
            lightOrder.Sort((a, b) => (a.PlanePosition - origin).sqrMagnitude.CompareTo((b.PlanePosition - origin).sqrMagnitude));
            int lightCount = Mathf.Min(maxLights, lightOrder.Count);
            int version = VisionWorld.Occluders.Version;
            for (int i = 0; i < lightCount; i++)
            {
                VisionLight light = lightOrder[i];
                List<Vector2> poly = light.GetPolygon(vc, version);
                AddPolygon(poly, true, light.PlanePosition, new Color(light.CurrentIntensity, 0f, 0f, 0f), light.WorldRange, 0.25f);
                polygons++;
            }

            // A: soft character shadows, cast away from the flashlight and from the nearest light sources.
            LastShadowCount = 0;
            int castingLights = Mathf.Min(shadowLights, lightCount);
            foreach (CharacterShadow caster in VisionWorld.Casters)
            {
                Vector2 at = caster.PlanePosition;
                bool seen = ViewerLightAt(at) >= entityThreshold;
                bool isViewer = caster.transform.IsChildOf(viewer.transform);
                float cs = caster.Scale;
                if (CastsFlashlightShadow(caster.isEntity, isViewer, Contains(conePolygon, at), seen))
                    AddShadow(at, origin, viewer.lightHeight * k, coneRange, caster.radius * cs, caster.height * cs, caster.strength);
                for (int i = 0; i < castingLights; i++)
                {
                    VisionLight light = lightOrder[i];
                    float range = light.WorldRange;
                    Vector2 lp = light.PlanePosition;
                    if ((at - lp).sqrMagnitude > range * range) continue;
                    if (!CastsLightShadow(caster.isEntity, Contains(light.GetPolygon(vc, version), at), seen)) continue;
                    float height = (light.WorldLightPosition.y - light.transform.position.y);
                    AddShadow(at, lp, height, range, caster.radius * cs, caster.height * cs, caster.strength * light.CurrentIntensity);
                }
            }

            // The player's feet, for the composite's distance blur.
            Vector3 feet = viewer.transform.position;
            Shader.SetGlobalVector(ViewerPosId, new Vector4(feet.x, feet.y, feet.z, 1f));

            LastPolygonCount = polygons;
            LastLightPolygonCount = lightCount;
            LastRayCount = rays;

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }

        /// <summary>
        /// Whether a character casts a flashlight shadow: never the viewer's own body, only inside the beam,
        /// and an entity only while the viewer can see it (a shadow never reveals what is hidden).
        /// </summary>
        public static bool CastsFlashlightShadow(bool isEntity, bool isViewer, bool inBeam, bool viewerSees) =>
            !isViewer && inBeam && (!isEntity || viewerSees);

        /// <summary>
        /// Whether a character casts a shadow away from a campfire or lantern: when that light reaches it, and an
        /// entity only while the viewer can see it. The player's own body does cast these.
        /// </summary>
        public static bool CastsLightShadow(bool isEntity, bool inLight, bool viewerSees) =>
            inLight && (!isEntity || viewerSees);

        /// <summary>Width (world units) of a shadow edge's blur at a distance from the light casting it (matches Hidden/Vision/Blur).</summary>
        public static float PenumbraAt(float distance, float growth, float max) => Mathf.Min(growth * Mathf.Max(0f, distance), max);

        /// <summary>
        /// The viewer's own light (mask channel B, before the blur) at a ground point, as the mask shader
        /// computes it: compared with the entity threshold to tell whether an entity there is visible.
        /// </summary>
        float ViewerLightAt(Vector2 p)
        {
            Vector2 to = p - origin;
            float d = to.magnitude, b = 0f;
            if (Contains(conePolygon, p))
                b = DistanceFalloff(d, coneRangeW, coneStartW)
                    * BeamFalloff(to, new Vector2(Mathf.Cos(viewer.FacingAngle), Mathf.Sin(viewer.FacingAngle)), viewer.coneHalfAngleDeg * Mathf.Deg2Rad, viewer.coneEdgeSoftness);
            if (Contains(proximityPolygon, p)) b = Mathf.Max(b, DistanceFalloff(d, proximityW, viewer.proximityFalloffStart));
            if (seeThroughPolygon.Count > 0 && Contains(seeThroughPolygon, p))
                b = Mathf.Max(b, viewer.seeThroughStrength * DistanceFalloff(d, seeThroughW, 0.6f));
            return b;
        }

        /// <summary>Distance falloff of a polygon (matches Hidden/Vision/Mask).</summary>
        public static float DistanceFalloff(float distance, float range, float falloffStart) =>
            falloffStart >= 1f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range * falloffStart, range, distance));

        /// <summary>The beam ends this far past the screen edge, so its end is never seen.</summary>
        public const float EdgeMargin = 1.06f;

        /// <summary>
        /// Distance from <paramref name="origin"/> along <paramref name="axis"/> (ground plane) to the edge of the
        /// ground the camera sees: the view rectangle centred on the ground focus, its depth stretched by the pitch.
        /// </summary>
        public static float BeamReach(Camera cam, Vector2 origin, Vector2 axis, float groundY = 0f)
        {
            float pitch = Mathf.Max(10f, cam.transform.eulerAngles.x) * Mathf.Deg2Rad;
            Vector2 c = GroundFocus(cam, groundY);
            var half = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize / Mathf.Sin(pitch));
            return RectExit(origin - c, axis.normalized, half);
        }

        /// <summary>Distance from p (inside or near a centred rectangle of half-size h) along unit d to its boundary.</summary>
        public static float RectExit(Vector2 p, Vector2 d, Vector2 h)
        {
            float t = float.MaxValue;
            if (Mathf.Abs(d.x) > 1e-5f) t = Mathf.Min(t, ((d.x > 0f ? h.x : -h.x) - p.x) / d.x);
            if (Mathf.Abs(d.y) > 1e-5f) t = Mathf.Min(t, ((d.y > 0f ? h.y : -h.y) - p.y) / d.y);
            return Mathf.Max(1f, t);
        }

        /// <summary>
        /// Angular falloff of the flashlight beam (matches Hidden/Vision/Mask): full strength on the axis, fading
        /// over the outer <paramref name="softness"/> fraction of the half angle to zero at the cone's side.
        /// </summary>
        public static float BeamFalloff(Vector2 toPoint, Vector2 beamDir, float halfAngle, float softness)
        {
            if (halfAngle <= 0f || toPoint.sqrMagnitude < 1e-8f) return 1f;
            float t = Vector2.Angle(beamDir, toPoint) * Mathf.Deg2Rad / halfAngle;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - Mathf.Max(softness, 1e-3f), 1f, t));
        }

        /// <summary>Even-odd point-in-polygon test.</summary>
        public static bool Contains(List<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// A soft, tapering shadow from a caster standing at <paramref name="at"/>, pointing away from a light at
        /// <paramref name="lightAt"/> (plane position) and <paramref name="lightHeight"/> above the ground.
        /// Length follows the light's height relative to the caster (long when the light is low), capped;
        /// strength fades toward the light's range and along the shadow.
        /// </summary>
        void AddShadow(Vector2 at, Vector2 lightAt, float lightHeight, float lightRange, float radius, float height, float strength)
        {
            if (!ShadowPolygon(at, lightAt, lightHeight, lightRange, radius, height, strength, shadowPolygon, out Vector2 start, out float alpha, out float length)) return;
            AddPolygon(shadowPolygon, true, start, new Color(0f, 0f, 0f, alpha), length, 0.15f);
            LastShadowCount++;
        }

        /// <summary>
        /// Shape of a character shadow on the ground plane: a tapering, slightly widening outline starting just
        /// past the caster's feet and pointing away from the light. Returns false when there is no shadow
        /// (caster outside the light's range, or too faint).
        /// </summary>
        public static bool ShadowPolygon(Vector2 at, Vector2 lightAt, float lightHeight, float lightRange, float radius, float height,
            float strength, List<Vector2> points, out Vector2 start, out float alpha, out float length)
        {
            points.Clear();
            start = at;
            alpha = 0f;
            length = 0f;
            Vector2 d = at - lightAt;
            float dist = d.magnitude;
            if (dist < 1e-3f || dist > lightRange) return false;
            d /= dist;
            var side = new Vector2(-d.y, d.x);
            float len = Mathf.Min(height * dist / Mathf.Max(lightHeight - height * 0.85f, height * 0.25f), height * 1.2f);
            alpha = 0.85f * strength * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lightRange * 0.55f, lightRange, dist)));
            if (alpha <= 0.01f || len <= radius) return false;
            start = at + d * (radius * 0.8f);
            points.Add(start + side * radius);
            points.Add(start + d * (len * 0.5f) + side * (radius * 1.25f));
            points.Add(start + d * len + side * (radius * 1.5f));
            points.Add(start + d * (len * 1.08f));
            points.Add(start + d * len - side * (radius * 1.5f));
            points.Add(start + d * (len * 0.5f) - side * (radius * 1.25f));
            points.Add(start - side * radius);
            length = len * 1.08f;
            return true;
        }

        /// <summary>
        /// Appends a polygon as a triangle fan around its origin. Cones already start with the origin;
        /// full circles are a ring, so an origin vertex is added and the fan is closed.
        /// </summary>
        void AddPolygon(List<Vector2> poly, bool ring, Vector2 origin, Color channel, float range, float falloffStart)
        {
            if (drawPolygons) CopyDebug(poly, ring, origin);
            if (poly.Count < 2) return;
            int baseIndex = vertices.Count;
            var data = new Vector2(range, falloffStart);
            if (ring) AddVertex(origin, channel, origin, data);
            for (int i = 0; i < poly.Count; i++) AddVertex(poly[i], channel, origin, data);

            int n = poly.Count;
            if (ring)
            {
                for (int i = 0; i < n; i++)
                {
                    indices.Add(baseIndex);
                    indices.Add(baseIndex + 1 + i);
                    indices.Add(baseIndex + 1 + (i + 1) % n);
                }
            }
            else
            {
                for (int i = 1; i < n - 1; i++)
                {
                    indices.Add(baseIndex);
                    indices.Add(baseIndex + i);
                    indices.Add(baseIndex + i + 1);
                }
            }
        }

        void AddVertex(Vector2 p, Color c, Vector2 origin, Vector2 data)
        {
            vertices.Add(new Vector3(p.x, 0f, p.y));
            colors.Add(c);
            uv0.Add(origin);
            uv1.Add(data);
            uv2.Add(beam);
        }

        Vector2 origin;
        float coneRangeW, coneStartW, proximityW, seeThroughW;

        /// <summary>Angular falloff for the polygon being added: (beam dir x, dir y, half angle, edge softness); zero = none.</summary>
        Vector4 beam;

        void CopyDebug(List<Vector2> poly, bool ring, Vector2 origin)
        {
            if (debugCount == debugPolygons.Count) debugPolygons.Add(new List<Vector2>(512));
            List<Vector2> copy = debugPolygons[debugCount++];
            copy.Clear();
            if (ring) copy.Add(origin);
            copy.AddRange(poly);
        }

        static Material lineMaterial;

        void DrawDebug(ScriptableRenderContext ctx, Camera cam)
        {
            if (!drawPolygons || cam != viewCamera || debugCount == 0) return;
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            for (int p = 0; p < debugCount; p++)
            {
                List<Vector2> poly = debugPolygons[p];
                GL.Color(Color.HSVToRGB(p * 0.17f % 1f, 0.8f, 1f));
                for (int i = 1; i < poly.Count; i++)
                {
                    Vector2 a = poly[i];
                    Vector2 b = i + 1 < poly.Count ? poly[i + 1] : poly[1];
                    GL.Vertex3(a.x, 0.05f, a.y);
                    GL.Vertex3(b.x, 0.05f, b.y);
                }
            }
            GL.End();
            GL.PopMatrix();
        }
    }
}
