using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  [CustomEditor(typeof(LocalizationSettings))]
  public class LocalizationSettingsEditor : EditorBase
  {
    private SerializedProperty _defaultLanguage;
    private SerializedProperty _languages;

    private Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();
    private Dictionary<int, string> _keyValidationMessages = new Dictionary<int, string>();
    private int _pendingDeleteIndex = -1;

    private void OnEnable()
    {
      Title = EditorLocalization.Get("localization.title");
      _defaultLanguage = serializedObject.FindProperty("defaultLanguage");
      _languages = serializedObject.FindProperty("languages");
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();

      Title = EditorLocalization.Get("localization.title");
      serializedObject.Update();

      DrawDefaultLanguageSection();
      EditorGUILayout.Space(SpaceLarge);

      DrawLanguageListSection();
      EditorGUILayout.Space(SpaceMedium);

      serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultLanguageSection()
    {
      EditorGUILayout.LabelField(EditorLocalization.Get("localization.defaultLanguage"), EditorStyles.boldLabel);

      _defaultLanguage.stringValue = DefaultLanguagePopup(_languages, _defaultLanguage.stringValue, EditorLocalization.Get("localization.defaultLanguage.label"));
    }

    // The default language: "auto", then each language as "code - name".
    // Returns the code picked, "" for auto; a code no language has shows
    // as auto. Also drawn by the player inspectors.
    public static string DefaultLanguagePopup(SerializedProperty languages, string currentCode, string label)
    {
      var optionCodes = new List<string> { "" };
      var optionNames = new List<string> { EditorLocalization.Get("localization.defaultLanguage.auto") };

      for (int i = 0; i < languages.arraySize; i++)
      {
        var language = languages.GetArrayElementAtIndex(i);
        var codeProperty = language.FindPropertyRelative("languageCode");
        var displayNameProperty = language.FindPropertyRelative("displayName");
        var code = codeProperty != null ? codeProperty.stringValue : "";
        var displayName = displayNameProperty != null ? displayNameProperty.stringValue : $"Language {i}";
        optionCodes.Add(code);
        optionNames.Add($"{code} - {displayName}");
      }

      int current = string.IsNullOrEmpty(currentCode) ? 0 : Mathf.Max(0, optionCodes.IndexOf(currentCode));
      return optionCodes[EditorGUILayout.Popup(label, current, optionNames.ToArray())];
    }

    private void DrawLanguageListSection()
    {
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.LabelField(
          $"{EditorLocalization.Get("localization.languageList")} ({_languages.arraySize})",
          EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(EditorLocalization.Get("localization.add"), GUILayout.Width(60)))
        {
          AddNewLanguage();
        }
      }

      EditorGUILayout.Space(SpaceSmall);

      if (_languages.arraySize == 0)
      {
        EditorGUILayout.HelpBox(EditorLocalization.Get("localization.noLanguages"), MessageType.Info);
      }
      else
      {
        DrawLanguageListBox();
      }
    }

    private void DrawLanguageListBox()
    {
      EditorGUILayout.BeginVertical();

      for (int i = 0; i < _languages.arraySize; i++)
      {
        DrawLanguageRow(i);
      }

      EditorGUILayout.EndVertical();

      ExecutePendingDelete();
    }

    private void ExecutePendingDelete()
    {
      if (_pendingDeleteIndex < 0) return;

      if (_pendingDeleteIndex < _languages.arraySize)
      {
        _languages.DeleteArrayElementAtIndex(_pendingDeleteIndex);
        _foldouts.Clear();
        _keyValidationMessages.Clear();
      }
      _pendingDeleteIndex = -1;
    }

    private void DrawLanguageRow(int index)
    {
      var langProp = _languages.GetArrayElementAtIndex(index);
      var languageCode = langProp.FindPropertyRelative("languageCode");
      var displayName = langProp.FindPropertyRelative("displayName");
      var translationFile = langProp.FindPropertyRelative("translationFile");
      var font = langProp.FindPropertyRelative("font");

      var rowRect = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(rowRect, RowColor(index));

      using (new EditorGUILayout.HorizontalScope(GUILayout.Height(22)))
      {
        GUILayout.Space(4);

        if (!_foldouts.ContainsKey(index)) _foldouts[index] = false;

        var foldoutContent = new GUIContent(_foldouts[index] ? "▼" : "▶");
        if (GUILayout.Button(foldoutContent, EditorStyles.label, GUILayout.Width(16), GUILayout.Height(22)))
        {
          _foldouts[index] = !_foldouts[index];
        }

        EditorGUILayout.LabelField(languageCode.stringValue, RowNameStyle, GUILayout.Width(50));
        EditorGUILayout.LabelField($"({displayName.stringValue})", DisplayNameStyle, GUILayout.MinWidth(80));

        GUILayout.FlexibleSpace();

        if (_defaultLanguage.stringValue == languageCode.stringValue)
        {
          var defaultStyle = new GUIStyle(EditorStyles.miniLabel)
          {
            normal = { textColor = DefaultMarkColor },
            alignment = TextAnchor.MiddleRight
          };
          GUILayout.Label(EditorLocalization.Get("localization.default"), defaultStyle, GUILayout.Width(70), GUILayout.Height(22));
        }

        if (GUILayout.Button("✕", EditorStyles.label, GUILayout.Width(20), GUILayout.Height(22)))
        {
          if (EditorUtility.DisplayDialog(
              EditorLocalization.Get("localization.deleteLanguage"),
              string.Format(EditorLocalization.Get("localization.deleteConfirm"), displayName.stringValue),
              EditorLocalization.Get("localization.delete"),
              EditorLocalization.Get("button.cancel")))
          {
            _pendingDeleteIndex = index;
          }
        }
        GUILayout.Space(4);
      }

      if (_foldouts[index])
      {
        DrawLanguageDetails(index, languageCode, displayName, translationFile, font);
      }

      EditorGUILayout.EndVertical();
    }

    private void DrawLanguageDetails(int index, SerializedProperty languageCode, SerializedProperty displayName, SerializedProperty translationFile, SerializedProperty font)
    {
      var detailRect = EditorGUILayout.BeginVertical();
      EditorGUI.DrawRect(detailRect, DetailColor);

      GUILayout.Space(SpaceSmall);
      using (new EditorGUILayout.HorizontalScope())
      {
        GUILayout.Space(20);
        using (new EditorGUILayout.VerticalScope())
        {
          float originalLabelWidth = EditorGUIUtility.labelWidth;
          EditorGUIUtility.labelWidth = originalLabelWidth - 20;

          EditorGUILayout.PropertyField(languageCode, new GUIContent(EditorLocalization.Get("localization.languageCode")));
          EditorGUILayout.PropertyField(displayName, new GUIContent(EditorLocalization.Get("localization.displayName")));

          GUILayout.Space(SpaceSmall);

          using (new EditorGUILayout.HorizontalScope())
          {
            var previousFile = translationFile.objectReferenceValue;
            EditorGUILayout.PropertyField(translationFile, new GUIContent(EditorLocalization.Get("localization.jsonFile")));

            if (translationFile.objectReferenceValue != previousFile && translationFile.objectReferenceValue != null)
            {
              ValidateTranslationKeysForLanguage(index, translationFile.objectReferenceValue as TextAsset);
            }

            if (translationFile.objectReferenceValue != null)
            {
              if (GUILayout.Button(EditorLocalization.Get("button.edit"), GUILayout.Width(50)))
              {
                OpenTranslationFile(translationFile.objectReferenceValue as TextAsset);
              }
            }
          }

          if (translationFile.objectReferenceValue == null)
          {
            EditorGUILayout.HelpBox(EditorLocalization.Get("localization.assignTranslationFile"), MessageType.Warning);
          }
          else if (_keyValidationMessages.TryGetValue(index, out var message) && !string.IsNullOrEmpty(message))
          {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
          }

          GUILayout.Space(SpaceSmall);

          EditorGUILayout.PropertyField(font, new GUIContent(EditorLocalization.Get("localization.font")));
          if (font.objectReferenceValue == null)
          {
            EditorGUILayout.HelpBox(EditorLocalization.Get("localization.fontDesc"), MessageType.Info);
          }

          EditorGUIUtility.labelWidth = originalLabelWidth;
        }
        GUILayout.Space(8);
      }
      GUILayout.Space(SpaceSmall);

      EditorGUILayout.EndVertical();
    }

    private void ValidateTranslationKeysForLanguage(int index, TextAsset newFile)
    {
      if (newFile == null)
      {
        _keyValidationMessages.Remove(index);
        return;
      }

      TextAsset referenceFile = null;
      string referenceCode = null;

      for (int i = 0; i < _languages.arraySize; i++)
      {
        if (i == index) continue;

        var lang = _languages.GetArrayElementAtIndex(i);
        var file = lang.FindPropertyRelative("translationFile").objectReferenceValue as TextAsset;
        if (file != null)
        {
          referenceFile = file;
          referenceCode = lang.FindPropertyRelative("languageCode").stringValue;
          break;
        }
      }

      if (referenceFile == null)
      {
        _keyValidationMessages.Remove(index);
        return;
      }

      var referenceKeys = ParseJsonKeys(referenceFile.text);
      var newKeys = ParseJsonKeys(newFile.text);

      if (referenceKeys == null || newKeys == null)
      {
        _keyValidationMessages[index] = EditorLocalization.Get("localization.jsonParseError");
        return;
      }

      var missingKeys = referenceKeys.Except(newKeys).ToList();
      var extraKeys = newKeys.Except(referenceKeys).ToList();

      if (missingKeys.Count == 0 && extraKeys.Count == 0)
      {
        _keyValidationMessages.Remove(index);
        return;
      }

      var issues = new List<string>();
      if (missingKeys.Count > 0)
      {
        var keyList = string.Join(", ", missingKeys.Take(3));
        var others = missingKeys.Count > 3
          ? $" {string.Format(EditorLocalization.Get("localization.keyOthers"), missingKeys.Count - 3)}"
          : "";
        issues.Add($"{EditorLocalization.Get("localization.keyMissing")}: {keyList}{others}");
      }
      if (extraKeys.Count > 0)
      {
        var keyList = string.Join(", ", extraKeys.Take(3));
        var others = extraKeys.Count > 3
          ? $" {string.Format(EditorLocalization.Get("localization.keyOthers"), extraKeys.Count - 3)}"
          : "";
        issues.Add($"{EditorLocalization.Get("localization.keyExtra")}: {keyList}{others}");
      }

      _keyValidationMessages[index] = $"{string.Format(EditorLocalization.Get("localization.keyDifference"), referenceCode)}\n{string.Join("\n", issues)}";
    }

    private HashSet<string> ParseJsonKeys(string jsonText)
    {
      try
      {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonText);
        return dict != null ? new HashSet<string>(dict.Keys) : null;
      }
      catch
      {
        return null;
      }
    }

    private void AddNewLanguage()
    {
      _languages.InsertArrayElementAtIndex(_languages.arraySize);
      var newLang = _languages.GetArrayElementAtIndex(_languages.arraySize - 1);

      newLang.FindPropertyRelative("languageCode").stringValue = "";
      newLang.FindPropertyRelative("displayName").stringValue = "";
      newLang.FindPropertyRelative("translationFile").objectReferenceValue = null;
      newLang.FindPropertyRelative("font").objectReferenceValue = null;

      _foldouts[_languages.arraySize - 1] = true;
    }

    private void OpenTranslationFile(TextAsset file)
    {
      if (file == null) return;
      TranslationEditorWindow.Open(file);
    }
  }
}
