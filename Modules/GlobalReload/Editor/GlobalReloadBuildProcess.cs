using System.Linq;
using UnityEngine;
using Yamadev.YamaStream.Editor;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.GlobalReload.Editor
{
  // Links each global reload button to the GlobalReload module of the player
  // its inspector names. A button with no player, or whose player has the
  // module missing or switched off, would do nothing, so it is left out of
  // the world with all its parts, and the build log says so (issue #248).
  // After YamaPlayerModuleBuildProcess, which sets each module's Controller;
  // a module counts exactly when that builds it (IsModuleEnabled).
  internal class GlobalReloadBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2900;

    public void Process()
    {
      var modules = Object.FindObjectsByType<GlobalReload>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(module => YamaPlayerModuleBuildProcess.IsModuleEnabled(module))
        .ToList();
      var buttons = Object.FindObjectsByType<GlobalReloadButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var button in buttons)
      {
        var uiController = button.GetComponentInParent<UIController>(true);
        var controller = uiController != null ? uiController.GetProgramVariable("_controller") as Controller : null;
        var module = controller != null ? modules.FirstOrDefault(m => m.GetComponentInParent<Controller>(true) == controller) : null;
        if (module != null)
        {
          button.SetProgramVariable("_module", module);
          continue;
        }

        var root = GetPlacedObject(button);
        root.SetActive(false);
        root.tag = "EditorOnly";
        var buttonPath = YamaPlayerModuleBuildProcess.GetPath(root.transform);
        if (controller == null)
        {
          Debug.LogWarning($"[KawaPlayer] The Global Reload Button {buttonPath} has no player set, so it is left out of the world. Choose the player in its inspector.");
        }
        else
        {
          var playerPath = YamaPlayerModuleBuildProcess.GetPath(GetPlacedObject(controller).transform);
          Debug.LogWarning($"[KawaPlayer] The Global Reload Button {buttonPath} is left out of the world: its player {playerPath} has no Global Reload module switched on. Add it, or switch it on, in the player's Module Manager.");
        }
      }
    }

    // The button's object that carries the inspector where its player is chosen.
    private static GameObject GetPlacedObject(GlobalReloadButton button)
    {
      var subController = button.GetComponentInParent<YamaPlayerSubController>(true);
      return subController != null ? subController.gameObject : button.gameObject;
    }

    // The player's object, as the creator sees it in the Hierarchy.
    private static GameObject GetPlacedObject(Controller controller)
    {
      var player = controller.GetComponentInParent<YamaPlayer>(true);
      return player != null ? player.gameObject : controller.gameObject;
    }
  }
}
