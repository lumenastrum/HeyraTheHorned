using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════
    //  CompProperties_AbilityChangeForm
    // ═══════════════════════════════════════════════════
    public class CompProperties_AbilityChangeForm : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;             // Hediff to apply (toggle-on) or remove (toggle-off)
        public bool dropAllApparels = true;
        public bool dropAllEquipments = true;
        public bool unequipAllApparels;
        public bool unequipAllEquipments;

        public CompProperties_AbilityChangeForm()
        {
            compClass = typeof(CompAbilityEffect_ChangeForm);
        }
    }

    // ═══════════════════════════════════════════════════
    //  CompAbilityEffect_ChangeForm — toggleable transformation
    //
    //  Toggle ON:  stash gear → apply hediff → lightning VFX
    //  Toggle OFF: remove hediff (cleanup comps do the rest)
    //
    //  Hediff removal triggers all cleanup automatically:
    //    - HediffComp_ChangeForm.CompPostPostRemoved (VFX, work priorities,
    //      render tree, gear restore — so timer/downed removal restores too)
    //    - HediffComp_AddHediffOnRemove (post-fatigue)
    //    - HediffComp_AddHediffToBodyPart.CompPostPostRemoved (claws, breath cleanup)
    // ═══════════════════════════════════════════════════
    public class CompAbilityEffect_ChangeForm : CompAbilityEffect
    {
        public new CompProperties_AbilityChangeForm Props => (CompProperties_AbilityChangeForm)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;

            // ── TOGGLE OFF: already in beast form → revert to normal ──
            if (pawn.InMonsterform())
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                if (hediff != null)
                    pawn.health.RemoveHediff(hediff);

                // Hediff removal fires all cleanup comps (de-transform VFX, fatigue,
                // work priorities, sub-hediff strip, render tree dirty) — including
                // gear restore in HediffComp_ChangeForm.CompPostPostRemoved, which
                // also covers timer-expiry and downed-revert removal paths.
                return;
            }

            // ── TOGGLE ON: not in beast form → transform ──

            // Drop apparel (if configured)
            if (Props.dropAllApparels && !pawn.apparel.WornApparel.NullOrEmpty())
            {
                pawn.apparel.DropAll(pawn.Position, true, true);
            }

            // Drop equipment (if configured)
            if (Props.dropAllEquipments && pawn.equipment != null)
            {
                pawn.equipment.DropAllEquipment(pawn.Position, true, false);
            }

            // Stash apparel to inventory instead of dropping
            if (Props.unequipAllApparels && !pawn.apparel.WornApparel.NullOrEmpty())
            {
                pawn.apparel.GetDirectlyHeldThings()
                    .TryTransferAllToContainer(pawn.inventory.innerContainer, true);
            }

            // Stash equipment to inventory
            if (Props.unequipAllEquipments && pawn.equipment != null)
            {
                pawn.equipment.GetDirectlyHeldThings()
                    .TryTransferAllToContainer(pawn.inventory.innerContainer, true);
            }

            // Apply monsterform hediff with timed duration from Ability_Duration stat
            Hediff newHediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
            HediffComp_Disappears disappears = newHediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                float durationSeconds = parent.def.statBases
                    .GetStatValueFromList(StatDefOf.Ability_Duration, 90f);
                disappears.ticksToDisappear = (int)(durationSeconds * 60f);
            }
            pawn.health.AddHediff(newHediff);

            // Dramatic lightning strike VFX — on the PAWN's map, not the viewed one
            // (Find.CurrentMap diverges from pawn.Map with multiple colonies)
            if (pawn.Spawned)
            {
                pawn.Map.weatherManager.eventHandler.AddEvent(
                    new WeatherEvent_HarmlessLightningStrike(pawn.Map, pawn.Position));
            }
        }

        // Gear restore lives in HeyraUtility.RestoreGearFromInventory, invoked by
        // HediffComp_ChangeForm.CompPostPostRemoved on every removal path.
    }
}
