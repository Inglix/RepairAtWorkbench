using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RepairAtWorkbench.Patches.Core
{
    [HarmonyPatch(
        typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing),
        new Type[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
    static class Patch__WorkGiver_DoBill__JobOnThing
    {
        static void Postfix(
            Pawn pawn,
            Thing thing,
            bool forced,
            ref Job __result
        ) {
            var job = __result;

            if (job != null && job.def == JobDefOf.DoBill && job.RecipeDef.Worker is RecipeWorker_Repair worker)
            {
                __result = new Job(ResourceBank.Job.RepairAtWorkbench, job.targetA)
                {
                    targetQueueB = job.targetQueueB,
                    countQueue = job.countQueue,
                    haulMode = job.haulMode,
                    bill = job.bill
                };

                return;
            }
        }
    }
}

