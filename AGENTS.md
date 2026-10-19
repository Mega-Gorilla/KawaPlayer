# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity/VPM package for VRChat. Core runtime code lives in `Runtime/`, with public scene components in `Runtime/Components/` and the main UdonSharp controller split across partials in `Runtime/Internal/`. Editor-only tooling lives in `Editor/`. Optional features are isolated under `Modules/`, typically with separate runtime/editor `.asmdef` files. Reusable prefabs are in `Prefabs/`, package assets and localization data are in `Assets/`, and reference docs are in `docs/`.

## Build, Test, and Development Commands
There is no standalone CLI build for the package; open it in Unity `2022.3` with the VRChat Worlds SDK installed.

- `vpm add repo https://vpm.kwxxw.net/index.json`: add the package repository to VCC/VPM.
- Open the package in Unity: compile runtime and editor assemblies, inspect prefabs, and validate serialized references.
- `git log --pretty=format:"%h %s" -6`: review recent commit prefixes before writing a commit.

Releases are cut from the manual GitHub Actions workflow in `.github/workflows/release.yml`, which packages ZIP and `.unitypackage` artifacts from `package.json`.

## Coding Style & Naming Conventions
Follow the existing C# style: 2-space indentation, braces on their own lines, `PascalCase` for types/methods/properties, and `_camelCase` for private fields. Keep namespaces under `Yamadev.YamaStream`. Match existing assembly boundaries: runtime code must stay UdonSharp-safe, while UnityEditor APIs belong only in `Editor/` or module editor assemblies. Preserve Unity `.meta` files when moving or adding assets.

## Compatibility & Technical Debt Policy

実装・レビューの共通ルール（ユーザー合意、2026-10-15）。

- **旧版との互換性より、現行仕様に沿った単純で一貫した設計を優先する。** 旧バージョンの内部実装・挙動を維持するためだけの互換コードは原則不要とする。
- 公開済みワールドにはビルド時の KawaPlayer が組み込まれており、新しいパッケージを公開しただけでは差し替わらない。更新・再アップロードしない旧版ワールドの動作を守る目的で、新版に旧処理を残さない。
- 変更範囲の不要なコード・暫定分岐・二重実装・ラッパーは削除し、互換性を理由に技術負債を温存しない。未公開版間の互換性維持や移行処理も原則不要とする。
- **既存プロジェクトを新版へ更新したあとの動作確認は行う。** シーン・Prefab の参照、設定、プレイリスト、永続化データへの影響を確認する。破壊的変更や移行・再設定が必要な場合は、その影響と手順を PR・利用者向けドキュメントに明示し、旧実装を恒久的に残すことで対処しない。
- レビューでは現行仕様の正しさ・設計の単純さ・更新後の動作・変更点の案内を評価する。旧版の内部実装や挙動と違うことだけを理由に差し戻さない。一方、具体的な参照切れ、意図しない設定・データの消失、コンパイル・ビルドエラーは不具合として指摘する。
- この方針は Unity / VRChat / Udon の制約を無視する許可ではない。Unity の `.meta` / GUID、Network ID、宣言した SDK 対応範囲、現行版の PC / Quest 間の整合性に関するルールは引き続き守る。

## Testing Guidelines
No automated test suite is present in this repository. Validate changes in Unity by entering Play Mode where possible, checking console errors, and verifying prefab/module wiring on a sample scene or packaged world object. For UI, playlist, localization, and prefab changes, confirm both editor behavior and runtime behavior after serialization.

## Commit & Pull Request Guidelines
Recent history uses short prefixes such as `fix:`, `feat:`, and `update:`. Keep commit subjects imperative and scoped, for example `fix: timeline sync` or `feat: add playlist import guard`. Pull requests should describe the user-facing change, list affected folders/modules, note Unity or VRChat SDK assumptions, and include screenshots or short recordings for prefab/editor UI updates.
