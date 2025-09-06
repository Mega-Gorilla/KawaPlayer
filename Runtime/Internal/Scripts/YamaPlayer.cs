using UnityEngine;

#if USE_VRCLV
using VRCLightVolumes;
#endif

namespace Yamadev.YamaStream.Script
{
    public sealed class YamaPlayer : MonoBehaviour
    {
        public Transform Internal;
        public Renderer MainScreen;

        public bool UseLTCGI;
        public bool UseLightVolumes;
#if USE_VRCLV
        public LightVolume[] TargetLightVolumes;
#else
        public MonoBehaviour[] TargetLightVolumes;
#endif
    }
}