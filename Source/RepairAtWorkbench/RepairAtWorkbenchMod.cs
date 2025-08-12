using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace RepairAtWorkbench
{
    public class RepairAtWorkbenchMod : Mod
    {
        private static readonly FieldInfo AllRecipesCached = typeof(ThingDef).GetField("allRecipesCached", BindingFlags.Instance | BindingFlags.NonPublic);

        // TODO: this should really just read recipedefs from XML that have a
        // specific flag set that marks them as "repair only", instead of being
        // hard-coded

        // workbenchDefName => (productdefname, skilldefname, number) pairs
        private static readonly Dictionary<string, List<(string, string, int)>> HardcodedRepairables = new Dictionary<string, List<(string, string, int)>>
        {
            {
                "HandTailoringBench",
                new List<(string, string, int)> {
                    ("Apparel_PsyfocusHelmet", "Crafting", 8),
                    ("Apparel_EltexSkullcap", "Crafting", 8),
                    ("Apparel_PsyfocusShirt", "Crafting", 8),
                    ("Apparel_PsyfocusVest", "Crafting", 8),
                    ("Apparel_PsyfocusRobe", "Crafting", 8)
                }
            },
            {
                "ElectricTailoringBench",
                new List<(string, string, int)> {
                    ("Apparel_PsyfocusHelmet", "Crafting", 8),
                    ("Apparel_EltexSkullcap", "Crafting", 8),
                    ("Apparel_PsyfocusShirt", "Crafting", 8),
                    ("Apparel_PsyfocusVest", "Crafting", 8),
                    ("Apparel_PsyfocusRobe", "Crafting", 8)
                }
            }
        };

        public RepairAtWorkbenchMod(ModContentPack content) : base(content)
        {
        }

        public override string SettingsCategory()
        {
            return "Inglix.RepairAtWorkbench".Translate();
        }

        private static void SetIngredientsForRepair(RecipeDef r, IEnumerable<ThingDef> thingDefs)
		{
            r.ingredients.Clear();

			var ingredientCount = new IngredientCount();
            ingredientCount.SetBaseCount(1);

			foreach (var thingDef in thingDefs)
			{
				ingredientCount.filter.SetAllow(thingDef, allow: true);
			}
			ingredientCount.filter.AllowedHitPointsPercents = new FloatRange(0f, 0.99f);

			r.ingredients.Add(ingredientCount);
		}

		private static bool IsBasicProductionRecipe(RecipeDef recipeDef)
        {
            // must be a recipe that creates only one type of thing, and that
            // has an unfinished state
            if (!(recipeDef.unfinishedThingDef != null && recipeDef.products.Count == 1))
            {
                return false;
            }
            // must be a non-bulk recipe which produces exactly one of the thing
            var productAndCount = recipeDef.products.First();
            if (productAndCount == null || productAndCount.count != 1)
            {
                return false;
            }
            // the thing must be either apparel or a weapon
            var product = productAndCount.thingDef;
            return product != null && (product.IsApparel || product.IsWeapon);
        }

        private void CreateRepairRecipeForWorkbenchAndThingGroup(
            int skillDiffCategory,
            SkillDef skill,
			ThingDef workBenchDef,
			List<(RecipeDef, ThingDef)> repairablesForWorkbenchAndWg
        )
		{
            if (repairablesForWorkbenchAndWg.Count == 0) { return; }

            var logicalName = workBenchDef.defName + "_" + (skill?.defName ?? "NoSkill") + "_" + skillDiffCategory;
            string label;
            // in theory these two conditions should be identical
            if (skill == null)
            {
                label = "Repair simple items";
            }
            else switch (skillDiffCategory)
            {
	            case 0:
		            label = "Repair simple " + skill.label + " items";
		            break;
	            case 1:
		            label = "Repair complex " + skill.label + " items";
		            break;
	            case 2:
		            label = "Repair advanced " + skill.label + " items";
		            break;
	            case 3:
		            label = "Repair hyper-advanced " + skill.label + " items";
		            break;
	            default:
		            label = "Repair impossibly high-skilled (" + skillDiffCategory + ") " + skill.label + " items";
		            break;
            }

            if (repairablesForWorkbenchAndWg.Count == 0) { return; }

            //var firstRecipe = repairablesForWorkbenchAndWg
            //    .Select(x => x.Item1).Where(x => x != null).First();
            var repairRecipe = new RecipeDef
            {
                workerClass = typeof(RecipeWorker_Repair),
                defName = "RAW_RepairAt_" + logicalName,
                label = label,
                jobString = "Repairing",
                modContentPack = Content,
                displayPriority = 0,
                workAmount = 100,
                workSpeedStat = StatDefOf.GeneralLaborSpeed,//firstRecipe.workSpeedStat,
                efficiencyStat = null,//firstRecipe.efficiencyStat,
                ingredients = new List<IngredientCount>(),
                useIngredientsForColor = false,
                defaultIngredientFilter = new ThingFilter(),
                products = new List<ThingDefCountClass>(),
                targetCountAdjustment = 1,
                skillRequirements = new List<SkillRequirement>(),
                workSkill = skill,
                workSkillLearnFactor = 0.5f * (skillDiffCategory + 1),
                requiredGiverWorkType = null, //firstRecipe.requiredGiverWorkType,
                unfinishedThingDef = null,
                recipeUsers = new List<ThingDef>(),

                /*
                mechanitorOnlyRecipe = false,
                effectWorking = firstRecipe.effectWorking,
                soundWorking = firstRecipe.soundWorking,
                researchPrerequisite = null,
                memePrerequisitesAny = new List<MemeDef>(),
                researchPrerequisites = new List<ResearchProjectDef>(),
                factionPrerequisiteTags = new List<string>(),
                fromIdeoBuildingPreceptOnly = false,
                */

                description = "",
                //descriptionHyperlinks = new List<DefHyperlink>(),
            };

            if (skillDiffCategory > 0)
            {
                var skr = new SkillRequirement
                {
                    minLevel = skillDiffCategory * 4,
                    skill = skill
                };
                repairRecipe.skillRequirements.Add(skr);
            }

            SetIngredientsForRepair(repairRecipe, repairablesForWorkbenchAndWg.Select(x => x.Item2));
            repairRecipe.fixedIngredientFilter.SetAllowAll(repairRecipe.ingredients.First().filter);
            repairRecipe.defaultIngredientFilter.SetAllowAll(repairRecipe.ingredients.First().filter);
            repairRecipe.defaultIngredientFilter.SetAllow(SpecialThingFilterDefOf.AllowDeadmansApparel, false);

            repairRecipe.recipeUsers.Add(workBenchDef);
            DefDatabase<RecipeDef>.Add(repairRecipe);

            // clear the cache, since we just changed the recipes
            AllRecipesCached.SetValue(workBenchDef, null);
		}

		public void DefsLoaded()
		{
			// defs are global and immutable in gameplay, so it's safe to use them
			// as dictionary keys (as they will just be reference-compared)

			// dictionary of workbench -> workGiver -> recipe -> product
			var repairables = new Dictionary<ThingDef, (WorkGiverDef, List<(RecipeDef, ThingDef)>)>();
			var workbenchToWorkGiver = new Dictionary<ThingDef, WorkGiverDef>();

			// find every workgiver type that uses bills and operates on workbenches
			foreach (var workGiverDef in DefDatabase<WorkGiverDef>.AllDefs)
			{
				if (workGiverDef.giverClass != typeof(WorkGiver_DoBill)
				    || workGiverDef.fixedBillGiverDefs == null
				    || workGiverDef.fixedBillGiverDefs.Count <= 0) continue;
				foreach (var fixedGiver in workGiverDef.fixedBillGiverDefs)
				{
					// ensure that each workbench only has one workgiver.
					// If it has more than one, then we ignore that
					// workbench for generating repair bills later
					if (workbenchToWorkGiver.ContainsKey(fixedGiver))
					{
						repairables.Remove(fixedGiver);
						continue;
					}

					if (fixedGiver.building?.buildingTags?.Contains("Production") != true) continue;
					
					workbenchToWorkGiver.Add(fixedGiver, workGiverDef);
					if (!repairables.ContainsKey(fixedGiver))
					{
						repairables.Add(fixedGiver, (workGiverDef, new List<(RecipeDef, ThingDef)>()));
					}
					
					var repairablesForWorkbenchAndWg = repairables.TryGetValue(fixedGiver).Item2;
					repairablesForWorkbenchAndWg.AddRange(
						from recipe in fixedGiver.AllRecipes 
						where IsBasicProductionRecipe(recipe) 
						let product = recipe.products?.First()?.thingDef 
						where product != null select (recipe, product));
				}
			}

			foreach (var (workbenchDef, (wgDef, repairablesForWorkbenchAndWg)) in repairables)
			{
				if (repairablesForWorkbenchAndWg.Count == 0) { continue; }

                var hardcodedRepairables = (
                    HardcodedRepairables.TryGetValue(workbenchDef.defName, out var repairable)
                    ?  repairable
                    : new List<(string, string, int)>()
                ).Select(thingAndSkillAndSkillLevel => (
                    DefDatabase<ThingDef>.GetNamed(thingAndSkillAndSkillLevel.Item1, false),
                    DefDatabase<SkillDef>.GetNamed(thingAndSkillAndSkillLevel.Item2, false),
                    thingAndSkillAndSkillLevel.Item3
                )).Where(thingAndSkillAndSkillLevel => thingAndSkillAndSkillLevel.Item1 != null);

                var thingsBySkillAndDifficultyType = new Dictionary<(int, SkillDef), List<(RecipeDef, ThingDef)>>();
                foreach (var (recipeDef, productDef) in repairablesForWorkbenchAndWg)
                {
                    var highestSkillPair = Utils.GetHighestRequiredSkillAndValue(recipeDef);
                    var skillCategory = Utils.SkillToSkillDiffCategory(highestSkillPair.Item1);

                    var key = (skillCategory, highestSkillPair.Item2);
                    if (!thingsBySkillAndDifficultyType.ContainsKey(key))
                    {
                        thingsBySkillAndDifficultyType.Add(key, new List<(RecipeDef, ThingDef)>());
                    }
                    thingsBySkillAndDifficultyType.TryGetValue(key).Add((recipeDef, productDef));
                }

                foreach (var r in hardcodedRepairables)
                {
                    var key = (Utils.SkillToSkillDiffCategory(r.Item3), r.Item2);

                    if (!thingsBySkillAndDifficultyType.ContainsKey(key))
                    {
                        thingsBySkillAndDifficultyType.Add(key, new List<(RecipeDef, ThingDef)>());
                    }
                    thingsBySkillAndDifficultyType.TryGetValue(key).Add((null, r.Item1));
                }

                foreach (var ((skillDiffCategory, skillDef), repairablesFor) in thingsBySkillAndDifficultyType)
                {
                    Log.Message("Adding " + repairablesFor.Count + " repair tasks for " + workbenchDef.defName + ":" + (skillDef?.ToString() ?? "NoSkill") + " : " + skillDiffCategory);

                    CreateRepairRecipeForWorkbenchAndThingGroup(
                        skillDiffCategory, skillDef, workbenchDef, repairablesFor
                        );
                }

			}
		}
    }
}