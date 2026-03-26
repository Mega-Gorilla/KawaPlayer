using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneDump
{
    private const string OutputPath = "Assets/Tests/scene-dump.json";

    [MenuItem("Debug/Dump Scene Hierarchy")]
    public static void DumpHierarchy()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        sb.AppendLine($"  \"scene\": \"{scene.name}\",");
        sb.AppendLine($"  \"rootCount\": {roots.Length},");
        sb.AppendLine("  \"roots\": [");

        for (int i = 0; i < roots.Length; i++)
        {
            DumpGameObject(sb, roots[i], 2, 4);
            if (i < roots.Length - 1) sb.AppendLine(",");
        }

        sb.AppendLine("  ],");
        sb.AppendLine($"  \"_timestamp\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
        sb.AppendLine("}");

        File.WriteAllText(OutputPath, sb.ToString());
        Debug.Log($"[SceneDump] Hierarchy written to {OutputPath}");
        AssetDatabase.Refresh();
    }

    [MenuItem("Debug/Dump Selected Object")]
    public static void DumpSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[SceneDump] No GameObject selected.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"selected\": \"{go.name}\",");
        sb.AppendLine($"  \"path\": \"{GetPath(go)}\",");

        // Components
        sb.AppendLine("  \"components\": [");
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            DumpComponent(sb, comps[i], "    ");
            if (i < comps.Length - 1) sb.AppendLine(",");
        }
        sb.AppendLine("  ],");

        // Children
        sb.AppendLine("  \"children\": [");
        for (int i = 0; i < go.transform.childCount; i++)
        {
            DumpGameObject(sb, go.transform.GetChild(i).gameObject, 2, 3);
            if (i < go.transform.childCount - 1) sb.AppendLine(",");
        }
        sb.AppendLine("  ],");

        sb.AppendLine($"  \"_timestamp\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
        sb.AppendLine("}");

        File.WriteAllText(OutputPath, sb.ToString());
        Debug.Log($"[SceneDump] Selected object written to {OutputPath}");
        AssetDatabase.Refresh();
    }

    [MenuItem("Debug/Dump All SerializedProperties of Selected")]
    public static void DumpSelectedProperties()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[SceneDump] No GameObject selected.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"selected\": \"{go.name}\",");
        sb.AppendLine($"  \"path\": \"{GetPath(go)}\",");

        var comps = go.GetComponents<Component>();
        for (int c = 0; c < comps.Length; c++)
        {
            if (comps[c] == null) continue;
            var typeName = comps[c].GetType().Name;
            sb.AppendLine($"  \"{typeName}\": {{");

            var so = new SerializedObject(comps[c]);
            var prop = so.GetIterator();
            bool first = true;
            while (prop.NextVisible(first))
            {
                first = false;
                DumpSerializedProperty(sb, prop, "    ");
            }

            sb.AppendLine("  },");
        }

        sb.AppendLine($"  \"_timestamp\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
        sb.AppendLine("}");

        File.WriteAllText(OutputPath, sb.ToString());
        Debug.Log($"[SceneDump] Properties written to {OutputPath}");
        AssetDatabase.Refresh();
    }

    private static void DumpGameObject(StringBuilder sb, GameObject go, int depth, int maxDepth)
    {
        string indent = new string(' ', depth * 2);
        var comps = GetComponentNames(go);
        sb.Append($"{indent}{{");
        sb.Append($"\"name\": \"{Escape(go.name)}\", ");
        sb.Append($"\"active\": {(go.activeSelf ? "true" : "false")}, ");
        sb.Append($"\"components\": \"{comps}\", ");
        sb.Append($"\"children\": {go.transform.childCount}");

        if (depth < maxDepth && go.transform.childCount > 0)
        {
            sb.AppendLine(", \"childList\": [");
            for (int i = 0; i < go.transform.childCount; i++)
            {
                DumpGameObject(sb, go.transform.GetChild(i).gameObject, depth + 1, maxDepth);
                if (i < go.transform.childCount - 1) sb.AppendLine(",");
            }
            sb.AppendLine();
            sb.Append($"{indent}]}}");
        }
        else
        {
            sb.Append("}");
        }
    }

    private static void DumpComponent(StringBuilder sb, Component comp, string indent)
    {
        var type = comp.GetType();
        sb.Append($"{indent}{{\"type\": \"{type.Name}\", \"fullType\": \"{type.FullName}\"");

        // For MonoBehaviour, show enabled state
        if (comp is Behaviour behaviour)
        {
            sb.Append($", \"enabled\": {(behaviour.enabled ? "true" : "false")}");
        }

        sb.Append("}");
    }

    private static void DumpSerializedProperty(StringBuilder sb, SerializedProperty prop, string indent)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                var obj = prop.objectReferenceValue;
                sb.AppendLine(obj == null
                    ? $"{indent}\"{prop.name}\": null,"
                    : $"{indent}\"{prop.name}\": \"{Escape(obj.name)} ({obj.GetType().Name})\",");
                break;
            case SerializedPropertyType.String:
                sb.AppendLine($"{indent}\"{prop.name}\": \"{Escape(prop.stringValue)}\",");
                break;
            case SerializedPropertyType.Integer:
                sb.AppendLine($"{indent}\"{prop.name}\": {prop.intValue},");
                break;
            case SerializedPropertyType.Boolean:
                sb.AppendLine($"{indent}\"{prop.name}\": {(prop.boolValue ? "true" : "false")},");
                break;
            case SerializedPropertyType.Float:
                sb.AppendLine($"{indent}\"{prop.name}\": {prop.floatValue},");
                break;
            case SerializedPropertyType.Enum:
                sb.AppendLine($"{indent}\"{prop.name}\": \"{prop.enumNames[prop.enumValueIndex]}\",");
                break;
            case SerializedPropertyType.ArraySize:
                sb.AppendLine($"{indent}\"{prop.name}\": {prop.intValue},");
                break;
            default:
                sb.AppendLine($"{indent}\"{prop.name}\": \"({prop.propertyType})\",");
                break;
        }
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static string GetComponentNames(GameObject go)
    {
        var sb = new StringBuilder();
        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(comp.GetType().Name);
        }
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
