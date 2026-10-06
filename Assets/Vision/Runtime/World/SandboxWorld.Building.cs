using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Visibility;
using BF = Vision.World.BuildingPlan.Furn;

namespace Vision.World
{
    /// <summary>Builds the central building from <see cref="MapLayout.Plan"/>: floors, walls, openings, the gate and yard, furniture and lamps.</summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>The building's exit gate (opens when every generator runs).</summary>
        public ExitGate Gate { get; private set; }
        public float BuildingFloor { get; private set; }

        static readonly Color[] WallPaints = { LowPolyModels.Bldg.Paint, LowPolyModels.Bldg.Paint, LowPolyModels.Bldg.PaintCream, LowPolyModels.Bldg.PaintGreen, LowPolyModels.Bldg.PaintBlue };

        void BuildBuilding()
        {
            BuildingPlan plan = Layout.Plan;
            if (plan == null) return;
            Transform root = Group("Building");
            float floor = H(plan.Bounds.center);
            BuildingFloor = floor;
            BuildFloor(plan, root, floor);

            // Wall runs, stretched to meet at corners (but never into a doorway).
            var ends = new List<Vector2>();
            foreach (BuildingPlan.Opening o in plan.Openings) { ends.Add(o.A); ends.Add(o.B); }
            bool AtOpening(Vector2 p) { foreach (Vector2 e in ends) if ((e - p).sqrMagnitude < 1e-4f) return true; return false; }
            foreach (BuildingPlan.WallRun w in plan.Walls)
            {
                float t = w.Thickness;
                Vector2 d = (w.B - w.A).normalized;
                Vector2 a = AtOpening(w.A) ? w.A : w.A - d * t * 0.5f, b = AtOpening(w.B) ? w.B : w.B + d * t * 0.5f;
                float len = Vector2.Distance(a, b);
                Color paint = WallPaints[Mathf.Abs(Mathf.RoundToInt(w.A.x * 3.1f + w.A.y * 7.7f)) % WallPaints.Length];
                Mesh m = w.Exterior
                    ? LowPolyModels.ExteriorWall(rng, len, BuildingPlan.WallHeight, t, OutsideSign(plan, w.A, w.B), paint)
                    : LowPolyModels.InteriorWall(rng, len, BuildingPlan.WallHeight, t, paint);
                Wall(w.Exterior ? "Outer Wall" : "Wall", a, b, BuildingPlan.WallHeight, t, m, true, true, floor - 0.02f);
            }

            foreach (BuildingPlan.Opening o in plan.Openings) BuildOpening(plan, o, root, floor);
            foreach (BuildingPlan.Barricade b in plan.Barricades) BuildBarricade(b, root, floor);
            BuildGate(plan, root, floor);
            BuildYard(root, floor);
            foreach (BuildingPlan.Item item in plan.Items) BuildItem(item, root, floor);
            foreach (BuildingPlan.Lamp lamp in plan.Lamps) BuildLamp(lamp, root, floor);
        }

        /// <summary>+1 when the wall mesh's +Z side faces out of the building.</summary>
        static float OutsideSign(BuildingPlan plan, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized, mid = (a + b) * 0.5f;
            Vector2 localZ = new Vector2(-d.y, d.x);
            return plan.RoomAt(mid + localZ * 0.4f) < 0 ? 1f : -1f;
        }

        static float Yaw(Vector2 along) => -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;

        // ---------------------------------------------------------------- floors

