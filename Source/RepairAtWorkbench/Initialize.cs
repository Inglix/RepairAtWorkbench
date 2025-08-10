using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RepairAtWorkbench
{
    [StaticConstructorOnStartup]
    public static class Initialize
    {
        static Initialize()
        {
            new Harmony("Inglix.RepairAtWorkbench").PatchAll(Assembly.GetExecutingAssembly());
            var controller = LoadedModManager.GetMod<RepairAtWorkbenchController>();
            controller.DefsLoaded();
        }
    }
    
    [HarmonyPatch(typeof(WorkGiver_DoBill))]
    [HarmonyPatch(nameof(WorkGiver_DoBill.JobOnThing))]
    [HarmonyPatch(new[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
    class Patch_WorkGiver_DoBill_JobOnThing
    {
        static void Postfix(Pawn pawn, Thing thing, bool forced, ref Job __result)
        {
            var job = __result;

            if (job != null && job.def == JobDefOf.DoBill && job.RecipeDef.Worker is RecipeWorker_Repair worker)
            {
                __result = new Job(ResourceBank.Job.RepairAtCraftingBench, job.targetA)
                {
                    targetQueueB = job.targetQueueB,
                    countQueue = job.countQueue,
                    haulMode = job.haulMode,
                    bill = job.bill
                };
            }
        }
    }
}