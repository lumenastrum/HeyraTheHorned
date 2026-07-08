using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════
    //  HediffComp_ChangeForm — core transformation behavior
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_ChangeForm : HediffComp
    {
        private Dictionary<WorkTypeDef, int> workPriorities = new Dictionary<WorkTypeDef, int>();

        public HediffCompProperties_ChangeForm Props => (HediffCompProperties_ChangeForm)props;
        public Vector2 DrawSize => Props.customDrawSize;
        public GraphicData NakedGraphic => Props.graphic;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            SaveDisabledWorkPriorities();
            parent.pawn.Notify_DisabledWorkTypesChanged();

            // Force render tree to re-resolve with beast graphic
            parent.pawn.DirtyPawnRenderTree();
        }

        public override void CompPostPostRemoved()
        {
            Pawn pawn = parent.pawn;

            pawn.Notify_DisabledWorkTypesChanged();
            RestoreWorkPriorities();

            // Restore stashed gear here, NOT in the ability's toggle-off branch:
            // the hediff can also end via timer (HediffComp_Disappears) or
            // removeOnDowned, and those paths must re-dress the pawn too.
            pawn.RestoreGearFromInventory();

            // Force render tree to re-resolve back to normal graphic
            pawn.DirtyPawnRenderTree();

            // Interrupt current job
            if (pawn.jobs?.curJob != null)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);

            // De-transform VFX: smoke + sparks + glow
            if (pawn.Spawned)
            {
                Vector3 pos = pawn.Position.ToVector3Shifted();
                for (int i = 0; i < 4; i++)
                {
                    FleckMaker.ThrowSmoke(pos, pawn.Map, 1.5f);
                    FleckMaker.ThrowMicroSparks(pos, pawn.Map);
                    FleckMaker.ThrowLightningGlow(pos, pawn.Map, 1.5f);
                }
            }

            // Play removal sound
            if (Props.removeSound != null && pawn.Spawned)
            {
                Props.removeSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map)));
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Props.removeOnDowned && parent.pawn.health.Downed)
            {
                parent.pawn.health.RemoveHediff(parent);
            }
        }

        public override void CompExposeData()
        {
            Scribe_Collections.Look(ref workPriorities, "HediffComp_ChangeForm.workPriorities", LookMode.Def, LookMode.Value);
            base.CompExposeData();
        }

        private void SaveDisabledWorkPriorities()
        {
            if (parent.pawn.workSettings == null) return;
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if ((workType.workTags & parent.CurStage.disabledWorkTags) != 0)
                {
                    workPriorities[workType] = parent.pawn.workSettings.GetPriority(workType);
                }
            }
        }

        private void RestoreWorkPriorities()
        {
            if (workPriorities.NullOrEmpty() || parent.pawn.Dead || parent.pawn.workSettings == null)
                return;

            foreach (var kvp in workPriorities)
            {
                parent.pawn.workSettings.SetPriority(kvp.Key, kvp.Value);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HediffComp_DirectionalEffecters — flame hair per facing
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_DirectionalEffecters : HediffComp
    {
        public HediffCompProperties_DirectionalEffecters Props =>
            (HediffCompProperties_DirectionalEffecters)props;

        public EffecterDef CurrentDirectionalEffecter()
        {
            Rot4 rot = parent.pawn.Rotation;

            if (rot == Rot4.West)  return Props.effecterWest;
            if (rot == Rot4.East)  return Props.effecterEast;
            if (rot == Rot4.North) return Props.effecterNorth;
            if (rot == Rot4.South) return Props.effecterSouth;

            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HediffComp_AddHediffOnRemove — post-fatigue on de-transform
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_AddHediffOnRemove : HediffComp
    {
        public HediffCompProperties_AddHediffOnRemove Props =>
            (HediffCompProperties_AddHediffOnRemove)props;

        public override void CompPostPostRemoved()
        {
            if (parent.pawn.Dead) return;

            foreach (HediffToPart htp in Props.hediffsToParts)
            {
                htp.AddHediff(parent.pawn);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HediffComp_AddHediffToBodyPart — claws/breath on transform
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_AddHediffToBodyPart : HediffComp
    {
        public HediffCompProperties_AddHediffToBodyPart Props =>
            (HediffCompProperties_AddHediffToBodyPart)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            foreach (HediffToPart htp in Props.hediffsToParts)
            {
                htp.AddHediff(parent.pawn);
            }
        }

        public override void CompPostPostRemoved()
        {
            // Strip all hediffs that were added by this comp
            foreach (HediffToPart htp in Props.hediffsToParts)
            {
                while (parent.pawn.health.hediffSet.HasHediff(htp.hediffDef))
                {
                    parent.pawn.health.RemoveHediff(
                        parent.pawn.health.hediffSet.GetFirstHediffOfDef(htp.hediffDef));
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HediffComp_DispelAllHediffsOnMap — AoE de-transform
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_DispelAllHediffsOnMap : HediffComp
    {
        public HediffCompProperties_DispelAllHediffsOnMap Props =>
            (HediffCompProperties_DispelAllHediffsOnMap)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (!parent.pawn.Spawned) return;

            foreach (Pawn other in parent.pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (other == parent.pawn || other.Faction == null)
                    continue;

                if (Props.dispelPlayerPawns && other.Faction.IsPlayer)
                    DispelHediffs(other);

                if (Props.dispelHostilePawns && !other.Faction.IsPlayer &&
                    other.Faction.HostileTo(Faction.OfPlayer))
                    DispelHediffs(other);

                if (Props.dispelNonHostilePawns && !other.Faction.IsPlayer &&
                    !other.Faction.HostileTo(Faction.OfPlayer))
                    DispelHediffs(other);
            }
        }

        public void DispelHediffs(Pawn pawn)
        {
            foreach (HediffDef hDef in Props.hediffDefs)
            {
                while (pawn.health.hediffSet.HasHediff(hDef))
                {
                    pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(hDef));
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HediffComp_PlayEffecter — plays effecter while hediff active
    //  Replaces MoHAR InnerShine for spark ring visuals.
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_PlayEffecter : HediffComp
    {
        public HediffCompProperties_PlayEffecter Props =>
            (HediffCompProperties_PlayEffecter)props;

        private Effecter effecter;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!parent.pawn.Spawned) return;

            if (effecter == null)
                effecter = Props.effecterDef.Spawn();

            effecter.EffectTick(parent.pawn, parent.pawn);
        }

        public override void CompPostPostRemoved()
        {
            effecter?.Cleanup();
            effecter = null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HediffComp_RemoveIfHediffNotExists — cleanup comp
    // ═══════════════════════════════════════════════════════════
    public class HediffComp_RemoveIfHediffNotExists : HediffComp
    {
        public HediffCompProperties_RemoveIfHediffNotExists Props =>
            (HediffCompProperties_RemoveIfHediffNotExists)props;

        public override bool CompShouldRemove =>
            !parent.pawn.health.hediffSet.HasHediff(Props.hediffDef);
    }
}
