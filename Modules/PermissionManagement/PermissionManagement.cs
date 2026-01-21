using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.PermissionManagement
{
  public enum PlayerPermission
  {
    Viewer,
    Editor,
    Admin,
    Owner
  }

  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  [DefaultExecutionOrder(-900)]
  public class PermissionManagement : YamaPlayerModule
  {
    [SerializeField] private PlayerPermission _defaultPermission = PlayerPermission.Editor;
    [SerializeField] private string[] _ownerList = new string[] { };
    [SerializeField] private bool _grantPermissionToInstanceOwner = true;
    [SerializeField] private bool _grantPermissionToInstanceMaster = true;
    [UdonSynced] private string _permissionString;
    private DataDictionary _permission = new DataDictionary();
    private YamaPlayerListener[] _listeners = new YamaPlayerListener[0];

    public PlayerPermission DefaultPermission => _defaultPermission;
    public string[] OwnerList => _ownerList;
    public bool GrantPermissionToInstanceOwner => _grantPermissionToInstanceOwner;
    public bool GrantPermissionToInstanceMaster => _grantPermissionToInstanceMaster;

    public override void Start()
    {
      base.Start();
      if (!Networking.IsMaster) return;
      Initialize();
    }

    public void AddListener(YamaPlayerListener listener)
    {
      if (!Utilities.IsValid(listener) || Array.IndexOf(_listeners, listener) >= 0) return;
      _listeners = _listeners.Add(listener);
    }

    public bool IsPlayerOwner(VRCPlayerApi player)
    {
      if (!Utilities.IsValid(player)) return false;
      if (_grantPermissionToInstanceOwner && player.isInstanceOwner) return true;
      if (_grantPermissionToInstanceMaster && player.isMaster) return true;
      return Array.IndexOf(_ownerList, player.displayName) >= 0;
    }

    public DataDictionary PermissionData
    {
      get => _permission;
      set
      {
        _permission = value;
        int len = _listeners.Length;
        for (int i = 0; i < len; i++) _listeners[i].SendCustomEvent("AfterPermissionChanged");
      }
    }

    public PlayerPermission PlayerPermission =>
      IsLocalPlayerValid ? GetPermissionByPlayerId(LocalPlayer.playerId) : PlayerPermission.Viewer;

    private DataDictionary InitializePlayerPermission(VRCPlayerApi player)
    {
      DataDictionary result = new DataDictionary();
      result.Add("displayName", player.displayName);
      result.Add("permission", IsPlayerOwner(player) ? (int)PlayerPermission.Owner : (int)_defaultPermission);
      return result;
    }

    private void Initialize()
    {
      if (LocalPlayer == null) return;
      _permission.Add(LocalPlayer.playerId.ToString(), InitializePlayerPermission(LocalPlayer));
      PermissionData = _permission;
      SyncVariables();
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
      if (!IsObjectOwner) return;
      if (_permission.ContainsKey(player.playerId.ToString())) return;
      _permission.Add(player.playerId.ToString(), InitializePlayerPermission(player));
      PermissionData = _permission;
      SyncVariables();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
      if (!IsObjectOwner) return;
      if (!_permission.ContainsKey(player.playerId.ToString())) return;
      _permission.Remove(player.playerId.ToString());
      PermissionData = _permission;
      SyncVariables();
    }

    public override void OnPreSerialization()
    {
      VRCJson.TrySerializeToJson(_permission, JsonExportType.Minify, out var json);
      _permissionString = json.String;
    }

    public override void OnDeserialization()
    {
      if (string.IsNullOrEmpty(_permissionString)) return;
      if (!VRCJson.TryDeserializeFromJson(_permissionString, out DataToken result)) return;
      if (result.TokenType != TokenType.DataDictionary) return;

      _permission = result.DataDictionary;
      for (int i = 0; i < _permission.Count; i++)
      {
        double value = _permission.GetValues()[i].DataDictionary["permission"].Double;
        _permission.GetValues()[i].DataDictionary.SetValue("permission", (int)value);
      }

      PermissionData = _permission;
    }

    public void SetPermission(int index, PlayerPermission permission)
    {
      if (index < 0 || index >= _permission.Count) return;
      DataToken key = _permission.GetKeys()[index];
      int value = (int)permission;
      _permission[key].DataDictionary["permission"] = value;
      PermissionData = _permission;
      SyncVariables();
    }

    public PlayerPermission GetPermissionByPlayerId(int playerId)
    {
      if (!_permission.ContainsKey(playerId.ToString())) return _defaultPermission;
      _permission.TryGetValue(playerId.ToString(), out var result);
      return (PlayerPermission)result.DataDictionary["permission"].Int;
    }

    public void AfterLanguageChanged()
    {
      int len = _listeners.Length;
      for (int i = 0; i < len; i++) _listeners[i].SendCustomEvent("AfterLanguageChanged");
    }
  }
}
