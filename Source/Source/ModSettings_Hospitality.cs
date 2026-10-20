using System;
using UnityEngine;
using Verse;

// need Waylith equivalents - UPDATE

namespace Hospitality;

public class ModSettings_Hospitality : ModSettings
{
    public static int
        minGuestWorkSkill = 7,
        maxIncidentsPer3Days = 5,
        minGuestGroupSize = 1,
        maxGuestGroupSize = 5;

    public static bool
        disableGuests,
        disableWork,
        disableGifts,
        disableArtAndCraft = true,
        disableOperations = true,
        disableMedical,
        disableGuestsTab,
        useIcon,
        enableBuyNotification,
        enableRecruitNotification = true,
        disableFriendlyGearDrops = true;

    public static float
        silverMultiplier = 1f;

    private Vector2 scrollPosition;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref minGuestWorkSkill, "minGuestWorkSkill", 7);
        Scribe_Values.Look(ref maxIncidentsPer3Days, "maxIncidentsPer3Days", 5);
        Scribe_Values.Look(ref minGuestGroupSize, "minGuestGroupSize", 1);
        Scribe_Values.Look(ref maxGuestGroupSize, "maxGuestGroupSize", 5);
        Scribe_Values.Look(ref silverMultiplier, "silverMultiplier", 1f);
        Scribe_Values.Look(ref disableGuests, "disableGuests");
        Scribe_Values.Look(ref disableWork, "disableWork");
        Scribe_Values.Look(ref disableGifts, "disableGifts");
        Scribe_Values.Look(ref disableArtAndCraft, "disableArtAndCraft", true);
        Scribe_Values.Look(ref disableOperations, "disableOperations", true);
        Scribe_Values.Look(ref disableMedical, "disableMedical");
        Scribe_Values.Look(ref disableGuestsTab, "disableGuestsTab");
        Scribe_Values.Look(ref useIcon, "useIcon");
        Scribe_Values.Look(ref enableBuyNotification, "enableBuyNotification");
        Scribe_Values.Look(ref enableRecruitNotification, "enableRecruitNotification", true);
        Scribe_Values.Look(ref disableFriendlyGearDrops, "disableFriendlyGearDrops", true);
        base.ExposeData();
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        Rect rect = new(inRect.x, inRect.y, inRect.width - 20f, inRect.height);
        var contentHeight = 700f;
        Widgets.BeginScrollView(inRect, ref scrollPosition, new Rect(0f, 0f, rect.width, contentHeight));
        Listing_Custom options = new();
        options.Begin(rect);
        options.GapLine();
        Text.Font = GameFont.Medium;
        Text.Font = GameFont.Small;
        options.CustomCheckboxLabeled("DisableVisitors".Translate(), ref disableGuests, "DisableVisitorsDesc".Translate());
        options.CustomCheckboxLabeled("DisableGuestsHelping".Translate(), ref disableWork, "DisableGuestsHelpingDesc".Translate());
        options.CustomCheckboxLabeled("DisableArtAndCraft".Translate(), ref disableArtAndCraft, "DisableArtAndCraftDesc".Translate());
        options.CustomCheckboxLabeled("DisableOperations".Translate(), ref disableOperations, "DisableOperationsDesc".Translate());
        options.CustomCheckboxLabeled("DisableMedical".Translate(), ref disableMedical, "DisableMedicalDesc".Translate());
        options.CustomCheckboxLabeled("DisableGifts".Translate(), ref disableGifts, "DisableGiftsDesc".Translate());
        options.GapLine();
        minGuestWorkSkill = options.CustomSliderLabelInt("MinGuestWorkSkill".Translate(), minGuestWorkSkill, 1, 20, 0.5f, "MinGuestWorkSkillDesc".Translate(), minGuestWorkSkill.ToString(), 20.ToString(), 1.ToString(), 1);
        options.Gap();
        maxIncidentsPer3Days = options.CustomSliderLabelInt("MaxIncidentsPer3Days".Translate(), maxIncidentsPer3Days, 1, 10, 0.5f, "MaxIncidentsPer3DaysDesc".Translate(), maxIncidentsPer3Days.ToString(), 10.ToString(), 1.ToString(), 1);
        options.Gap();
        silverMultiplier = options.CustomSliderLabel("SilverMultiplier".Translate(), silverMultiplier, 0f, 10f, 0.5f, "SilverMultiplierDesc".Translate(), Math.Ceiling(silverMultiplier * 100) + "%", 10f.ToString(), 0f.ToString(), 0.1f);
        options.Gap();
        (minGuestGroupSize, maxGuestGroupSize) = options.CustomSliderLabelIntRange("GuestGroupSize".Translate(), minGuestGroupSize, maxGuestGroupSize, 1, 20, 0.5f, "GuestGroupSizeDesc".Translate(), minGuestGroupSize + "~" + maxGuestGroupSize, 1.ToString(), 20.ToString(), 1);
        options.GapLine();
        var previousUseIcon = useIcon;
        var previousGuestTab = disableGuestsTab;
        options.CustomCheckboxLabeled("DisableGuestsTab".Translate(), ref disableGuestsTab, "DisableGuestsTabDesc".Translate());
        options.CustomCheckboxLabeled("UseIcon".Translate(), ref useIcon, "UseIconDesc".Translate());
        if (useIcon != previousUseIcon || previousGuestTab != disableGuestsTab)
        {
            Mod_Hospitality.SettingsChanged();
        }

        options.CustomCheckboxLabeled("EnableBuyNotification".Translate(), ref enableBuyNotification, "EnableBuyNotificationDesc".Translate());
        options.CustomCheckboxLabeled("EnableRecruitNotification".Translate(), ref enableRecruitNotification, "EnableRecruitNotificationDesc".Translate());
        options.CustomCheckboxLabeled("DisableFriendlyGearDrops".Translate(), ref disableFriendlyGearDrops, "DisableFriendlyGearDropsDesc".Translate());
        options.GapLine();
        options.Gap();
        if (options.ButtonText("Reset to Defaults"))
        {
            ResetSettingsToDefault();
        }

        options.End();
        Widgets.EndScrollView();
    }

    private static void ResetSettingsToDefault()
    {
        minGuestWorkSkill = 7;
        maxIncidentsPer3Days = 5;
        minGuestGroupSize = 1;
        maxGuestGroupSize = 5;

        disableGuests = false;
        disableWork = false;
        disableGifts = false;
        disableArtAndCraft = true;
        disableOperations = true;
        disableMedical = false;
        disableGuestsTab = false;
        useIcon = false;
        enableBuyNotification = false;
        enableRecruitNotification = true;
        disableFriendlyGearDrops = true;

        silverMultiplier = 1f;
    }
}