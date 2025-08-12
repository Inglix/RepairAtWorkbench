using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RepairAtWorkbench
{
    public static class Utils
    {
        public static int SkillToSkillDiffCategory(int skill)
        {
            return (int)Math.Floor(skill / 6f); ;
        }

        public static (int, SkillDef) GetHighestRequiredSkillAndValue(RecipeDef recipeDef)
        {
            var skillRequirements = recipeDef?.skillRequirements;
            (int, SkillDef) returnValue = (0, recipeDef?.workSkill);

            if (skillRequirements == null) { return returnValue; }

            foreach (var skillRequirement in skillRequirements.Where(skillRequirement => skillRequirement.minLevel > returnValue.Item1))
            {
                returnValue.Item1 = skillRequirement.minLevel;
                returnValue.Item2 = skillRequirement.skill;
            }
            return returnValue;
        }
    }
}