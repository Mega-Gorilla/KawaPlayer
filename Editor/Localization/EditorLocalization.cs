using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  public static class EditorLocalization
  {
    private const string TranslationFolder = "Packages/com.vhub.kawaplayer/Assets/Localization/Editor";
    private const string LanguageKey = "YamaPlayer_EditorLanguage";
    private const string DefaultLanguage = "en";

    private static readonly (string code, string name)[] SupportedLanguages =
    {
      ("ja", "日本語"),
      ("en", "English"),
      ("es", "Español"),
      ("ko", "한국어"),
      ("ru", "Русский"),
      ("uk", "Українська"),
      ("it", "Italiano"),
      ("zh-CN", "简体中文"),
      ("zh-TW", "繁體中文")
    };

    private static Dictionary<string, Dictionary<string, string>> _translations;

    public static string[] AvailableLanguages => SupportedLanguages.Select(l => l.code).ToArray();

    public static string CurrentLanguage
    {
      get
      {
        var saved = EditorPrefs.GetString(LanguageKey);
        if (!string.IsNullOrEmpty(saved) && AvailableLanguages.Contains(saved))
          return saved;

        var systemLanguage = CultureInfo.CurrentCulture.Name;
        var matched = AvailableLanguages.FirstOrDefault(l =>
            systemLanguage.StartsWith(l, StringComparison.OrdinalIgnoreCase));

        var language = matched ?? DefaultLanguage;
        EditorPrefs.SetString(LanguageKey, language);
        return language;
      }
      set => EditorPrefs.SetString(LanguageKey, value);
    }

    private static Dictionary<string, Dictionary<string, string>> Translations
    {
      get
      {
        if (_translations == null)
        {
          _translations = new Dictionary<string, Dictionary<string, string>>();
          foreach (var (code, _) in SupportedLanguages)
          {
            var path = $"{TranslationFolder}/{code}.json";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) continue;
            try
            {
              var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
              if (dict != null)
              {
                _translations[code] = dict;
              }
            }
            catch (Exception e)
            {
              Debug.LogWarning($"[EditorLocalization] Failed to parse {path}: {e.Message}");
            }
          }

          LoadModuleTranslations();
        }
        return _translations;
      }
    }

    private static void LoadModuleTranslations()
    {
      ModuleManagerEditor.FindYamaPlayerModules();

      // Standalone modules: editor translation file is reachable via the
      // module definition registered with ModuleManager.ModuleDefinitions.
      foreach (var (moduleDefinition, _) in ModuleManager.ModuleDefinitions)
      {
        MergeEditorTranslations(moduleDefinition.editorTranslationFile, moduleDefinition.moduleName);
      }

      // Embedded modules (e.g. KawaPlayer.prefab/Modules/DefaultUrl/Controller):
      // their YamaPlayerModuleDefinition is intentionally NOT in
      // ModuleDefinitions because that dictionary feeds Module Manager's
      // Available Modules section (where the dictionary value is the prefab to
      // InstantiatePrefab). Registering an embedded definition there would
      // clone the parent prefab (KawaPlayer.prefab) into the scene. Instead we
      // walk every prefab independently here just to harvest editor
      // translation files, deduping against the standalone set.
      string[] guids = AssetDatabase.FindAssets("t:Prefab");
      foreach (string guid in guids)
      {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) continue;

        var nestedDefinitions = prefab.GetComponentsInChildren<YamaPlayerModuleDefinition>(true);
        foreach (var def in nestedDefinitions)
        {
          if (def == null) continue;
          if (ModuleManager.ModuleDefinitions.ContainsKey(def)) continue; // already merged above
          MergeEditorTranslations(def.editorTranslationFile, def.moduleName);
        }
      }
    }

    private static void MergeEditorTranslations(TextAsset file, string label)
    {
      if (file == null) return;

      try
      {
        var nested = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(file.text);
        if (nested == null) return;

        foreach (var (langCode, translations) in nested)
        {
          if (!_translations.ContainsKey(langCode))
            _translations[langCode] = new Dictionary<string, string>();

          foreach (var (key, value) in translations)
          {
            _translations[langCode][key] = value;
          }
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[EditorLocalization] Failed to parse module translation ({label}): {e.Message}");
      }
    }

    public static string GetValue(string key, string language)
    {
      if (Translations.TryGetValue(language, out var dict) && dict.TryGetValue(key, out var value))
        return value;
      return null;
    }

    public static string Get(string key) =>
        GetValue(key, CurrentLanguage) ?? GetValue(key, DefaultLanguage) ?? key;

    public static GUIContent GetLayout(string key) => new GUIContent(Get(key));

    public static GUIContent GetLayout(string labelKey, string tooltipKey) =>
        new GUIContent(Get(labelKey), Get(tooltipKey));

    public static string GetLanguageName(string languageCode)
    {
      var found = SupportedLanguages.FirstOrDefault(l => l.code == languageCode);
      return found.name ?? languageCode;
    }

    public static void ReloadTranslations()
    {
      _translations = null;
    }
  }
}
