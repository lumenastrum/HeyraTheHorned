using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace Heyra
{
    // ═══════════════════════════════════════════════════════════════
    //  MOD SETTINGS — Heyra-owned replacement for the two
    //  VEF.PatchOperationToggableSequence toggles in Patches/Config.xml.
    //
    //  The VEF toggles never applied in the revival: VEF matches the
    //  <mods> entry by exact display name ("Heyra the Horned"), and we
    //  are "Heyra the Horned (Revived)".
    //
    //  RimWorld constructs Mod classes (CreateModClasses) before it
    //  applies XML patches (ApplyPatches), so PatchOperationHeyraSetting
    //  can read these values at patch time. Changes need a restart.
    // ═══════════════════════════════════════════════════════════════

    public class HeyraSettings : ModSettings
    {
        public bool milksoyLoomFuel;
        public bool unrestrictedApparel;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref milksoyLoomFuel, "milksoyLoomFuel", false);
            Scribe_Values.Look(ref unrestrictedApparel, "unrestrictedApparel", false);
        }
    }

    public class HeyraMod : Mod
    {
        public static HeyraSettings Settings;

        public HeyraMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HeyraSettings>();
        }

        public override string SettingsCategory() => "Heyra the Horned (Revived)";

        private const float IconSize = 72f;
        private const float ColumnWidth = 560f;
        private static readonly Color Subtle = new Color(0.65f, 0.65f, 0.68f);
        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.42f);
        private static readonly Color Note = new Color(0.95f, 0.72f, 0.55f);

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Texture2D icon = HeyraSettingsTextures.MainIcon;

            // Header: icon, name, credit line
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, IconSize);
            float textX = header.x;
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(header.x, header.y, IconSize, IconSize), icon, ScaleMode.ScaleToFit);
                textX += IconSize + 14f;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(textX, header.y + 8f, header.xMax - textX, 34f), "Heyra the Horned");
            Text.Font = GameFont.Small;
            GUI.color = Subtle;
            Widgets.Label(new Rect(textX, header.y + 40f, header.xMax - textX, 24f), "Heyra_Setting_Credit".Translate());
            GUI.color = Color.white;

            // Options, in a readable column instead of stretched across the window
            Rect body = new Rect(inRect.x, header.yMax + 12f, Mathf.Min(ColumnWidth, inRect.width), inRect.height - IconSize - 12f);
            var list = new Listing_Standard();
            list.Begin(body);
            list.GapLine();
            GUI.color = Gold;
            list.Label("Heyra_Setting_Options".Translate());
            GUI.color = Color.white;
            list.Gap(6f);

            Option(list, "Heyra_Setting_MilksoyLoomFuel", ref Settings.milksoyLoomFuel);
            Option(list, "Heyra_Setting_UnrestrictedApparel", ref Settings.unrestrictedApparel);

            list.GapLine();
            GUI.color = Note;
            list.Label("Heyra_Setting_RestartNote".Translate());
            GUI.color = Color.white;
            list.End();
        }

        private static void Option(Listing_Standard list, string key, ref bool value)
        {
            list.CheckboxLabeled((key).Translate(), ref value, (key + "Desc").Translate());
            Text.Font = GameFont.Tiny;
            GUI.color = Subtle;
            list.Label("    " + (key + "Desc").Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            list.Gap(10f);
        }
    }

    // Textures must load on the main thread at startup, not in the Mod class (RimWorld warns otherwise).
    [StaticConstructorOnStartup]
    internal static class HeyraSettingsTextures
    {
        public static readonly Texture2D MainIcon = ContentFinder<Texture2D>.Get("Icon/Heyra_MainIcon", reportFailure: false);
    }

    /// <summary>
    /// Runs its operations only when the named Heyra setting is on.
    /// Usage: &lt;Operation Class="Heyra.PatchOperationHeyraSetting"&gt;&lt;setting&gt;milksoyLoomFuel&lt;/setting&gt;&lt;operations&gt;...
    /// </summary>
    public class PatchOperationHeyraSetting : PatchOperation
    {
        public string setting;
        public List<PatchOperation> operations = new List<PatchOperation>();

        private PatchOperation lastFailedOperation;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            if (!IsOn())
                return true;

            foreach (PatchOperation operation in operations)
            {
                if (!operation.Apply(xml))
                {
                    lastFailedOperation = operation;
                    return false;
                }
            }
            return true;
        }

        private bool IsOn()
        {
            HeyraSettings s = HeyraMod.Settings;
            if (s == null)
                return false;

            switch (setting)
            {
                case "milksoyLoomFuel": return s.milksoyLoomFuel;
                case "unrestrictedApparel": return s.unrestrictedApparel;
                default:
                    Log.Error($"[Heyra] PatchOperationHeyraSetting: unknown setting '{setting}'");
                    return false;
            }
        }

        public override void Complete(string modIdentifier)
        {
            base.Complete(modIdentifier);
            lastFailedOperation = null;
        }

        public override string ToString()
        {
            string text = $"{base.ToString()}(setting={setting}, count={operations?.Count ?? 0}";
            if (lastFailedOperation != null)
                text += ", lastFailedOperation=" + lastFailedOperation;
            return text + ")";
        }
    }
}
