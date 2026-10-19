using System.Collections.Generic;
using UnityEngine;
using BF = Vision.World.BuildingPlan.Furn;

namespace Vision.World
{
    /// <summary>
    /// The cabins' insides, lived in once and left: a floor of boards or of stone flags (nothing grows up through it), and
    /// about a dozen things beyond the wardrobe, bed, table and chair, never crowding the doorway or each other: an iron
    /// stove against the back wall, a shelf, a crate or a barrel in a corner, a rug under the table, papers, bottles and
    /// debris on the floor, sometimes a chair knocked over; wallpaper torn and peeling in some, water and grime stains on
    /// the walls, a poster, a mirror or a clock, cobwebs up in a corner, and in some, blood: a pool dragged across the
    /// floor, perhaps a smear on a wall.
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>Each cabin's floor (by its index in the layout): stone flags, or boards.</summary>
        public readonly List<bool> CabinStoneFloors = new List<bool>();

        /// <summary>Whether a point is inside a cabin with a stone floor.</summary>
        public bool OnStoneFloor(Vector2 at)
        {
            if (Layout == null) return false;
            for (int i = 0; i < Layout.Cabins.Count && i < CabinStoneFloors.Count; i++)
                if (Layout.Cabins[i].Area.Contains(at)) return CabinStoneFloors[i];
            return false;
        }

