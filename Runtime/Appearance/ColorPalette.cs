using UnityEngine;

namespace Yamadev.YamaStream
{
  // The one place KawaPlayer's colours are kept (issue #161): the colour sets a
  // UI can take its accent from, and the neutral colours under them. Each
  // AppearanceSettings points here, and the build gives every ColorDefinition
  // the colour of its role, so a colour changed here reaches every UI.
  [CreateAssetMenu(menuName = "KawaPlayer/Color Palette", fileName = "ColorPalette")]
  public class ColorPalette : ScriptableObject
  {
    public ColorSetData[] colorSets = new ColorSetData[0];

    public Color background = new Color(0x1A / 255f, 0x1B / 255f, 0x1F / 255f, 1f);
    public Color surface = new Color(0x2A / 255f, 0x2C / 255f, 0x32 / 255f, 1f);
    public Color surfaceVariant = new Color(0x1F / 255f, 0x20 / 255f, 0x25 / 255f, 1f);
    public Color onSurface = new Color(0xE4 / 255f, 0xE5 / 255f, 0xEA / 255f, 1f);
    public Color onSurfaceVariant = new Color(0xA3 / 255f, 0xA6 / 255f, 0xB0 / 255f, 1f);

    // How far a container leans from the primary colour towards the background:
    // dark enough that light text on it reads at 7:1 or more with every set here.
    private const float ContainerDepth = 0.72f;
    // A dark text colour in the primary's hue, for text on a primary fill.
    private const float OnPrimaryDepth = 0.8f;

    public Color GetColor(ColorType type, ColorSetData colorSet)
    {
      switch (type)
      {
        case ColorType.Primary: return colorSet.primaryColor;
        case ColorType.Secondary: return colorSet.secondaryColor;
        case ColorType.PrimaryContainer: return PrimaryContainer(colorSet);
        case ColorType.OnPrimary: return OnPrimary(colorSet);
        case ColorType.Background: return background;
        case ColorType.Surface: return surface;
        case ColorType.SurfaceVariant: return surfaceVariant;
        case ColorType.OnSurface: return onSurface;
        case ColorType.OnSurfaceVariant: return onSurfaceVariant;
        default: throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
      }
    }

    // The neutral roles give a colour but leave the alpha to the graphic, so a
    // see-through overlay stays see-through. Listed by name: a role added later
    // takes the next number whichever kind it is.
    public static bool IsNeutral(ColorType type)
    {
      switch (type)
      {
        case ColorType.Background:
        case ColorType.Surface:
        case ColorType.SurfaceVariant:
        case ColorType.OnSurface:
        case ColorType.OnSurfaceVariant:
          return true;
        default:
          return false;
      }
    }

    // A dark fill in the primary's hue, for what is selected; light text goes on it.
    private Color PrimaryContainer(ColorSetData colorSet)
    {
      var color = Color.Lerp(colorSet.primaryColor, background, ContainerDepth);
      color.a = 1f;
      return color;
    }

    // Dark text on a light primary, light text on a dark one: whichever reads better.
    private Color OnPrimary(ColorSetData colorSet)
    {
      var dark = Color.Lerp(colorSet.primaryColor, Color.black, OnPrimaryDepth);
      dark.a = 1f;
      return Contrast(colorSet.primaryColor, dark) >= Contrast(colorSet.primaryColor, onSurface) ? dark : onSurface;
    }

    private static float Contrast(Color a, Color b)
    {
      float la = Luminance(a), lb = Luminance(b);
      return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    private static float Luminance(Color color)
    {
      var linear = color.linear;
      return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
    }
  }
}
