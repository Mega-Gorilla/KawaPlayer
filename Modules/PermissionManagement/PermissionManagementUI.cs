using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using Yamadev.YamaStream.UI;

namespace Yamadev.YamaStream.Modules.PermissionManagement
{
  [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
  public class PermissionManagementUI : YamaPlayerListener
  {
    [SerializeField] private PermissionManagement _permissionManagement;
    [SerializeField] private LoopScroll _permissionScroll;
    [SerializeField] private GameObject _permissionEntry;
    [SerializeField] private GameObject _permissionPage;
    [SerializeField] private Color _ownerColor = new Color(0.39f, 0.71f, 0.96f, 1f);
    [SerializeField] private Color _adminColor = new Color(0.73f, 0.41f, 0.78f, 1f);
    [SerializeField] private Color _editorColor = new Color(0.51f, 0.78f, 0.52f, 1f);
    [SerializeField] private Color _viewerColor = new Color(1f, 0.72f, 0.30f, 1f);

    [Header("Localization")]
    [SerializeField] private Text _toggleLabelText;
    [SerializeField] private Text _descriptionText;

    private UIController _uiController;
    private int _permissionIndex = -1;

    private void Start()
    {
      _uiController = GetComponentInParent<UIController>();
      if (!Utilities.IsValid(_uiController) || !Utilities.IsValid(_permissionManagement)) return;
      _permissionManagement.AddListener(this);
      UpdateTranslation();
      GeneratePermissionView();
    }

    public override void BeforeUserChangePlayerHandler() => CheckPermission();
    public override void BeforeUserPlayTrack() => CheckPermission();
    public override void BeforeUserPlayVideo() => CheckPermission();
    public override void BeforeUserPauseVideo() => CheckPermission();
    public override void BeforeUserStopVideo() => CheckPermission();
    public override void BeforeUserSetTime() => CheckPermission();
    public override void BeforeUserBackward() => CheckPermission();
    public override void BeforeUserForward() => CheckPermission();
    public override void BeforeUserReloadVideo() => CheckPermission();
    public override void BeforeUserChangeLoop() => CheckPermission();
    public override void BeforeUserChangeShufflePlay() => CheckPermission();
    public override void BeforeUserChangeSpeed() => CheckPermission();
    public override void BeforeUserChangeRepeat() => CheckPermission();
    public override void BeforeUserAddTrackToQueue() => CheckPermission();
    public override void BeforeUserRemoveTrackFromQueue() => CheckPermission();
    public override void BeforeUserMoveTrackUp() => CheckPermission();
    public override void BeforeUserMoveTrackDown() => CheckPermission();

    private void CheckPermission()
    {
      if (!Utilities.IsValid(_uiController) || !Utilities.IsValid(_permissionManagement)) return;
      if ((int)_permissionManagement.PlayerPermission >= (int)PlayerPermission.Editor) return;

      _uiController.CancelCurrentAction();
      _uiController.ShowMessage(
        _uiController.GetTranslation("module.permissionManagement.noPermission"),
        _uiController.GetTranslation("module.permissionManagement.noPermissionMessage")
      );
    }

    public void GeneratePermissionView()
    {
      if (!Utilities.IsValid(_permissionManagement) || !Utilities.IsValid(_permissionScroll)) return;

      _permissionScroll.SetUp(_permissionManagement.PermissionData.Count, this, nameof(UpdatePermissionView));

      bool showPage = _permissionManagement.PlayerPermission == PlayerPermission.Owner ||
        _permissionManagement.PlayerPermission == PlayerPermission.Admin;

      if (Utilities.IsValid(_permissionEntry)) _permissionEntry.SetActive(showPage);
      if (Utilities.IsValid(_permissionPage) && !showPage && _permissionPage.activeSelf)
      {
        _permissionPage.SetActive(false);
      }
    }

    public void UpdatePermissionView()
    {
      if (!Utilities.IsValid(_permissionScroll)) return;

      ScrollRect scrollRect = _permissionScroll.GetComponent<ScrollRect>();
      if (!Utilities.IsValid(scrollRect)) return;

      for (int i = 0; i < _permissionScroll.LineCount; i++)
      {
        int index = _permissionScroll.Indexes[i];
        if (index == _permissionScroll.LastIndexes[i] || index == -1) continue;

        Transform cell = scrollRect.content.GetChild(i);
        DataToken value = _permissionManagement.PermissionData.GetValues()[index];

        if (value.DataDictionary.TryGetValue("displayName", TokenType.String, out DataToken displayName) &&
          cell.TryFind("Name", out var name) &&
          name.TryGetComponentLocal(out Text nameText))
        {
          nameText.text = displayName.String;
        }

        PlayerPermission permission = (PlayerPermission)value.DataDictionary["permission"].Int;
        bool couldControl = (int)_permissionManagement.PlayerPermission > (int)permission;

        if (cell.TryFind("Label", out var label) && label.TryGetComponentLocal(out Text labelText))
        {
          labelText.text = permission == PlayerPermission.Owner ? "Owner" : "Admin";
        }

        if (cell.TryFind("Dropdown", out var dropdown))
        {
          dropdown.gameObject.SetActive(couldControl);
        }

        if (cell.TryFind("Mark", out var mark) && mark.TryGetComponentLocal(out Image markImage))
        {
          switch (permission)
          {
            case PlayerPermission.Owner:
              markImage.color = _ownerColor;
              break;
            case PlayerPermission.Admin:
              markImage.color = _adminColor;
              if (Utilities.IsValid(dropdown)) dropdown.GetComponent<Dropdown>().SetValueWithoutNotify(0);
              break;
            case PlayerPermission.Editor:
              markImage.color = _editorColor;
              if (Utilities.IsValid(dropdown)) dropdown.GetComponent<Dropdown>().SetValueWithoutNotify(1);
              break;
            case PlayerPermission.Viewer:
              markImage.color = _viewerColor;
              if (Utilities.IsValid(dropdown)) dropdown.GetComponent<Dropdown>().SetValueWithoutNotify(2);
              break;
            default:
              markImage.color = _viewerColor;
              break;
          }
        }

        if (cell.TryGetComponentLocal<IndexTrigger>(out var trigger))
        {
          trigger.SetProgramVariable("_variableObject", index);
        }

        cell.gameObject.SetActive(true);
      }
    }

    public void SetPermissionIndex(int index)
    {
      _permissionIndex = index;
    }

    public void SetPermission()
    {
      if (!Utilities.IsValid(_permissionManagement) || !Utilities.IsValid(_permissionScroll) || _permissionIndex < 0) return;

      int cellIndex = System.Array.IndexOf(_permissionScroll.Indexes, _permissionIndex);
      if (cellIndex < 0) return;

      ScrollRect scrollRect = _permissionScroll.GetComponent<ScrollRect>();
      if (!Utilities.IsValid(scrollRect)) return;

      Transform cell = scrollRect.content.GetChild(cellIndex);
      if (!cell.TryFind("Dropdown", out var dropdownTransform)) return;

      Dropdown dropdown = dropdownTransform.GetComponent<Dropdown>();
      if (!Utilities.IsValid(dropdown)) return;

      PlayerPermission playerPermission = PlayerPermission.Viewer;
      switch (dropdown.value)
      {
        case 0:
          playerPermission = PlayerPermission.Admin;
          break;
        case 1:
          playerPermission = PlayerPermission.Editor;
          break;
        case 2:
          playerPermission = PlayerPermission.Viewer;
          break;
      }

      _permissionManagement.TakeOwnership();
      _permissionManagement.SetPermission(_permissionIndex, playerPermission);
    }

    private void UpdateTranslation()
    {
      if (!Utilities.IsValid(_uiController)) return;
      if (Utilities.IsValid(_toggleLabelText)) _toggleLabelText.text = _uiController.GetTranslation("module.permissionManagement.toggleLabelText");
      if (Utilities.IsValid(_descriptionText))
      {
        _descriptionText.text = $"<color=#64B5F6>{_uiController.GetTranslation("module.permissionManagement.owner")}</color>\t\t\t{_uiController.GetTranslation("module.permissionManagement.ownerDesc")}\r\n" +
          $"<color=#BA68C8>{_uiController.GetTranslation("module.permissionManagement.admin")}</color>\t\t\t{_uiController.GetTranslation("module.permissionManagement.adminDesc")}\r\n" +
          $"<color=#81C784>{_uiController.GetTranslation("module.permissionManagement.editor")}</color>\t\t\t{_uiController.GetTranslation("module.permissionManagement.editorDesc")}\r\n" +
          $"<color=#FFB74D>{_uiController.GetTranslation("module.permissionManagement.viewer")}</color>\t\t\t{_uiController.GetTranslation("module.permissionManagement.viewerDesc")}";
      }
    }

    public void AfterLanguageChanged() => UpdateTranslation();
    public void AfterPermissionChanged() => GeneratePermissionView();
  }
}
