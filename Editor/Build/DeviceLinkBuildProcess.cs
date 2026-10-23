using System.Linq;
using UnityEngine;
using Yamadev.YamaStream.UI;

using Object = UnityEngine.Object;

namespace Yamadev.YamaStream.Editor
{
  // Names each device that is not connected to a player, and says how to
  // connect it (issue #294). A device learns its player only when one is
  // chosen in its Sub Controller's inspector, which writes it into the parts
  // under it (YamaPlayerSubControllerEditor), so a device left without one,
  // or a part added to it by a prefab update after the player was chosen,
  // does nothing in the world. Before, only the parts said so, at run time
  // and each under its own name ("UI"). One warning is given per device, at
  // scene build and on play-mode entry, with the device as its context. What
  // is left out of the world (EditorOnly) is not looked at:
  // GlobalReloadBuildProcess, which runs before, leaves out a Global Reload
  // Button with no player and says so itself.
  internal class DeviceLinkBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2800;

    public void Process()
    {
      var uiControllers = Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(uiController => uiController.GetProgramVariable("_controller") as Controller == null);
      var screens = Object.FindObjectsByType<YamaPlayerScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(screen => screen.controller == null);
      var speakers = Object.FindObjectsByType<YamaPlayerSpeaker>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(speaker => speaker.controller == null);
      var parts = uiControllers.Cast<Component>().Concat(screens).Concat(speakers)
        .Where(part => !IsLeftOut(part.transform));

      foreach (var device in parts.GroupBy(part => part.GetComponentInParent<YamaPlayerSubController>(true)))
      {
        if (device.Key == null)
        {
          // A part placed on its own, outside any device: a UI of the creator's own.
          foreach (var part in device)
          {
            Debug.LogWarning($"[KawaPlayer] {YamaPlayerModuleBuildProcess.GetPath(part.transform)} has no player set, so it does nothing in the world. Set the Controller in its inspector.", part);
          }
          continue;
        }

        var devicePath = YamaPlayerModuleBuildProcess.GetPath(device.Key.transform);
        if (device.Key.YamaPlayer == null)
        {
          Debug.LogWarning($"[KawaPlayer] {devicePath} has no player set, so it does nothing in the world. Choose the player in its inspector.", device.Key);
          continue;
        }

        // The inspector writes the player only when the field changes, so
        // choosing the same one again does nothing: it is cleared first.
        var playerPath = YamaPlayerModuleBuildProcess.GetPath(device.Key.YamaPlayer.transform);
        var unlinked = string.Join(", ", device.Select(part => GetPath(part.transform, device.Key.transform)).Distinct());
        Debug.LogWarning($"[KawaPlayer] Parts of {devicePath} are not connected to its player {playerPath}, so they do nothing in the world: {unlinked}. Clear the player in its inspector, then choose it again.", device.Key);
      }
    }

    private static bool IsLeftOut(Transform transform)
    {
      for (var t = transform; t != null; t = t.parent)
      {
        if (t.CompareTag("EditorOnly")) return true;
      }
      return false;
    }

    // The part's path under its device.
    private static string GetPath(Transform transform, Transform device)
    {
      if (transform == device) return transform.name;
      var path = transform.name;
      for (var parent = transform.parent; parent != null && parent != device; parent = parent.parent)
      {
        path = $"{parent.name}/{path}";
      }
      return path;
    }
  }
}
