using System;
using UnityEngine;

namespace Yamadev.YamaStream
{
  public class YamaPlayerModuleDefinition : MonoBehaviour
  {
    public string moduleName;
    public string moduleDescription;
    public string version;
    public bool allowMultiple;
    public bool noNeedSetUp;

    public ModuleUISlot[] uiSlots;

    public string moduleNameTranslationKey;
    public string moduleDescriptionTranslationKey;

    public TextAsset editorTranslationFile;
    public TextAsset playerTranslationFile;

    // The object switched off and deleted with the module, for a module whose
    // other parts sit beside it rather than under it: DefaultUrl names
    // Modules/DefaultUrl, which holds its controller and its storage. It must
    // hold this object; empty means this object alone.
    public GameObject moduleRoot;
  }

  [Serializable]
  public class ModuleUISlot
  {
    public string targetPath;
    public GameObject content;
    public int siblingIndex;
  }
}
