using System.Collections.Generic;
using Verse;

namespace Heyra
{
    /// <summary>
    /// Data class that maps a hediff to specific body parts (by tag or label).
    /// Used by AddHediffOnRemove and AddHediffToBodyPart comps.
    /// </summary>
    public class HediffToPart
    {
        public HediffDef hediffDef;
        public BodyPartTagDef bodyPartTagDef;
        public List<string> bodyPartLable;  // sic — Bando's original spelling, kept for XML compat
        public bool changeSeverityIfAlreadyExist;
        public float severity;

        public override string ToString()
        {
            string text = "Hediff: " + hediffDef.defName;

            if (changeSeverityIfAlreadyExist)
                text += ", Will change severity if hediff already exist, Severity: " + severity;

            if (bodyPartTagDef != null)
                text += ", Body part tag def: " + bodyPartTagDef.defName;

            if (!bodyPartLable.NullOrEmpty())
            {
                text += ", Body part labels: ";
                foreach (string label in bodyPartLable)
                    text += label + ", ";
            }
            return text;
        }

        public void AddHediff(Pawn pawn)
        {
            if (pawn == null || hediffDef == null)
                return;

            // No body part specified — add to whole pawn
            if (bodyPartTagDef == null && bodyPartLable.NullOrEmpty())
            {
                if (!pawn.health.hediffSet.HasHediff(hediffDef))
                {
                    pawn.health.AddHediff(hediffDef);
                }
                else if (changeSeverityIfAlreadyExist)
                {
                    pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef).Severity += severity;
                }
                return; // Early return — don't fall through to tag/label checks
            }

            // Match by body part tag
            if (bodyPartTagDef != null)
            {
                foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                {
                    if (!part.def.tags.Contains(bodyPartTagDef))
                        continue;

                    bool alreadyHas = false;
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff.Part == part && hediff.def == hediffDef)
                        {
                            alreadyHas = true;
                            if (changeSeverityIfAlreadyExist)
                                hediff.Severity += severity;
                            break;
                        }
                    }
                    if (!alreadyHas)
                        pawn.health.AddHediff(hediffDef, part);
                }
            }

            // Match by body part label
            if (!bodyPartLable.NullOrEmpty())
            {
                foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                {
                    if (!bodyPartLable.Contains(part.Label))
                        continue;

                    bool alreadyHas = false;
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff.Part == part && hediff.def == hediffDef)
                        {
                            alreadyHas = true;
                            if (changeSeverityIfAlreadyExist)
                                hediff.Severity += severity;
                            break;
                        }
                    }
                    if (!alreadyHas)
                        pawn.health.AddHediff(hediffDef, part);
                }
            }
        }
    }
}
