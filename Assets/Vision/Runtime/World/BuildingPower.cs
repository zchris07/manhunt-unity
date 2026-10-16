using System.Collections.Generic;
using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// The building's ceiling lights, room by room: dark until one of the building's generators runs, then the whole
    /// interior is lit, for good.
    /// </summary>
    public sealed class BuildingPower : MonoBehaviour
    {
        public readonly List<GeneratorObjective> generators = new List<GeneratorObjective>();
        public readonly List<VisionLight> lights = new List<VisionLight>();

        /// <summary>Whether the building's lights are on (one of its generators is running).</summary>
        public bool On { get; private set; }

        void Start() => Apply(Powered());

        void LateUpdate()
        {
            bool powered = Powered();
            if (powered != On) Apply(powered);
        }

        bool Powered()
        {
            foreach (GeneratorObjective g in generators) if (g != null && g.Running) return true;
            return false;
        }

        void Apply(bool on)
        {
            On = on;
            foreach (VisionLight l in lights) if (l != null) l.enabled = on;
        }

        /// <summary>Puts the lights to match the generators now (tests and tools; the game does it every frame).</summary>
        public void Refresh() => Apply(Powered());
    }
}
