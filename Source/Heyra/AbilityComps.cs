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
    //  Toggle OFF: remove hediff → restore gear from inventory
    //
    //  Hediff removal triggers all cleanup automatically:
    //    - HediffComp_ChangeForm.CompPostPostRemoved (VFX, work priorities, render tree)
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

                // Hediff removal already fired all cleanup comps (de-transform VFX,
                // fatigue, work priorities, sub-hediff strip, render tree dirty).
                // Now restore gear that was stashed to inventory on transform.
                RestoreGearFromInventory(pawn);
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

            // Dramatic lightning strike VFX
            if (pawn.Spawned)
            {
                Find.CurrentMap.weatherManager.eventHandler.AddEvent(
                    new WeatherEvent_HarmlessLightningStrike(pawn.Map, pawn.Position));
            }
        }

        /// <summary>
        /// Moves apparel and weapons from inventory back to worn/equipped slots.
        /// Called on toggle-off to re-dress the pawn after reverting from beast form.
        /// </summary>
        private void RestoreGearFromInventory(Pawn pawn)
        {
            if (pawn.inventory?.innerContainer == null) return;

            // Snapshot items first — can't modify collection while iterating
            List<Apparel> toWear = new List<Apparel>();
            List<ThingWithComps> toEquip = new List<ThingWithComps>();

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (thing is Apparel apparel)
                    toWear.Add(apparel);
                else if (thing is ThingWithComps twc && twc.def.IsWeapon)
                    toEquip.Add(twc);
            }

            // Re-wear all apparel (conflicts go back to inventory, not dropped)
            foreach (Apparel apparel in toWear)
            {
                if (pawn.inventory.innerContainer.Contains(apparel))
                {
                    pawn.inventory.innerContainer.Remove(apparel);
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                }
            }

            // Re-equip weapons (primary slot only — extras stay in inventory)
            foreach (ThingWithComps weapon in toEquip)
            {
                if (pawn.inventory.innerContainer.Contains(weapon))
                {
                    if (pawn.equipment.Primary == null)
                    {
                        pawn.inventory.innerContainer.Remove(weapon);
                        pawn.equipment.AddEquipment(weapon);
                    }
                }
            }
        }
    }
}
