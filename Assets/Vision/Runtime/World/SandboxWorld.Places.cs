using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.World
{
    /// <summary>
    /// Miga Sushi and the Diddy Chamber (<see cref="MapLayout.Sushi"/>, <see cref="MapLayout.Chamber"/>): small buildings
    /// on the outskirts, walled (blocking movement, sight and shots), an open doorway facing in toward the middle, a light
    /// inside. Miga Sushi is a Japanese restaurant (13 by 10 m) built of dark timber posts and beams over pale plaster: a
    /// sushi bar across the back with a conveyor belt of plates, a glass case of fish and soy, ginger and tea at each
    /// wooden stool, the chef's shelf of sake bottles and menu plaques behind it; a tatami room to one side of the
    /// entrance behind shoji screens, with low tables and floor cushions and a scroll on the wall; wooden tables and chairs
    /// on the other; red paper lanterns down the middle and by the door, a beckoning cat and a bonsai on the bar, the noren
    /// curtain and sign over the door; it holds a Hemp Beam and a 0.50 cal. The Diddy Chamber is a tiny dark dungeon of a room with chains on the walls, a big comfy bed and
    /// bottles of baby oil all over the floor. Their names show on screen as a player walks in (GameHud's place banner).
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>The named places on the map, for the banner shown on walking in: (name, area).</summary>
        public readonly List<(string name, Rect area)> Places = new List<(string, Rect)>();

        static readonly Vector2[] DoorOut = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        /// <summary>
        /// Four walls round <paramref name="r"/>, an open doorway <paramref name="doorW"/> wide in the middle of the door side
        /// (plain painted ones, or each run made by <paramref name="make"/> from its length).
        /// </summary>
        void PlaceWalls(Rect r, int doorSide, float doorW, float height, Color color, float y, System.Func<float, Mesh> make = null)
        {
            var corners = new[] { new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin) };
            void Run(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                if (len < 0.05f) return;
                Wall("Wall", a, b, height, 0.3f, make != null ? make(len) : LowPolyModels.PlainWall(rng, len, height, 0.3f, color), true, true, y);
            }
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[(side + 1) % 4];
                if (side != doorSide) { Run(a, b); continue; }
                Vector2 mid = (a + b) * 0.5f, u = (b - a).normalized;
                Run(a, mid - u * doorW * 0.5f);
                Run(mid + u * doorW * 0.5f, b);
            }
        }

        /// <summary>A supply lying inside a building, on its floor.</summary>
        void PlaceSupply(ItemType item, Vector2 at, float y, float yaw, Transform parent)
        {
            var go = new GameObject($"Pickup {item}");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(rng, item);
            PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            var pickup = go.AddComponent<Pickup>();
            pickup.item = item;
            pickup.count = 1;
            go.transform.SetLocalPositionAndRotation(new Vector3(at.x, y, at.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * 1.6f;
            Pickups.Add(pickup);
        }

        /// <summary>The building's floor: a slab over the flattened ground.</summary>
        void PlaceFloor(string name, Rect r, float y, Color color)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-r.width * 0.5f, -0.2f, -r.height * 0.5f), new Vector3(r.width, 0.23f, r.height), color, 0.04f);
            MakeStatic(name, b.ToMesh(name), new Vector3(r.center.x, y, r.center.y), Quaternion.identity, lowPolyMaterial).SetActive(true);
        }

        void BuildSushi()
        {
            Rect r = Layout.Sushi;
            if (r.width <= 0f) return;
            Transform parent = Group("Miga Sushi");
            float y = H(r.center);
            int door = Layout.SushiDoor;
            Vector2 outward = DoorOut[door], inward = -outward, along = new Vector2(-outward.y, outward.x);
            float depth = door % 2 == 0 ? r.height : r.width, width = door % 2 == 0 ? r.width : r.height;
            float yawIn = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg, yawOut = yawIn + 180f, yawAlong = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            Vector2 doorAt = r.center + outward * depth * 0.5f;
            // A point of the room: x across it (from its middle, toward along), z in from the door wall.
            Vector2 P(float x, float z) => doorAt + along * x + inward * z;
            Vector3 At(float x, float z, float up = 0f) { Vector2 q = P(x, z); return new Vector3(q.x, y + up, q.y); }
            float hw = width * 0.5f;

            PlaceFloor("Miga Sushi Floor", r, y, new Color(0.34f, 0.24f, 0.16f));
            PlaceWalls(r, door, 1.6f, 2.9f, Color.white, y, len => LowPolyModels.JapaneseWall(rng, len, 2.9f, 0.3f));
            // Over the door, on the outside: the noren and the sign, a red lantern either side.
            Piece("Noren", LowPolyModels.Noren(rng, 1.6f), parent, new Vector3(doorAt.x, y, doorAt.y), yawOut);
            Piece("Miga Sushi Sign", LowPolyModels.SushiSign(rng, 2.4f), parent, new Vector3(doorAt.x + outward.x * 0.16f, y, doorAt.y + outward.y * 0.16f), yawOut);
            foreach (float sx in new[] { -1.25f, 1.25f })
                Piece("Paper Lantern", LowPolyModels.HangingLantern(rng, 2.6f, new Color(0.86f, 0.14f, 0.1f)), parent, At(sx, -0.3f), yawOut);

            // The sushi bar across the back, open at one end for the chef; the shelf and menu plaques behind it.
            float barFrom = -hw + 0.15f, barTo = hw - 2.05f, barLen = barTo - barFrom, barX = (barFrom + barTo) * 0.5f, barZ = depth - 2.3f;
            GameObject bar = Piece("Sushi Counter", LowPolyModels.SushiBar(rng, barLen), parent, At(barX, barZ), yawOut);
            AddBox(bar, new Vector3(0f, 0.6f, 0f), new Vector3(barLen, 1.2f, 0.85f));
            for (float x = barFrom + 0.45f; x < barTo - 0.3f; x += 0.9f)
                Piece("Stool", LowPolyModels.WoodStool(rng), parent, At(x, barZ - 0.85f), Range(0f, 360f));
            GameObject shelf = Piece("Sake Shelf", LowPolyModels.SakeShelf(rng, width - 1f), parent, At(0f, depth - 0.42f), yawOut);
            AddBox(shelf, new Vector3(0f, 0.45f, 0f), new Vector3(width - 1f, 0.9f, 0.5f));
            Piece("Menu Plaques", LowPolyModels.MenuPlaques(rng, width - 1.6f), parent, At(0f, depth - 0.16f), yawOut);
            Piece("Maneki Neko", LowPolyModels.ManekiNeko(rng), parent, At(barTo - 0.35f, barZ + 0.05f, 1.06f), yawOut);
            Piece("Bonsai", LowPolyModels.Bonsai(rng), parent, At(barFrom + 0.35f, barZ + 0.05f, 1.06f), yawOut);

            // The tatami room on one side of the entrance, behind shoji screens: low tables, cushions, a scroll on the wall.
            float tatFrom = -hw + 0.15f, tatTo = -0.9f, tatNear = 0.3f, tatFar = Mathf.Max(tatNear + 3f, barZ - 1.6f);
            Piece("Tatami", LowPolyModels.Tatami(rng, tatTo - tatFrom, tatFar - tatNear), parent, At(tatFrom, tatNear, 0.01f), yawIn);
            float tatMid = (tatFrom + tatTo) * 0.5f;
            foreach (float k in new[] { 0.3f, 0.72f })
            {
                GameObject t = Piece("Low Table", LowPolyModels.LowTableSet(rng), parent, At(tatMid, Mathf.Lerp(tatNear, tatFar, k), 0.04f), yawAlong);
                AddBox(t, new Vector3(0f, 0.18f, 0f), new Vector3(0.8f, 0.36f, 1.2f));
            }
            float shojiFrom = tatNear + 1.4f, shojiLen = tatFar - shojiFrom;
            if (shojiLen > 0.6f)
            {
                GameObject sh = Piece("Shoji", LowPolyModels.Shoji(rng, shojiLen, 1.5f), parent, At(tatTo + 0.05f, shojiFrom + shojiLen * 0.5f), yawAlong);
                AddBox(sh, new Vector3(0f, 0.75f, 0f), new Vector3(shojiLen, 1.5f, 0.08f));
            }
            Piece("Wall Scroll", LowPolyModels.WallScroll(rng), parent, At(-hw + 0.16f, (tatNear + tatFar) * 0.5f), yawAlong);

            // Wooden tables and chairs on the other side.
            float tableX = hw * 0.5f + 0.3f;
            var tables = new List<Vector2>();
            foreach (float k in new[] { 0.26f, 0.56f })
            {
                float z = Mathf.Min(depth * k, barZ - 2.4f);
                GameObject table = Piece("Table", LowPolyModels.Table(rng), parent, At(tableX, z), yawIn);
                AddBox(table, new Vector3(0f, 0.38f, 0f), new Vector3(1.2f, 0.75f, 0.75f));
                foreach (float cx in new[] { -0.4f, 0.4f })
                    foreach (float cz in new[] { -0.65f, 0.65f })
                        Piece("Chair", LowPolyModels.Chair(rng), parent, At(tableX + cx, z + cz), cz < 0f ? yawIn : yawOut);
                tables.Add(P(tableX, z));
            }

            // Red and white paper lanterns down the middle and over the bar; the lights.
            for (int i = 0; i < 4; i++)
                Piece("Paper Lantern", LowPolyModels.HangingLantern(rng, 2.85f, i % 2 == 0 ? new Color(0.86f, 0.14f, 0.1f) : new Color(0.95f, 0.93f, 0.86f)), parent,
                    At(0f, Mathf.Lerp(1.5f, barZ - 1.2f, i / 3f)), yawOut);
            foreach (float x in new[] { barFrom + barLen * 0.3f, barFrom + barLen * 0.7f })
                Piece("Paper Lantern", LowPolyModels.HangingLantern(rng, 2.85f, new Color(0.86f, 0.14f, 0.1f)), parent, At(x, barZ - 0.2f), yawOut);
            Lamp(At(0f, depth * 0.4f), 3.6f, 0.8f, 0.05f);
            Lamp(At(barX, barZ - 0.6f), 3.2f, 0.75f, 0.04f);

            // What it holds: a Hemp Beam on the bar by the cat, a 0.50 cal on the first table.
            PlaceSupply(ItemType.HempBeam, P(barTo - 0.9f, barZ - 0.1f), y + 1.07f, Range(0f, 360f), parent);
            // And a golden pump on the counter (the user's rule).
            PlaceGoldenPump(P(barFrom + 0.9f, barZ - 0.1f), y + 1.07f, 75f, parent);
            PlaceSupply(ItemType.Sniper, tables[0], y + 0.77f, Range(0f, 360f), parent);
            blocked.Add(r.center);
            Places.Add(("MIGA SUSHI", r));
        }

        void BuildChamber()
        {
            Rect r = Layout.Chamber;
            if (r.width <= 0f) return;
            Transform parent = Group("Diddy Chamber");
            float y = H(r.center);
            int door = Layout.ChamberDoor;
            Vector2 outward = DoorOut[door], inward = -outward, along = new Vector2(-outward.y, outward.x);
            float yawIn = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg;
            PlaceFloor("Diddy Chamber Floor", r, y, new Color(0.14f, 0.12f, 0.13f));
            PlaceWalls(r, door, 1.1f, 2.6f, new Color(0.24f, 0.22f, 0.23f), y);
            // The bed against the back wall, head to the wall.
            Vector2 bedAt = r.center + inward * (r.height * 0.5f - 1.2f);
            GameObject bed = Piece("Comfy Bed", LowPolyModels.ComfyBed(rng), parent, new Vector3(bedAt.x, y, bedAt.y), yawIn + 180f);
            AddBox(bed, new Vector3(0f, 0.35f, 0f), new Vector3(1.8f, 0.7f, 2.1f));
            // Chains on the two side walls.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 at = r.center + along * s * (r.width * 0.5f - 0.2f) + outward * 0.4f;
                // Hung on the wall, facing into the room.
                float yaw = Mathf.Atan2(-along.x * s, -along.y * s) * Mathf.Rad2Deg;
                Piece("Wall Chains", LowPolyModels.WallChains(rng), parent, new Vector3(at.x, y, at.y), yaw);
            }
            // Baby oil everywhere: standing, lying on their sides, rolled about the floor.
            for (int i = 0; i < 14; i++)
            {
                var at = new Vector2(Range(r.xMin + 0.35f, r.xMax - 0.35f), Range(r.yMin + 0.35f, r.yMax - 0.35f));
                if (Vector2.Distance(at, bedAt) < 1.1f && i % 3 != 0) continue;
                GameObject bottle = Piece("Baby Oil", LowPolyModels.BabyOil(rng), parent, new Vector3(at.x, y + 0.01f, at.y), Range(0f, 360f));
                if (i % 2 == 1)
                {
                    bottle.transform.localRotation *= Quaternion.Euler(0f, 0f, 90f);
                    bottle.transform.localPosition += Vector3.up * 0.045f;
                }
                if (Vector2.Distance(at, bedAt) < 1.1f) bottle.transform.localPosition += Vector3.up * 0.68f;
            }
            Lamp(new Vector3(r.center.x, y, r.center.y), 2.5f, 0.55f, 0.15f);
            blocked.Add(r.center);
            Places.Add(("DIDDY CHAMBER", r));
        }
    }
}
