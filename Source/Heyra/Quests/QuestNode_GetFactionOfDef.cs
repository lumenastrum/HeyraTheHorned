using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  QUEST FACTION BY DEF — Heyra-owned replacement for
    //  VEF.Storyteller.QuestNode_GetFaction, used by the Rong lab quest
    //  (Script_Lab.xml) to put the site under Heyra_Rong_SubFaction.
    //
    //  Same behaviour as the VEF node:
    //    - RunInt: if <factionDef> exists in the world, use it (no filter
    //      check, as VEF did); otherwise fall back to vanilla's normal
    //      filtered pick (allowEnemy, mustBePermanentEnemy, ...).
    //    - TestRunInt: vanilla's, unchanged (VEF ignored factionDef there too).
    //  Vanilla's QuestNode_GetFaction carries every filter field, so we
    //  only add factionDef.
    // ═══════════════════════════════════════════════════════════════

    public class QuestNode_GetFactionOfDef : QuestNode_GetFaction
    {
        public SlateRef<FactionDef> factionDef;

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            FactionDef def = factionDef.GetValue(slate);
            Faction faction = def != null ? Find.FactionManager.FirstFactionOfDef(def) : null;
            if (faction == null)
            {
                base.RunInt();
                return;
            }

            slate.Set(storeAs.GetValue(slate), faction);
            if (!faction.Hidden)
            {
                var involved = new QuestPart_InvolvedFactions();
                involved.factions.Add(faction);
                QuestGen.quest.AddPart(involved);
            }
        }
    }
}
