# Types into the test client's window, only while it is the window in front. Only for the VRChat client started for this test.
# -Key enter|esc|end|home|none presses one key. -CasesFile <utf-8 file of "prekey|text" lines> -Index <n> -Part pre|text does that line's part.
param([int]$ProcessId, [string]$Key = '', [string]$CasesFile = '', [int]$Index = -1, [string]$Part = '')
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class TypeTest {
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit)] public struct UNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public UNION u; }
  [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  static INPUT Make(ushort vk, ushort scan, uint flags) { var i = new INPUT(); i.type = 1; i.u.ki.wVk = vk; i.u.ki.wScan = scan; i.u.ki.dwFlags = flags; return i; }
  public static uint Unit(char c) { return SendInput(2, new[] { Make(0, c, 0x0004), Make(0, c, 0x0004 | 0x0002) }, Marshal.SizeOf(typeof(INPUT))); }
  public static uint Vk(ushort vk, bool extended) { uint e = extended ? 1u : 0u; return SendInput(2, new[] { Make(vk, 0, e), Make(vk, 0, e | 0x0002) }, Marshal.SizeOf(typeof(INPUT))); }
}
"@
$p = Get-Process -Id $ProcessId -ErrorAction Stop
if ($p.ProcessName -ne 'VRChat') { throw "not VRChat: $($p.ProcessName)" }
function Front { [TypeTest]::GetForegroundWindow() -eq $p.MainWindowHandle }
function Press([string]$name) {
  switch ($name) {
    'enter' { [void][TypeTest]::Vk(0x0D, $false) }
    'esc' { [void][TypeTest]::Vk(0x1B, $false) }
    'end' { [void][TypeTest]::Vk(0x23, $true) }
    'home' { [void][TypeTest]::Vk(0x24, $true) }
    default { }
  }
}
if (-not (Front)) { "not in front: nothing sent"; exit 0 }
if ($Key -ne '') { Press $Key; "key $Key"; exit 0 }
$lines = [IO.File]::ReadAllLines($CasesFile, [Text.Encoding]::UTF8)
if ($Index -lt 0 -or $Index -ge $lines.Length) { "no line $Index"; exit 0 }
$cut = $lines[$Index].IndexOf('|')
$pre = $lines[$Index].Substring(0, $cut)
$text = $lines[$Index].Substring($cut + 1)
if ($Part -eq 'pre') { Press $pre; "pre $pre"; exit 0 }
$sent = 0
foreach ($c in $text.ToCharArray()) {
  if (-not (Front)) { "lost the front after $sent"; exit 0 }
  [void][TypeTest]::Unit($c)
  $sent++
  Start-Sleep -Milliseconds 60
}
"typed $sent"
