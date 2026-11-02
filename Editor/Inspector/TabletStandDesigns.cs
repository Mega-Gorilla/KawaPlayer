using UnityEditor;
using UnityEngine;
using Yamadev.YamaStream.Tablet;

namespace Yamadev.YamaStream.Editor
{
  // The tablet's stand (issue #349): its designs and their colours, and
  // choosing one. The stand is the tablet's child named Stand; each design
  // is a child of it (a model with its material), holding a Dock, where the
  // tablet rests, and a ButtonPlace, where the stand's return button goes.
  // One design is on; the others are off, and the build leaves them out.
  // The models and their build scripts are in Assets/Models/TabletStand.
  internal static class TabletStandDesigns
  {
    public const string StandName = "Stand";
    public const string ButtonName = "Button";
    public const string DockName = "Dock";
    public const string ButtonPlaceName = "ButtonPlace";
    private const string Folder = "Packages/com.vhub.kawaplayer/Assets/Models/TabletStand/";

    public class Design
    {
      public string Child;   // the child of the stand, and the models' and materials' middle name
      public string Key;     // tablet.standDesign.<Key>
      public string[] Colors; // the materials' last name; tablet.standColor.<colour, first letter lower>
    }

    // In the order the inspector offers them: those for a table, then the one for the floor.
    public static readonly Design[] All =
    {
      new Design { Child = "Bentwood", Key = "bentwood", Colors = new[] { "Walnut", "WhiteOak", "SmokedWalnut", "Black" } },
      new Design { Child = "Magnet", Key = "magnet", Colors = new[] { "SpaceGrey" } },
      new Design { Child = "Cushion", Key = "cushion", Colors = new[] { "Red", "Grey" } },
      new Design { Child = "Floor", Key = "floor", Colors = new[] { "White" } },
    };

    public static Material MaterialOf(Design design, string color)
    {
      return AssetDatabase.LoadAssetAtPath<Material>($"{Folder}KawaTabletStand_{design.Child}_{color}.mat");
    }

    public static string ColorKey(string color) => "tablet.standColor." + char.ToLowerInvariant(color[0]) + color.Substring(1);

    // The design that is on, and the colour its material is, or 0 and 0.
    public static void Current(Transform stand, out int design, out int color)
    {
      design = 0;
      color = 0;
      for (int i = 0; i < All.Length; i++)
      {
        var model = stand.Find(All[i].Child);
        if (model == null || !model.gameObject.activeSelf) continue;
        design = i;
        var material = model.GetComponent<MeshRenderer>()?.sharedMaterial;
        for (int c = 0; c < All[i].Colors.Length; c++)
        {
          if (material != null && material == MaterialOf(All[i], All[i].Colors[c])) color = c;
        }
        return;
      }
    }

    // Puts a design and colour on: that model on with the colour's material,
    // the others off, the tablet's rest at its Dock, the return button at its
    // ButtonPlace, and the tablet on it, so it shows where it will start.
    public static void Apply(Transform stand, TabletPickup pickup, int design, int color)
    {
      Transform dock = null;
      for (int i = 0; i < All.Length; i++)
      {
        var model = stand.Find(All[i].Child);
        if (model == null) continue;
        bool on = i == design;
        if (model.gameObject.activeSelf != on)
        {
          Undo.RecordObject(model.gameObject, "Tablet Stand Design");
          model.gameObject.SetActive(on);
        }
        if (!on) continue;
        var renderer = model.GetComponent<MeshRenderer>();
        var material = MaterialOf(All[i], All[i].Colors[Mathf.Clamp(color, 0, All[i].Colors.Length - 1)]);
        if (renderer != null && material != null && renderer.sharedMaterial != material)
        {
          Undo.RecordObject(renderer, "Tablet Stand Colour");
          renderer.sharedMaterial = material;
        }
        dock = model.Find(DockName);
        var place = model.Find(ButtonPlaceName);
        var button = stand.Find(ButtonName);
        if (place != null && button != null)
        {
          Undo.RecordObject(button, "Tablet Stand Design");
          button.SetPositionAndRotation(place.position, place.rotation);
        }
      }
      SetDock(pickup, dock);
    }

    // The tablet's rest (none without a stand), and the tablet on it.
    public static void SetDock(TabletPickup pickup, Transform dock)
    {
      if (pickup == null) return;
      var so = new SerializedObject(pickup);
      so.FindProperty("_dock").objectReferenceValue = dock;
      so.ApplyModifiedProperties();
      if (dock == null) return;
      Undo.RecordObject(pickup.transform, "Tablet Stand Design");
      pickup.transform.SetPositionAndRotation(dock.position, dock.rotation);
    }
  }
}
