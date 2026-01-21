using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  public static class Utils
  {
    public static bool TryFind(this Transform t, string n, out Transform result)
    {
      result = t.Find(n);
      return Utilities.IsValid(result);
    }

    public static bool TryGetComponentLocal<T>(this Transform t, out T component)
    {
      component = t.GetComponent<T>();
      return Utilities.IsValid(component);
    }
  }
}
