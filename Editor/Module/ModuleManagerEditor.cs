using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Yamadev.YamaStream.Editor
{
  [CustomEditor(typeof(ModuleManager))]
  public class ModuleManagerEditor : EditorBase
  {
    private const float RowHeight = 36f;

    private ModuleManager _moduleManager;
    private Vector2 _installedScrollPos;
    private Vector2 _availableScrollPos;

    private static readonly Color ActiveColor = new Color(0.4f, 0.8f, 0.4f);
    private static Color SubTextColor => EditorGUIUtility.isProSkin ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.45f, 0.45f, 0.45f);

    private void OnEnable()
    {
      _moduleManager = (ModuleManager)target;
      Title = EditorLocalization.Get("module.manager.title");
      FindYamaPlayerModules();
    }

    public override void OnInspectorGUI()
    {
      base.OnInspectorGUI();

      DrawInstalledModulesSection();
      EditorGUILayout.Space(SpaceLarge);
      DrawAvailableModulesSection();
    }

    private void DrawInstalledModulesSection()
    {
      EditorGUILayout.LabelField(EditorLocalization.Get("module.manager.installedModules"), EditorStyles.boldLabel);
      EditorGUILayout.Space(SpaceSmall);

      DrawModuleErrors(_moduleManager);

      var installedModules = GetInstalledModules(_moduleManager);

      if (installedModules.Count == 0)
      {
        EditorGUILayout.HelpBox(EditorLocalization.Get("module.manager.noInstalledModules"), MessageType.Info);
        return;
      }

      for (int i = 0; i < installedModules.Count; i++)
      {
        DrawInstalledModuleRow(installedModules[i], i);
      }
    }

    // What the build reports about these modules, shown before it is built.
    // Counted over the whole player, as the build does, so a module outside
    // this list is caught too. Also used by the player inspector.
    internal static void DrawModuleErrors(ModuleManager moduleManager)
    {
      var controller = moduleManager == null ? null : moduleManager.GetComponentInParent<Controller>(true);
      if (controller == null) return;

      // A player nested inside this one owns its own modules.
      var definitions = controller.GetComponentsInChildren<YamaPlayerModuleDefinition>(true)
        .Where(definition => definition.GetComponentInParent<Controller>(true) == controller)
        .ToList();

      // The Add button keeps a module to one copy, but one dragged in by hand
      // still gets built (issue #55).
      foreach (var copies in YamaPlayerModuleBuildProcess.FindDuplicateModules(definitions))
      {
        EditorGUILayout.HelpBox(
          string.Format(EditorLocalization.Get("module.manager.duplicate"), MessageName(copies[0]), copies.Count),
          MessageType.Error);
      }

      foreach (var (owner, world) in YamaPlayerModuleBuildProcess.FindAutoplayConflicts(definitions))
      {
        EditorGUILayout.HelpBox(
          string.Format(EditorLocalization.Get("module.manager.autoplayConflict"), MessageName(owner), MessageName(world)),
          MessageType.Error);
      }
    }

    // A help box wraps at the first space it can, even in Japanese, so a
    // name such as "デフォルト URL" was split across lines; within a name
    // the space does not break.
    private static string MessageName(YamaPlayerModuleDefinition module)
    {
      return GetModuleName(module).Replace(' ', '\u00A0');
    }

    // The module's translated name, or its own name without a translation.
    // Also used by the player inspector's module list.
    internal static string GetModuleName(YamaPlayerModuleDefinition module)
    {
      if (!string.IsNullOrEmpty(module.moduleNameTranslationKey))
      {
        var translated = EditorLocalization.Get(module.moduleNameTranslationKey);
        if (!string.IsNullOrEmpty(translated))
          return translated;
      }
      return module.moduleName;
    }

    private string GetModuleDescription(YamaPlayerModuleDefinition module)
    {
      if (!string.IsNullOrEmpty(module.moduleDescriptionTranslationKey))
      {
        var translated = EditorLocalization.Get(module.moduleDescriptionTranslationKey);
        if (!string.IsNullOrEmpty(translated))
          return translated;
      }
      return module.moduleDescription;
    }

    private void DrawInstalledModuleRow(YamaPlayerModuleDefinition module, int index)
    {
      bool isActive = YamaPlayerModuleBuildProcess.IsModuleEnabled(module);
      var rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
      EditorGUI.DrawRect(rowRect, RowColor(index));

      var statusBarRect = new Rect(rowRect.x, rowRect.y, 3, rowRect.height);
      EditorGUI.DrawRect(statusBarRect, isActive ? ActiveColor : SubTextColor);

      var nameStyle = new GUIStyle(EditorStyles.label)
      {
        fontSize = 11,
        fontStyle = FontStyle.Bold
      };
      var nameRect = new Rect(rowRect.x + 10, rowRect.y + 2, rowRect.width - 180, 16);
      EditorGUI.LabelField(nameRect, GetModuleName(module), nameStyle);

      var subStyle = new GUIStyle(EditorStyles.miniLabel)
      {
        fontSize = 9
      };
      subStyle.normal.textColor = SubTextColor;

      string moduleDescription = GetModuleDescription(module);
      string subText = "";
      if (!string.IsNullOrEmpty(module.version))
        subText = $"v{module.version}";
      if (!string.IsNullOrEmpty(moduleDescription))
        subText = string.IsNullOrEmpty(subText) ? moduleDescription : $"{subText} | {moduleDescription}";

      var subRect = new Rect(rowRect.x + 10, rowRect.y + 18, rowRect.width - 180, 14);
      EditorGUI.LabelField(subRect, subText, subStyle);

      float buttonWidth = 50f;
      float buttonHeight = 18f;
      float buttonY = rowRect.y + (rowRect.height - buttonHeight) / 2;
      float buttonsStartX = rowRect.xMax - 168;

      if (!module.noNeedSetUp)
      {
        var settingsRect = new Rect(buttonsStartX, buttonY, buttonWidth, buttonHeight);
        if (GUI.Button(settingsRect, EditorLocalization.Get("module.manager.button.settings")))
        {
          if (ActiveEditorTracker.sharedTracker.isLocked)
          {
            EditorUtility.DisplayDialog(
              EditorLocalization.Get("msg.inspectorLocked.title"),
              EditorLocalization.Get("msg.inspectorLocked"),
              EditorLocalization.Get("button.ok"));
          }
          else
          {
            Selection.activeGameObject = module.gameObject;
          }
        }
      }

      var toggleRect = new Rect(buttonsStartX + buttonWidth + 4, buttonY, buttonWidth + 6, buttonHeight);
      if (GUI.Button(toggleRect, isActive ? EditorLocalization.Get("module.manager.button.disable") : EditorLocalization.Get("module.manager.button.enable")))
      {
        SetModuleEnabled(module, !isActive);
      }

      var root = YamaPlayerModuleBuildProcess.GetModuleRoot(module);
      bool canDelete = CanDeleteModule(root);
      var deleteRect = new Rect(buttonsStartX + buttonWidth * 2 + 14, buttonY, buttonWidth, buttonHeight);
      var deleteContent = new GUIContent(EditorLocalization.Get("module.manager.button.delete"), canDelete ? "" : EditorLocalization.Get("module.manager.deleteInPrefab"));
      using (new EditorGUI.DisabledScope(!canDelete))
      {
        if (GUI.Button(deleteRect, deleteContent))
        {
          if (EditorUtility.DisplayDialog(
            EditorLocalization.Get("module.manager.deleteTitle"),
            string.Format(EditorLocalization.Get("module.manager.deleteConfirm"), GetModuleName(module)),
            EditorLocalization.Get("module.manager.button.delete"),
            EditorLocalization.Get("button.cancel")))
          {
            Undo.DestroyObjectImmediate(root);
          }
        }
      }
    }

    // A module the prefab brings with it, such as KawaPlayer.prefab's own
    // PlaylistLoader and DefaultUrl, is only switched off here. Deleting it
    // would leave a removal override on the instance, and it could not be
    // added back from Available Modules, which lists standalone module
    // prefabs only. An instance's root, and a module added to an instance,
    // can go.
    private static bool CanDeleteModule(GameObject root)
    {
      return !PrefabUtility.IsPartOfPrefabInstance(root)
        || PrefabUtility.IsOutermostPrefabInstanceRoot(root)
        || PrefabUtility.IsAddedGameObjectOverride(root);
    }

    private void DrawAvailableModulesSection()
    {
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.LabelField(EditorLocalization.Get("module.manager.availableModules"), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(EditorLocalization.Get("module.manager.button.refresh"), GUILayout.Width(50)))
        {
          FindYamaPlayerModules();
        }
      }

      EditorGUILayout.Space(SpaceSmall);

      var installedModules = GetInstalledModules(_moduleManager);
      var definitions = ModuleManager.ModuleDefinitions
        .Where(d => d.Key.allowMultiple || !installedModules.Any(m => m.moduleName == d.Key.moduleName))
        .ToList();

      if (definitions.Count == 0)
      {
        EditorGUILayout.HelpBox(EditorLocalization.Get("module.manager.noAvailableModules"), MessageType.Info);
        return;
      }

      for (int i = 0; i < definitions.Count; i++)
      {
        DrawAvailableModuleRow(definitions[i].Key, definitions[i].Value, installedModules, i);
      }
    }

    private void DrawAvailableModuleRow(YamaPlayerModuleDefinition definition, GameObject prefab, List<YamaPlayerModuleDefinition> installedModules, int index)
    {
      bool isInstalled = installedModules.Any(m => m.moduleName == definition.moduleName);
      bool canAdd = definition.allowMultiple || !isInstalled;

      var rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
      EditorGUI.DrawRect(rowRect, RowColor(index));

      var installedBarRect = new Rect(rowRect.x, rowRect.y, 3, rowRect.height);
      EditorGUI.DrawRect(installedBarRect, isInstalled ? ActiveColor : Color.white);

      var nameStyle = new GUIStyle(EditorStyles.label)
      {
        fontSize = 11,
        fontStyle = FontStyle.Bold
      };
      var nameRect = new Rect(rowRect.x + 10, rowRect.y + 2, rowRect.width - 80, 16);
      EditorGUI.LabelField(nameRect, GetModuleName(definition), nameStyle);

      var subStyle = new GUIStyle(EditorStyles.miniLabel)
      {
        fontSize = 9
      };
      subStyle.normal.textColor = SubTextColor;

      string moduleDescription = GetModuleDescription(definition);
      string subText = "";
      if (!string.IsNullOrEmpty(definition.version))
        subText = $"v{definition.version}";
      if (!string.IsNullOrEmpty(moduleDescription))
        subText = string.IsNullOrEmpty(subText) ? moduleDescription : $"{subText} | {moduleDescription}";
      if (isInstalled)
      {
        string installedLabel = EditorLocalization.Get("module.manager.installed");
        subText = string.IsNullOrEmpty(subText) ? installedLabel : $"{subText} {installedLabel}";
      }

      var subRect = new Rect(rowRect.x + 10, rowRect.y + 18, rowRect.width - 80, 14);
      EditorGUI.LabelField(subRect, subText, subStyle);

      float buttonWidth = 50f;
      float buttonHeight = 18f;
      float buttonY = rowRect.y + (rowRect.height - buttonHeight) / 2;
      var addRect = new Rect(rowRect.xMax - buttonWidth - 8, buttonY, buttonWidth, buttonHeight);

      using (new EditorGUI.DisabledScope(!canAdd))
      {
        if (GUI.Button(addRect, EditorLocalization.Get("module.manager.button.add")))
        {
          AddModule(prefab);
        }
      }
    }

    private void AddModule(GameObject prefab)
    {
      GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _moduleManager.transform);
      Undo.RegisterCreatedObjectUndo(instance, "Add Module");
      instance.name = prefab.name;
      EditorUtility.SetDirty(_moduleManager);
    }

    // Every module under these Modules however deep it is kept (DefaultUrl's
    // definition is on Modules/DefaultUrl/Controller), except those of a
    // player nested inside, which has Modules of its own. Also used by the
    // player inspector's module list.
    internal static List<YamaPlayerModuleDefinition> GetInstalledModules(ModuleManager moduleManager)
    {
      if (moduleManager == null) return new List<YamaPlayerModuleDefinition>();
      return moduleManager.GetComponentsInChildren<YamaPlayerModuleDefinition>(true)
        .Where(definition => definition.GetComponentInParent<ModuleManager>(true) == moduleManager)
        .ToList();
    }

    // Switches the module's root (YamaPlayerModuleBuildProcess.GetModuleRoot)
    // so the module's other parts go with it. Switching on also switches on
    // the module's own object, in case it was switched off by hand. Also used
    // by the player inspector's module list.
    internal static void SetModuleEnabled(YamaPlayerModuleDefinition module, bool enabled)
    {
      var root = YamaPlayerModuleBuildProcess.GetModuleRoot(module);
      var objects = enabled ? new[] { root, module.gameObject }.Distinct().ToArray() : new[] { root };
      Undo.RecordObjects(objects, enabled ? "Enable Module" : "Disable Module");
      foreach (var moduleObject in objects)
      {
        moduleObject.SetActive(enabled);
        EditorUtility.SetDirty(moduleObject);
      }
    }

    public static void FindYamaPlayerModules()
    {
      ModuleManager.ModuleDefinitions.Clear();

      // Standalone module prefabs only: the dictionary value is used by
      // AddModule() to InstantiatePrefab(...), so registering an embedded
      // module here would clone the parent prefab (e.g. all of
      // KawaPlayer.prefab) into the scene's ModuleManager. Embedded module
      // discovery (for editor translation lookup, etc.) must use a separate
      // path that does not feed Available Modules.
      string[] guids = AssetDatabase.FindAssets("t:Prefab");
      foreach (string guid in guids)
      {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) continue;

        YamaPlayerModuleDefinition moduleDefinition = prefab.GetComponent<YamaPlayerModuleDefinition>();
        if (moduleDefinition != null)
        {
          ModuleManager.ModuleDefinitions[moduleDefinition] = prefab;
        }
      }
    }
  }
}
