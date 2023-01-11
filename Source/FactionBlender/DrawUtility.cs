using System;
using System.Collections.Generic;
using HugsLib.Settings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// Much of this borrowed from rheirman's excellent Run and Gun mod.
// See also: https://github.com/rheirman/RunAndGun/blob/master/Source/RunAndGun/Utilities/DrawUtility.cs

namespace FactionBlender;

internal class DrawUtility
{
    private const float IconSize = 32f;
    private const float IconGap = 1f;
    private const float TextMargin = 20f;
    private const float BottomMargin = 2f;

    private static readonly Color iconBaseColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Color iconMouseOverColor = new Color(0.6f, 0.6f, 0.4f, 1f);
    private static readonly Color background = new Color(0.5f, 0, 0, 0.1f);

    private static readonly Dictionary<string, Pawn> pawnKindExamples = new Dictionary<string, Pawn>();

    private static void DrawBackground(Rect rect)
    {
        var save = GUI.color;
        GUI.color = background;
        GUI.DrawTexture(rect, TexUI.FastFillTex);
        GUI.color = save;
    }

    private static void DrawLabel(string labelText, Rect textRect, float offset)
    {
        var labelHeight = Text.CalcHeight(labelText, textRect.width);
        labelHeight -= 2f;
        var labelRect = new Rect(textRect.x, textRect.yMin - labelHeight + offset, textRect.width, labelHeight);
        GUI.DrawTexture(labelRect, TexUI.GrayTextBG);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(labelRect, labelText);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }

    private static void DrawIconForPawnKind(PawnKindDef pawn, string extraAttr, Rect contentRect, Vector2 iconOffset,
        int buttonID)
    {
        var iconRect = new Rect(contentRect.x + iconOffset.x, contentRect.y + iconOffset.y, IconSize, IconSize);

        if (!contentRect.Contains(iconRect))
        {
            return;
        }

        var label = Find.ActiveLanguageWorker.ToTitleCase(pawn.label);
        if (!extraAttr.NullOrEmpty())
        {
            label += $" ({extraAttr})";
        }

        TooltipHandler.TipRegion(iconRect, label);

        MouseoverSounds.DoRegion(iconRect, SoundDefOf.Mouseover_Command);
        GUI.color = Mouse.IsOver(iconRect) ? iconMouseOverColor : iconBaseColor;
        GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("square"));

        // Humanlikes don't have textures, so an example pawn needs to be generated.  Of course, this can't happen in
        // the main menu, so it'll be skipped when a world isn't present

        if (pawn.RaceProps.Humanlike && Current.Game != null)
        {
            var name = pawn.defName;
            var examplePawn = pawnKindExamples.ContainsKey(name)
                ? pawnKindExamples[name]
                : PawnGenerator.GeneratePawn(pawn);
            pawnKindExamples[name] = examplePawn;

            Widgets.ThingIcon(iconRect, examplePawn);
        }
        else
        {
            Widgets.ThingIcon(iconRect, pawn.race);
        }

        if (!Widgets.ButtonInvisible(iconRect))
        {
            return;
        }

