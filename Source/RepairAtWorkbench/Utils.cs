using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RepairAtWorkbench
{
    public static class Utils
    {

        public static int SkillToSkillDiffCategory(int skill)
        {
            return (int)Math.Floor(skill / 6f); ;
        }

        public static (int, SkillDef) GetHighestSkillAndValue(List<SkillRequirement> skrs)
        {
            (int, SkillDef) ret = (0, null);

            if (skrs == null) { return ret; }

            foreach(var skr in skrs)
            {
                if (skr.minLevel > ret.Item1)
                {
                    ret.Item1 = skr.minLevel;
                    ret.Item2 = skr.skill;
                }
            }
            return ret;
        }
    }
}
