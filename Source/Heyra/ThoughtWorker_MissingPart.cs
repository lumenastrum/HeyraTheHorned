using RimWorld;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  ThoughtWorker_MissingPart — port of MoHAR's
    //  MoharThoughts.RaceBPHediff as Bando used it for
    //  Heyra_Missinghornthought: a pawn of `race` whose `bodyPart` is
    //  missing feels it, unless an added part replaced it. The MoHAR
    //  strip in Feb 2026 left the ThoughtDef with no worker, so the
    //  "Horns missing" mood never fired.
    // ═══════════════════════════════════════════════════════════
    public class MissingPartThoughtExtension : DefModExtension
    {
        public ThingDef race;
        public BodyPartDef bodyPart;
        public bool ignoreIfAddedPart = true;
    }

    public class ThoughtWorker_MissingPart : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            MissingPartThoughtExtension ext = def.GetModExtension<MissingPartThoughtExtension>();
            if (ext == null || ext.bodyPart == null || (ext.race != null && p.def != ext.race)) return false;

            HediffSet set = p.health.hediffSet;
            foreach (Hediff h in set.hediffs)
            {
                if (h.def != HediffDefOf.MissingBodyPart || h.Part?.def != ext.bodyPart) continue;
                if (ext.ignoreIfAddedPart && set.PartOrAnyAncestorHasDirectlyAddedParts(h.Part)) continue;
                return true;
            }
            return false;
        }
    }
}
