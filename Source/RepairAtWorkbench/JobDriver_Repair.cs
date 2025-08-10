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

        int costHitPointsPerCycle;
        float workCycle;
        float workCycleProgress;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref costHitPointsPerCycle, "costHitPointsPerCycle", 1);
            Scribe_Values.Look(ref workCycle, "workCycle", 1f);
            Scribe_Values.Look(ref workCycleProgress, "workCycleProgress", 1f);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected Toil DoBill()
        {
            var tableThing = job.GetTarget(BillGiverInd).Thing as Building_WorkTable;
            var refuelableComp = tableThing.GetComp<CompRefuelable>();

            var toil = new Toil ();
            toil.initAction = delegate {
                var objectThing = job.GetTarget(IngredientInd).Thing;

                job.bill.Notify_DoBillStarted(pawn);

                costHitPointsPerCycle = (int)(objectThing.MaxHitPoints * 0.05f / Settings.techCostFactor[objectThing.def.techLevel]);

                workCycleProgress = workCycle = Math.Max(job.bill.recipe.workAmount, 10f);
            };
            toil.tickAction = delegate
            {
                var objectThing = job.GetTarget(IngredientInd).Thing;

                if (objectThing == null || objectThing.Destroyed)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }

                workCycleProgress -= StatExtension.GetStatValue(pawn, StatDefOf.WorkToMake, true);

                tableThing.UsedThisTick();
            };
            toil.tickIntervalAction = delegate(int delta)
            {
                var objectThing = job.GetTarget(IngredientInd).Thing;
                if (! (tableThing.CurrentlyUsableForBills() && (refuelableComp == null || refuelableComp.HasFuel)) ) {
                    pawn.jobs.EndCurrentJob (JobCondition.Incompletable);
                }

                if (workCycleProgress <= 0) {
                    int remainingHitPoints = objectThing.MaxHitPoints - objectThing.HitPoints;
                    if (remainingHitPoints > 0) {
                        objectThing.HitPoints += (int) Math.Min(remainingHitPoints, costHitPointsPerCycle);
                    }

                    float skillPerc = 0.5f;

                    var skillDef = job.RecipeDef.workSkill;
                    if (skillDef != null) {
                        var skill = pawn.skills.GetSkill (skillDef);

                        if (skill != null) {
                            skillPerc = (float)skill.Level / 20f;

                            skill.Learn (0.11f * job.RecipeDef.workSkillLearnFactor);
                        }
                    }

                    pawn.GainComfortFromCellIfPossible(delta);

                    if (objectThing.HitPoints == objectThing.MaxHitPoints) {
                        // fixed!

                        // removes deadman
                        //if (objectThing is Apparel mendApparel) {
                        //    ApparelWornByCorpseInt.SetValue(mendApparel, false);
                        //}

                        var list = new List<Thing> ();
                        list.Add(objectThing);
                        job.bill.Notify_IterationCompleted (pawn, list);

                        ReadyForNextToil();

                    } else if (objectThing.HitPoints > objectThing.MaxHitPoints) {
                        Log.Error("MendAndRecycle :: This should never happen! HitPoints > MaxHitPoints");
                        pawn.jobs.EndCurrentJob (JobCondition.Incompletable);
                    }

                    workCycleProgress = workCycle;
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect (() => job.bill.recipe.effectWorking, BillGiverInd);
            toil.PlaySustainerOrSound (() => toil.actor.CurJob.bill.recipe.soundWorking);
            toil.WithProgressBar(BillGiverInd, () => {
                var objectThing = job.GetTarget(IngredientInd).Thing;
                return (float)objectThing.HitPoints / (float)objectThing.MaxHitPoints;
            }, false, 0.5f);
            toil.FailOn(() => {
                var billGiver = job.GetTarget (BillGiverInd).Thing as IBillGiver;

                return job.bill.suspended || job.bill.DeletedOrDereferenced || (billGiver != null && !billGiver.CurrentlyUsableForBills ());
            });
            return toil;
        }

        Toil Store ()
        {
            return new Toil () {
                initAction = delegate {
                    var objectThing = job.GetTarget (IngredientInd).Thing;

                    if (job.bill.GetStoreMode () != BillStoreModeDefOf.DropOnFloor) {
                        IntVec3 vec = IntVec3.Invalid;
                        if (job.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                        {
                            StoreUtility.TryFindBestBetterStoreCellFor(objectThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out vec, true);
                        }
                        else if (job.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                        {
                            StoreUtility.TryFindBestBetterStoreCellForIn(objectThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, job.bill.GetSlotGroup(), out vec, true);
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
