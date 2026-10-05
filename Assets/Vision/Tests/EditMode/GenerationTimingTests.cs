using NUnit.Framework;
using UnityEngine;
using Vision.World;

namespace Vision.Tests
{
    public class GenerationTimingTests
    {
        [Test]
        public void Generate_TheFullMap_InUnderTenSeconds_InTheEditor()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            try
            {
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                world.Generate();
                Debug.Log("[Vision] Timing: " + world.LastGenerationReport);
                Assert.Less(sw.ElapsedMilliseconds, 10000, world.LastGenerationReport);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
