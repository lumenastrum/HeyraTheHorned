using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  THOUGHT SUPPRESSION — prevents "Naked" mood during transform
    // ═══════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought))]
    public static class Patch_CanGetThought
    {
        [HarmonyPostfix]
        static void Postfix(Pawn pawn, ThoughtDef def, ref bool __result)
        {
            if (__result && pawn.InMonsterform() && def == ThoughtDefOf.Naked)
                __result = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  IDEOLOGY PRECEPT — suppress "uncovered" thoughts in monsterform
    // ═══════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(ThoughtWorker_Precept_GroinUncovered), "HasUncoveredGroin")]
    public static class Patch_ThoughtWorker_Precept_GroinUncovered
    {
        [HarmonyPostfix]
        static void Postfix(Pawn p, ref bool __result)
        {
            if (p.InMonsterform()) __result = false;
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_Precept_GroinOrChestUncovered), "HasUncoveredGroinOrChest")]
    public static class Patch_ThoughtWorker_Precept_GroinOrChestUncovered
    {
        [HarmonyPostfix]
        static void Postfix(Pawn p, ref bool __result)
        {
            if (p.InMonsterform()) __result = false;
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_Precept_GroinChestOrHairUncovered), "HasUncoveredGroinChestOrHair")]
    public static class Patch_ThoughtWorker_Precept_GroinChestOrHairUncovered
    {
        [HarmonyPostfix]
        static void Postfix(Pawn p, ref bool __result)
        {
            if (p.InMonsterform()) __result = false;
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_Precept_GroinChestHairOrFaceUncovered), "HasUncoveredGroinChestHairOrFace")]
    public static class Patch_ThoughtWorker_Precept_GroinChestHairOrFaceUncovered
    {
        [HarmonyPostfix]
        static void Postfix(Pawn p, ref bool __result)
        {
            if (p.InMonsterform()) __result = false;
        }
    }

// Applied manually in HarmonyInitial — TryGiveJob is non-public in 1.6
// [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.TryGiveJob))]
public static class Patch_OptimizeApparel_TryGiveJob
{
    public static bool Prefix(ref Job __result, Pawn pawn)
    {
        if (pawn.InMonsterform())
        {
            __result = null;
            return false;
        }
        return true;
    }
}

    // ═══════════════════════════════════════════════════════════
    //  MENTAL STATE — Totem flesh type immune to Manhunter
    // ═══════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(CompAbilityEffect_GiveMentalState), nameof(CompAbilityEffect_GiveMentalState.Apply))]
    public static class Patch_GiveMentalState_Apply
    {
        [HarmonyPrefix]
        static bool Prefix(CompAbilityEffect_GiveMentalState __instance,
                           LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn targetPawn;
            if (!__instance.Props.applyToSelf)
            {
                targetPawn = target.Thing as Pawn;
            }
            else
            {
                targetPawn = __instance.parent.pawn;
            }

            if (targetPawn != null &&
                __instance.Props.stateDef == MentalStateDefOf.Manhunter &&
                targetPawn.RaceProps.FleshType == HeyraFleshTypeDefOf.Heyra_TotemFleshType)
            {
                return false;  // Totems can't be driven manhunter
            }
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  EFFECTER DIRECTION — swap effecter def based on facing
    // ═══════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(HediffComp_Effecter), nameof(HediffComp_Effecter.CurrentStateEffecter))]
    public static class Patch_CurrentStateEffecter
    {
        [HarmonyPostfix]
        static void Postfix(HediffComp_Effecter __instance, ref EffecterDef __result)
        {
            var dirComp = __instance.parent.TryGetComp<HediffComp_DirectionalEffecters>();
            if (dirComp != null)
            {
                __result = dirComp.CurrentDirectionalEffecter();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ABILITY VERB WIRING FIX — ensures Verb_CastAbility.ability
    //  is set before targeting mode starts.
    //
    //  Root cause: Ability.Initialize() sets verb.ability = this
    //  via IAbilityVerb, but something in the lifecycle clears or
    //  replaces it (likely VerbTracker recreation during save/load
    //  or lazy re-initialization). Self-targeting abilities (like
    //  Electricfield) never hit this because DrawHighlight/
    //  ValidateTarget are skipped for targetRequired=false.
    //
    //  This safety net re-wires the connection each time gizmos
    //  are collected — runs before the user can click into
    //  targeting mode. Exactly mirrors what Initialize() does.
    // ═══════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_EnsureVerbAbilityWired
    {
        private static bool logged;

        [HarmonyPrefix]
        static void Prefix(Ability __instance)
        {
            Verb verb = __instance.VerbTracker?.PrimaryVerb;
            if (verb is Verb_CastAbility vca && vca.ability == null)
            {
                vca.ability = __instance;
                if (!logged)
                {
                    Log.Warning("[Heyra] Fixed null ability reference on verb for "
                        + (__instance.def?.defName ?? "unknown")
                        + ". This is a safety fix — ability system did not wire verb.ability.");
                    logged = true;
                }
            }
        }
    }
}
