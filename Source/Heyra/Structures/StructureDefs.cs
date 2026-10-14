using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  STRUCTURE LAYOUTS — Heyra-owned replacement for KCSG's
    //  StructureLayoutDef / SymbolDef (VEF), phase 4 of the VEF removal.
    //
    //  The XML format is KCSG's, so Bando's layouts only change their
    //  class name:
    //    <layouts>   one or more layers of comma-separated rows. Row i is
    //                placed at z = minZ + i, so row 0 is the SOUTH edge
    //                (the text reads upside down vs the map). That's KCSG's
    //                mapping, kept so every layout lands exactly as before.
    //    <roofGrid>  "." none, "1" constructed, "2" thin rock, "3" thick rock
    //
    //  A grid token resolves, in order, to:
    //    1. a Heyra.SymbolDef with that defName
    //    2. "<token>_North|_East|_South|_West": the token without the
    //       suffix (rule 1 or 3), rotated
    //    3. automatic: a ThingDef named <token>, or "<Thing>_<Stuff>"
    //       (a stuffable thing + a stuff), or a TerrainDef named <token>
    //  KCSG resolved rules 1-3 the same way for things, but it no longer
    //  understands terrain tokens, so the labs' floors (Concrete,
    //  Flagstone*, PavedTile) were silently dropped and the labs stood on
    //  raw marsh and rock. We lay them again, as Bando drew them.
    // ═══════════════════════════════════════════════════════════════

    public class StructureLayoutDef : Def
    {
        public List<List<string>> layouts = new List<List<string>>();
        public List<string> roofGrid = new List<string>();
        public List<string> tags = new List<string>();
        public bool spawnConduits = true;   // KCSG field, kept so old XML loads; our labs say false
        public bool isStorage;              // KCSG field, unused (Palace)

        [Unsaved] private List<SymbolDef[,]> resolved;
        [Unsaved] private string[,] roofs;
        [Unsaved] private IntVec2 size;

        /// <summary>Width (x) and height (z) in cells, from the first layer.</summary>
        public IntVec2 Size { get { Resolve(); return size; } }

        public IReadOnlyList<SymbolDef[,]> Layers { get { Resolve(); return resolved; } }

        /// <summary>Roof token at grid (row, col), "." when the grid is missing or short.</summary>
        public string RoofAt(int row, int col)
        {
            Resolve();
            return roofs?[row, col] ?? ".";
        }

        public bool HasRoofGrid => !roofGrid.NullOrEmpty();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors()) yield return e;
            if (layouts.NullOrEmpty() || layouts[0].NullOrEmpty()) yield return "no layouts";
        }

        // Lazy, so every ThingDef/TerrainDef/SymbolDef is loaded and resolved before we look tokens up.
        private void Resolve()
        {
            if (resolved != null) return;
            int rows = layouts[0].Count;
            int cols = layouts[0][0].Split(',').Length;
            size = new IntVec2(cols, rows);

            var missing = new HashSet<string>();
            var pawns = new HashSet<string>();
            resolved = new List<SymbolDef[,]>();
            foreach (List<string> layer in layouts)
            {
                var grid = new SymbolDef[rows, cols];
                for (int r = 0; r < rows && r < layer.Count; r++)
                {
                    string[] tokens = layer[r].Split(',');
                    for (int c = 0; c < cols && c < tokens.Length; c++)
                    {
                        string tok = tokens[c].Trim();
                        if (tok.Length == 0 || tok == ".") continue;
                        SymbolDef sym = SymbolDef.Resolve(tok);
                        if (sym?.thingDef?.category == ThingCategory.Pawn
                            || (sym == null && DefDatabase<PawnKindDef>.GetNamedSilentFail(tok) != null))
                        {
                            // KCSG's exporter recorded whoever stood there (Bando's Tribesperson, Colonist,
                            // Monkey, Husky, Boomrat). KCSG would have spawned them into the player's
                            // faction; we leave them out on purpose.
                            pawns.Add(tok);
                            continue;
                        }
                        if (sym == null) missing.Add(tok);
                        grid[r, c] = sym;
                    }
                }
                resolved.Add(grid);
            }
            if (missing.Count > 0)
                Log.Warning("[Heyra] Layout " + defName + " has tokens that match no symbol, thing or terrain (skipped): "
                    + string.Join(", ", missing));
            if (pawns.Count > 0 && Prefs.DevMode)
                Log.Message("[Heyra] Layout " + defName + ": pawn tokens left out (exporter leftovers): " + string.Join(", ", pawns));

            if (HasRoofGrid)
            {
                roofs = new string[rows, cols];
                for (int r = 0; r < rows; r++)
                {
                    string[] tokens = r < roofGrid.Count ? roofGrid[r].Split(',') : new string[0];
                    for (int c = 0; c < cols; c++) roofs[r, c] = c < tokens.Length ? tokens[c].Trim() : ".";
                }
            }
        }
    }

    public class SymbolDef : Def
    {
        // KCSG's fields that Heyra's layouts use, same names and defaults.
        public string thing;
        public string stuff;
        public Rot4 rotation = Rot4.North;
        public float chanceToContainPawn = 1f;
        public List<PawnKindDef> containPawnKindAnyOf = new List<PawnKindDef>();
        public bool spawnPartOfFaction = true;
        public int maxStackSize = -1;

        // Heyra additions.
        public string terrain;
        public ColorInt? glowColor;   // e.g. the old StandingLamp_Red, which 1.6 no longer has

        [Unsaved] public ThingDef thingDef;
        [Unsaved] public ThingDef stuffDef;
        [Unsaved] public TerrainDef terrainDef;

        private static readonly Dictionary<string, SymbolDef> autoCache = new Dictionary<string, SymbolDef>();

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (thing != null) thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thing);
            if (stuff != null) stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuff);
            if (terrain != null) terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(terrain);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors()) yield return e;
            if (thing == null && terrain == null) yield return "needs <thing> or <terrain>";
        }

        public static SymbolDef Resolve(string token)
        {
            SymbolDef sym = DefDatabase<SymbolDef>.GetNamedSilentFail(token);
            if (sym != null) return sym.thingDef != null || sym.terrainDef != null ? sym : null;
            if (autoCache.TryGetValue(token, out sym)) return sym;

            sym = Auto(token);
            for (int i = 0; sym == null && i < RotationSuffixes.Length; i++)
                sym = TryRotated(token, RotationSuffixes[i].suffix, RotationSuffixes[i].rot);
            autoCache[token] = sym;
            return sym;
        }

        // KCSG's exporter writes rotations in the game's language. Bando exported the Palace from a
        // Chinese game (_北 _东 _南 _西), which KCSG itself never parsed: its furniture lost rotation.
        private static readonly (string suffix, Rot4 rot)[] RotationSuffixes =
        {
            ("_North", Rot4.North), ("_East", Rot4.East), ("_South", Rot4.South), ("_West", Rot4.West),
            ("_北", Rot4.North), ("_东", Rot4.East), ("_南", Rot4.South), ("_西", Rot4.West),
        };

        private static SymbolDef TryRotated(string token, string suffix, Rot4 rot)
        {
            if (!token.EndsWith(suffix)) return null;
            string baseTok = token.Substring(0, token.Length - suffix.Length);
            SymbolDef baseSym = DefDatabase<SymbolDef>.GetNamedSilentFail(baseTok) ?? Auto(baseTok);
            if (baseSym == null || baseSym.thingDef == null) return null;
            SymbolDef copy = (SymbolDef)baseSym.MemberwiseClone();
            copy.defName = token;
            copy.rotation = rot;
            return copy;
        }

        private static SymbolDef Auto(string token)
        {
            ThingDef t = DefDatabase<ThingDef>.GetNamedSilentFail(token);
            if (t != null && !t.IsCorpse) return new SymbolDef { defName = token, thing = token, thingDef = t };

            // "<Thing>_<Stuff>", where either side may contain underscores (Armchair_Leather_Fox).
            for (int us = token.IndexOf('_'); us > 0; us = token.IndexOf('_', us + 1))
            {
                ThingDef made = DefDatabase<ThingDef>.GetNamedSilentFail(token.Substring(0, us));
                ThingDef st = DefDatabase<ThingDef>.GetNamedSilentFail(token.Substring(us + 1));
                if (made != null && made.MadeFromStuff && st != null && st.IsStuff)
                    return new SymbolDef { defName = token, thing = made.defName, thingDef = made, stuff = st.defName, stuffDef = st };
            }

            TerrainDef terr = DefDatabase<TerrainDef>.GetNamedSilentFail(token);
            if (terr != null) return new SymbolDef { defName = token, terrain = token, terrainDef = terr };
            return null;
        }
    }
}
