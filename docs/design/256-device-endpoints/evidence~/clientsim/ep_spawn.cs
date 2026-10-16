// Gives the temporary #256 scene's descriptor a spawn point.
if (UnityEditor.EditorApplication.isPlaying) return "playing";
System.Type t = null;
foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("VRC.SDK3.Components.VRCSceneDescriptor"); if (t != null) break; }
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/__EpTest/EpTest.unity") return "not the test scene: " + scene.path;
GameObject d = null; foreach (var r in scene.GetRootGameObjects()) if (r.name == "__Descriptor") d = r;
d.transform.position = new Vector3(0, 0, -4);
var c = d.GetComponent(t);
var f = t.GetField("spawns") ?? t.BaseType.GetField("spawns");
f.SetValue(c, new Transform[] { d.transform });
UnityEditor.EditorUtility.SetDirty(c);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return $"spawns={((Transform[])f.GetValue(c)).Length} saved={saved}";
