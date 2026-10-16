// Adds a VRC scene descriptor to the temporary #256 scene so ClientSim can start.
System.Type t = null;
foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("VRC.SDK3.Components.VRCSceneDescriptor"); if (t != null) break; }
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/__EpTest/EpTest.unity") return "not the test scene: " + scene.path;
GameObject d = null; foreach (var r in scene.GetRootGameObjects()) if (r.name == "__Descriptor") d = r;
if (t != null && d.GetComponent(t) == null) d.AddComponent(t);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return $"type={t?.AssemblyQualifiedName} added={d.GetComponent(t) != null} saved={saved}";
