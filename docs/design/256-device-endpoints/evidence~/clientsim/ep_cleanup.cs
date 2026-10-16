// Goes back to Room N and deletes the temporary #256 scene folder.
if (UnityEditor.EditorApplication.isPlaying) return "playing";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Room N.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
bool deleted = UnityEditor.AssetDatabase.DeleteAsset("Assets/__EpTest");
return $"scene={scene.path} dirty={scene.isDirty} folderDeleted={deleted} folderExists={UnityEditor.AssetDatabase.IsValidFolder("Assets/__EpTest")}";