        void BuildFloor(BuildingPlan plan, Transform root, float floor)
        {
            var b = new LowPolyMeshBuilder(rng);
            float y = floor + 0.015f;
            foreach (BuildingPlan.Room r in plan.Rooms)
            {
                float tile;
                Color c0, c1;
                switch (r.Type)
                {
                    case BuildingPlan.RoomType.Restroom:
                    case BuildingPlan.RoomType.LockerRoom:
                        tile = 0.5f; c0 = new Color(0.45f, 0.45f, 0.42f); c1 = new Color(0.28f, 0.32f, 0.30f); break;
                    case BuildingPlan.RoomType.BreakRoom:
                        tile = 0.5f; c0 = new Color(0.46f, 0.42f, 0.33f); c1 = new Color(0.30f, 0.20f, 0.17f); break;
                    case BuildingPlan.RoomType.Office:
                    case BuildingPlan.RoomType.ServerRoom:
                        tile = 1f; c0 = new Color(0.22f, 0.22f, 0.25f); c1 = c0; break;
                    case BuildingPlan.RoomType.Hallway:
                        tile = 0.6f; c0 = new Color(0.36f, 0.35f, 0.31f); c1 = new Color(0.33f, 0.32f, 0.28f); break;
                    case BuildingPlan.RoomType.StudioSet:
                        tile = 1.5f; c0 = new Color(0.12f, 0.12f, 0.12f); c1 = c0; break;
                    default:
                        tile = 1.5f; c0 = new Color(0.31f, 0.31f, 0.30f); c1 = c0; break;
                }
                Rect a = r.Area;
                int nx = Mathf.Max(1, Mathf.RoundToInt(a.width / tile)), nz = Mathf.Max(1, Mathf.RoundToInt(a.height / tile));
                float sx = a.width / nx, sz = a.height / nz;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        float x0 = a.xMin + i * sx, z0 = a.yMin + j * sz;
                        Color c = b.Jitter((i + j) % 2 == 0 ? c0 : c1, 0.06f);
                        if (Range(0f, 1f) < 0.08f) c *= 0.82f;
                        var p00 = new Vector3(x0, y, z0);
                        var p01 = new Vector3(x0, y, z0 + sz);
                        var p11 = new Vector3(x0 + sx, y, z0 + sz);
                        var p10 = new Vector3(x0 + sx, y, z0);
                        b.AddTriangle(p00, p01, p11, c);
                        b.AddTriangle(p00, p11, p10, c);
                    }
            }
            MakeStatic("Building Floor", b.ToMesh("Building Floor"), Vector3.zero, Quaternion.identity, lowPolyMaterial).transform.SetParent(root, false);
            foreach (Transform t in root) t.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- openings

        void BuildOpening(BuildingPlan plan, BuildingPlan.Opening o, Transform root, float floor)
        {
            BuildingPlan.Interface f = plan.Interfaces[o.Interface];
            float t = f.Thickness, width = o.Width;
            Vector2 along = (o.B - o.A).normalized, mid = o.Centre;
            Color paint = LowPolyModels.Bldg.Paint;
            switch (o.Kind)
            {
                case BuildingPlan.OpeningKind.Door:
                    MakeDoor(o.A, along, width, BuildingPlan.DoorHeight - 0.02f, false, floor);
                    if (o.StartsOpen) Doors[Doors.Count - 1].SetOpen(true);
                    Header(mid, along, width, BuildingPlan.DoorHeight, t, paint, root, floor);
                    break;
                case BuildingPlan.OpeningKind.Doorway:
                    Header(mid, along, width, BuildingPlan.DoorHeight + 0.1f, t, paint, root, floor);
                    break;
                case BuildingPlan.OpeningKind.Window:
                case BuildingPlan.OpeningKind.BoardedWindow:
                {
                    // A sill wall (stops you, not your sight), the dirty pane, a header; boarded windows block sight too.
                    Mesh sill = LowPolyModels.InteriorWall(rng, width, 0.9f, t, LowPolyModels.Bldg.Block);
                    Wall("Window Sill", o.A, o.B, 0.9f, t, sill, false, true, floor - 0.02f);
                    GameObject pane = Piece("Window", LowPolyModels.WindowPane(width), root, new Vector3(o.A.x, floor, o.A.y), Yaw(along));
                    AddBox(pane, new Vector3(width * 0.5f, 1.5f, 0f), new Vector3(width, 1.2f, 0.1f));
                    Header(mid, along, width, 2.05f, t, paint, root, floor);
                    if (o.Kind == BuildingPlan.OpeningKind.BoardedWindow)
                    {
                        Vector2 outside = f.Exterior ? f.Normal : Vector2.zero;
                        GameObject boards = Piece("Boarded Window", LowPolyModels.Boards(rng, width), root,
                            new Vector3(o.A.x + outside.x * (t * 0.5f - 0.05f), floor, o.A.y + outside.y * (t * 0.5f - 0.05f)), Yaw(along));
                        var occ = boards.AddComponent<Occluder>();
                        occ.shape = Occluder.Shape.Box;
                        occ.size = new Vector2(width, 0.1f);
                        occ.offset = new Vector2(width * 0.5f, 0f);
                    }
                    break;
                }
            }
        }

