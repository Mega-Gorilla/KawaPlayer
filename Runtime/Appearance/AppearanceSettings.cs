using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yamadev.YamaStream
{
  [AddComponentMenu("KawaPlayer/Appearance Settings")]
  public class AppearanceSettings : MonoBehaviour
  {
    // Where the colour sets and the neutral colours come from (issue #161).
    public ColorPalette palette;
    // Empty is Auto: the first colour set, the KawaPlayer one.
    public string defaultColorSet = "";
    // Sets a world adds to the palette's. One with a palette set's name takes its place.
    public ColorSetData[] colorSets = new ColorSetData[0];

    public ColorSetData DefaultColorSet
    {
      get
      {
        var names = ColorSetNames;
        if (names.Length == 0) return null;
        return FindColorSet(defaultColorSet) ?? FindColorSet(names[0]);
      }
    }

    // The palette's sets first, then this one's own.
    public string[] ColorSetNames
    {
      get
      {
        var names = new List<string>();
        foreach (var colorSet in PaletteColorSets.Concat(OwnColorSets))
        {
          if (colorSet == null || string.IsNullOrEmpty(colorSet.colorSetName) || names.Contains(colorSet.colorSetName)) continue;
          names.Add(colorSet.colorSetName);
        }
        return names.ToArray();
      }
    }

    public ColorSetData FindColorSet(string colorSetName)
    {
      if (string.IsNullOrEmpty(colorSetName)) return null;
      return OwnColorSets.FirstOrDefault(cs => cs != null && cs.colorSetName == colorSetName)
        ?? PaletteColorSets.FirstOrDefault(cs => cs != null && cs.colorSetName == colorSetName);
    }

    private ColorSetData[] OwnColorSets => colorSets ?? new ColorSetData[0];
    private ColorSetData[] PaletteColorSets => palette != null && palette.colorSets != null ? palette.colorSets : new ColorSetData[0];
  }
}
