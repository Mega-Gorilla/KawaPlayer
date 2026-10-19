using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;

namespace Yamadev.YamaStream.Editor
{
  // Compiles U# again when one of KawaPlayer's programs is empty or older
  // than its script (issue #265). Opened without a Library, U# compiles
  // before the program assets are imported, so it binds none of them
  // ("Compile of 0 scripts") yet records every script as compiled, and never
  // compiles again: the programs new in this version stay empty, and those a
  // project had keep the code of the version it had. Play then fails with
  // "Could not load the program", or runs the old code. Checked once per
  // session and version, once importing and U#'s own compile are over; a
  // world build compiles U# anyway.
  [InitializeOnLoad]
  internal static class UdonSharpProgramCheck
  {
    private const string CheckedKeyBase = "KawaPlayer_UdonSharpProgramChecked";
    // KawaPlayer's scripts, wherever it was installed (VCC or .unitypackage).
    private const string KawaPlayerNamespace = "Yamadev.YamaStream";
    // What U# calls its compile in Unity's progress list. No public API says
    // whether U# is compiling; checking during its compile would find the
    // programs it is about to write and compile a second time.
    private const string UdonSharpCompileProgress = "U# Compile";
    private const double SettleSeconds = 1.0;
    private const int NamesShown = 5;

    private static double _settledSince = -1;

    // Per version, so updating KawaPlayer with the editor open checks again.
    private static string CheckedKey => $"{CheckedKeyBase}_{PackageManager.Version}";

    static UdonSharpProgramCheck()
    {
      if (SessionState.GetBool(CheckedKey, false)) return;
      EditorApplication.update += CheckWhenSettled;
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
      SessionState.SetBool(CheckedKey, true);

      var programs = FindProgramsToCompile();
      if (programs.Count == 0) return;

      string names = string.Join(", ", programs.Take(NamesShown));
      if (programs.Count > NamesShown) names += $" and {programs.Count - NamesShown} more";
      Debug.LogWarning($"[KawaPlayer] {programs.Count} of KawaPlayer's U# programs are empty or out of date ({names}). Compiling all U# programs again.");
      UdonSharpCompilerV1.CompileSync();
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

    // Empty, or missing a field its script declares: the field definitions
    // are written by each compile in this project, and ship with the package
    // from the compile it was made with.
    private static bool NeedsCompile(UdonSharpProgramAsset programAsset)
    {
      var serializedProgram = programAsset.SerializedProgramAsset;
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
  }
}
