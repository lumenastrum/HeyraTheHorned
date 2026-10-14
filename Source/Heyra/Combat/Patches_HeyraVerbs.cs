using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  ATTACK VERB SELECTION — vanilla never offers hediff ranged verbs
    //  as a pawn's attack (MVCF patched that in). Scoped to pawns that
    //  carry a HediffComp_HeyraVerbs: when the pawn has no usable
    //  primary weapon, its command verb (the breath) is the attack for
    //  anything in its range band; closer than minRange it keeps melee.
    //  Covers drafted auto-attack (CurrentEffectiveVerb → TryGetAttackVerb(null))
    //  and NPC AI (JobGiver_AIFightEnemy → TryGetAttackVerb(enemy)).
    // ═══════════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryGetAttackVerb))]
    public static class Patch_Pawn_TryGetAttackVerb_HeyraVerbs
    {
        public static void Postfix(Pawn __instance, Thing target, ref Verb __result)
        {
            if (__result != null && !__result.verbProps.IsMeleeAttack) return;   // a real ranged weapon wins
            Verb breath = CommandVerbOf(__instance);
            if (breath == null || !breath.Available()) return;
            if (target != null)
            {
                float d = __instance.Position.DistanceTo(target.Position);
                if (d < breath.verbProps.minRange || d > breath.verbProps.range) return;
            }
            __result = breath;
        }

        public static Verb CommandVerbOf(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is HediffWithComps hw)) continue;
                for (int j = 0; j < hw.comps.Count; j++)
                    if (hw.comps[j] is HediffComp_HeyraVerbs hv && hv.CommandVerb is Verb v)
                        return v;
            }
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FORCED-MISS GUARD — vanilla VerbProperties.GetForceMissFactorFor
    //  dereferences equipment.def, so any verb with forcedMissRadius and
    //  no weapon (a hediff verb: the breath, forcedMissRadius 1.9) threw
    //  every cast. MVCF covered this. With no equipment it can't be a
    //  mortar, so vanilla's own answer is 1.
    // ═══════════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.GetForceMissFactorFor))]
    public static class Patch_ForceMissFactor_NoEquipment
    {
        public static bool Prefix(Thing equipment, ref float __result)
        {
            if (equipment != null) return true;
            __result = 1f;
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  BRAWLER TOLERANCE — the gunblade is a melee weapon with an
    //  integrated cannon; Brawlers shouldn't sulk about it (was MVCF's
    //  brawlerCaresAbout=false). Weapons opt in with HeyraBrawlerFriendly.
    // ═══════════════════════════════════════════════════════════════

    public class HeyraBrawlerFriendly : DefModExtension { }

    [HarmonyPatch(typeof(ThoughtWorker_IsCarryingRangedWeapon), "CurrentStateInternal")]
    public static class Patch_BrawlerUnhappy_HeyraFriendly
    {
        public static void Postfix(Pawn p, ref ThoughtState __result)
        {
            if (__result.Active && p.equipment?.Primary?.def.HasModExtension<HeyraBrawlerFriendly>() == true)
                __result = ThoughtState.Inactive;
        }
    }

    [HarmonyPatch(typeof(Alert_BrawlerHasRangedWeapon), "BrawlersWithRangedWeapon", MethodType.Getter)]
    public static class Patch_BrawlerAlert_HeyraFriendly
    {
        public static void Postfix(List<Pawn> __result)
        {
            __result?.RemoveAll(p => p.equipment?.Primary?.def.HasModExtension<HeyraBrawlerFriendly>() == true);
        }
    }
}
