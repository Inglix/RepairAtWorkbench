using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RepairAtWorkbench
{
    public class Settings : ModSettings
    {
        public static Dictionary<TechLevel, float> techCostFactor = new Dictionary<TechLevel, float>()
        {
            {TechLevel.Undefined, 1f},
            {TechLevel.Animal, 1f},
            {TechLevel.Neolithic, 1.25f},
            {TechLevel.Medieval, 1.5f},
            {TechLevel.Industrial, 2f},
            {TechLevel.Spacer, 3f},
            {TechLevel.Ultra, 4f},
            {TechLevel.Archotech, 5f}
        };
    }
}
