using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Yamadev.YamaStream.UI;
using UnityEngine.UI;

namespace Yamadev.YamaStream.Editor
{
  public class YamaPlayerModuleBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -3000;

    public void Process()
    {
      ReportDuplicateModules();
      var modules = Object.FindObjectsByType<YamaPlayerModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var module in modules)
      {
        ProcessModule(module);
      }
    }

    // allowMultiple = false is only enforced by Module Manager's Add button,
    // so a copy dragged in by hand is still built: it runs on its own, and any
    // UI the module has is added once per copy (issue #55). Say so rather than
    // drop one, because which copy the creator meant to keep is theirs to decide.
    private static void ReportDuplicateModules()
    {
      var definitions = Object.FindObjectsByType<YamaPlayerModuleDefinition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var copies in FindDuplicateModules(definitions))
      {
        Debug.LogError(
          $"[KawaPlayer] Module \"{copies[0].moduleName}\" is placed {copies.Count} times on one player, but only one is allowed. " +
          "Each copy runs separately, and any UI the module has appears once per copy. Remove all but one:\n" +
          string.Join("\n", copies.Select(copy => GetPath(copy.transform))),
          copies[1].gameObject);
      }
    }

    // Grouped by the nearest Controller, so two players in one scene may
    // each have the same module. A module counts exactly when ProcessModule
    // builds it (IsModuleEnabled).
    internal static List<List<YamaPlayerModuleDefinition>> FindDuplicateModules(IEnumerable<YamaPlayerModuleDefinition> definitions)
    {
      return definitions
        .Where(definition => definition != null && IsModuleEnabled(definition)
          && !definition.allowMultiple && !string.IsNullOrEmpty(definition.moduleName))
        .Select(definition => (definition, controller: definition.GetComponentInParent<Controller>(true)))
        .Where(entry => entry.controller != null)
        .GroupBy(entry => (entry.controller, entry.definition.moduleName))
        .Where(group => group.Count() > 1)
        .Select(group => group.Select(entry => entry.definition).OrderBy(definition => GetPath(definition.transform)).ToList())
        .ToList();
    }

    private static string GetPath(Transform transform)
    {
      var path = transform.name;
      for (var parent = transform.parent; parent != null; parent = parent.parent)
      {
        path = $"{parent.name}/{path}";
      }
      return path;
    }

    // The object directly under the player's Modules that holds a module: the
    // module's own object, or the one it shares with its other parts --
    // DefaultUrl's definition is on Modules/DefaultUrl/Controller, beside the
    // storage in Modules/DefaultUrl/OwnerStorage. Switching a module off
    // switches this off, so its other parts go with it. A module outside a
    // Modules is its own root.
    internal static GameObject GetModuleRoot(Component module)
    {
      var manager = module.GetComponentInParent<ModuleManager>(true);
      if (manager == null) return module.gameObject;
      var transform = module.transform;
      while (transform.parent != null && transform.parent != manager.transform) transform = transform.parent;
      return transform.parent == manager.transform ? transform.gameObject : module.gameObject;
    }

    // Whether ProcessModule builds the module: both its own object and its
    // root are on. Another parent being off, or its component being disabled,
    // does not leave it out.
    internal static bool IsModuleEnabled(Component module)
    {
      return module.gameObject.activeSelf && GetModuleRoot(module).activeSelf;
    }

    private static void ProcessModule(YamaPlayerModule module)
    {
      if (module == null) return;
      if (!module.gameObject.activeSelf)
      {
        module.gameObject.tag = "EditorOnly";
        return;
      }
      var root = GetModuleRoot(module);
      if (!root.activeSelf)
      {
        root.tag = "EditorOnly";
        return;
      }
      var definition = module.GetComponent<YamaPlayerModuleDefinition>();
      var controller = module.GetComponentInParent<Controller>(true);
      if (controller != null)
      {
        module.SetProgramVariable("_controller", controller);
        ProcessModuleUISlots(definition.uiSlots, controller);
      }
    }

    private static void ProcessModuleUISlots(ModuleUISlot[] uiSlots, Controller controller)
    {
      if (uiSlots == null || uiSlots.Length == 0 || controller == null) return;

      var uiControllers = Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var uiController in uiControllers)
      {
        if (uiController == null || uiController.GetProgramVariable("_controller") as Controller != controller) continue;

        var objectMapping = new Dictionary<Object, Object>();
        var instantiatedSlots = new List<(ModuleUISlot slot, GameObject copy, Transform target)>();

        foreach (var uiSlot in uiSlots)
        {
          if (uiSlot == null || uiSlot.content == null) continue;
          if (string.IsNullOrEmpty(uiSlot.targetPath)) continue;

          var targetTransform = uiController.transform.Find(uiSlot.targetPath);
          if (targetTransform == null) continue;

          var copy = Object.Instantiate(uiSlot.content, targetTransform);
          copy.name = uiSlot.content.name;
          instantiatedSlots.Add((uiSlot, copy, targetTransform));

          BuildObjectMapping(uiSlot.content, copy, objectMapping);
        }

        foreach (var (_, copy, _) in instantiatedSlots)
        {
          ReplaceReferences(copy, objectMapping);
        }

        foreach (var (uiSlot, copy, targetTransform) in instantiatedSlots)
        {
          var childCount = targetTransform.childCount;
          var siblingIndex = uiSlot.siblingIndex;

          if (siblingIndex >= 0)
          {
            copy.transform.SetSiblingIndex(siblingIndex >= childCount ? childCount - 1 : siblingIndex);
          }
          else
          {
            var calculatedIndex = childCount + siblingIndex;
            copy.transform.SetSiblingIndex(calculatedIndex < 0 ? 0 : calculatedIndex);
          }
        }

        var toggleGroups = uiController.GetComponentsInChildren<ToggleGroup>(true);
        foreach (var toggleGroup in toggleGroups)
        {
          foreach (var toggle in toggleGroup.GetComponentsInChildren<Toggle>(true))
          {
            if (toggle == null || toggle.transform.parent != toggleGroup.transform) continue;
            toggle.group = toggleGroup;
          }
        }
      }

      foreach (var uiSlot in uiSlots)
      {
        if (uiSlot?.content != null)
        {
          Object.DestroyImmediate(uiSlot.content);
        }
      }
    }

    private static void BuildObjectMapping(GameObject original, GameObject copy, Dictionary<Object, Object> mapping)
    {
      mapping[original] = copy;

      var originalComponents = original.GetComponents<Component>();
      var copyComponents = copy.GetComponents<Component>();
      for (int i = 0; i < originalComponents.Length && i < copyComponents.Length; i++)
      {
        if (originalComponents[i] != null && copyComponents[i] != null)
        {
          mapping[originalComponents[i]] = copyComponents[i];
        }
      }

      for (int i = 0; i < original.transform.childCount && i < copy.transform.childCount; i++)
      {
        BuildObjectMapping(original.transform.GetChild(i).gameObject, copy.transform.GetChild(i).gameObject, mapping);
      }
    }

    private static void ReplaceReferences(GameObject target, Dictionary<Object, Object> mapping)
    {
      var components = target.GetComponentsInChildren<Component>(true);
      foreach (var component in components)
      {
        if (component == null) continue;

        var serializedObject = new SerializedObject(component);
        var iterator = serializedObject.GetIterator();
        var modified = false;

        while (iterator.NextVisible(true))
        {
          if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null)
          {
            if (mapping.TryGetValue(iterator.objectReferenceValue, out var replacement))
            {
              iterator.objectReferenceValue = replacement;
              modified = true;
            }
          }
        }

        if (modified)
        {
          serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
      }
    }
  }
}
