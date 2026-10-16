// Makes a temporary scene with only VizVid (Separated Controls), for #256. Room N is not touched.
if (UnityEditor.EditorApplication.isPlaying) return "playing";
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (active.isDirty) return "active scene has unsaved changes; stopping";
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/__EpTest")) UnityEditor.AssetDatabase.CreateFolder("Assets", "__EpTest");
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Packages/idv.jlchntoz.vvmw/VVMW (Separated Controls).prefab");
var vv = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
vv.name = "__VVMWTest";
// VRChat needs a scene descriptor for ClientSim to start.
var descType = System.Type.GetType("VRC.SDK3.Components.VRCSceneDescriptor, VRC.SDK3");
var desc = new GameObject("__Descriptor");
if (descType != null) desc.AddComponent(descType);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/__EpTest/EpTest.unity");
return $"saved={saved} roots={string.Join(",", System.Linq.Enumerable.Select(scene.GetRootGameObjects(), g => g.name))} descriptor={descType != null}";
