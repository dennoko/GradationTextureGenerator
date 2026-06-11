# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Gradation Baker is a Unity Editor extension (Unity 2022.3+, MIT license) that bakes 3D-space gradients into UV-space textures for meshes, aimed at VRChat avatar workflows. The repo root is `Assets/Editor/GradationBaker` inside a VRChat Creator Companion Unity project — there is no standalone build, lint, or test command. Changes are verified by letting the Unity Editor recompile and opening the window via `dennokoworks > Gradation Baker`.

Documentation, UI strings, and commit messages are primarily Japanese. User docs live in [Docs/](Docs/); the original requirements/design are in [Plan.md](Plan.md).

## Architecture

Code is split into four namespaces matching the folders:

- **GradationBaker.Data** ([Data/GradationSettings.cs](Data/GradationSettings.cs)) — all settings, enums, and `MeshEntry` (per-mesh state) in a single file. `GradationSettings` holds a list of `MeshEntry` plus global gradient/box/mirror options. The gradient region is modeled as an oriented box (`BoxCenter`/`BoxRotation`/`BoxHeight`/`BoxWidth`/`BoxDepth`); the gradient direction is derived as `BoxRotation * Vector3.up`.
- **GradationBaker.Execute** — the bake pipeline and helpers (see below).
- **GradationBaker.UI** — `GradationBakerWindow` (the `EditorWindow`, owns everything), `GradationSceneHandle` (SceneView box handles with Undo support), `GradationPreview` (real-time preview), `StatusBar`, `GradationBakerTheme`.
- **GradationBaker.Localization** — `LocalizationManager` loads [Localization/ja.json](Localization/ja.json) / [Localization/en.json](Localization/en.json); language choice persists via `EditorPrefs`. Any new UI string needs keys added to **both** JSON files.

### Bake pipeline (Execute/)

Baking is GPU-based, not `SetPixel`: `GradationBakingExecutor` renders the mesh with the hidden shader `Hidden/GradationBaker/Bake` ([Shaders/GradationBake.shader](Shaders/GradationBake.shader)), whose vertex shader remaps positions to UV space so the gradient (sampled through a LUT texture generated from the `UnityEngine.Gradient`) is written directly into a `RenderTexture`, then read back to `Texture2D` and saved as PNG. Supporting pieces:

- **WorkMeshManager** — creates an offset `[GradGen_Work]` copy (plain MeshFilter+MeshRenderer) of the source renderer. This exists to get clean object-space coordinates when the source has non-destructive avatar components; `MeshEntry.ActiveRenderer` returns the work mesh when present. The window cleans these up in `OnDisable`.
- **Mirror** — implemented as a second bake with mirrored coordinates, blended into the main texture with Max/Min (`BlendTextures` in `GradationBakingExecutor`), not in-shader.
- **Multi-material** — `MeshEntry.SplitByMaterial` bakes per-submesh into `BakeResult.SubMeshResults`; material slots can be individually toggled via `EnabledMaterialSlots`.
- **EdgePadding** — dilates baked texels past UV island edges to avoid seams.
- **OutputPathResolver** — output can target a `gradation/` subfolder next to the renderer's main texture, or a default path.
- **MeshReadWriteEnabler** — flips Read/Write on mesh import settings before baking.
- **FileLogger** — logs to `Assets/Editor/GradationBaker/Log/` (path is hardcoded; the `Log/` contents are gitignored).

### Preview

`GradationPreview` creates per-renderer proxy objects rendered with `Hidden/GradationBaker/Preview` ([Shaders/GradationPreview.shader](Shaders/GradationPreview.shader)), which evaluates the same gradient math in real time and supports blend modes (Replace/Additive/Screen/Multiply) against the original texture. Gradient math changes must be kept in sync between the bake shader, the preview shader, and any C#-side equivalents.

## UI theming

[dennokoworks_color_schema/](dennokoworks_color_schema/) is a vendored design-system reference (color values in `colors.json`, IMGUI implementation templates in `forUnity/`). When styling editor UI, follow `forUnity/README.md` there: dark layered "floating" surfaces drawn with `EditorGUI.DrawRect`, colors taken from `colors.json`.

## Conventions

- `.meta` files are gitignored at the repo root (`*.meta`), but the nested `dennokoworks_color_schema/` tracks its own files — check the relevant `.gitignore` before assuming.
- Editor-prefs keys use the `GradGen_` prefix.
- SceneView handle edits and work-mesh creation are registered with Undo; preserve that for any new interactive editing.