        Event.current.button = buttonID;
    }

    public static bool CustomDrawer_Filter(Rect rect, SettingHandle<float> slider, bool def_isPercentage, float def_min,
        float def_max, float roundTo = -1)
    {
        DrawBackground(rect);
        var labelWidth = 50;

        var sliderPortion = new Rect(rect);
        sliderPortion.width -= labelWidth;

        var labelPortion = new Rect(rect)
        {
            width = labelWidth,
            position = new Vector2(sliderPortion.position.x + sliderPortion.width + 5f, sliderPortion.position.y + 4f)
        };

        sliderPortion = sliderPortion.ContractedBy(2f);

        if (def_isPercentage)
        {
            Widgets.Label(labelPortion, $"{Mathf.Round(slider.Value * 100f):F0}%");
        }
        else if (roundTo >= 1)
        {
            Widgets.Label(labelPortion, slider.Value.ToString("N0"));
        }
        else
        {
            Widgets.Label(labelPortion, slider.Value.ToString("F2"));
        }

        var val = Widgets.HorizontalSlider_NewTemp(sliderPortion, slider.Value, def_min, def_max, true, null, null,
            null,
            roundTo);
        var change = slider.Value != val;

        slider.Value = val;
        return change;
    }

    internal static bool CustomDrawer_FilteredPawnKinds(
        Rect wholeRect, SettingHandle setting, List<PawnKindDef> allPawnKinds, Predicate<PawnKindDef> filter,
        Action<List<PawnKindDef>> sorter, Func<PawnKindDef, string> pawnAttr
    )
    {
        // Reset the Rect back to full size
        wholeRect.Set(wholeRect.x - wholeRect.width, wholeRect.y, wholeRect.width * 2f, wholeRect.height);

        DrawBackground(wholeRect);

        GUI.color = Color.white;

        var iconRect = new Rect(wholeRect)
        {
            height = wholeRect.height - TextMargin + BottomMargin
        };
        iconRect.position = new Vector2(iconRect.position.x, iconRect.position.y);

        DrawLabel("Filtered".Translate(), iconRect, TextMargin);

        iconRect.position = new Vector2(iconRect.position.x, iconRect.position.y + TextMargin);

        var iconsPerRow = (int)(iconRect.width / (IconGap + IconSize));
        var numSelected = 0f;

        var filteredList = allPawnKinds.FindAll(filter);
        sorter(filteredList);

        var biggerRows = Math.Max(numSelected / iconsPerRow, (filteredList.Count - numSelected) / iconsPerRow) + 1;
        setting.CustomDrawerHeight = (biggerRows * IconSize) + (biggerRows * IconGap) + TextMargin;
        var index = 0;
        foreach (var pawn in filteredList)
        {
            var column = index % iconsPerRow;
            var row = index / iconsPerRow;

            DrawIconForPawnKind(
                pawn, pawnAttr(pawn), iconRect,
                new Vector2((IconSize * column) + (column * IconGap), (IconSize * row) + (row * IconGap)), index
            );
            index++;
        }

        return false;
    }

    // Draws the input control for larger textbox string settings.  Unlike DrawHandleInputText, this will
    // also update validation and change status on focused keypresses.
    public static bool CustomDrawer_InputTextbox(Rect controlRect, SettingHandle handle)
    {
        // HugsLibs keeps the HandleControlInfo private, so we'll just have to work around that...
        var controlName = $"control{handle.GetHashCode()}";
        var validationScheduled = false;
        var badInput = false;

        var evt = Event.current;
        GUI.SetNextControlName(controlName);

        // XXX: Because we're setting the "temp" value directly on the SettingHandle, validation is only
        // used for colorizing the input box on badInput.

        // XXX: This setting of global stuff and resetting it back is silly, but it's apparently the Unity way of doing things...
        var taStyle = Text.CurTextAreaStyle;
        var oldWordWrap = taStyle.wordWrap;
        var oldAlignment = taStyle.alignment;

        taStyle.wordWrap = true;
        taStyle.alignment = TextAnchor.UpperLeft;

        // TODO: Use TextAreaScrollable.  This would also require a more permanent HandleControlInfo-like
        // object to hold the scrollbar data
        handle.StringValue = Widgets.TextArea(controlRect, handle.StringValue);

        taStyle.wordWrap = oldWordWrap;
        taStyle.alignment = oldAlignment;

        // Trigger on each keypress
        var focused = GUI.GetNameOfFocusedControl() == controlName;
        if (focused && evt.type == EventType.KeyUp)
        {
            validationScheduled = true;
        }

        var changed = false;
        if (validationScheduled && !focused)
        {
            try
            {
                if (handle.Validator != null && !handle.Validator(handle.StringValue))
                {
                    badInput = true;
                }
                else
                {
                    badInput = false;
                    changed = true;
                }
            }
            catch (Exception e)
            {
                Base.Instance.ModLogger.ReportException(e, Base.Instance.ModIdentifier, false,
                    "SettingsHandle.Validator");
            }
        }

        if (badInput)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(.9f, .1f, .1f, 1f);
            Widgets.DrawBox(controlRect);
            GUI.color = prevColor;
        }

        return changed;
    }
}