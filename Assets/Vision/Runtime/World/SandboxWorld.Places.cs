using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.World
{
    /// <summary>
    /// Miga Sushi and the Diddy Chamber (<see cref="MapLayout.Sushi"/>, <see cref="MapLayout.Chamber"/>): small buildings
    /// on the outskirts, walled (blocking movement, sight and shots), an open doorway facing in toward the middle, a light
    /// inside. Miga Sushi has a sushi bar with stools, the noren curtain and its sign over the door, and holds a Hemp Beam
    /// and a 0.50 cal; the Diddy Chamber is a tiny dark dungeon of a room with chains on the walls, a big comfy bed and
    /// bottles of baby oil all over the floor. Their names show on screen as a player walks in (GameHud's place banner).
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>The named places on the map, for the banner shown on walking in: (name, area).</summary>
        public readonly List<(string name, Rect area)> Places = new List<(string, Rect)>();

        static readonly Vector2[] DoorOut = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        /// <summary>Four walls round <paramref name="r"/>, an open doorway <paramref name="doorW"/> wide in the middle of the door side.</summary>
        void PlaceWalls(Rect r, int doorSide, float doorW, float height, Color color, float y)
        {
            var corners = new[] { new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin) };
            void Run(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                if (len < 0.05f) return;
                Wall("Wall", a, b, height, 0.3f, LowPolyModels.PlainWall(rng, len, height, 0.3f, color), true, true, y);
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
            float yawIn = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg;
            PlaceFloor("Miga Sushi Floor", r, y, new Color(0.42f, 0.32f, 0.22f));
            PlaceWalls(r, door, 1.6f, 2.9f, new Color(0.86f, 0.82f, 0.72f), y);
            Vector2 doorAt = r.center + outward * depth * 0.5f;
            // Over the door, on the outside: the noren and the sign.
            Piece("Noren", LowPolyModels.Noren(rng, 1.6f), parent, new Vector3(doorAt.x, y, doorAt.y), yawIn + 180f);
            Piece("Miga Sushi Sign", LowPolyModels.SushiSign(rng, 2.4f), parent, new Vector3(doorAt.x + outward.x * 0.16f, y, doorAt.y + outward.y * 0.16f), yawIn + 180f);
            // The bar across the back, the chef's side behind it; stools in front.
            Vector2 barAt = r.center + inward * (depth * 0.5f - 1.6f);
            GameObject bar = Piece("Sushi Counter", LowPolyModels.SushiCounter(rng, width - 2f), parent, new Vector3(barAt.x, y, barAt.y), yawIn + 180f);
            AddBox(bar, new Vector3(0f, 0.55f, 0f), new Vector3(width - 2f, 1.1f, 0.8f));
            int stools = Mathf.Max(3, Mathf.RoundToInt((width - 2.5f) / 1f));
            for (int i = 0; i < stools; i++)
            {
                Vector2 at = barAt - inward * 0.85f + along * ((i - (stools - 1) * 0.5f) * (width - 2.5f) / Mathf.Max(1, stools - 1));
                Piece("Stool", LowPolyModels.Stool(rng), parent, new Vector3(at.x, y, at.y), Range(0f, 360f));
            }
            // A table and chairs by the window side.
            Vector2 tableAt = r.center + outward * (depth * 0.5f - 1.6f) + along * (width * 0.5f - 1.3f);
            GameObject table = Piece("Table", LowPolyModels.Table(rng), parent, new Vector3(tableAt.x, y, tableAt.y), yawIn);
            AddBox(table, new Vector3(0f, 0.38f, 0f), new Vector3(1.2f, 0.75f, 0.75f));
            Piece("Chair", LowPolyModels.Chair(rng), parent, new Vector3(tableAt.x + along.x * 0.9f, y, tableAt.y + along.y * 0.9f), Range(0f, 360f));
            Lamp(new Vector3(r.center.x, y, r.center.y), 2.8f, 0.8f, 0.05f);
            // What it holds: a Hemp Beam on the bar's end, a 0.50 cal on the table.
            Vector2 beamAt = barAt - inward * 0.55f + along * ((width - 2f) * 0.5f - 0.6f);
            PlaceSupply(ItemType.HempBeam, beamAt, y + 0.02f, Range(0f, 360f), parent);
            PlaceSupply(ItemType.Sniper, tableAt - inward * 0.7f, y + 0.02f, Range(0f, 360f), parent);
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
