using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yamadev.YamaStream
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
  [RequireComponent(typeof(YamaPlayerModuleDefinition))]
  public abstract class YamaPlayerModule : YamaPlayerListener
  {
    [SerializeField, HideInInspector] protected Controller _controller;
    public virtual void Start()
    {
      if (!Utilities.IsValid(_controller))
      {
        PrintError($"Controller is not set in module {gameObject.name}");
        return;
      }
      _controller.AddListener(this);
    }
  }
}