        void Header(Vector2 mid, Vector2 along, float width, float from, float thickness, Color paint, Transform root, float floor) =>
            Piece("Header", LowPolyModels.WallHeader(width, from, BuildingPlan.WallHeight, thickness, paint), root, new Vector3(mid.x, floor - 0.02f, mid.y), Yaw(along));

        void BuildBarricade(BuildingPlan.Barricade bar, Transform root, float floor)
        {
            Vector2 along = (bar.B - bar.A).normalized;
            float length = Vector2.Distance(bar.A, bar.B) + 0.2f;
            var go = new GameObject("Barricade");
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(bar.A.x - along.x * 0.1f, floor, bar.A.y - along.y * 0.1f), Quaternion.Euler(0f, Yaw(along), 0f));
            // Local +Z is (-along.y, along.x) on the map: stand on the requested side of the wall.
            float side = Vector2.Dot(new Vector2(-along.y, along.x), bar.Side) >= 0f ? 1f : -1f;
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(go.transform, false);
            hinge.SetLocalPositionAndRotation(new Vector3(0f, 0f, side * 0.24f), Quaternion.Euler(0f, 0f, 90f));
            var pallet = new GameObject("Pallet");
            pallet.transform.SetParent(hinge, false);
            pallet.transform.SetLocalPositionAndRotation(new Vector3(length * 0.5f, 0.45f, -0.06f), Quaternion.Euler(90f, 0f, 0f));
            pallet.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Pallet(rng, length, 0.9f);
            PropFactory.NoShadows(pallet.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            BoxCollider blocker = AddBox(go, new Vector3(length * 0.5f, 0.6f, side * 0.24f), new Vector3(length, 1.2f, 0.25f));
            blocker.enabled = false;
            var barricade = go.AddComponent<Barricade>();
            barricade.hinge = hinge;
            barricade.blocker = blocker;
            go.SetActive(true);
        }

        // ---------------------------------------------------------------- the gate and its yard

        void BuildGate(BuildingPlan plan, Transform root, float floor)
        {
            if (plan.GateRoom < 0) return;
            float w = BuildingPlan.GateWidth, top = BuildingPlan.WallHeight, h = 2.4f;
            var at = new Vector3(plan.GateX - w * 0.5f, floor, plan.Bounds.yMax);
            var go = new GameObject("Exit Gate");
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            Piece("Gate Frame", LowPolyModels.RollUpFrame(w, h, top), go.transform, Vector3.zero, 0f);
            GameObject door = Piece("Roll-up Door", LowPolyModels.RollUpDoor(rng, w, h), go.transform, Vector3.zero, 0f);
            door.isStatic = false;
            BoxCollider blocker = AddBox(go, new Vector3(w * 0.5f, 1.2f, 0f), new Vector3(w, 2.4f, 0.3f));
            var occ = go.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Box;
            occ.size = new Vector2(w, 0.1f);
            occ.offset = new Vector2(w * 0.5f, 0f);

            // The lever on the inside of the wall, east of the gate.
            GameObject box = Piece("Gate Lever", LowPolyModels.LeverBox(), root, new Vector3(plan.Lever.x, floor, plan.Bounds.yMax - BuildingPlan.ExteriorThickness * 0.5f), 180f);
            var handle = new GameObject("Handle").transform;
            handle.SetParent(box.transform, false);
            handle.localPosition = new Vector3(0f, 1.17f, 0.17f);
            var hm = handle.gameObject;
            hm.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.LeverHandle();
            PropFactory.NoShadows(hm.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;

            Gate = go.AddComponent<ExitGate>();
            Gate.door = door.transform;
            Gate.leverHandle = handle;
            Gate.lever = box.transform;
            Gate.blocker = blocker;
            Gate.occluder = occ;
            Gate.height = h;
            go.SetActive(true);
        }

        void BuildYard(Transform root, float floor)
        {
            Rect y = Layout.Yard;
            var corners = new[] { new Vector2(y.xMin, y.yMin), new Vector2(y.xMin, y.yMax), new Vector2(y.xMax, y.yMax), new Vector2(y.xMax, y.yMin) };
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = corners[i], b = corners[i + 1];
                float len = Vector2.Distance(a, b);
                GameObject fence = Piece("Yard Fence", LowPolyModels.ChainLink(rng, len), root, new Vector3(a.x, Mathf.Min(H(a), H(b)) - 0.05f, a.y), Yaw(b - a));
                AddBox(fence, new Vector3(len * 0.5f, 1.1f, 0f), new Vector3(len, 2.2f, 0.12f));
            }
        }

