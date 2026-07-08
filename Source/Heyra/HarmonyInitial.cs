using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Heyra
{
    [StaticConstructorOnStartup]
    public static class HarmonyInitial
    {
        private const string HarmonyId = "Foxed.HeyraTheHorned";
        private const string LogPrefix = "[Heyra] ";

        static HarmonyInitial()
        {
            var harmony = new Harmony(HarmonyId);

            // ═══════════════════════════════════════
            //  PHASE 1: Stable attribute-based patches
            //  (Patches_Stable.cs — auto-detected by PatchAll)
            // ═══════════════════════════════════════
            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message(LogPrefix + "Stable patches applied.");
            }
            catch (Exception e)
            {
                Log.Error(LogPrefix + $"PatchAll failed: {e}");
            }

            // ═══════════════════════════════════════
            //  PHASE 2: Manual CanEquip postfix
            //  (prevents equipping apparel/weapons in monsterform)
            // ═══════════════════════════════════════
            try
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(EquipmentUtility), "CanEquip",
                        new Type[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(HarmonyInitial), nameof(CanEquipPostfix)));
                Log.Message(LogPrefix + "CanEquip postfix applied.");
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"CanEquip patch failed: {e.Message}");
            }

            // ═══════════════════════════════════════
            //  PHASE 3: Critical render patches (manual, with fallback)
            // ═══════════════════════════════════════
            int renderCount = 0;
            if (Patches_Render.TryPatchGraphicSwap(harmony))   renderCount++;
            if (Patches_Render.TryPatchBodyGraphic(harmony))   renderCount++;
            if (Patches_Render.TryPatchHeadSuppression(harmony)) renderCount++;
            if (Patches_Render.TryPatchMeshSetFor(harmony))    renderCount++;
            if (Patches_Render.TryPatchBodyOffset(harmony))    renderCount++;
            if (Patches_Render.TryPatchForceShowBody(harmony)) renderCount++;
            if (Patches_Render.TryPatchSuppressEyes(harmony))  renderCount++;

            Log.Message(LogPrefix + $"Render patches: {renderCount}/7 applied.");

            if (renderCount < 2)
            {
                Log.Error(LogPrefix + $"Only {renderCount}/7 render patches applied — visual transformation may not work! " +
                          "This likely means 1.6 PawnRenderer API has changed. Check the mod page for updates.");
            }

            // ═══════════════════════════════════════
            //  PHASE 4: HAR interop patches (manual, graceful)
            // ═══════════════════════════════════════
            int harCount = 0;
            if (Patches_HAR.TryPatchCanDrawAddon(harmony))  harCount++;
            if (Patches_HAR.TryPatchDrawSize(harmony))      harCount++;
            if (Patches_HAR.TryPatchCanDrawAddon_BioHorn(harmony)) harCount++;

            Log.Message(LogPrefix + $"HAR patches: {harCount}/3 applied.");

            // ═══════════════════════════════════════
            //  PHASE 5: Other risky patches (manual)
            // ═══════════════════════════════════════
            // OptimizeApparel — TryGiveJob is non-public in 1.6
            try
            {
                var tryGiveJob = AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob");
                if (tryGiveJob != null)
                {
                    harmony.Patch(tryGiveJob,
                        prefix: new HarmonyMethod(typeof(Patch_OptimizeApparel_TryGiveJob), nameof(Patch_OptimizeApparel_TryGiveJob.Prefix)));
                    Log.Message(LogPrefix + "OptimizeApparel patch applied.");
                }
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + $"OptimizeApparel patch failed: {e.Message}");
            }
            Patches_Risky.TryPatchStagger(harmony);
            Patches_Risky.TryPatchDisabledWorkTypes(harmony);

            Log.Message(LogPrefix + "Initialization complete.");
        }

        /// <summary>
        /// Prevents pawns in monsterform from equipping apparel/weapons.
        /// </summary>
        public static void CanEquipPostfix(ref bool __result, Pawn pawn, Thing thing, ref string cantReason)
        {
            if (!__result || pawn?.abilities == null) return;
            if (!pawn.InMonsterform()) return;

            var ability = pawn.abilities.GetAbility(HeyraAbilityDefOf.Heyra_Ability_Formchange);
            if (ability == null) return;

            var changeFormComp = ability.CompOfType<CompAbilityEffect_ChangeForm>();
            if (changeFormComp == null) return;

            bool blocksApparel = changeFormComp.Props.dropAllApparels || changeFormComp.Props.unequipAllApparels;
            bool blocksWeapons = changeFormComp.Props.dropAllEquipments || changeFormComp.Props.unequipAllEquipments;

            if (thing.def.IsApparel && blocksApparel)
            {
                cantReason = "PawnInChangedFormApparel".Translate();
                __result = false;
            }
            else if (thing.def.IsWeapon && blocksWeapons)
            {
                cantReason = "PawnInChangedFormWeapon".Translate();
                __result = false;
            }
        }
    }
}
