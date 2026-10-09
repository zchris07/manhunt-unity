using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;

namespace Vision.Tests
{
    /// <summary>
    /// Keeps the port honest against the original. <see cref="Balance"/> is a line-for-line copy of the original's
    /// balance.ts, so a constant nothing reads is a behaviour of the original that was copied but never built (the night
    /// vision and Hemp Battery light growing out over its fade-in time was missed exactly this way). Every constant must be
    /// read by the game, or be listed here with the reason it isn't.
    /// </summary>
    public class ReferenceCoverageTests
    {
        /// <summary>Constants deliberately not read, and why. Anything else unread fails the test.</summary>
        static readonly Dictionary<string, string> NotRead = new Dictionary<string, string>
        {
            ["World.WarehouseSize"] = "the original's warehouse map; the Unity world builds its own central building (BuildingPlan)",
            ["Net.InterpolationDelayMs"] = "the original's snapshot buffer; Unity puppets ease toward each 30 Hz tick instead",
            ["Survivor.Radius"] = "an alias of Balance.SurvivorRadius, which the game reads",
            ["Hunter.Radius"] = "an alias of Balance.HunterRadius, which the game reads",
            ["Hunter.VisionRange"] = "Zach's beam runs to the edge of the screen, as the original's does past its reach",
            ["Survivor.AllyConeAlpha"] = "NOT YET PORTED: downed and staked survivors seeing their teammates' torch cones",
            ["Hunter.ScentSendEvery"] = "the original's network cadence for the scent; Unity draws the trail from the match directly",
            ["Hunter.BreakBarricadeTime"] = "unused in the original too",
            ["Barricade.DropTime"] = "unused in the original too",
            ["Items.AnywhereShare"] = "the original's map generator; Unity places supplies by its own room table",
            ["Sniper.PelletSpeed"] = "unused in the original too",
            ["Sniper.LaserMax"] = "the 0.50 cal's laser is drawn to the edge of the view",
            ["Xray.Range"] = "the see-through light runs past the edge of the screen whichever way you look (asked for by the user)",
            ["Sexton.JarvisMinimapMul"] = "unused in the original too",
            ["Shane.StepsNear"] = "Shane's steps use the audio manager's distance falloff",
            ["Shane.StepsFar"] = "Shane's steps use the audio manager's distance falloff",
            ["Chris.AmbulanceLength"] = "the original's map generator; the Unity ambulance is a model of its own size",
            ["Chris.AmbulanceWidth"] = "the original's map generator; the Unity ambulance is a model of its own size",
            ["Lights.MaxPolygonsPerFrame"] = "the Unity mask renderer has its own nearest-lights cap",
            ["Lights.CampfireRadius"] = "light reach was tuned for the 3D world (PropFactory)",
            ["Lights.GeneratorRadius"] = "light reach was tuned for the 3D world (PropFactory)",
            ["Lights.LampRadius"] = "light reach was tuned for the 3D world (BuildingPlan)",
            ["Lights.FlickerSpeed"] = "each light's flicker was tuned for the 3D world (PropFactory)",
        };

        static IEnumerable<Type> Nested(Type t)
        {
            yield return t;
            foreach (Type n in t.GetNestedTypes(BindingFlags.Public))
                foreach (Type m in Nested(n)) yield return m;
        }

        [Test]
        public void EveryOriginalConstant_IsReadByTheGame_OrListedWithAReason()
        {
            string runtime = Path.Combine(Application.dataPath, "Vision", "Runtime");
            string[] files = Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories);
            string balancePath = files.Single(f => Path.GetFileName(f) == "Balance.cs");
            string balance = File.ReadAllText(balancePath);
            string game = string.Join("\n", files.Where(f => f != balancePath).Select(File.ReadAllText));

            var unread = new List<string>();
            var stale = new List<string>();
            int count = 0;
            foreach (Type t in Nested(typeof(Balance)))
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (!f.IsLiteral && !f.IsInitOnly) continue;
                    count++;
                    string key = t.Name + "." + f.Name;
                    bool read = Regex.IsMatch(game, $@"\b{t.Name}\.{f.Name}\b");
                    if (!read)
                    {
                        // Built into another constant in Balance itself (declared once, named again).
                        int named = Regex.Matches(balance, $@"\b{f.Name}\b").Count;
                        int declared = Regex.Matches(balance, $@"\b{f.Name}\s*=").Count;
                        read = named > declared;
                    }
                    if (!read && !NotRead.ContainsKey(key)) unread.Add(key);
                    if (read && NotRead.ContainsKey(key)) stale.Add(key);
                }
            }
            Assert.Greater(count, 300, "the whole of balance.ts");
            Assert.IsEmpty(unread, "copied from the original but never used (build the behaviour, or list it with a reason): " + string.Join(", ", unread));
            Assert.IsEmpty(stale, "listed as unread but now used (take them off the list): " + string.Join(", ", stale));
        }
    }
}
