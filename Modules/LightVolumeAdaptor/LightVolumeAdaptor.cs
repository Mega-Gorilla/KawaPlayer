using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
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
      // Dark until a video's colour comes, whatever colour the scene gave the
      // volumes. A video already showing, told of as this joined the listeners
      // above, replaces it with its first readback.
      SetColor(Color.black);
    }

    // The video's texture, sampled all the while a video shows. The
    // controller tells of a texture only when it changes (#356), so the
    // sampling runs here rather than on each telling. It is forgotten when
    // the video stops or fails, and told again when a video is ready.
    private Texture _texture;

    // The readback last asked for. One takes a few frames to come back, and
    // one asked for meanwhile tells the colour no sooner. Each makes garbage
    // in the SDK (#358), so the next waits until this one is done.
    private VRCAsyncGPUReadbackRequest _readback;

    public override void AfterTextureUpdated(Texture texture)
    {
      _texture = texture;
      if (!Utilities.IsValid(texture)) SetColor(Color.black);
    }

    private void Update()
    {
      if (!Utilities.IsValid(_texture) || (Utilities.IsValid(_readback) && !_readback.done)) return;
      VRCGraphics.Blit(_texture, _downsampledTexture);
      _readback = VRCAsyncGPUReadback.Request(_downsampledTexture, _downsampledTexture.mipmapCount - 1, (IUdonEventReceiver)this);
    }

    public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
    {
      // A readback asked for before the video stopped comes in a frame or
      // more after it, and lit the room with the last frame again.
      if (!Utilities.IsValid(_texture)) return;
      if (request.TryGetData(_pixels))
      {
        SetColor(_pixels[0]);
      }
    }

    // The colour the volumes were last given. Most readbacks of a video bring
    // it again, between its frames, and they need not be told (#358).
    private Color _shownColor;

    // Writing Color is all a volume needs: Light Volumes (2.0 to 3.0) hears
    // the change itself, works out the point light's range again and updates
    // the light. The manager's AutoUpdateVolumes is not needed for the colour,
    // so it is left as the world set it (issue #195).
    private void SetColor(Color color)
    {
      if (color == _shownColor) return;
      _shownColor = color;
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
      _texture = null;
      SetColor(Color.black);
    }

    // A player stopped by an error keeps its last frame and tells of no
    // texture: the light keeps its colour, without reading that frame back
    // again and again until a retry or another video is ready.
    public override void AfterVideoErrorOccurred(VideoError videoError) => _texture = null;
#endif
  }
}
