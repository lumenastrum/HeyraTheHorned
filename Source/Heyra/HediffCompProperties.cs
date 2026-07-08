using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════
    //  ChangeForm — transformation visual/behavior
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_ChangeForm : HediffCompProperties
    {
        public GraphicData graphic;
        public Vector2 customDrawSize = Vector2.one;
        public Vector2 floatOffset = Vector2.zero;
        public bool removeOnDowned = true;
        public SoundDef removeSound;
        public bool immuneToStagger;

        public HediffCompProperties_ChangeForm()
        {
            compClass = typeof(HediffComp_ChangeForm);
        }
    }

    // ═══════════════════════════════════════════════
    //  DirectionalEffecters — flame hair per facing
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_DirectionalEffecters : HediffCompProperties
    {
        public EffecterDef effecterWest;
        public EffecterDef effecterEast;
        public EffecterDef effecterNorth;
        public EffecterDef effecterSouth;

        public HediffCompProperties_DirectionalEffecters()
        {
            compClass = typeof(HediffComp_DirectionalEffecters);
        }
    }

    // ═══════════════════════════════════════════════
    //  AddHediffOnRemove — post-fatigue on transform end
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_AddHediffOnRemove : HediffCompProperties
    {
        public List<HediffToPart> hediffsToParts;

        public HediffCompProperties_AddHediffOnRemove()
        {
            compClass = typeof(HediffComp_AddHediffOnRemove);
        }
    }

    // ═══════════════════════════════════════════════
    //  AddHediffToBodyPart — claws/breath on transform
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_AddHediffToBodyPart : HediffCompProperties
    {
        public List<HediffToPart> hediffsToParts;

        public HediffCompProperties_AddHediffToBodyPart()
        {
            compClass = typeof(HediffComp_AddHediffToBodyPart);
        }
    }

    // ═══════════════════════════════════════════════
    //  DispelAllHediffsOnMap — remove transform from others
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_DispelAllHediffsOnMap : HediffCompProperties
    {
        public List<HediffDef> hediffDefs;
        public bool dispelPlayerPawns;
        public bool dispelNonHostilePawns;
        public bool dispelHostilePawns;

        public HediffCompProperties_DispelAllHediffsOnMap()
        {
            compClass = typeof(HediffComp_DispelAllHediffsOnMap);
        }
    }

    // ═══════════════════════════════════════════════
    //  PlayEffecter — continuous effecter on hediff
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_PlayEffecter : HediffCompProperties
    {
        public EffecterDef effecterDef;

        public HediffCompProperties_PlayEffecter()
        {
            compClass = typeof(HediffComp_PlayEffecter);
        }
    }

    // ═══════════════════════════════════════════════
    //  RemoveIfHediffNotExists — cleanup comp
    // ═══════════════════════════════════════════════
    public class HediffCompProperties_RemoveIfHediffNotExists : HediffCompProperties
    {
        public HediffDef hediffDef;

        public HediffCompProperties_RemoveIfHediffNotExists()
        {
            compClass = typeof(HediffComp_RemoveIfHediffNotExists);
        }
    }

}
