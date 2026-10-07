using System.Linq;
using UnityEngine;
using Yamadev.YamaStream.Tablet;

namespace Yamadev.YamaStream.Editor
{
  // The visitors app keeps one record for the whole world (issue #153), but
  // every tablet carries a TabletVisitorRecorder, so a world needs nothing
  // more than its tablets. Here the first one is kept and moved to the scene
  // root, so it goes on when its tablet is switched off; the others are
  // switched off, and every visitors app is pointed at the one kept. Runs at
  // scene build and on play-mode entry.
  internal class TabletVisitorsBuildProcess : IYamaPlayerBuildProcess
  {
    public int callbackOrder => -2000;

    public void Process()
    {
      var recorders = Object.FindObjectsByType<TabletVisitorRecorder>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(recorder => recorder != null)
        .OrderBy(recorder => recorder.gameObject.activeInHierarchy ? 0 : 1)
        .ThenBy(recorder => HierarchyPath(recorder.transform))
        .ToArray();
      if (recorders.Length == 0) return;

      var kept = recorders[0];
      // Off on any tablet means off: someone turned them off for a reason.
      bool photos = recorders.All(recorder => (bool)recorder.GetProgramVariable("_photosEnabled"));
      var camera = kept.GetProgramVariable("_photoCamera") as Camera;
      if (camera != null) camera.backgroundColor = PhotoBackground(kept);

      foreach (var other in recorders.Skip(1)) other.gameObject.SetActive(false);
      kept.transform.SetParent(null, true);

      var apps = Object.FindObjectsByType<TabletVisitorsApp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      kept.SetProgramVariable("_photosEnabled", photos);
      kept.SetProgramVariable("_apps", apps);
      foreach (var app in apps) app.SetProgramVariable("_recorder", kept);
    }

    // Behind a photo: the colour of the empty photo, from its tablet's palette.
    private static Color PhotoBackground(TabletVisitorRecorder recorder)
    {
      var root = recorder.transform.parent != null ? recorder.transform.parent : recorder.transform;
      var settings = root.GetComponentInChildren<AppearanceSettings>(true);
      if (settings != null && settings.palette != null) return settings.palette.surfaceVariant;
      var defaults = ScriptableObject.CreateInstance<ColorPalette>();
      var color = defaults.surfaceVariant;
      Object.DestroyImmediate(defaults);
      return color;
    }

    private static string HierarchyPath(Transform transform)
    {
      return transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;
    }
  }
}
