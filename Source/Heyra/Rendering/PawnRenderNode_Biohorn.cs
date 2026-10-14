using System.Collections.Generic;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  BIOHORN RENDER NODE — Heyra-owned replacement for the
    //  AncotLibrary.PawnRenderNodeProperties_Race filter the biohorn
    //  HediffDefs used to depend on.
    //
    //  The biohorn hediffs render through vanilla's
    //  DynamicPawnRenderNodeSetup_Hediffs. This worker only decides
    //  whether the node may draw:
    //    - the pawn's race must be listed in <races> (real ThingDef
    //      cross-references, so a typo is a load error instead of a
    //      horn that silently never draws — the old Alien_Rong bug)
    //    - never during monsterform (the beast has its own antlers)
    //
    //  The default HAR horn addon is still hidden by
    //  Patches_HAR.TryPatchCanDrawAddon_BioHorn so the two never stack.
    // ═══════════════════════════════════════════════════════════════

    public class PawnRenderNodeProperties_Biohorn : PawnRenderNodeProperties
    {
        public List<ThingDef> races;

        public PawnRenderNodeProperties_Biohorn()
        {
            workerClass = typeof(PawnRenderNodeWorker_Biohorn);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;
            if (races.NullOrEmpty())
                yield return "PawnRenderNodeProperties_Biohorn has no <races>; the horn would never draw.";
        }
    }

    public class PawnRenderNodeWorker_Biohorn : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            Pawn pawn = parms.pawn;
            if (!(node.Props is PawnRenderNodeProperties_Biohorn props) || props.races == null || !props.races.Contains(pawn.def))
                return false;

            return !pawn.InMonsterform();
        }
    }
}
