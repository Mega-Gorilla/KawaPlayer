using UnityEditor;
using UnityEngine;
using Yamadev.YamaStream.Tablet;

namespace Yamadev.YamaStream.Editor
{
  // Lays the tablet out for either orientation in the editor, and captures
  // each layout into its TabletScreen (issue #183). To change the portrait
  // layout: open the tablet prefab, select the tablet, Show Portrait, move
  // the parts, Capture Portrait, then Show Landscape before saving. A part
  // newly added to TabletScreen's list needs capturing in both layouts.
  internal static class TabletLayoutMenu
  {
    private const string Menu = "KawaPlayer/Tablet Layout/";

    [MenuItem(Menu + "Show Landscape", priority = 1)]
    private static void ShowLandscape() => Show(Selected(), false);

    [MenuItem(Menu + "Show Portrait", priority = 2)]
    private static void ShowPortrait() => Show(Selected(), true);

    [MenuItem(Menu + "Capture Landscape", priority = 21)]
    private static void CaptureLandscape() => Capture(Selected(), false);

    [MenuItem(Menu + "Capture Portrait", priority = 22)]
    private static void CapturePortrait() => Capture(Selected(), true);

    [MenuItem(Menu + "Show Landscape", true)]
    [MenuItem(Menu + "Show Portrait", true)]
    [MenuItem(Menu + "Capture Landscape", true)]
    [MenuItem(Menu + "Capture Portrait", true)]
    private static bool HasSelection() => Selected() != null;

    private static TabletScreen Selected()
    {
      GameObject selected = Selection.activeGameObject;
      if (selected == null) return null;
      TabletScreen screen = selected.GetComponentInChildren<TabletScreen>(true);
      return screen != null ? screen : selected.GetComponentInParent<TabletScreen>(true);
    }

    // Records where each part is now as the given layout.
    public static void Capture(TabletScreen screen, bool portrait)
    {
      if (screen == null) return;
      SerializedObject so = new SerializedObject(screen);
      SerializedProperty parts = so.FindProperty("_layoutParts");
      SerializedProperty anchors = so.FindProperty(portrait ? "_portraitAnchors" : "_landscapeAnchors");
      SerializedProperty rects = so.FindProperty(portrait ? "_portraitRects" : "_landscapeRects");
      anchors.arraySize = parts.arraySize;
      rects.arraySize = parts.arraySize;
      for (int i = 0; i < parts.arraySize; i++)
      {
        RectTransform part = parts.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
        if (part == null) continue;
        anchors.GetArrayElementAtIndex(i).vector4Value = new Vector4(part.anchorMin.x, part.anchorMin.y, part.anchorMax.x, part.anchorMax.y);
        rects.GetArrayElementAtIndex(i).vector4Value = new Vector4(part.anchoredPosition.x, part.anchoredPosition.y, part.sizeDelta.x, part.sizeDelta.y);
      }
      so.ApplyModifiedProperties();
    }

    // Puts every part where the given layout says, as TabletScreen does at run
    // time, and turns the display so the layout reads upright in the editor.
    public static void Show(TabletScreen screen, bool portrait)
    {
      if (screen == null) return;
      SerializedObject so = new SerializedObject(screen);
      RectTransform display = so.FindProperty("_display").objectReferenceValue as RectTransform;
      if (display != null)
      {
        Undo.RecordObject(display, "Tablet Layout");
        Vector2 size = display.sizeDelta;
        bool tall = size.y > size.x;
        if (tall != portrait) display.sizeDelta = new Vector2(size.y, size.x);
        display.localEulerAngles = new Vector3(0f, 0f, portrait ? 90f : 0f);
      }

      SerializedProperty layouts = so.FindProperty("_landscapeLayouts");
      if (portrait) SetLayoutsEnabled(layouts, false);
      SerializedProperty parts = so.FindProperty("_layoutParts");
      SerializedProperty anchors = so.FindProperty(portrait ? "_portraitAnchors" : "_landscapeAnchors");
      SerializedProperty rects = so.FindProperty(portrait ? "_portraitRects" : "_landscapeRects");
      int count = Mathf.Min(parts.arraySize, Mathf.Min(anchors.arraySize, rects.arraySize));
      for (int i = 0; i < count; i++)
      {
        RectTransform part = parts.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
        if (part == null) continue;
        Undo.RecordObject(part, "Tablet Layout");
        Vector4 a = anchors.GetArrayElementAtIndex(i).vector4Value;
        Vector4 r = rects.GetArrayElementAtIndex(i).vector4Value;
        part.anchorMin = new Vector2(a.x, a.y);
        part.anchorMax = new Vector2(a.z, a.w);
        part.anchoredPosition = new Vector2(r.x, r.y);
        part.sizeDelta = new Vector2(r.z, r.w);
      }
      if (!portrait) SetLayoutsEnabled(layouts, true);
    }

    private static void SetLayoutsEnabled(SerializedProperty layouts, bool enabled)
    {
      for (int i = 0; i < layouts.arraySize; i++)
      {
        Behaviour layout = layouts.GetArrayElementAtIndex(i).objectReferenceValue as Behaviour;
        if (layout == null) continue;
        Undo.RecordObject(layout, "Tablet Layout");
        layout.enabled = enabled;
      }
    }
  }
}
