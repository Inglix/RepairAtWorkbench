using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RepairAtWorkbench
{
    public static class ResourceBank
    {
        /*
        [DefOf]
        public static class Recipe
        {
            public static RecipeDef MendSimpleApparel;
            public static RecipeDef MendComplexApparel;
            public static RecipeDef MendSimpleWeapon;
            public static RecipeDef MendComplexWeapon;
            public static RecipeDef MakeMendingKit;
        }
        */

        [DefOf]
        public static class Job
        {
            public static JobDef RepairAtCraftingBench;
        }

        /*
        [DefOf]
        public static class Thing
        {
            public static ThingDef TableMending;
        }

        [DefOf]
        public static class ResearchProject
        {
            public static ResearchProjectDef Mending;
            public static ResearchProjectDef Electricity;
        }

        [StaticConstructorOnStartup]
        public static class Textures
        {
            public static readonly Texture2D Outside = ContentFinder<Texture2D>.Get("UI/Designators/NoRoofArea", true);
            public static readonly Texture2D Inside = ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOn", true);
        }
        */
    }
}
