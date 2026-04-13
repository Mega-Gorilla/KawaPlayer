using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  public static class YamaPlayerMenu
  {
    const string menuPrefix = "GameObject/KawaPlayer/";
    private static readonly string _yamaplayerPrefabGuid = "68f1537220fe62b40910a7187f8e5408";
    private static readonly string _subScreenPrefabGuid = "1d1c026d8b023d04ea81f85594f05aec";
    private static readonly string _controllerBarPrefabGuid = "ddf7f58d0d20d6843a79711f81f34bf2";
    private static readonly string _playlistPanelPrefabGuid = "32aa1985af9229540a44cf22406ee1a2";

    [MenuItem(menuPrefix + "Main", priority = 1)]
    public static void CreateKawaPlayer() =>
        CreateGameObject(AssetDatabase.GUIDToAssetPath(_yamaplayerPrefabGuid));

    [MenuItem(menuPrefix + "SubScreen", priority = 101)]
    public static void CreateSubScreen() =>
        CreateGameObject(AssetDatabase.GUIDToAssetPath(_subScreenPrefabGuid));

    [MenuItem(menuPrefix + "Controller Bar", priority = 102)]
    public static void CreateControllerBar() =>
        CreateGameObject(AssetDatabase.GUIDToAssetPath(_controllerBarPrefabGuid));

    [MenuItem(menuPrefix + "Playlist Panel", priority = 103)]
    public static void CreatePlaylistPanel() =>
        CreateGameObject(AssetDatabase.GUIDToAssetPath(_playlistPanelPrefabGuid));

    static void CreateGameObject(string path)
    {
      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
      if (prefab == null) return;

      Transform parent = Selection.activeTransform;
      GameObject obj = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
      if (obj == null) return;

      obj.name = GameObjectUtility.GetUniqueNameForSibling(parent, prefab.name);
      Undo.RegisterCreatedObjectUndo(obj, $"Create {obj.name}");
      Selection.activeGameObject = obj;
    }
  }
}
