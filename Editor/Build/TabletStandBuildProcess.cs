using UnityEditor;
using UnityEngine;
using Yamadev.YamaStream.Tablet;

namespace Yamadev.YamaStream.Editor
{
  // A tablet with a stand (issue #349) starts on the stand: here its body is
  // put on the chosen design's rest, wherever the stand was moved to, so the
  // place the tablet goes back to is the stand's. The designs not chosen are
  // left out of the world (EditorOnly), and so is the return button that
  // stands apart, which a stand replaces (its own button is on the stand).
  // Runs at scene build and on play-mode entry. The stand is the tablet's
  // child named Stand (TabletStandDesigns), found by its name, as a world
  // keeps no component of our own beside the Udon ones.
  internal class TabletStandBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2100;

    public void Process()
    {
      foreach (var pickup in Object.FindObjectsByType<TabletPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
      {
        var root = pickup.transform.parent;
        var stand = root != null ? root.Find(TabletStandDesigns.StandName) : null;
        if (stand == null) continue;

        foreach (var design in TabletStandDesigns.All)
        {
          var model = stand.Find(design.Child);
          if (model != null && !model.gameObject.activeSelf) model.tag = "EditorOnly";
        }
        // Switched off in the inspector, not merely hidden with the tablet or
        // one of its parents, which a world may show later.
        if (!stand.gameObject.activeSelf)
        {
          stand.tag = "EditorOnly";
          continue;
        }

        var dock = new SerializedObject(pickup).FindProperty("_dock").objectReferenceValue as Transform;
        if (dock != null) pickup.transform.SetPositionAndRotation(dock.position, dock.rotation);
        var apart = root.Find("ReturnButton");
        if (apart != null)
        {
          apart.gameObject.SetActive(false);
          apart.tag = "EditorOnly";
        }
      }
    }
  }
}
