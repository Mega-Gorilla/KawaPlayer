# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity/VPM package for VRChat. Core runtime code lives in `Runtime/`, with public scene components in `Runtime/Components/` and the main UdonSharp controller split across partials in `Runtime/Internal/`. Editor-only tooling lives in `Editor/`. Optional features are isolated under `Modules/`, typically with separate runtime/editor `.asmdef` files. Reusable prefabs are in `Prefabs/`, package assets and localization data are in `Assets/`, reference docs are in `docs/`, and `Samples~/` contains importable samples.

## Build, Test, and Development Commands
There is no standalone CLI build for the package; open it in Unity `2022.3` with the VRChat Worlds SDK installed.

- `vpm add repo https://vpm.kwxxw.net/index.json`: add the package repository to VCC/VPM.
- Open the package in Unity: compile runtime and editor assemblies, inspect prefabs, and validate serialized references.
- `git log --pretty=format:"%h %s" -6`: review recent commit prefixes before writing a commit.

Releases are cut from the manual GitHub Actions workflow in `.github/workflows/release.yml`, which packages ZIP and `.unitypackage` artifacts from `package.json`.

## Coding Style & Naming Conventions
Follow the existing C# style: 2-space indentation, braces on their own lines, `PascalCase` for types/methods/properties, and `_camelCase` for private fields. Keep namespaces under `Yamadev.YamaStream`. Match existing assembly boundaries: runtime code must stay UdonSharp-safe, while UnityEditor APIs belong only in `Editor/` or module editor assemblies. Preserve Unity `.meta` files when moving or adding assets.

## Testing Guidelines
No automated test suite is present in this repository. Validate changes in Unity by entering Play Mode where possible, checking console errors, and verifying prefab/module wiring on a sample scene or packaged world object. For UI, playlist, localization, and prefab changes, confirm both editor behavior and runtime behavior after serialization.

## Commit & Pull Request Guidelines
Recent history uses short prefixes such as `fix:`, `feat:`, and `update:`. Keep commit subjects imperative and scoped, for example `fix: timeline sync` or `feat: add playlist import guard`. Pull requests should describe the user-facing change, list affected folders/modules, note Unity or VRChat SDK assumptions, and include screenshots or short recordings for prefab/editor UI updates.
