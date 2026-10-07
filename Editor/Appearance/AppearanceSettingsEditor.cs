using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  [CustomEditor(typeof(AppearanceSettings))]
  public class AppearanceSettingsEditor : EditorBase
  {
    private SerializedProperty _palette;
    private SerializedProperty _defaultColorSet;
    private SerializedProperty _colorSets;

    private Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();
    private int _pendingDeleteIndex = -1;

    private void OnEnable()
    {
      Title = EditorLocalization.Get("appearance.title");
      _palette = serializedObject.FindProperty("palette");
      _defaultColorSet = serializedObject.FindProperty("defaultColorSet");
      _colorSets = serializedObject.FindProperty("colorSets");
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();

      Title = EditorLocalization.Get("appearance.title");
      serializedObject.Update();

      EditorGUILayout.PropertyField(_palette, new GUIContent(EditorLocalization.Get("appearance.palette")));
      EditorGUILayout.Space(SpaceMedium);

      DrawDefaultColorSetSection();
      EditorGUILayout.Space(SpaceLarge);

      DrawColorSetListSection();
      EditorGUILayout.Space(SpaceMedium);

      serializedObject.ApplyModifiedProperties();

      DrawApplyNowSection();
    }

    // The default colour set, from every set the UI can take: Auto (an empty
    // name, the first set), then the palette's sets, then the ones added here.
    // Also drawn by the player inspectors.
    public static void DrawColorSetPopup(AppearanceSettings settings, SerializedProperty defaultColorSet, string label)
    {
      var names = settings.ColorSetNames;
      var options = new string[names.Length + 1];
      options[0] = EditorLocalization.Get("appearance.defaultColorSet.auto");
      System.Array.Copy(names, 0, options, 1, names.Length);
      // A name no set has shows as Auto, which is what the build falls back to.
      int current = System.Array.IndexOf(names, defaultColorSet.stringValue) + 1;
      using (var check = new EditorGUI.ChangeCheckScope())
      {
        int selected = EditorGUILayout.Popup(label, current, options);
        if (check.changed) defaultColorSet.stringValue = selected == 0 ? "" : names[selected - 1];
      }
    }

    private void DrawApplyNowSection()
    {
      EditorGUILayout.HelpBox(EditorLocalization.Get("appearance.applyNow.help"), MessageType.Info);
      if (GUILayout.Button(EditorLocalization.Get("appearance.applyNow")))
      {
        foreach (var t in targets) AppearanceBuildProcess.ApplyNow((AppearanceSettings)t);
      }
    }

    private void DrawDefaultColorSetSection()
    {
      EditorGUILayout.LabelField(EditorLocalization.Get("appearance.defaultColorSet"), EditorStyles.boldLabel);
      DrawColorSetPopup((AppearanceSettings)target, _defaultColorSet, EditorLocalization.Get("appearance.defaultColorSet.label"));
    }

    private void DrawColorSetListSection()
    {
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.LabelField(
          $"{EditorLocalization.Get("appearance.colorSetList")} ({_colorSets.arraySize})",
          EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(EditorLocalization.Get("appearance.add"), GUILayout.Width(60)))
        {
          AddNewColorSet();
        }
      }

      EditorGUILayout.Space(SpaceSmall);

      if (_colorSets.arraySize == 0)
      {
        if (_palette.objectReferenceValue == null) EditorGUILayout.HelpBox(EditorLocalization.Get("appearance.noColorSets"), MessageType.Info);
      }
      else
      {
        DrawColorSetListBox();
      }
    }

    private void DrawColorSetListBox()
    {
      EditorGUILayout.BeginVertical();

      for (int i = 0; i < _colorSets.arraySize; i++)
      {
        DrawColorSetRow(i);
      }

      EditorGUILayout.EndVertical();

      ExecutePendingDelete();
    }

    private void ExecutePendingDelete()
    {
      if (_pendingDeleteIndex < 0) return;

      if (_pendingDeleteIndex < _colorSets.arraySize)
      {
        _colorSets.DeleteArrayElementAtIndex(_pendingDeleteIndex);
        _foldouts.Clear();
      }
      _pendingDeleteIndex = -1;
    }

    private void DrawColorSetRow(int index)
    {
      var colorSetProp = _colorSets.GetArrayElementAtIndex(index);
      var colorSetName = colorSetProp.FindPropertyRelative("colorSetName");
      var primaryColor = colorSetProp.FindPropertyRelative("primaryColor");
      var secondaryColor = colorSetProp.FindPropertyRelative("secondaryColor");

      var rowBgColor = index % 2 == 0
        ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f))
        : (EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.8f, 0.8f, 0.8f));

      var rowRect = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rowRect, rowBgColor);

      using (new EditorGUILayout.HorizontalScope(GUILayout.Height(22)))
      {
        GUILayout.Space(4);

        if (!_foldouts.ContainsKey(index)) _foldouts[index] = false;

        var foldoutContent = new GUIContent(_foldouts[index] ? "▼" : "▶");
        if (GUILayout.Button(foldoutContent, EditorStyles.label, GUILayout.Width(16), GUILayout.Height(22)))
        {
          _foldouts[index] = !_foldouts[index];
        }

        var displayName = string.IsNullOrEmpty(colorSetName.stringValue) ? $"ColorSet {index + 1}" : colorSetName.stringValue;
        float headerLabelWidth = EditorGUIUtility.labelWidth - 20;
        EditorGUILayout.LabelField(displayName, LanguageCodeStyle, GUILayout.Width(headerLabelWidth));

        var previewRect = GUILayoutUtility.GetRect(40, 16, GUILayout.Width(40));
        EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y, 20, 16), primaryColor.colorValue);
        EditorGUI.DrawRect(new Rect(previewRect.x + 20, previewRect.y, 20, 16), secondaryColor.colorValue);

        GUILayout.FlexibleSpace();

        if (_defaultColorSet.stringValue == colorSetName.stringValue && !string.IsNullOrEmpty(colorSetName.stringValue))
        {
          var defaultStyle = new GUIStyle(EditorStyles.miniLabel)
          {
            normal = { textColor = new Color(0.3f, 0.7f, 0.4f, 1f) },
            alignment = TextAnchor.MiddleRight
          };
          GUILayout.Label(EditorLocalization.Get("appearance.default"), defaultStyle, GUILayout.Width(70), GUILayout.Height(22));
        }

        if (GUILayout.Button("✕", EditorStyles.label, GUILayout.Width(20), GUILayout.Height(22)))
        {
          if (EditorUtility.DisplayDialog(
              EditorLocalization.Get("appearance.deleteColorSet"),
              string.Format(EditorLocalization.Get("appearance.deleteConfirm"), displayName),
              EditorLocalization.Get("appearance.delete"),
              EditorLocalization.Get("button.cancel")))
          {
            _pendingDeleteIndex = index;
          }
        }
        GUILayout.Space(4);
      }

      if (_foldouts[index])
      {
        DrawColorSetDetails(colorSetName, primaryColor, secondaryColor);
      }

      EditorGUILayout.EndVertical();
    }

    private void DrawColorSetDetails(SerializedProperty colorSetName, SerializedProperty primaryColor, SerializedProperty secondaryColor)
    {
      var detailBgColor = EditorGUIUtility.isProSkin
        ? new Color(0.18f, 0.18f, 0.18f)
        : new Color(0.7f, 0.7f, 0.7f);

      var detailRect = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(detailRect, detailBgColor);

      GUILayout.Space(SpaceSmall);
      using (new EditorGUILayout.HorizontalScope())
      {
        GUILayout.Space(20);
        using (new EditorGUILayout.VerticalScope())
        {
          float originalLabelWidth = EditorGUIUtility.labelWidth;
          EditorGUIUtility.labelWidth = originalLabelWidth - 20;

          EditorGUILayout.PropertyField(colorSetName, new GUIContent(EditorLocalization.Get("appearance.colorSetName")));

          GUILayout.Space(SpaceSmall);

          EditorGUILayout.PropertyField(primaryColor, new GUIContent(EditorLocalization.Get("appearance.primaryColor")));
          EditorGUILayout.PropertyField(secondaryColor, new GUIContent(EditorLocalization.Get("appearance.secondaryColor")));

          EditorGUIUtility.labelWidth = originalLabelWidth;
        }
        GUILayout.Space(8);
      }
      GUILayout.Space(SpaceSmall);

      EditorGUILayout.EndVertical();
    }

    private void AddNewColorSet()
    {
      _colorSets.InsertArrayElementAtIndex(_colorSets.arraySize);
      var newColorSet = _colorSets.GetArrayElementAtIndex(_colorSets.arraySize - 1);

      // A new set starts from the colours the UI has now.
      var current = ((AppearanceSettings)target).DefaultColorSet;
      newColorSet.FindPropertyRelative("colorSetName").stringValue = "";
      newColorSet.FindPropertyRelative("primaryColor").colorValue = current != null ? current.primaryColor : Color.white;
      newColorSet.FindPropertyRelative("secondaryColor").colorValue = current != null ? current.secondaryColor : Color.white;

      _foldouts[_colorSets.arraySize - 1] = true;
    }
  }
}
