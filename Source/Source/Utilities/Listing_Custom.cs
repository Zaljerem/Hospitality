using System;
using UnityEngine;
using Verse;

// need Waylith equivalents - UPDATE

namespace Hospitality;

[StaticConstructorOnStartup]
public class Listing_Custom : Listing_Standard
{
    public float CustomSliderLabel(string label, float val, float min, float max, float labelPct = 0.5f, string tooltip = null, string label2 = null, string rightLabel = null, string leftLabel = null, float roundTo = -1f)
    {
        var rect = GetRect(30f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect.LeftPart(labelPct), label);
        if (tooltip != null)
        {
            TooltipHandler.TipRegion(rect.LeftPart(labelPct), tooltip);
        }

        Text.Anchor = TextAnchor.UpperLeft;
        var result = CustomWidget.HorizontalSlider(rect.RightPart(1f - labelPct), val, min, max, true, label2, leftLabel, rightLabel, roundTo);
        Gap(verticalSpacing);
        return result;
    }

    public int CustomSliderLabelInt(string label, int val, int min, int max, float labelPct = 0.5f, string tooltip = null, string label2 = null, string rightLabel = null, string leftLabel = null, float roundTo = -1f)
    {
        var rect = GetRect(30f);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect.LeftPart(labelPct), label);
        if (tooltip != null)
        {
            TooltipHandler.TipRegion(rect.LeftPart(labelPct), tooltip);
        }

        Text.Anchor = TextAnchor.UpperLeft;
        var result = CustomWidget.HorizontalSlider(rect.RightPart(1f - labelPct), val, min, max, true, label2, leftLabel, rightLabel, roundTo);
        Gap(verticalSpacing);
        return (int)result;
    }

    public void CustomCheckboxLabeled(string label, ref bool checkOn, string tooltip = null, float height = 0f, float labelPct = 1f)
    {
        var height2 = height != 0f ? height : Text.CalcHeight(label, ColumnWidth * labelPct);
        var rect = GetRect(height2, labelPct);
        rect.width = Math.Min(rect.width + 24f, ColumnWidth);
        if (BoundingRectCached == null || rect.Overlaps(BoundingRectCached.Value))
        {
            if (!tooltip.NullOrEmpty())
            {
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                }

                TooltipHandler.TipRegion(rect, tooltip);
            }

            CustomWidget.CheckboxLabeled(rect, label, ref checkOn);
        }

        Gap(verticalSpacing);
    }

    public (int minValue, int maxValue) CustomSliderLabelIntRange(string label, int minValue, int maxValue, int min, int max, float labelPct = 0.5f, string tooltip = null, string label2 = null, string minLabel = null, string maxLabel = null, float roundTo = -1f)
    {
        var originalAnchor = Text.Anchor;

        try
        {
            var rect = GetRect(30f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.LeftPart(labelPct), label);
            if (tooltip != null)
            {
                TooltipHandler.TipRegion(rect.LeftPart(labelPct), tooltip);
            }

            var sliderRect = rect.RightPart(1f - labelPct);
            float floatMinValue = minValue;
            float floatMaxValue = maxValue;
            (floatMinValue, floatMaxValue) = CustomWidget.HorizontalRangeSlider(sliderRect, floatMinValue, floatMaxValue, min, max, true, label2, minLabel, maxLabel);
            minValue = Mathf.RoundToInt(floatMinValue);
            maxValue = Mathf.RoundToInt(floatMaxValue);
            return (minValue, maxValue);
        }
        finally
        {
            // Ensure the alignment is always reset, even if an exception occurs
            Text.Anchor = originalAnchor;
        }
    }
}