using System;
using System.Reflection;

namespace Yamadev.YamaStream.Editor
{
  internal static class EditorUtils
  {
    // A full name is looked up in each assembly's own table, without listing its
    // types: the AVPro check does this on every domain reload, and listing every
    // type of every assembly was most of its time (issue #195). Only a short
    // name, which no table holds, still goes through the types; a name with a
    // dot is never a short one.
    public static Type FindType(string typeName, bool useFullName = false, bool ignoreCase = false)
    {
      if (string.IsNullOrEmpty(typeName)) return null;
      Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (var assemb in assemblies)
      {
        Type type = null;
        try { type = assemb.GetType(typeName, false, ignoreCase); }
        catch (Exception) { }
        if (type != null) return type;
      }
      if (useFullName || typeName.IndexOf('.') >= 0) return null;

      StringComparison e = (ignoreCase) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
      foreach (var assemb in assemblies)
      {
        Type[] types;
        try { types = assemb.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }
        foreach (var t in types)
        {
          if (t != null && string.Equals(t.Name, typeName, e)) return t;
        }
      }
      return null;
    }
  }
}
