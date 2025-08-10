using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace RepairAtWorkbench
{
    public class JobDriver_Repair : JobDriver_DoBill
    {
        readonly FieldInfo ApparelWornByCorpseInt = typeof(Apparel).GetField("wornByCorpseInt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        protected override IEnumerable<Toil> MakeNewToils ()
        {
            AddEndCondition(delegate {
                var thing = GetActor().jobs.curJob.GetTarget(BillGiverInd).Thing;
                if (thing is Building && !thing.Spawned)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            this.FailOnBurningImmobile(BillGiverInd);
            this.FailOn(() => job.GetTarget(BillGiverInd).Thing is IBillGiver billGiver &&
                              (job.bill.DeletedOrDereferenced || !billGiver.CurrentlyUsableForBills()));

            yield return Toils_Reserve.Reserve(BillGiverInd, 1);
            yield return Toils_Reserve.ReserveQueue(IngredientInd, 1);
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(IngredientInd);
            yield return Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(IngredientInd);
            yield return Toils_Goto.GotoThing(BillGiverInd, PathEndMode.InteractionCell);
            yield return Toils_JobTransforms.SetTargetToIngredientPlaceCell(BillGiverInd, IngredientInd, IngredientPlaceCellInd);
            yield return Toils_Haul.PlaceHauledThingInCell(BillGiverInd, null, false);
            yield return Toils_Reserve.Reserve(IngredientInd, 1);
            yield return DoBill();
            yield return Store();
            yield return Toils_Reserve.Reserve(IngredientPlaceCellInd, 1);
            yield return Toils_Haul.CarryHauledThingToCell(IngredientPlaceCellInd);
            yield return Toils_Haul.PlaceHauledThingInCell(IngredientPlaceCellInd, null, false);
            yield return Toils_Reserve.Release(IngredientInd);
            yield return Toils_Reserve.Release(IngredientPlaceCellInd);
            yield return Toils_Reserve.Release(BillGiverInd);
        }

        int restoredHitPointsPerCycle;
        float workCycle;
        float workCycleProgress;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref restoredHitPointsPerCycle, "costHitPointsPerCycle", 1);
            Scribe_Values.Look(ref workCycle, "workCycle", 1f);
            Scribe_Values.Look(ref workCycleProgress, "workCycleProgress", 1f);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        private Toil DoBill()
        {
            if (job.GetTarget(BillGiverInd).Thing is Building_WorkTable tableThing)
            {
                var refuelableComp = tableThing.GetComp<CompRefuelable>();

                var toil = new Toil
                {
                    initAction = delegate
                    {
                        var objectThing = job.GetTarget(IngredientInd).Thing;
                        job.bill.Notify_DoBillStarted(pawn);
                        restoredHitPointsPerCycle = Math.Max(1, (int)(objectThing.MaxHitPoints * 0.05f / Settings.techCostFactor[objectThing.def.techLevel]));
                        workCycleProgress = workCycle = Math.Max(job.bill.recipe.workAmount, 10f);
                    },
                    tickAction = delegate
                    {
                        var objectThing = job.GetTarget(IngredientInd).Thing;
                        if (objectThing == null || objectThing.Destroyed)
                        {
                            pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        }

                        // Grabbing StatDefOf.WorkToMake from pawn is always going to return 1f; what the heck was the idea here?
                        // workCycleProgress -= pawn.GetStatValue(StatDefOf.WorkToMake);
                        workCycleProgress--;
                        tableThing.UsedThisTick();
                    },
                    tickIntervalAction = delegate(int delta)
                    {
                        var objectThing = job.GetTarget(IngredientInd).Thing;
                        if (!(tableThing.CurrentlyUsableForBills() && (refuelableComp == null || refuelableComp.HasFuel)))
                        {
                            pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        }
                        
                        var skillDef = job.RecipeDef.workSkill;
                        if (skillDef != null)
                        {
                            var skill = pawn.skills.GetSkill(skillDef);
                            skill?.Learn(0.1f * delta * job.RecipeDef.workSkillLearnFactor);
                            //TODO: Take a closer look at the workSkillLearnFactor values in this
                            //mod to make sure skill isn't gained faster by repairing than it is by crafting
                        }

                        pawn.GainComfortFromCellIfPossible(delta);

                        if (!(workCycleProgress <= 0)) return;
                        var missingHitPoints = objectThing.MaxHitPoints - objectThing.HitPoints;
                        if (missingHitPoints > 0)
                        {
                            objectThing.HitPoints += Math.Min(missingHitPoints, restoredHitPointsPerCycle);
                        }
                        
                        if (objectThing.HitPoints > objectThing.MaxHitPoints)
                        {
                            Log.Warning("RepairAtWorkbench - HitPoints exceeded MaxHitPoints for " + objectThing + ". Clamping to MaxHitPoints.");
                            objectThing.HitPoints = objectThing.MaxHitPoints;
                        }
                        
                        if (objectThing.HitPoints.Equals(objectThing.MaxHitPoints))
                        {
                            // fixed!

                            // removes deadman
                            //if (objectThing is Apparel mendApparel) {
                            //    ApparelWornByCorpseInt.SetValue(mendApparel, false);
                            //}
                            
                            //TODO: Add mod setting for whether this removes tainted apparel 

                            var list = new List<Thing> { objectThing };
                            job.bill.Notify_IterationCompleted(pawn, list);
                            ReadyForNextToil();
                        }

                        workCycleProgress = workCycle;
                    },
                    defaultCompleteMode = ToilCompleteMode.Never
                };
                toil.WithEffect(() => job.GetTarget(IngredientInd).Thing.def.recipeMaker.effectWorking, BillGiverInd);
                toil.PlaySustainerOrSound(() => job.GetTarget(IngredientInd).Thing.def.recipeMaker.soundWorking);
                toil.WithProgressBar(BillGiverInd, () =>
                {
                    var objectThing = job.GetTarget(IngredientInd).Thing;
                    return (float)objectThing.HitPoints / objectThing.MaxHitPoints;
                }, false, 0.5f);
                toil.FailOn(() =>
                {
                    var billGiver = job.GetTarget(BillGiverInd).Thing as IBillGiver;

                    return job.bill.suspended || job.bill.DeletedOrDereferenced || (billGiver != null && !billGiver.CurrentlyUsableForBills());
                });
                return toil;
            }

            Log.Error("RepairAtCrafingBench - DoBill() called on non-worktable thing: " + job.GetTarget(BillGiverInd).Thing);
            return new Toil()
            {
                initAction = delegate { pawn.jobs.EndCurrentJob(JobCondition.Incompletable); }
            };
        }

        private Toil Store ()
        {
            return new Toil () {
                initAction = delegate {
                    var objectThing = job.GetTarget (IngredientInd).Thing;

                    if (job.bill.GetStoreMode () != BillStoreModeDefOf.DropOnFloor) {
                        var vec = IntVec3.Invalid;
                        if (job.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                        {
                            StoreUtility.TryFindBestBetterStoreCellFor(objectThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out vec);
                        }
                        else if (job.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                        {
                            StoreUtility.TryFindBestBetterStoreCellForIn(objectThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, job.bill.GetSlotGroup(), out vec);
                        }
                        else
                        {
                            Log.ErrorOnce("Unknown store mode", 9158246);
                        }
                        if (vec.IsValid)
                        {
                            pawn.carryTracker.TryStartCarry(objectThing, objectThing.stackCount);
                            job.SetTarget(IngredientPlaceCellInd, vec);
                            job.count = 99999;
                            return;
                        }
                    }
                    pawn.carryTracker.TryStartCarry (objectThing, objectThing.stackCount);
                    pawn.carryTracker.TryDropCarriedThing (pawn.Position, ThingPlaceMode.Near, out objectThing);

                    pawn.jobs.EndCurrentJob (JobCondition.Succeeded);
                }
            };
        }
    }
}
