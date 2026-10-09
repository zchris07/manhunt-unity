using UnityEngine;

namespace Vision.World
{
    public static partial class LowPolyModels
    {
        /// <summary>Chris Zelley's ambulance at the original's footprint (300 x 150 units): width (x), height, length (z).</summary>
        public static readonly Vector3 AmbulanceSize = new Vector3(4.5f, 3.3f, 9f);

        /// <summary>
        /// Chris Zelley's ambulance: a white box-bodied ambulance (cab toward +Z), a red stripe down each side, the blue star
        /// of life on the sides and back, a red and blue light bar on the cab, dark glass, rear doors and four wheels.
        /// </summary>
        public static Mesh Ambulance(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Vector3 size = AmbulanceSize;
            float w = size.x * 0.5f, l = size.z * 0.5f;
            Color white = new Color(0.86f, 0.87f, 0.85f), red = new Color(0.72f, 0.1f, 0.1f), blue = new Color(0.12f, 0.3f, 0.75f);
            Color glass = new Color(0.1f, 0.12f, 0.15f), tyre = new Color(0.07f, 0.07f, 0.08f), trim = new Color(0.25f, 0.26f, 0.28f);
            float floor = 0.6f, boxFront = 1.2f, cabFront = 3.5f;

            // The patient box, the cab and its bonnet.
            b.AddBox(new Vector3(-w, floor, -l), new Vector3(size.x, size.y - floor, l + boxFront), white, 0.03f);
            b.AddBox(new Vector3(-w + 0.12f, floor, boxFront), new Vector3(size.x - 0.24f, 1.95f, cabFront - boxFront), white, 0.03f);
            b.AddBox(new Vector3(-w + 0.18f, floor, cabFront), new Vector3(size.x - 0.36f, 0.95f, l - cabFront - 0.05f), white, 0.03f);
            // Bumpers and the chassis under it all.
            b.AddBox(new Vector3(-w + 0.1f, 0.35f, -l - 0.12f), new Vector3(size.x - 0.2f, 0.3f, 0.2f), trim, 0.04f);
            b.AddBox(new Vector3(-w + 0.15f, 0.35f, l - 0.08f), new Vector3(size.x - 0.3f, 0.32f, 0.2f), trim, 0.04f);
            b.AddBox(new Vector3(-w + 0.4f, 0.3f, -l + 0.3f), new Vector3(size.x - 0.8f, 0.32f, size.z - 0.6f), trim * 0.6f, 0.04f);

            // Glass: the windscreen and the cab's side windows.
            b.AddBox(new Vector3(-w + 0.3f, floor + 1.0f, cabFront - 0.01f), new Vector3(size.x - 0.6f, 0.8f, 0.03f), glass, 0.03f);
            foreach (float s in new[] { -1f, 1f })
                b.AddBox(new Vector3(s < 0f ? -w + 0.1f : w - 0.13f, floor + 1.0f, boxFront + 0.4f), new Vector3(0.03f, 0.75f, 1.5f), glass, 0.03f);

            // The red stripe and the star of life down each side.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s < 0f ? -w - 0.012f : w - 0.008f;
                b.AddBox(new Vector3(x, 1.35f, -l + 0.05f), new Vector3(0.02f, 0.32f, size.z - 0.15f), red, 0.03f);
                Vector3 c = new Vector3(x, 2.35f, -l * 0.35f);
                b.AddBox(c + new Vector3(0f, -0.38f, -0.12f), new Vector3(0.02f, 0.76f, 0.24f), blue, 0.02f);
                AddBoxAt(b, c + new Vector3(0.01f, 0f, 0f), new Vector3(0.02f, 0.76f, 0.24f), Quaternion.Euler(60f, 0f, 0f), blue);
                AddBoxAt(b, c + new Vector3(0.01f, 0f, 0f), new Vector3(0.02f, 0.76f, 0.24f), Quaternion.Euler(-60f, 0f, 0f), blue);
            }
            // The back: the doors' seam, handles, and the star again.
            b.AddBox(new Vector3(-0.02f, floor + 0.1f, -l - 0.012f), new Vector3(0.04f, size.y - floor - 0.3f, 0.02f), trim, 0.02f);
            b.AddBox(new Vector3(-w + 0.05f, 1.35f, -l - 0.012f), new Vector3(size.x - 0.1f, 0.32f, 0.02f), red, 0.03f);
            b.AddBox(new Vector3(-0.25f, 2.3f, -l - 0.014f), new Vector3(0.5f, 0.5f, 0.02f), blue, 0.03f);

            // The light bar on the cab, red and blue.
            float barY = floor + 1.95f;
            b.AddBox(new Vector3(-w + 0.4f, barY, boxFront + 0.5f), new Vector3(size.x * 0.5f - 0.45f, 0.18f, 0.35f), red, 0.05f);
            b.AddBox(new Vector3(0.05f, barY, boxFront + 0.5f), new Vector3(size.x * 0.5f - 0.45f, 0.18f, 0.35f), blue, 0.05f);
            // Lights on the box's corners.
            foreach (float s in new[] { -1f, 1f })
            {
                b.AddBox(new Vector3(s < 0f ? -w + 0.05f : w - 0.3f, size.y - 0.22f, boxFront - 0.06f), new Vector3(0.25f, 0.16f, 0.08f), red, 0.05f);
                b.AddBox(new Vector3(s < 0f ? -w + 0.05f : w - 0.3f, size.y - 0.22f, -l - 0.06f), new Vector3(0.25f, 0.16f, 0.08f), red, 0.05f);
            }

            // Wheels.
            int sides = PolyBudget.Sides(0.55f, VehicleClass, 6, 8);
            foreach (float z in new[] { -l + 1.5f, l - 1.6f })
                foreach (float s in new[] { -1f, 1f })
                {
                    float x0 = s * (w - 0.35f), x1 = s * (w + 0.04f);
                    b.AddFrustum(new Vector3(x0, 0.55f, z), new Vector3(x1, 0.55f, z), 0.55f, 0.55f, sides, tyre, 0.04f);
                }
            return b.ToMesh("Ambulance");
        }

        /// <summary>One of the Four Notes: a creepy photo lying face up in the leaves, pinned by a pebble.</summary>
        public static Mesh Note(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            float tilt = b.Range(-0.02f, 0.02f);
            b.AddBox(new Vector3(-0.17f, 0.005f, -0.12f), new Vector3(0.34f, 0.012f, 0.24f), new Color(0.86f, 0.82f, 0.7f), 0.03f);
            b.AddBox(new Vector3(-0.14f, 0.018f + tilt * 0f, -0.09f), new Vector3(0.28f, 0.004f, 0.18f), new Color(0.32f, 0.26f, 0.2f), 0.12f);
            b.AddBlob(new Vector3(0.13f, 0.03f, 0.08f), new Vector3(0.045f, 0.03f, 0.04f), 0, 0.15f, _ => new Color(0.35f, 0.34f, 0.32f));
            return b.ToMesh("Note");
        }
    }
}
