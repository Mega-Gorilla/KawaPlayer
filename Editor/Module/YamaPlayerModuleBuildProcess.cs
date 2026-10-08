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
      ReportAutoplayConflicts();
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

    // DefaultUrl (the instance owner's autoplay) and AutoPlay (the world's)
    // both start a video when someone joins, and nothing decides between
    // them: AutoPlay usually runs first, and the owner's default URL then
    // finds the player busy and is not played. Reported rather than refused,
    // like a duplicate: which one to keep is the creator's to decide.
    internal const string OwnerAutoplayModuleName = "DefaultUrl";
    internal const string WorldAutoplayModuleName = "AutoPlay";

    private static void ReportAutoplayConflicts()
    {
      var definitions = Object.FindObjectsByType<YamaPlayerModuleDefinition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var (owner, world) in FindAutoplayConflicts(definitions))
      {
        Debug.LogError(
          "[KawaPlayer] DefaultUrl and AutoPlay are both enabled on one player. Both start a video when someone joins and nothing decides between them, " +
          "so the instance owner's default URL may not play. Disable one of them:\n" +
          GetPath(owner.transform) + "\n" + GetPath(world.transform),
          world.gameObject);
      }
    }

    // Per Controller, counted as the build counts modules (IsModuleEnabled).
    internal static List<(YamaPlayerModuleDefinition owner, YamaPlayerModuleDefinition world)> FindAutoplayConflicts(IEnumerable<YamaPlayerModuleDefinition> definitions)
    {
      return definitions
        .Where(definition => definition != null && IsModuleEnabled(definition))
        .Select(definition => (definition, controller: definition.GetComponentInParent<Controller>(true)))
        .Where(entry => entry.controller != null)
        .GroupBy(entry => entry.controller)
        .Select(group => (
          owner: group.Select(entry => entry.definition).FirstOrDefault(definition => definition.moduleName == OwnerAutoplayModuleName),
          world: group.Select(entry => entry.definition).FirstOrDefault(definition => definition.moduleName == WorldAutoplayModuleName && PlaysOnJoin(definition))))
        .Where(pair => pair.owner != null && pair.world != null)
        .ToList();
    }

    // An AutoPlay set to Off plays nothing. Its mode is read by its
    // serialized name, as this assembly does not reference the module's;
    // 0 is AutoPlayMode.Off.
    private static bool PlaysOnJoin(YamaPlayerModuleDefinition worldAutoplay)
    {
      var module = worldAutoplay.GetComponent<YamaPlayerModule>();
      if (module == null) return false;
      var mode = new SerializedObject(module).FindProperty("_autoPlayMode");
      return mode == null || mode.intValue != 0;
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

    // The object a module is switched off and deleted by: the largest one
    // under the player's Modules that holds this module and no other. That is
    // the module's own object, or the one it keeps its other parts in --
    // DefaultUrl's definition is on Modules/DefaultUrl/Controller, beside the
    // storage in Modules/DefaultUrl/OwnerStorage -- but never a folder the
    // creator sorts several modules into, which would take the others with
    // it. A module outside a Modules is its own root.
    internal static GameObject GetModuleRoot(Component module)
    {
      var manager = module.GetComponentInParent<ModuleManager>(true);
      if (manager == null) return module.gameObject;
      var root = module.transform;
      for (var parent = root.parent; parent != null && parent != manager.transform; parent = parent.parent)
      {
        if (parent.GetComponentsInChildren<YamaPlayerModuleDefinition>(true).Length > 1) break;
        root = parent;
      }
      return root.gameObject;
    }

    // Whether ProcessModule builds the module: both its own object and its
    // root are on. Another parent being off, or its component being disabled,
    // does not leave it out.
    internal static bool IsModuleEnabled(Component module)
    {
      return module.gameObject.activeSelf && GetModuleRoot(module).activeSelf;
    }

    // The root is checked first, so a module switched off at both is still
    // left out with all its parts, not just its own object.
    private static void ProcessModule(YamaPlayerModule module)
    {
      if (module == null) return;
      var root = GetModuleRoot(module);
      if (!root.activeSelf)
      {
        root.tag = "EditorOnly";
        return;
      }
      if (!module.gameObject.activeSelf)
      {
        module.gameObject.tag = "EditorOnly";
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
