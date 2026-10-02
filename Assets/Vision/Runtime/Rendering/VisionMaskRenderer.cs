using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
    ///
    /// The mask lives in world space rather than screen space because the camera is pitched and the
    /// world has height: every shader samples it at a fragment's world X,Z.
    /// Publishes _VisionMask, _VisionMaskRect and _VisionEntityThreshold as shader globals.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class VisionMaskRenderer : MonoBehaviour
    {
        public Camera viewCamera;
        public VisionViewer viewer;
        public Shader maskShader;
        public Shader blurShader;

        [Header("Mask")]
        [Tooltip("Side of the square of ground covered by the mask, in metres.")]
        public float worldSize = 48f;
        [Tooltip("Mask texels per screen pixel along the visible ground (0.5 = half resolution).")]
        [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
        [Range(0, 4)] public int blurIterations = 2;
        [Range(0.5f, 4f)] public float blurRadius = 1.5f;
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
        static readonly int BlurStepId = Shader.PropertyToID("_VisionBlurStep");

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
        readonly List<int> indices = new List<int>(16384);
        readonly List<VisionLight> lightOrder = new List<VisionLight>(32);

        // Debug: last polygons as line strips for the overlay.
        readonly List<List<Vector2>> debugPolygons = new List<List<Vector2>>();
        int debugCount;

        public VisibilityComputer Computer => computer ??= new VisibilityComputer(VisionWorld.Occluders);
        public RenderTexture MaskTexture => maskTexture;
        public Rect MaskRect { get; private set; }
        public int LastPolygonCount { get; private set; }
        public int LastLightPolygonCount { get; private set; }
        public int LastRayCount { get; private set; }

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

            int resolution = ComputeResolution();
            EnsureTextures(resolution);

            // Centre on the camera's ground focus, snapped to whole texels so the mask doesn't shimmer.
            Vector2 focus = GroundFocus(viewCamera);
            float texel = worldSize / resolution;
            focus.x = Mathf.Round(focus.x / texel) * texel;
            focus.y = Mathf.Round(focus.y / texel) * texel;
            Vector2 min = focus - Vector2.one * (worldSize * 0.5f);
            MaskRect = new Rect(min, Vector2.one * worldSize);
            var rect = new Vector4(min.x, min.y, 1f / worldSize, 1f / worldSize);
            Shader.SetGlobalVector(MaskRectId, rect);
            Shader.SetGlobalFloat(EntityThresholdId, entityThreshold);

            BuildPolygons();

            cmd.Clear();
            cmd.SetGlobalVector(MaskRectId, rect);
            cmd.SetRenderTarget(maskTexture);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.DrawMesh(mesh, Matrix4x4.identity, maskMaterial, 0, 0);

            for (int i = 0; i < blurIterations; i++)
            {
                float step = blurRadius / resolution;
                cmd.SetGlobalTexture(BlurSourceId, maskTexture);
                cmd.SetGlobalVector(BlurStepId, new Vector4(step, 0f, 0f, 0f));
                cmd.SetRenderTarget(blurTexture);
                cmd.DrawProcedural(Matrix4x4.identity, blurMaterial, 0, MeshTopology.Triangles, 3);
                cmd.SetGlobalTexture(BlurSourceId, blurTexture);
                cmd.SetGlobalVector(BlurStepId, new Vector4(0f, step, 0f, 0f));
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
            int res = Mathf.CeilToInt(worldSize * texelsPerMetre / 64f) * 64;
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
        public static Vector2 GroundFocus(Camera cam)
        {
            Transform t = cam.transform;
            Vector3 fwd = t.forward;
            float dist = Mathf.Abs(fwd.y) > 1e-4f ? -t.position.y / fwd.y : 0f;
            return VisionWorld.ToPlane(t.position + fwd * dist);
        }

        void BuildPolygons()
        {
            vertices.Clear();
            colors.Clear();
            uv0.Clear();
            uv1.Clear();
            indices.Clear();
            debugCount = 0;
            int polygons = 0, rays = 0;

            VisibilityComputer vc = Computer;
            Vector2 origin = viewer.PlanePosition;
            float dir = viewer.FacingAngle;
            float halfSize = worldSize * 0.5f;
            var blue = new Color(0f, 0f, 1f, 1f);

            // B: cone (capped near the screen edge), proximity circle, optional see-through cone.
            float k = viewer.Scale;
            float coneRange = Mathf.Min(viewer.coneRange * k, halfSize * 0.95f);
            vc.Compute(ViewQuery.Cone(origin, dir, viewer.coneHalfAngleDeg * Mathf.Deg2Rad, coneRange), polygon);
            AddPolygon(polygon, false, origin, blue, coneRange, viewer.coneFalloffStart);
            rays += vc.LastRayCount; polygons++;

            float proximity = viewer.proximityRadius * k;
            vc.Compute(ViewQuery.Circle(origin, proximity), polygon);
            AddPolygon(polygon, true, origin, blue, proximity, viewer.proximityFalloffStart);
            rays += vc.LastRayCount; polygons++;

            if (viewer.seeThroughEnabled)
            {
                float seeThrough = viewer.seeThroughRange * k;
                vc.Compute(ViewQuery.Cone(origin, dir, viewer.seeThroughHalfAngleDeg * Mathf.Deg2Rad, seeThrough, false), polygon);
                AddPolygon(polygon, false, origin, blue * viewer.seeThroughStrength, seeThrough, 0.6f);
                rays += vc.LastRayCount; polygons++;
            }

            // G: long-range 360° line of sight. No falloff (falloffStart >= 1 disables it).
            float losRange = Mathf.Min(viewer.lineOfSightRange * k, halfSize * 1.42f);
            vc.Compute(ViewQuery.Circle(origin, losRange), polygon);
            AddPolygon(polygon, true, origin, new Color(0f, 1f, 0f, 1f), losRange, 2f);
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
                AddPolygon(poly, true, light.PlanePosition, new Color(light.CurrentIntensity, 0f, 0f, 1f), light.WorldRange, 0.25f);
                polygons++;
            }

            LastPolygonCount = polygons;
            LastLightPolygonCount = lightCount;
            LastRayCount = rays;

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
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
        }

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
