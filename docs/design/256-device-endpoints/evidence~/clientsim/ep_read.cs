// Reads, for #256, VizVid's event targets and the links the build resolves: in edit mode from the U# proxies, in Play from Udon.
var playing = UnityEditor.EditorApplication.isPlaying;
var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
object Get(VRC.Udon.UdonBehaviour u, string name)
{
  if (playing) return u.GetProgramVariable(name);
  var proxy = UdonSharpEditor.UdonSharpEditorUtility.GetProxyBehaviour(u);
  for (var t = proxy.GetType(); t != null; t = t.BaseType)
  {
    var f = t.GetField(name, bf | System.Reflection.BindingFlags.DeclaredOnly);
    if (f != null) return f.GetValue(proxy);
  }
  return "<no field>";
}
string Names(object o)
{
  if (o is System.Array a) { var l = new System.Collections.Generic.List<string>(); foreach (var x in a) l.Add(x == null ? "null" : (x is Component c ? (c is VRC.Udon.UdonBehaviour ub && ub.programSource != null ? ub.programSource.name : c.GetType().Name) + "@" + c.name : x.ToString())); return $"{a.Length}[{string.Join(", ", l)}]"; }
  if (o == null) return "null";
  if (o is Component c2) return (c2 is VRC.Udon.UdonBehaviour ub2 && ub2.programSource != null ? ub2.programSource.name : c2.GetType().Name) + "@" + c2.name;
  return o.ToString();
}
VRC.Udon.UdonBehaviour core = null, handler = null; var uis = new System.Collections.Generic.List<VRC.Udon.UdonBehaviour>();
foreach (var u in UnityEngine.Object.FindObjectsByType<VRC.Udon.UdonBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
  if (u.programSource == null) continue;
  if (u.programSource.name == "Core") core = u;
  if (u.programSource.name == "FrontendHandler") handler = u;
  if (u.programSource.name == "UIHandler") uis.Add(u);
}
if (core == null) return "no core";
var sb = new System.Text.StringBuilder($"playing={playing}\n");
sb.Append("Core.targets=" + Names(Get(core, "targets")) + "\n");
var nt = Get(core, "namedTargets");
sb.Append("Core.namedTargets=" + (nt is VRC.SDK3.Data.DataDictionary d ? $"{d.Count} names: " + string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, d.Count), i => d.GetKeys()[i].String)) : Names(nt)) + "\n");
if (handler != null)
{
  sb.Append("FrontendHandler.targets=" + Names(Get(handler, "targets")) + "\n");
  sb.Append("FrontendHandler.core=" + Names(Get(handler, "core")) + " locked=" + Get(handler, "locked") + "\n");
}
foreach (var ui in uis)
{
  sb.Append($"UIHandler@{ui.name}: handler=" + Names(Get(ui, "handler")) + " core=" + Names(Get(ui, "core")));
  var input = Get(ui, "urlInput") as UnityEngine.UI.Selectable; sb.Append(" urlInput.interactable=" + (input != null ? input.interactable.ToString() : "?"));
  var play = Get(ui, "playButton") as UnityEngine.UI.Button; sb.Append(" playButton=" + (play != null ? (play.gameObject.activeSelf + "/" + play.interactable) : "?") + "\n");
}
return sb.ToString();
