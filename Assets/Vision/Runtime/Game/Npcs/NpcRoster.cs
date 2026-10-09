using System;

namespace Vision.Game
{
    /// <summary>
    /// The NPCs a match has, made in the original's order of who Zach's machete reaches first (Sexton, Chris, Marc, Plasma,
    /// Jaden, Njaaron, Monique, Thomas, Soham, Chacko, Waz), then Shane, whom it never touches.
    /// </summary>
    public static class NpcRoster
    {
        public static readonly Func<MatchSim, Npc>[] Makers =
        {
            s => new Sexton(s), s => new Chris(s), s => new Marc(s), s => new Plasma(s), s => new Jaden(s), s => new Njaaron(s), s => new Monique(s),
            s => new Thomas(s), s => new Soham(s), s => new Chacko(s), s => new Waz(s), s => new Shane(s),
        };

        /// <summary>Puts every NPC on the map (again, for testing's Respawn NPCs).</summary>
        public static void Spawn(MatchSim sim)
        {
            sim.Npcs.Clear();
            foreach (var make in Makers) sim.Npcs.Add(make(sim));
        }
    }
}
