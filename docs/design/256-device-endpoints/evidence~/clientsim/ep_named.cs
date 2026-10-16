// Play: lists, for #256, who each named event of VizVid's Core and FrontendHandler goes to.
string Who(VRC.SDK3.Data.DataToken t)
{
  if (t.TokenType == VRC.SDK3.Data.TokenType.Reference) { var u = t.Reference as VRC.Udon.UdonBehaviour; return u != null && u.programSource != null ? u.programSource.name : "?"; }
  if (t.TokenType == VRC.SDK3.Data.TokenType.DataList) { var l = new System.Collections.Generic.List<string>(); for (int i = 0; i < t.DataList.Count; i++) l.Add(Who(t.DataList[i])); return string.Join("+", l); }
  return t.TokenType.ToString();
}
var sb = new System.Text.StringBuilder();
foreach (var u in UnityEngine.Object.FindObjectsByType<VRC.Udon.UdonBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
  if (u.programSource == null || (u.programSource.name != "Core" && u.programSource.name != "FrontendHandler")) continue;
  var d = u.GetProgramVariable("namedTargets") as VRC.SDK3.Data.DataDictionary;
  var targets = u.GetProgramVariable("targets") as System.Array;
  sb.Append($"{u.programSource.name}: targets={(targets == null ? "null" : targets.Length.ToString())} named={(d == null ? 0 : d.Count)}\n");
  if (d == null) continue;
  var keys = d.GetKeys();
  for (int i = 0; i < keys.Count; i++) sb.Append($"  {keys[i].String} -> {Who(d[keys[i]])}\n");
}
return sb.ToString();