        void FurnishCabin(MapLayout.Cabin cabin, float y, Vector2 inward, System.Random cr, List<(Vector2 at, float r)> taken)
        {
            Rect r = cabin.Area;
            Vector2 c = r.center, perp = new Vector2(inward.y, -inward.x);
            bool deep = cabin.DoorSide % 2 == 0;
            float hf = (deep ? r.height : r.width) * 0.5f - 0.15f, hs = (deep ? r.width : r.height) * 0.5f - 0.15f;
            float R(float lo, float hi) => lo + (float)cr.NextDouble() * (hi - lo);
            bool Chance(float p) => cr.NextDouble() < p;
            Vector2 P(float f, float s) => c + inward * f + perp * s;
            float YawTo(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            Vector3 V(Vector2 p, float up = 0f) => new Vector3(p.x, y + up, p.y);
            Transform parent = Group("Cabin Fittings");
            // (Against a wall, a piece may reach right up to it.)
            bool Clear(Vector2 p, float rad, bool againstWall = false)
            {
                Vector2 l = new Vector2(Vector2.Dot(p - c, inward), Vector2.Dot(p - c, perp));
                float edge = againstWall ? 0.15f : rad - 0.05f;
                if (Mathf.Abs(l.x) > hf - edge || Mathf.Abs(l.y) > hs - edge) return false;
                // The way in from the door kept clear.
                if (Mathf.Abs(l.y) < 0.9f + rad && l.x < -hf + 1.6f + rad) return false;
                foreach (var (at, tr) in taken) if ((at - p).magnitude < rad + tr + 0.12f) return false;
                return true;
            }
            void Take(Vector2 p, float rad, bool solid)
            {
                taken.Add((p, rad));
                if (solid) blocked.Add(p);
            }
            Vector2? Spot(float rad, int tries = 40)
            {
                for (int i = 0; i < tries; i++)
                {
                    Vector2 p = P(R(-hf + rad, hf - rad), R(-hs + rad, hs - rad));
                    if (Clear(p, rad)) return p;
                }
                return null;
            }

            // The floor: boards or flags, the whole cabin (under its walls and across its doorway).
            bool stone = Chance(0.4f);
            CabinStoneFloors.Add(stone);
            Piece(stone ? "Stone Floor" : "Board Floor", LowPolyModels.CabinFloor(cr, r.width, r.height, stone), parent, V(c), 0f);

            // The walls' inner faces, between the corners and the door or window in each side.
            var walls = new List<(Vector2 a, Vector2 b, Vector2 n)>();
            var corners = new[] { new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[(side + 1) % 4], u = (b - a).normalized, n = new Vector2(u.y, -u.x), mid = (a + b) * 0.5f;
                float half = (side == cabin.DoorSide ? 1.3f : 1.4f) * 0.5f;
                Vector2 face = n * 0.154f;
                walls.Add((a + u * 0.15f + face, mid - u * half + face, n));
                walls.Add((mid + u * half + face, b - u * 0.15f + face, n));
            }
            void OnWall(string name, Mesh mesh, (Vector2 a, Vector2 b, Vector2 n) w, float along, float up, float proud)
            {
                Vector2 p = Vector2.Lerp(w.a, w.b, along) + w.n * proud;
                Piece(name, mesh, parent, V(p, up), YawTo(w.n));
            }

            // Wallpaper gone to ruin, in some.
            if (Chance(0.65f))
            {
                int palette = cr.Next(LowPolyModels.WallpaperPalettes);
                foreach (var w in walls)
                {
                    float len = Vector2.Distance(w.a, w.b);
                    if (len < 0.3f) continue;
                    // (Laid from the far end back: its x runs against the wall's direction.)
                    Piece("Wallpaper", LowPolyModels.Wallpaper(cr, len, 2.2f, palette), parent, V(w.b + w.n * 0.002f, LowPolyModels.CabinFloorTop), YawTo(w.n));
                }
            }
            // Water and grime on the walls.
            int stains = 2 + cr.Next(2);
            for (int i = 0; i < stains; i++)
            {
                var w = walls[cr.Next(walls.Count)];
                Color col = Chance(0.5f) ? new Color(0.36f, 0.3f, 0.2f) : new Color(0.2f, 0.19f, 0.16f);
                OnWall("Wall Stain", LowPolyModels.Blot(cr, R(0.2f, 0.45f), col, true, false), w, R(0.2f, 0.8f), R(0.4f, 1.8f), 0.006f);
            }
            // A poster, a mirror or a clock.
            if (Chance(0.7f))
            {
                var w = walls[cr.Next(walls.Count)];
                int pick = cr.Next(3);
                BF kind = pick == 0 ? BF.Poster : pick == 1 ? BF.Mirror : BF.Clock;
                float up = kind == BF.Poster ? 1.45f : kind == BF.Mirror ? 1.15f : 1.9f;
                OnWall(kind.ToString(), LowPolyModels.BuildingItem(kind, cr, new Vector2(kind == BF.Clock ? 0.32f : 0.55f, 0.1f), cr.Next(2)), w, R(0.3f, 0.7f), up, 0.008f);
            }
            // Cobwebs up in a corner or two.
            for (int i = 0, n = 1 + cr.Next(2); i < n; i++)
            {
                float sf = cr.Next(2) == 0 ? -1f : 1f, ss = cr.Next(2) == 0 ? -1f : 1f;
                Vector2 at = P(sf * (hf - 0.02f), ss * (hs - 0.02f));
                Piece("Cobweb", LowPolyModels.BuildingItem(BF.Cobweb, cr, Vector2.one * 0.6f, 0), parent, V(at, 2.25f), YawTo(c - at));
            }

            // Against the back wall: an iron stove.
            if (Chance(0.7f))
                foreach (float s in new[] { R(-0.5f, 0.5f) * hs, -hs * 0.6f, hs * 0.6f })
                {
                    Vector2 at = P(hf - 0.3f, s);
                    if (!Clear(at, 0.45f, true)) continue;
                    GameObject stove = Piece("Stove", LowPolyModels.Stove(cr), parent, V(at), YawTo(-inward));
                    AddBox(stove, new Vector3(0f, 0.37f, 0f), new Vector3(0.66f, 0.74f, 0.54f));
                    Take(at, 0.45f, true);
                    break;
                }
            // A shelf against a side wall.
            if (Chance(0.75f))
                for (int t = 0; t < 6; t++)
                {
                    float side = cr.Next(2) == 0 ? -1f : 1f;
                    Vector2 at = P(R(-hf + 1.4f, hf - 0.8f), side * (hs - 0.22f));
                    if (!Clear(at, 0.55f, true)) continue;
                    GameObject shelf = Piece("Shelf", LowPolyModels.BuildingItem(BF.Shelf, cr, new Vector2(1f, 0.4f), cr.Next(3)), parent, V(at), YawTo(perp * -side));
                    AddBox(shelf, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.9f, 0.4f));
                    Take(at, 0.55f, true);
                    break;
                }
            // A crate or a barrel in a back corner.
            if (Chance(0.7f))
            {
                float side = cr.Next(2) == 0 ? -1f : 1f;
                Vector2 at = P(hf - 0.42f, side * (hs - 0.42f));
                if (Clear(at, 0.38f))
                {
                    bool barrel = Chance(0.5f);
                    GameObject go = Piece(barrel ? "Barrel" : "Crate", LowPolyModels.BuildingItem(barrel ? BF.Barrel : BF.Crate, cr, new Vector2(0.6f, 0.6f), cr.Next(3)), parent, V(at), R(0f, 360f));
                    AddBox(go, new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.6f));
                    Take(at, 0.38f, true);
                }
            }
            // A rug under the table.
            if (Chance(0.5f) && taken.Count > 2)
            {
                Vector2 table = taken[2].at;
                Piece("Rug", LowPolyModels.Rug(cr, R(1.8f, 2.2f), R(1.3f, 1.6f)), parent, V(table, LowPolyModels.CabinFloorTop), YawTo(perp));
            }
            // A chair knocked over.
            if (Chance(0.4f) && Spot(0.45f) is Vector2 fallen)
            {
                Piece("Chair", LowPolyModels.Chair(cr, true), parent, V(fallen, LowPolyModels.CabinFloorTop), R(0f, 360f));
                Take(fallen, 0.45f, false);
            }
            // Things dropped on the floor.
            void Loose(BF kind, float size, int variant)
            {
                if (!(Spot(size * 0.6f) is Vector2 at)) return;
                Piece(kind.ToString(), LowPolyModels.BuildingItem(kind, cr, new Vector2(size, size), variant), parent, V(at, LowPolyModels.CabinFloorTop), R(0f, 360f));
                Take(at, size * 0.5f, false);
            }
            for (int i = 0, n = 1 + cr.Next(2); i < n; i++) Loose(BF.Papers, 0.5f, cr.Next(3));
            Loose(BF.Bottles, 0.4f, cr.Next(3));
            if (Chance(0.6f)) Loose(BF.Debris, 0.4f, cr.Next(2));
            // Blood, in some: a pool dragged across the floor, perhaps a smear on a wall.
            if (Chance(0.6f))
            {
                var blood = new Color(0.27f, 0.04f, 0.03f);
                if (Spot(0.5f) is Vector2 pool)
                    Piece("Blood", LowPolyModels.Blot(cr, R(0.3f, 0.5f), blood, false, true), parent, V(pool, LowPolyModels.CabinFloorTop), R(0f, 360f));
                if (Chance(0.4f))
                    OnWall("Blood", LowPolyModels.Blot(cr, R(0.12f, 0.2f), blood * 1.1f, true, true), walls[cr.Next(walls.Count)], R(0.2f, 0.8f), R(0.6f, 1.4f), 0.007f);
            }
        }
    }
}