        // ---------------------------------------------------------------- furniture and fittings

        static float SolidHeight(BF kind) => kind switch
        {
            BF.Desk or BF.Table => 0.76f,
            BF.FilingCabinet or BF.Transformer => 1.35f,
            BF.Shelf or BF.Locker or BF.LockerBank or BF.Wardrobe or BF.Vending or BF.Stall => 1.9f,
            BF.Rack or BF.ServerRack or BF.Forklift => 2.1f,
            BF.Boiler or BF.SetFlat => 2.4f,
            BF.Fridge => 1.8f,
            BF.CrateStack => 1.55f,
            BF.Barrel or BF.Counter or BF.Sink or BF.Workbench => 0.92f,
            BF.Bench => 0.45f,
            BF.Bed or BF.PalletStack or BF.Boxes => 0.6f,
            BF.Toilet => 0.75f,
            BF.Sofa or BF.Crate or BF.CableSpool => 0.85f,
            _ => 1f,
        };

        void BuildItem(BuildingPlan.Item item, Transform root, float floor)
        {
            var pos = new Vector3(item.Position.x, floor + 0.015f + item.Elevation, item.Position.y);
            if (item.Kind == BF.Generator)
            {
                GameObject gen = Prop(library != null ? library.generator : null, root,
                    () => PropFactory.CreateGenerator(LowPolyModels.Generator(rng), lowPolyMaterial));
                gen.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, item.Yaw, 0f));
                MakeObjective(gen);
                Generators.Add(gen.transform);
                return;
            }
            if (item.Kind == BF.Mannequin)
            {
                var figure = new GameObject("Mannequin");
                figure.transform.SetParent(root, false);
                figure.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(Range(-4f, 4f), item.Yaw + Range(-40f, 40f), Range(-3f, 3f)));
                figure.isStatic = true;
                figure.AddComponent<MeshFilter>().sharedMesh = library != null && library.player != null
                    ? library.player.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh
                    : MannequinBuilder.Build();
                PropFactory.NoShadows(figure.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                return;
            }

            Vector2 front = new Vector2(Mathf.Sin(item.Yaw * Mathf.Deg2Rad), Mathf.Cos(item.Yaw * Mathf.Deg2Rad));
            bool mounted = item.Elevation > 0f && item.Kind != BF.Pipes && item.Kind != BF.Duct && item.Kind != BF.Cobweb;
            // Wall fittings sit on the wall face behind their strip; runs of pipe start at their given point.
            if (mounted) pos -= new Vector3(front.x, 0f, front.y) * 0.15f;
            Mesh mesh = LowPolyModels.BuildingItem(item.Kind, rng, item.Size, item.Variant);
            GameObject go = Piece(item.Kind.ToString(), mesh, root, pos, item.Yaw);
            if (item.Kind == BF.Stain || item.Kind == BF.Papers || item.Kind == BF.Pallet) go.transform.localPosition += Vector3.up * 0.002f;
            if (!item.Solid) return;

            float height = SolidHeight(item.Kind);
            AddBox(go, new Vector3(0f, height * 0.5f, 0f), new Vector3(item.Size.x, height, item.Size.y));
            if (item.Tall)
            {
                var occ = go.AddComponent<Occluder>();
                occ.shape = Occluder.Shape.Box;
                occ.size = item.Size;
            }
            if (item.Hide)
            {
                HidingSpot.Kind kind = item.Kind switch
                {
                    BF.Barrel => HidingSpot.Kind.Barrel,
                    BF.Bed => HidingSpot.Kind.Bed,
                    BF.Wardrobe => HidingSpot.Kind.Wardrobe,
                    _ => HidingSpot.Kind.Locker,
                };
                AddHiding(go, kind, kind == HidingSpot.Kind.Bed ? 1.3f : 1.0f, new Vector3(0f, 0f, item.Size.y * 0.5f + 0.45f));
            }
        }

        void BuildLamp(BuildingPlan.Lamp lamp, Transform root, float floor)
        {
            const float ceiling = 2.55f;
            var go = new GameObject(lamp.Working ? "Lamp" : "Dead Lamp");
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(lamp.Position.x, floor, lamp.Position.y);
            Material glow = lamp.Working ? glowMaterial : lowPolyMaterial;
            float range = BuildingPlan.LampRange, intensity = 0.75f;
            switch (lamp.Kind)
            {
                case BuildingPlan.LampKind.Fluorescent:
                {
                    float yaw = Range(0f, 1f) < 0.5f ? 0f : 90f;
                    bool hanging = !lamp.Working && Range(0f, 1f) < 0.4f;
                    Piece("Fixture", LowPolyModels.FluorescentHousing(rng, hanging), go.transform, Vector3.up * ceiling, yaw);
                    if (!hanging) Piece("Tubes", LowPolyModels.FluorescentTubes(), go.transform, Vector3.up * ceiling, yaw, glow);
                    break;
                }
                case BuildingPlan.LampKind.Bulb:
                    Piece("Bulb", LowPolyModels.HangingBulb(rng, 0.5f), go.transform, Vector3.up * ceiling, 0f, glow);
                    range = 8f;
                    intensity = 0.7f;
                    break;
                case BuildingPlan.LampKind.Desk:
                    Piece("Desk Lamp", LowPolyModels.DeskLamp(), go.transform, Vector3.up * 0.765f, lamp.Yaw);
                    Piece("Bulb", LowPolyModels.DeskLampBulb(), go.transform, Vector3.up * 0.765f, lamp.Yaw, glow);
                    range = 5f;
                    intensity = 0.6f;
                    break;
                case BuildingPlan.LampKind.Exit:
                    Piece("Exit Sign", LowPolyModels.ExitSign(), go.transform, Vector3.up * lamp.Elevation, lamp.Yaw);
                    Piece("Exit Face", LowPolyModels.ExitSignGlow(), go.transform, Vector3.up * lamp.Elevation, lamp.Yaw, glow);
                    range = 3.5f;
                    intensity = 0.35f;
                    break;
                default:
                    Piece("Glow", LowPolyModels.LampGlow(lamp.Kind, lamp.Size), go.transform, Vector3.up * 0.015f, lamp.Yaw, glow);
                    (range, intensity) = lamp.Kind switch
                    {
                        BuildingPlan.LampKind.Vending => (5f, 0.5f),
                        BuildingPlan.LampKind.Furnace => (6f, 0.65f),
                        BuildingPlan.LampKind.Server => (4f, 0.4f),
                        _ => (9f, 0.8f),
                    };
                    break;
            }
            if (lamp.Working)
            {
                var light = go.AddComponent<VisionLight>();
                light.range = range;
                light.intensity = intensity;
                light.flickerAmount = lamp.Flicker;
                light.height = Mathf.Clamp(lamp.Elevation - 0.7f, 0.3f, ceiling - 0.7f);
            }
            go.SetActive(true);
        }
    }
}
