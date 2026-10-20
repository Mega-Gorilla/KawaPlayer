using System;
using System.Reflection;
using NUnit.Framework;

namespace Yamadev.YamaStream.Editor.Tests
{
  // PlaylistBuildProcess adds the world's playlists through U#'s internal
  // RunBehaviourSetup, so that Play does not initialize them before the SDK
  // gives them networking (issue #164). An SDK that drops or changes it would
  // only log a warning there; this fails instead. U# is found by name, as this
  // assembly does not reference it.
  public class UdonSharpSetupTests
  {
    [Test]
    public void RunBehaviourSetupIsThere()
    {
      var utility = Type.GetType("UdonSharpEditor.UdonSharpEditorUtility, UdonSharp.Editor");
      var behaviour = Type.GetType("UdonSharp.UdonSharpBehaviour, UdonSharp.Runtime");
      Assert.IsNotNull(utility);
      Assert.IsNotNull(behaviour);
      var method = utility.GetMethod("RunBehaviourSetup", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { behaviour }, null);
      Assert.IsNotNull(method, "UdonSharpEditorUtility.RunBehaviourSetup(UdonSharpBehaviour) is gone");
    }
  }
}
