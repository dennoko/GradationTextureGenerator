# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Gradation Baker is a Unity Editor extension (Unity 2022.3+, MIT license) that bakes 3D-space gradients into UV-space textures for meshes, aimed at VRChat avatar workflows. The repo root is `Assets/Editor/GradationBaker` inside a VRChat Creator Companion Unity project — there is no standalone build, lint, or test command. Changes are verified by letting the Unity Editor recompile and opening the window via `dennokoworks > Gradation Baker`.

Documentation, UI strings, and commit messages are primarily Japanese. User docs live in [Docs/](Docs/); the original requirements/design are in [Plan.md](Plan.md).

## Architecture

Code is split into four namespaces matching the folders:

- **GradationBaker.Data** ([Data/GradationSettings.cs](Data/GradationSettings.cs)) — all settings, enums, and `MeshEntry` (per-mesh state) in a single file. `GradationSettings` holds a list of `MeshEntry` plus global gradient/box/mirror options. The gradient region is modeled as an oriented box (`BoxCenter`/`BoxRotation`/`BoxHeight`/`BoxWidth`/`BoxDepth`); the gradient direction is derived as `BoxRotation * Vector3.up`.
- **GradationBaker.Execute** — the bake pipeline and helpers (see below).
- **GradationBaker.UI** — `GradationBakerWindow` (the `EditorWindow`, owns everything) built with UI Toolkit from [UI/GradationBakerWindow.uxml](UI/GradationBakerWindow.uxml) + [UI/DennokoTheme.uss](UI/DennokoTheme.uss); `DennokoUIFont` (shared Meiryo SDF font, self-healing); `GradationSceneHandle` (SceneView box handles with Undo support — the only remaining IMGUI code, since `Handles` requires it); `GradationPreview` (real-time preview); `NdmfPreviewBridge` (NDMF interop via reflection); `DennokoVersionChecker` + `GradationBakerVersion` (update check against `version.json`).
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

The window UI follows the **dennokoworks floating design system**, vendored as a skill at [.claude/skills/dennokoworks_color_schema/](.claude/skills/dennokoworks_color_schema/) (a clone of `dennoko/dennokoworks_color_schema`). Start from its `SKILL.md`, which routes to the relevant guide in `references/`; `references/uss-conventions.md` is required reading before adding or changing any USS.

Key rules that this repo depends on:

- UI Toolkit only — UXML = structure, USS = style, C# = logic. UXML/USS are loaded by GUID in `GradationBakerWindow`.
- The root element carries the `dennoko-root` class; that class is both the USS-variable definition site and the specificity anchor that makes the UI independent of the editor's Light/Dark theme. Without it, all styling dies.
- Colors go through `var(--dennoko-*)`. No hex in UXML inline `style` or in C# `style.color`; hardcoding is confined to the variable block at the top of `DennokoTheme.uss`.
- USS has no `!important` — custom selectors must be prefixed with `.dennoko-root` and win by specificity.
- `UI/DennokoTheme.uss` is the upstream theme file plus tool-specific classes (`dennoko-drop-area*`, `dennoko-mesh-item*`, `dennoko-button-mini/-browse`, `dennoko-lang-button`, …). When re-syncing with the skill, take upstream changes without deleting these.

## Conventions

- `.meta` files are gitignored at the repo root (`*.meta`), but the nested `.claude/skills/dennokoworks_color_schema/` is its own git clone with its own `.gitignore` — check the relevant one before assuming.
- Editor-prefs keys use the `GradGen_` prefix.
- Fonts: call `DennokoUIFont.Apply(root)` once in `CreateGUI()`. Never create or cache a `FontAsset` in the window — `DennokoUIFont` handles atlas protection, fake-null detection, and domain-reload recovery. New Japanese UI strings should be appended to its `WarmupJapanese`.
- SceneView handle edits and work-mesh creation are registered with Undo; preserve that for any new interactive editing.
