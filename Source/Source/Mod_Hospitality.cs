using HarmonyLib;
using Hospitality.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

// need Waylith equivalent - Rect - UPDATE

namespace Hospitality;

public class Mod_Hospitality : Mod
{
    private static ModSettings_Hospitality settings;

    public Mod_Hospitality(ModContentPack content) : base(content)
    {
        settings = GetSettings<ModSettings_Hospitality>();
        Harmony harmony = new(Content.PackageIdPlayerFacing);
        harmony.PatchAll();
        PostLoad();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return "Hospitality";
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
    }

    private static void PostLoad()
    {
        LongEventHandler.ExecuteWhenFinished(DefsUtility.CheckForInvalidDefs);
    }

    // This isn't called on every setting changes
    public static void SettingsChanged()
    {
        ToggleTabIfNeeded();
        UpdateMainButtonIcon();
    }

    public static void ToggleTabIfNeeded()
    {
        DefDatabase<MainButtonDef>.GetNamed("Guests").buttonVisible = !ModSettings_Hospitality.disableGuestsTab;
    }

    public static void UpdateMainButtonIcon()
    {
        var mainButtonDef = DefDatabase<MainButtonDef>.GetNamed("Guests");
        mainButtonDef.iconPath = ModSettings_Hospitality.useIcon ? "UI/Buttons/MainButtons/IconHospitality" : null;
        if (mainButtonDef.iconPath == null) mainButtonDef.icon = null;
    }
}