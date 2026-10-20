using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;

// VRC_LIGHT_VOLUMES is the project-wide define Light Volumes adds when its
// editor code loads. It has to be a project-wide one while KawaPlayer
// supports SDKs before 3.10.5: their U# compiles every program with the
// project's defines only, so a define from this assembly's versionDefines
// would reach the C# assembly but not the Udon program, and the two would
// disagree on the fields (issue #195). U# reads each assembly's defines from
// 3.10.5 on; once that is the minimum SDK, this and the inspector can move to
// one define set in both of this module's asmdefs.
#if VRC_LIGHT_VOLUMES
using VRCLightVolumes;
#endif

namespace Yamadev.YamaStream.Modules.LightVolumeAdaptor
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class LightVolumeAdaptor : YamaPlayerModule
  {
#if VRC_LIGHT_VOLUMES
    [SerializeField] private LightVolumeInstance[] _lightVolumes = new LightVolumeInstance[0];
    [SerializeField] private PointLightVolumeInstance[] _pointLightVolumes = new PointLightVolumeInstance[0];
    private Color32[] _pixels;
    private RenderTexture _downsampledTexture;

    public override void Start()
    {
      base.Start();
      _downsampledTexture = new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
      _downsampledTexture.useMipMap = true;
      _downsampledTexture.autoGenerateMips = true;
      _downsampledTexture.Create();
      _pixels = new Color32[1];
    }

    public override void AfterTextureUpdated(Texture texture)
    {
      if (!Utilities.IsValid(texture))
      {
        SetColor(Color.black);
        return;
      }

      VRCGraphics.Blit(texture, _downsampledTexture);
      VRCAsyncGPUReadback.Request(_downsampledTexture, _downsampledTexture.mipmapCount - 1, (IUdonEventReceiver)this);
    }

    public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
    {
      if (request.TryGetData(_pixels))
      {
        SetColor(_pixels[0]);
      }
    }

    // Writing Color is all a volume needs: Light Volumes (2.0 to 3.0) hears
    // the change itself, works out the point light's range again and updates
    // the light, so the manager's AutoUpdateVolumes, which only follows moving
    // volumes, is left as the world set it (issue #195).
    private void SetColor(Color color)
    {
      for (int i = 0; i < _lightVolumes.Length; i++)
      {
        if (Utilities.IsValid(_lightVolumes[i]))
        {
          _lightVolumes[i].Color = color;
        }
      }

      for (int i = 0; i < _pointLightVolumes.Length; i++)
      {
        if (Utilities.IsValid(_pointLightVolumes[i]))
        {
          _pointLightVolumes[i].Color = color;
        }
      }
    }

    public override void AfterVideoStopped()
    {
      SetColor(Color.black);
    }
#endif
  }
}
