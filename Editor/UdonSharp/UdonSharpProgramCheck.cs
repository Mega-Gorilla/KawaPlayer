using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace Yamadev.YamaStream.Editor
{
  // Compiles U# again when one of KawaPlayer's programs is missing, empty or
  // lacks a field its script declares (issue #265). Opened without a Library,
  // U# compiles before the program assets are imported, so it binds none of
  // them ("Compile of 0 scripts") yet records every script as compiled, and
  // never compiles again: the programs new in this version stay empty, and
  // those a project had keep the code of the version it had. Play then fails
  // with "Could not load the program", or runs the old code.
  //
  // This repairs that state, and is not a general check for old programs: one
  // whose code alone changed, with the same fields, is not found. In the state
  // above some program is empty whenever the version added one, and compiling
  // all of U# brings the others up to date too.
  //
  // Checked once per session and version, once importing and U#'s own compile
  // are over, or when Play is pressed before that. A world build compiles U#
  // anyway, unless U#'s settings turn that off.
  [InitializeOnLoad]
  internal static class UdonSharpProgramCheck
  {
    private const string CheckedKeyBase = "KawaPlayer_UdonSharpProgramChecked";
    private const string FailedKeyBase = "KawaPlayer_UdonSharpProgramCheckFailed";
    // KawaPlayer's scripts, wherever it was installed (VCC or .unitypackage).
    private const string KawaPlayerNamespace = "Yamadev.YamaStream";
    // Where Udon keeps a program asset's compiled program: named after the
    // program asset's GUID, as UdonProgramAsset.SerializedProgramAsset finds it.
    private const string SerializedProgramFolder = "Assets/SerializedUdonPrograms";
    // What U# calls its compile in Unity's progress list. No public API says
    // whether U# is compiling; checking during its compile would find the
    // programs it is about to write and compile a second time.
    private const string UdonSharpCompileProgress = "U# Compile";
    private const double SettleSeconds = 1.0;
    private const int NamesShown = 5;

    private static double _settledSince = -1;

    // Per version, so updating KawaPlayer with the editor open checks again.
    private static string CheckedKey => $"{CheckedKeyBase}_{PackageManager.Version}";
    private static string FailedKey => $"{FailedKeyBase}_{PackageManager.Version}";

    static UdonSharpProgramCheck()
    {
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
      if (!SessionState.GetBool(CheckedKey, false)) EditorApplication.update += CheckWhenSettled;
    }

    private static void CheckWhenSettled()
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
          EditorApplication.isUpdating || IsUdonSharpCompiling())
      {
        _settledSince = -1;
        return;
      }

      double now = EditorApplication.timeSinceStartup;
      if (_settledSince < 0) _settledSince = now;
      if (now - _settledSince < SettleSeconds) return;

      EditorApplication.update -= CheckWhenSettled;
      Repair();
    }

    // Play pressed before the check ran: check now, as entering Play loads
    // the programs. After a repair that failed, every Play checks again, with
    // no compile, until the programs are sound.
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
      if (state != PlayModeStateChange.ExitingEditMode) return;

      bool sound;
      if (!SessionState.GetBool(CheckedKey, false))
      {
        EditorApplication.update -= CheckWhenSettled;
        sound = Repair();
      }
      else if (SessionState.GetBool(FailedKey, false))
      {
        var programs = FindProgramsToCompile();
        sound = programs.Count == 0;
        if (sound) SessionState.SetBool(FailedKey, false);
        else LogStillBroken(programs);
      }
      else return;

      if (!sound) EditorApplication.isPlaying = false;
    }

    // Compiles all of U# at most once per session and version, and returns
    // whether KawaPlayer's programs are sound afterwards.
    private static bool Repair()
    {
      SessionState.SetBool(CheckedKey, true);

      var programs = FindProgramsToCompile();
      if (programs.Count == 0) return true;

      Debug.LogWarning($"[KawaPlayer] {programs.Count} of KawaPlayer's U# programs are missing, empty or out of date ({Names(programs)}). Compiling all U# programs again.");
      UdonSharpCompilerV1.CompileSync();

      programs = FindProgramsToCompile();
      if (programs.Count == 0)
      {
        Debug.Log("[KawaPlayer] KawaPlayer's U# programs are up to date.");
        return true;
      }

      SessionState.SetBool(FailedKey, true);
      LogStillBroken(programs);
      return false;
    }

    private static void LogStillBroken(List<string> programs)
    {
      Debug.LogError($"[KawaPlayer] {programs.Count} of KawaPlayer's U# programs are still missing, empty or out of date ({Names(programs)}), so Play was stopped. Fix the U# errors in the Console if there are any, then select one of KawaPlayer's U# program assets (such as Runtime/Internal/Controller) and press Compile All UdonSharp Programs in the Inspector.");
    }

    private static string Names(List<string> programs)
    {
      string names = string.Join(", ", programs.Take(NamesShown));
      return programs.Count > NamesShown ? $"{names} and {programs.Count - NamesShown} more" : names;
    }

    private static bool IsUdonSharpCompiling()
    {
      return Progress.EnumerateItems().Any(item => item.name == UdonSharpCompileProgress && item.running);
    }

    private static List<string> FindProgramsToCompile()
    {
      var names = new List<string>();
      foreach (var programAsset in UdonSharpProgramAsset.GetAllUdonSharpPrograms())
      {
        if (programAsset == null || programAsset.sourceCsScript == null) continue;
        string space = programAsset.sourceCsScript.GetClass()?.Namespace;
        if (space == null || !(space == KawaPlayerNamespace || space.StartsWith(KawaPlayerNamespace + "."))) continue;
        if (NeedsCompile(programAsset)) names.Add(programAsset.name);
      }
      return names;
    }

    // Missing or empty, or lacking a field its script declares: the field
    // definitions are written by each compile in this project, and ship with
    // the package from the compile it was made with.
    private static bool NeedsCompile(UdonSharpProgramAsset programAsset)
    {
      var serializedProgram = FindSerializedProgram(programAsset);
      var program = serializedProgram != null ? serializedProgram.RetrieveProgram() : null;
      if (program == null) return true;
      if (programAsset.fieldDefinitions == null) return false;

      foreach (string field in programAsset.fieldDefinitions.Keys)
      {
        // An auto-property's backing field has no symbol in the program.
        if (field.StartsWith("<")) continue;
        if (!program.SymbolTable.HasAddressForSymbol(field)) return true;
      }
      return false;
    }

    // Finds the compiled program as UdonProgramAsset.SerializedProgramAsset
    // does, but only reads: that getter creates, saves and refreshes assets
    // when it finds none.
    private static AbstractSerializedUdonProgramAsset FindSerializedProgram(UdonSharpProgramAsset programAsset)
    {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string guid, out long _)) return null;

      var reference = new SerializedObject(programAsset).FindProperty("serializedUdonProgramAsset")?.objectReferenceValue as AbstractSerializedUdonProgramAsset;
      if (reference != null && reference.name == guid) return reference;

      return AssetDatabase.LoadAssetAtPath<AbstractSerializedUdonProgramAsset>($"{SerializedProgramFolder}/{guid}.asset");
    }
  }
}
