using UnityEngine;
using Yamadev.YamaStream.Editor;
using Yamadev.YamaStream.UI;
using VRC.SDK3.Components;

namespace Yamadev.YamaStream.Modules.PlaylistLoader.Editor
{
  // Wires two independent things. Runs at scene build and on play-mode entry
  // (IProcessSceneWithReport), after YamaPlayerModuleBuildProcess (-3000) has
  // tagged inactive modules EditorOnly.
  //
  // - Playlist slots (issue #88), per PlaylistLoader
  // - The URL interceptor (issue #82), per PlaylistLoaderUI
  public class PlaylistLoaderBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2500;

    public void Process()
    {
      // Slots are wired off the loader itself, not off the UI: DefaultUrl
      // (DefaultUrlController.cs:62) and Auto Load call LoadPlaylistFromUrl
      // directly, so a world can run PlaylistLoader with no
      // PlaylistLoaderUI at all and still needs somewhere to load into.
      var loaders = Object.FindObjectsByType<PlaylistLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var loader in loaders)
      {
        ProcessSlots(loader);
      }

      var loaderUis = Object.FindObjectsByType<PlaylistLoaderUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var loaderUi in loaderUis)
      {
        ProcessInterceptor(loaderUi);
      }
    }

    // Instance-lifetime playlist slots the loader may fill (issue #88). Read
    // from the Controller hierarchy so the slot count is whatever the prefab
    // ships with, and Controller.ReadPlaylists sees the same set.
    private static void ProcessSlots(PlaylistLoader loader)
    {
      if (loader == null || !loader.gameObject.activeInHierarchy) return;
      var controller = ResolveController(loader);
      if (controller == null) return;

      loader.SetProgramVariable("_dynamicPlaylists", DynamicPlaylistBuildProcess.CollectUsableSlots(controller));
    }

    // Prefer the parent chain (module under Controller/Modules), but fall
    // back to the serialized _controller reference for placements outside
    // that chain.
    private static Controller ResolveController(PlaylistLoader loader)
    {
      var controller = loader.GetComponentInParent<Controller>(true);
      if (controller == null) controller = loader.GetProgramVariable("_controller") as Controller;
      return controller;
    }

    private static void ProcessInterceptor(PlaylistLoaderUI loaderUi)
    {
      if (loaderUi == null || !loaderUi.gameObject.activeInHierarchy) return;
      var loader = loaderUi.GetProgramVariable("_loader") as PlaylistLoader;
      if (loader == null) return;
      var controller = ResolveController(loader);
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
        // Tells the player which URL kinds this world accepts (issue #84).
        // Set on every paired UIController so multi-panel setups all show it.
        uiController.SetProgramVariable("_urlHintKey", "module.playlistLoader.inputHint");
      }
    }
  }
}
