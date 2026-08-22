using UnityEngine;
using Yamadev.YamaStream.Editor;
using Yamadev.YamaStream.UI;
using VRC.SDK3.Components;

namespace Yamadev.YamaStream.Modules.PlaylistLoader.Editor
{
  // Wires the URL interceptor (issue #82): UIController._urlInterceptor gets
  // the PlaylistLoaderUI proxy, and PlaylistLoaderUI._uiController gets the
  // UIController that owns the main URL inputs. Runs at scene build and on
  // play-mode entry (IProcessSceneWithReport), after YamaPlayerModuleBuildProcess
  // (-3000) has tagged inactive modules EditorOnly.
  public class PlaylistLoaderBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2500;

    public void Process()
    {
      var loaderUis = Object.FindObjectsByType<PlaylistLoaderUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var loaderUi in loaderUis)
      {
        ProcessInterceptor(loaderUi);
      }
    }

    private static void ProcessInterceptor(PlaylistLoaderUI loaderUi)
    {
      if (loaderUi == null || !loaderUi.gameObject.activeInHierarchy) return;
      var loader = loaderUi.GetProgramVariable("_loader") as PlaylistLoader;
      if (loader == null) return;
      // Prefer the parent chain (module under Controller/Modules), but fall
      // back to the serialized _controller reference for placements outside
      // that chain.
      var controller = loader.GetComponentInParent<Controller>(true);
      if (controller == null) controller = loader.GetProgramVariable("_controller") as Controller;
      if (controller == null) return;

      var uiControllers = Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var uiController in uiControllers)
      {
        if (uiController == null || uiController.GetProgramVariable("_controller") as Controller != controller) continue;
        // Only the UIController that owns the main URL inputs participates;
        // panels without inputs (PlaylistPanel etc.) never reach PlayUrlField.
        var mainInput = uiController.GetProgramVariable("_urlInputField") as VRCUrlInputField;
        var topInput = uiController.GetProgramVariable("_urlInputFieldTop") as VRCUrlInputField;
        if (mainInput == null && topInput == null) continue;

        uiController.SetProgramVariable("_urlInterceptor", loaderUi);
        loaderUi.SetProgramVariable("_uiController", uiController);
      }
    }
  }
}
