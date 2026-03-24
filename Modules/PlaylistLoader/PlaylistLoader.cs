using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream.Modules.PlaylistLoader
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PlaylistLoader : YamaPlayerModule
  {
    [SerializeField] private VRCUrl[] _redirectPool = new VRCUrl[0];
    [SerializeField] private string _poolId;

    public VRCUrl[] RedirectPool => _redirectPool;
    public string PoolId => _poolId;
  }
}
