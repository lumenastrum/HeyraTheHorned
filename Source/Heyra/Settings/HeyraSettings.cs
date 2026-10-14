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

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Heyra_Setting_MilksoyLoomFuel".Translate(), ref Settings.milksoyLoomFuel,
                "Heyra_Setting_MilksoyLoomFuelDesc".Translate());
            list.CheckboxLabeled("Heyra_Setting_UnrestrictedApparel".Translate(), ref Settings.unrestrictedApparel,
                "Heyra_Setting_UnrestrictedApparelDesc".Translate());
            list.Gap();
            list.Label("Heyra_Setting_RestartNote".Translate());
            list.End();
        }
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
