using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Editor

{
  public class AppearanceBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2000;

    public void Process()
    {
      var appearanceSettings = Object.FindObjectsByType<AppearanceSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var appearanceSetting in appearanceSettings)
      {
        ProcessAppearanceSetting(appearanceSetting);
      }
    }

    private void ProcessAppearanceSetting(AppearanceSettings appearanceSetting)
    {
      if (appearanceSetting == null) return;
      var uiController = appearanceSetting.GetComponent<UIController>();
      if (uiController == null) return;
      var defaultColorSet = appearanceSetting.DefaultColorSet;
      if (defaultColorSet == null) return;

      uiController.SetProgramVariable("_primaryColor", defaultColorSet.primaryColor);
      uiController.SetProgramVariable("_secondaryColor", defaultColorSet.secondaryColor);
      ApplyColors(appearanceSetting, defaultColorSet, false);
    }

    // The inspector's "apply now": what the build does, on the scene or prefab
    // being edited, so the colours can be seen without building.
    public static void ApplyNow(AppearanceSettings appearanceSetting)
    {
      if (appearanceSetting == null) return;
      var defaultColorSet = appearanceSetting.DefaultColorSet;
      if (defaultColorSet == null) return;

      var uiController = appearanceSetting.GetComponent<UIController>();
      if (uiController != null)
      {
        var so = new SerializedObject(uiController);
        so.FindProperty("_primaryColor").colorValue = defaultColorSet.primaryColor;
        so.FindProperty("_secondaryColor").colorValue = defaultColorSet.secondaryColor;
        so.ApplyModifiedProperties();
      }
      ApplyColors(appearanceSetting, defaultColorSet, true);
    }

    private static void ApplyColors(AppearanceSettings appearanceSetting, ColorSetData colorSet, bool recordUndo)
    {
      // Without a palette the neutral roles take ColorPalette's defaults, KawaPlayer's colours.
      var palette = appearanceSetting.palette != null ? appearanceSetting.palette : ScriptableObject.CreateInstance<ColorPalette>();
      foreach (var colorDefinition in appearanceSetting.GetComponentsInChildren<ColorDefinition>(true))
      {
        if (colorDefinition == null) continue;
        // A UI inside this one takes the colours of its own settings.
        if (colorDefinition.GetComponentInParent<AppearanceSettings>(true) != appearanceSetting) continue;
        ApplyColor(colorDefinition, palette.GetColor(colorDefinition.colorType, colorSet), recordUndo);
      }
      if (appearanceSetting.palette == null) Object.DestroyImmediate(palette);
    }

    private static void ApplyColor(ColorDefinition colorDefinition, Color color, bool recordUndo)
    {
      var graphic = colorDefinition.GetComponent<Graphic>();
      if (graphic == null) return;

      // A button or toggle whose colour comes from its tint: move the whole
      // tint, so hover and press keep their shading.
      var selectable = TintingSelectable(graphic);
      if (selectable != null)
      {
        if (recordUndo) Undo.RecordObject(selectable, "Apply Colors");
        var colors = selectable.colors;
        var shift = color - colors.normalColor;
        colors.normalColor = Shift(colors.normalColor, shift);
        colors.highlightedColor = Shift(colors.highlightedColor, shift);
        colors.pressedColor = Shift(colors.pressedColor, shift);
        colors.selectedColor = Shift(colors.selectedColor, shift);
        colors.disabledColor = Shift(colors.disabledColor, shift);
        selectable.colors = colors;
        if (recordUndo) PrefabUtility.RecordPrefabInstancePropertyModifications(selectable);
        return;
      }

      if (recordUndo) Undo.RecordObject(graphic, "Apply Colors");
      if (ColorPalette.IsNeutral(colorDefinition.colorType)) color.a = graphic.color.a;
      graphic.color = color;
      if (recordUndo) PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
    }

    // A tint that only fades its graphic in and out (white, at some alpha) does
    // not carry the colour: the graphic does.
    private static Selectable TintingSelectable(Graphic graphic)
    {
      foreach (var selectable in graphic.GetComponentsInParent<Selectable>(true))
      {
        if (selectable.targetGraphic != graphic || selectable.transition != Selectable.Transition.ColorTint) continue;
        var normal = selectable.colors.normalColor;
        if (normal.r < 1f || normal.g < 1f || normal.b < 1f) return selectable;
      }
      return null;
    }

    // Moves the colour and keeps the alpha.
    private static Color Shift(Color color, Color shift)
    {
      return new Color(Mathf.Clamp01(color.r + shift.r), Mathf.Clamp01(color.g + shift.g), Mathf.Clamp01(color.b + shift.b), color.a);
    }
  }
}
