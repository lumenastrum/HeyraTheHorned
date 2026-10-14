using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  STARTING GOODWILL — Heyra-owned replacement for
    //  VEF.Factions.FactionDefExtension.startingGoodwillByFactionDefs
    //  (Heyra_TradFaction's starting relations in Heyra_Factions.xml).
    //
    //  Same rules as VEF's TryMakeInitialRelationsWith postfix:
    //    - runs after HAR, only when a new faction pair first meets
    //      (new world / new faction / relation reset), never on save load
    //      (vanilla only calls TryMakeInitialRelationsWith from
    //      FactionGenerator and FactionUtility.ResetAllFactionRelations)
    //    - if either side lists the other, take the stricter range
    //      (lower min, lower max), roll once, apply to both sides
    //    - kind from the roll: <= -10 hostile, -9..74 neutral, 75+ ally
    //  Our extension is its own type, so VEF (if installed for other
    //  mods) never sees it and can't apply it twice.
    // ═══════════════════════════════════════════════════════════════

    public class FactionGoodwillExtension : DefModExtension
    {
        public List<StartingGoodwill> startingGoodwillByFactionDefs = new List<StartingGoodwill>();

        public StartingGoodwill For(FactionDef other)
        {
            return startingGoodwillByFactionDefs?.Find(x => x.factionDef == other);
        }
    }

    /// <summary>XML: &lt;PlayerColony&gt;-75~-50&lt;/PlayerColony&gt;</summary>
    public class StartingGoodwill
    {
        public FactionDef factionDef;
        public IntRange range;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot.ChildNodes.Count != 1)
            {
                Log.Error("[Heyra] Misconfigured starting goodwill: " + xmlRoot.OuterXml);
                return;
            }
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, nameof(factionDef), xmlRoot.Name);
            range = ParseHelper.FromString<IntRange>(xmlRoot.FirstChild.Value);
        }
    }

    [HarmonyPatch(typeof(Faction), "TryMakeInitialRelationsWith")]
    public static class Patch_Faction_StartingGoodwill
    {
        // Vanilla only creates the relation when none exists yet; only then do we roll.
        // (VEF's postfix rolled on every call, so FactionUtility.ResetAllFactionRelations,
        // which calls both directions, rolled each pair twice.)
        public static void Prefix(Faction __instance, Faction other, out bool __state)
        {
            __state = __instance.RelationWith(other, allowNull: true) == null;
        }

        [HarmonyAfter("rimworld.erdelf.alien_race.main")]
        public static void Postfix(Faction __instance, Faction other, bool __state)
        {
            if (!__state)
                return;

            StartingGoodwill mine = __instance.def.GetModExtension<FactionGoodwillExtension>()?.For(other.def);
            StartingGoodwill theirs = other.def.GetModExtension<FactionGoodwillExtension>()?.For(__instance.def);
            if (mine == null && theirs == null)
                return;

            int min = Stricter(mine?.range.min, theirs?.range.min);
            int max = Stricter(mine?.range.max, theirs?.range.max);
            int goodwill = Rand.RangeInclusive(min, max);
            FactionRelationKind kind = goodwill <= -10 ? FactionRelationKind.Hostile
                : goodwill < 75 ? FactionRelationKind.Neutral
                : FactionRelationKind.Ally;

            FactionRelation a = __instance.RelationWith(other);
            a.baseGoodwill = goodwill;
            a.kind = kind;
            FactionRelation b = other.RelationWith(__instance);
            b.baseGoodwill = goodwill;
            b.kind = kind;
        }

        private static int Stricter(int? x, int? y)
        {
            if (x.HasValue && y.HasValue) return x.Value < y.Value ? x.Value : y.Value;
            return x ?? y ?? 0;
        }
    }
}
