// Play: locks VizVid's frontend (as an outside permission system would), then reads what changed. ACTION: lock | unlock
VRC.Udon.UdonBehaviour handler = null;
foreach (var u in UnityEngine.Object.FindObjectsByType<VRC.Udon.UdonBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
  if (u.programSource != null && u.programSource.name == "FrontendHandler") handler = u;
string action = "ACTION";
handler.SendCustomEvent(action == "lock" ? "_Lock" : "_OnUnlock");
return $"{action}: locked={handler.GetProgramVariable("locked")}";
