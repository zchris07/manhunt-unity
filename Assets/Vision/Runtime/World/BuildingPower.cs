using System.Collections.Generic;
using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// The central building's ceiling lights, split between its generators: each light belongs to the generator nearest it,
    /// so with two generators each lights its own half of the building when it runs, and a lone generator lights all of it.
    /// The lights show the rooms and halls at night vision's shaded brightness: they light the place, but nobody is seen
    /// by them (players only ever see who's in their own light).
    /// </summary>
    public sealed class BuildingPower : MonoBehaviour
    {
        public readonly List<GeneratorObjective> generators = new List<GeneratorObjective>();
        public readonly List<VisionLight> lights = new List<VisionLight>();
        /// <summary>Which of <see cref="generators"/> powers each of <see cref="lights"/>.</summary>
        public readonly List<int> owners = new List<int>();

        /// <summary>Whether any of the building's lights are on (one of its generators is running).</summary>
        public bool On { get; private set; }
        bool[] shown;

        void Start() => Refresh();

        void LateUpdate() => Apply(false);

        /// <summary>Whether generator <paramref name="g"/>'s lights are on.</summary>
        public bool Powered(int g) => g >= 0 && g < generators.Count && generators[g] != null && generators[g].Running;

        /// <summary>How many of the lights are on.</summary>
        public int LitCount
        {
            get
            {
                int n = 0;
                foreach (VisionLight l in lights) if (l != null && l.enabled) n++;
                return n;
            }
        }

        void Apply(bool force)
        {
            if (shown == null || shown.Length != generators.Count) { shown = new bool[generators.Count]; force = true; }
            bool any = false;
            for (int g = 0; g < generators.Count; g++)
            {
                bool on = Powered(g);
                any |= on;
                if (!force && shown[g] == on) continue;
                shown[g] = on;
                for (int i = 0; i < lights.Count; i++)
                    if (lights[i] != null && OwnerOf(i) == g) lights[i].enabled = on;
            }
            On = any;
        }

        int OwnerOf(int light) => light < owners.Count ? owners[light] : 0;

        /// <summary>Puts the lights to match the generators now (tests and tools; the game does it every frame).</summary>
        public void Refresh() => Apply(true);
    }
}
