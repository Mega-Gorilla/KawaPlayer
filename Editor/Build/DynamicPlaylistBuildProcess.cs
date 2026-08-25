using System.Collections.Generic;
using UdonSharpEditor;
using UnityEngine;
using Yamadev.YamaStream.UI;

using Object = UnityEngine.Object;

namespace Yamadev.YamaStream.Editor
{
  // Hands every UIController the dynamic playlist slots under its Controller
  // (issue #92), so the playlist header can offer per-playlist actions
  // without the UI knowing which module filled a slot.
  public class DynamicPlaylistBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2600;

    public void Process()
    {
      var uiControllers = Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      foreach (var uiController in uiControllers)
      {
        if (uiController == null) continue;
        var controller = uiController.GetProgramVariable("_controller") as Controller;
        if (controller == null) continue;

        uiController.SetProgramVariable("_dynamicPlaylists", CollectUsableSlots(controller));
      }
    }

    // The slots the runtime will actually see. Controller.ReadPlaylists
    // collects with the active-only overload of GetComponentsInChildren, so a
    // slot whose Playlist is inactive never enters Controller.Playlists.
    // Handing one out would let a caller fill a playlist that no UI can show
    // and that Forward() cannot advance through, because Array.IndexOf leaves
    // _activePlaylistIndex at -1.
    //
    // Tested against the Playlist rather than the slot so that a slot left
    // active with its Playlist child disabled is caught too.
    public static DynamicPlaylist[] CollectUsableSlots(Controller controller)
    {
      var slots = new List<DynamicPlaylist>();
      if (controller == null) return slots.ToArray();

      foreach (var slot in controller.GetComponentsInChildren<DynamicPlaylist>(true))
      {
        if (slot == null) continue;
        var playlist = slot.GetProgramVariable("_playlist") as Playlist;
        if (playlist == null || !playlist.gameObject.activeInHierarchy) continue;
        slots.Add(slot);
      }

      return slots.ToArray();
    }
  }
}
