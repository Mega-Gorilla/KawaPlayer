using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using Yamadev.YamaStream.Editor;
using Yamadev.YamaStream.UI;
using VRC.SDK3.Components;

namespace Yamadev.YamaStream.Modules.PlaylistLoader.Editor
{
  // Wires four independent things. Runs at scene build and on play-mode entry
  // (IProcessSceneWithReport), after YamaPlayerModuleBuildProcess (-3000) has
  // tagged inactive modules EditorOnly.
  //
  // - Playlist slots (issue #88), per PlaylistLoader
  // - The URL interceptor (issue #82), per PlaylistLoaderUI
  // - The playlist refresh handler (issue #91), per PlaylistLoaderUI
  // - The search page (issue #145), per PlaylistSearch
  public class PlaylistLoaderBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2500;

    public void Process()
    {
      // Slots are wired off the loader itself, not off the UI: DefaultUrl
      // (DefaultUrlController.TryAutoPlay) and Auto Load call
      // LoadPlaylistFromUrl directly, so a world can run PlaylistLoader with
      // no PlaylistLoaderUI at all and still needs somewhere to load into.
      var loaders = Object.FindObjectsByType<PlaylistLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var loader in loaders)
      {
        ProcessSlots(loader);
      }

      var loaderUis = Object.FindObjectsByType<PlaylistLoaderUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var loaderUi in loaderUis)
      {
        var uiControllers = FindPlayerUIControllers(loaderUi);
        ProcessInterceptor(loaderUi, uiControllers);
        ProcessRefreshHandler(loaderUi, uiControllers);
      }

      var searches = Object.FindObjectsByType<PlaylistSearch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var search in searches)
      {
        ProcessSearch(search);
      }
    }

    // The search page (issue #145). Its box holds the address of the
    // loader's server's search, made here because only the editor can make
    // a URL from text. A UI gets the search where it can take a
    // typed URL and has a dialog to report a load with: the same UIs the
    // list's "+" shows on, so not the Playlist Panel. There the row that
    // opens the search stays off, as it is in the prefab.
    private static void ProcessSearch(PlaylistSearch search)
    {
      if (search == null || !search.gameObject.activeInHierarchy) return;
      var loader = search.GetProgramVariable("_loader") as PlaylistLoader;
      var loaderUi = search.GetProgramVariable("_loaderUi") as PlaylistLoaderUI;
      if (loader == null || loaderUi == null || !loaderUi.gameObject.activeInHierarchy) return;

      string baseUrl = loader.GetProgramVariable("_poolBaseUrl") as string;
      if (string.IsNullOrEmpty(baseUrl)) return;
      search.SetProgramVariable("searchFieldUrl", new VRCUrl($"{baseUrl.TrimEnd('/')}{PlaylistUrlUtils.SearchPath}"));

      foreach (var uiController in FindPlayerUIControllers(loaderUi))
      {
        if (uiController.GetProgramVariable("_urlInputField") as VRCUrlInputField == null) continue;
        if (uiController.GetProgramVariable("_modalDialog") as Component == null) continue;

        foreach (var searchPage in uiController.GetComponentsInChildren<SearchPage>(true))
        {
          searchPage.SetProgramVariable("_handler", search);
          var entry = searchPage.GetProgramVariable("_entryButton") as Button;
          if (entry != null) entry.gameObject.SetActive(true);
        }
      }
    }

    // The refresh button (issue #91) lives in the Playlists tab, which exists
    // on panels that have no URL input at all -- PlaylistPanel is one. So this
    // deliberately does NOT reuse the interceptor's "owns the main URL input"
    // filter, or the button would never work there.
    private static void ProcessRefreshHandler(PlaylistLoaderUI loaderUi, UIController[] uiControllers)
    {
      foreach (var uiController in uiControllers)
      {
        uiController.SetProgramVariable("_playlistRefreshHandler", loaderUi);
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

    // The UIControllers of the player whose loader this UI uses: none when
    // the UI is inactive, or has no loader, or the loader no player.
    private static UIController[] FindPlayerUIControllers(PlaylistLoaderUI loaderUi)
    {
      if (loaderUi == null || !loaderUi.gameObject.activeInHierarchy) return new UIController[0];
      var loader = loaderUi.GetProgramVariable("_loader") as PlaylistLoader;
      if (loader == null) return new UIController[0];
      var controller = ResolveController(loader);
      if (controller == null) return new UIController[0];

      return Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(uiController => uiController != null && uiController.GetProgramVariable("_controller") as Controller == controller)
        .ToArray();
    }

    private static void ProcessInterceptor(PlaylistLoaderUI loaderUi, UIController[] uiControllers)
    {
      foreach (var uiController in uiControllers)
      {
        // Only the UIController that owns the URL input participates;
        // panels without one (PlaylistPanel etc.) never reach PlayUrlField.
        var urlInput = uiController.GetProgramVariable("_urlInputField") as VRCUrlInputField;
        if (urlInput == null) continue;

        uiController.SetProgramVariable("_urlInterceptor", loaderUi);
        loaderUi.SetProgramVariable("_uiController", uiController);
        // Tells the player which URL kinds this world accepts (issue #84).
        // Set on every paired UIController so multi-panel setups all show it.
        uiController.SetProgramVariable("_urlHintKey", "module.playlistLoader.inputHint");
      }
    }
  }
}
