using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RepairAtWorkbench
{
    public class Bill_RepairApparelOrWeapon : Bill_ProductionWithUft
    {
        public Bill_RepairApparelOrWeapon(RecipeDef recipe, Precept_ThingStyle precept = null)
            : base(recipe, precept)
        { }
    }
}
