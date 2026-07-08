using System.Collections.Generic;
using Verse;

namespace Heyra
{
    /// <summary>
    /// Data class that maps a hediff to specific body parts (by def, tag, or label).
    /// Used by AddHediffOnRemove and AddHediffToBodyPart comps.
    ///
    /// Prefer bodyPartDefs (defName matching) — it is locale-independent.
    /// bodyPartLable matches part.Label, which is a TRANSLATED string: on any
    /// non-English client (e.g. our own ChineseSimplified translation renames
    /// "left hand" to 左手) label entries silently fail to match. Kept only for
    /// backward compatibility with existing XML and third-party patches.
    /// </summary>
    public class HediffToPart
    {
        public HediffDef hediffDef;
        public BodyPartTagDef bodyPartTagDef;
        public List<BodyPartDef> bodyPartDefs;
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

            if (!bodyPartDefs.NullOrEmpty())
            {
                text += ", Body part defs: ";
                foreach (BodyPartDef def in bodyPartDefs)
                    text += def.defName + ", ";
            }

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
            if (bodyPartTagDef == null && bodyPartDefs.NullOrEmpty() && bodyPartLable.NullOrEmpty())
            {
                if (!pawn.health.hediffSet.HasHediff(hediffDef))
                {
                    pawn.health.AddHediff(hediffDef);
                }
                else if (changeSeverityIfAlreadyExist)
                {
                    pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef).Severity += severity;
                }
                return; // Early return — don't fall through to def/tag/label checks
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                bool matches =
                    (bodyPartTagDef != null && part.def.tags.Contains(bodyPartTagDef)) ||
                    (!bodyPartDefs.NullOrEmpty() && bodyPartDefs.Contains(part.def)) ||
                    (!bodyPartLable.NullOrEmpty() && bodyPartLable.Contains(part.Label));

                if (matches)
                    AddToPart(pawn, part);
            }
        }

        /// <summary>
        /// Adds the hediff to one part, or bumps severity if it's already there.
        /// </summary>
        private void AddToPart(Pawn pawn, BodyPartRecord part)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.Part == part && hediff.def == hediffDef)
                {
                    if (changeSeverityIfAlreadyExist)
                        hediff.Severity += severity;
                    return;
                }
            }
            pawn.health.AddHediff(hediffDef, part);
        }
    }
}
