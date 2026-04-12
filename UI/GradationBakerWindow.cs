using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using GradationBaker.Data;
using GradationBaker.Execute;
using GradationBaker.Localization;
using System.IO;
using System.Collections.Generic;

namespace GradationBaker.UI
{
    public class GradationBakerWindow : EditorWindow
    {
        [SerializeField]
        private GradationSettings _settings = new GradationSettings();
        private GradationBakingExecutor _baker = new GradationBakingExecutor();
        private GradationSceneHandle _sceneHandle = new GradationSceneHandle();
        private GradationPreview _preview = new GradationPreview();
        private StatusBar _statusBar = new StatusBar();

        // ReorderableList for meshes
        private ReorderableList _meshList;

        // Scroll position
        private Vector2 _scrollPosition;

        [MenuItem("dennokoworks/Gradation Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<GradationBakerWindow>();
            window.titleContent = new GUIContent("Gradation Baker");
            window.minSize = new Vector2(350, 500);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LocalizationManager.Initialize();
            SetupMeshList();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _sceneHandle.Cleanup();
            _preview.Cleanup();
            CleanupAllWorkMeshes();
        }

        private void SetupMeshList()
        {
            _meshList = new ReorderableList(_settings.MeshEntries, typeof(MeshEntry), true, false, false, false);

            _meshList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= _settings.MeshEntries.Count) return;
                var entry = _settings.MeshEntries[index];

                float removeWidth = 24f;
                float spacing = 4f;
                float statusWidth = 50f;
                float lineHeight = EditorGUIUtility.singleLineHeight;

                // --- First Line: Renderer Field ---
                Rect headerRect = new Rect(rect.x, rect.y + 2, rect.width, lineHeight);

                float fieldWidth = rect.width - removeWidth - statusWidth - spacing * 3 - 20;
                Rect foldoutRect = new Rect(rect.x, headerRect.y, 20, lineHeight);
                Rect fieldRect = new Rect(rect.x + 20, headerRect.y, fieldWidth, lineHeight);

                entry.ShowDetails = EditorGUI.Foldout(foldoutRect, entry.ShowDetails, "");

                EditorGUI.BeginChangeCheck();
                entry.SourceRenderer = (Renderer)EditorGUI.ObjectField(fieldRect, entry.SourceRenderer, typeof(Renderer), true);
                if (EditorGUI.EndChangeCheck() && entry.SourceRenderer != null)
                {
                    if (_settings.MeshEntries.Count == 1 && index == 0)
                    {
                        InitializeBoxFromRenderer(entry.SourceRenderer);
                    }
                    entry.UVChannel = _settings.UVChannel;
                    entry.MaskTexture = _settings.MaskTexture;
                    entry.UseVertexColorMask = _settings.UseVertexColorMask;
                    entry.InvertMask = _settings.InvertMask;
                }

                // Work mesh status label
                Rect statusRect = new Rect(rect.x + 20 + fieldWidth + spacing, headerRect.y, statusWidth, lineHeight);
                if (entry.HasWorkMesh)
                {
                    var captionStyle = new GUIStyle(EditorStyles.miniLabel);
                    captionStyle.normal.textColor = GradationBakerTheme.TextTertiary;
                    GUI.Label(statusRect, "[" + L("work") + "]", captionStyle);
                }

                // Remove button
                Rect removeRect = new Rect(rect.x + rect.width - removeWidth, headerRect.y, removeWidth, lineHeight);
                if (GUI.Button(removeRect, "×", EditorStyles.miniButton))
                {
                    RemoveMeshEntry(index);
                }

                // --- Detailed Settings (if expanded) ---
                if (entry.ShowDetails)
                {
                    EditorGUI.BeginChangeCheck();

                    float y = rect.y + lineHeight + 6;
                    float indent = 20f;
                    float labelW = 80f;
                    float contentW = rect.width - indent - labelW - 10;

                    var labelStyle = new GUIStyle(EditorStyles.miniLabel);
                    labelStyle.normal.textColor = GradationBakerTheme.TextSecondary;

                    // UV Channel
                    Rect uvLabelRect = new Rect(rect.x + indent, y, labelW, lineHeight);
                    Rect uvFieldRect = new Rect(rect.x + indent + labelW, y, contentW, lineHeight);
                    GUI.Label(uvLabelRect, L("uv_channel"), labelStyle);
                    string[] uvOptions = { "UV0", "UV1", "UV2", "UV3" };
                    entry.UVChannel = EditorGUI.Popup(uvFieldRect, entry.UVChannel, uvOptions);

                    y += lineHeight + 2;

                    // Mask Texture
                    Rect maskLabelRect = new Rect(rect.x + indent, y, labelW, lineHeight);
                    Rect maskFieldRect = new Rect(rect.x + indent + labelW, y, contentW, lineHeight);
                    GUI.Label(maskLabelRect, L("mask_texture"), labelStyle);
                    entry.MaskTexture = (Texture2D)EditorGUI.ObjectField(maskFieldRect, entry.MaskTexture, typeof(Texture2D), false);

                    y += lineHeight + 2;

                    // Mask Options
                    Rect optRect1 = new Rect(rect.x + indent + labelW, y, 120, lineHeight);
                    Rect optRect2 = new Rect(rect.x + indent + labelW + 120, y, 100, lineHeight);
                    entry.UseVertexColorMask = EditorGUI.ToggleLeft(optRect1, L("use_vertex_color"), entry.UseVertexColorMask);
                    entry.InvertMask = EditorGUI.ToggleLeft(optRect2, L("invert_mask"), entry.InvertMask);

                    y += lineHeight + 2;

                    // Split by Material Option
                    Rect splitRect = new Rect(rect.x + indent + labelW, y, 200, lineHeight);
                    entry.SplitByMaterial = EditorGUI.ToggleLeft(splitRect, L("split_by_material"), entry.SplitByMaterial);

                    if (EditorGUI.EndChangeCheck())
                    {
                        SceneView.RepaintAll();
                    }
                }
            };

            _meshList.elementHeightCallback = (int index) =>
            {
                if (index >= _settings.MeshEntries.Count) return EditorGUIUtility.singleLineHeight + 6;
                var entry = _settings.MeshEntries[index];
                if (entry.ShowDetails)
                {
                    return (EditorGUIUtility.singleLineHeight + 2) * 5 + 10;
                }
                return EditorGUIUtility.singleLineHeight + 6;
            };
        }

        // ─── OnGUI ───────────────────────────────────────────────────────────

        private void OnGUI()
        {
            GradationBakerTheme.Initialize();

            // ウィンドウ全面に surface.level0 を塗る
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), GradationBakerTheme.Surface0);

            DrawHeader();

            if (!_settings.IsToolActive)
            {
                GUILayout.BeginVertical(GradationBakerTheme.CardStyle);
                GUILayout.Label(L("tool_disabled"), GradationBakerTheme.SecondaryTextStyle);
                GUILayout.EndVertical();
                _statusBar.Draw();
                if (_statusBar.NeedsRepaint()) Repaint();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawMeshSection();
            DrawGradientSection();

            // メッシュがない場合はヘルプを表示して早期リターン
            if (_settings.MeshEntries.Count == 0 || _settings.GetPrimaryRenderer() == null)
            {
                DrawSection(L("help_title"), () =>
                {
                    GUILayout.Label(L("help_usage"), GradationBakerTheme.SecondaryTextStyle);
                    EditorGUILayout.Space(4);
                    GUILayout.Label(L("help_warning"), GradationBakerTheme.CaptionStyle);
                });

                EditorGUILayout.EndScrollView();
                _statusBar.Draw();
                if (_statusBar.NeedsRepaint()) Repaint();
                return;
            }

            DrawBoxControlSection();
            DrawMirrorSection();
            DrawOutputSection();
            DrawPreviewSection();

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();

            // フッター: Bake ボタン
            GUILayout.BeginVertical(GradationBakerTheme.CardStyle);
            if (GUILayout.Button("🎨 " + L("bake_and_save"), GradationBakerTheme.ActionButtonStyle))
            {
                BakeAndSave();
            }
            GUILayout.EndVertical();

            _statusBar.Draw();
            if (_statusBar.NeedsRepaint()) Repaint();
        }

        // ─── Header ──────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Gradation Baker", GradationBakerTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            _settings.IsToolActive = EditorGUILayout.ToggleLeft(
                L("enable_tool"), _settings.IsToolActive,
                GradationBakerTheme.SecondaryTextStyle, GUILayout.Width(120));
            if (LocalizationManager.DrawLanguageSelector()) Repaint();
            GUILayout.Space(6);
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
            DrawSeparator();
        }

        // ─── Sections ────────────────────────────────────────────────────────

        private void DrawMeshSection()
        {
            DrawSection(L("section_target_meshes"), () =>
            {
                // Drag & Drop エリア
                Rect dropArea = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true), GUILayout.MinWidth(100));
                EditorGUI.DrawRect(dropArea, GradationBakerTheme.Surface2);

                // ドロップエリア枠線
                var borderColor = GradationBakerTheme.Outline;
                EditorGUI.DrawRect(new Rect(dropArea.x, dropArea.y, dropArea.width, 1), borderColor);
                EditorGUI.DrawRect(new Rect(dropArea.x, dropArea.yMax - 1, dropArea.width, 1), borderColor);
                EditorGUI.DrawRect(new Rect(dropArea.x, dropArea.y, 1, dropArea.height), borderColor);
                EditorGUI.DrawRect(new Rect(dropArea.xMax - 1, dropArea.y, 1, dropArea.height), borderColor);

                var dropLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                dropLabelStyle.normal.textColor = GradationBakerTheme.TextTertiary;
                dropLabelStyle.fontStyle = FontStyle.Italic;
                GUI.Label(dropArea, L("drop_meshes_here"), dropLabelStyle);
                HandleDragAndDrop(dropArea);

                if (_settings.MeshEntries.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    _meshList.DoLayoutList();

                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    bool hasAnyWorkMesh = HasAnyWorkMesh();
                    if (GUILayout.Button(
                        hasAnyWorkMesh ? L("delete_work_mesh") : L("create_work_mesh"),
                        GradationBakerTheme.SecondaryButtonStyle))
                    {
                        ToggleAllWorkMeshes();
                    }
                    if (GUILayout.Button(L("clear_all"), GradationBakerTheme.SecondaryButtonStyle))
                    {
                        ClearAllMeshes();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            });
        }

        private void DrawGradientSection()
        {
            DrawSection(L("section_gradient"), () =>
            {
                EditorGUI.BeginChangeCheck();
                _settings.Gradient = EditorGUILayout.GradientField(L("colors"), _settings.Gradient);
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(L("save_gradient"), GradationBakerTheme.SecondaryButtonStyle))
                    SaveGradientAsTexture();
                if (GUILayout.Button(L("load_gradient"), GradationBakerTheme.SecondaryButtonStyle))
                    LoadGradientFromTexture();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawBoxControlSection()
        {
            DrawSection(L("section_box_control"), () =>
            {
                GUILayout.Label(L("box_help"), GradationBakerTheme.CaptionStyle);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();

                string[] shapeOptions = { L("shape_linear"), L("shape_spherical") };
                _settings.Shape = (GradationShape)EditorGUILayout.Popup(L("shape"), (int)_settings.Shape, shapeOptions);
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                Vector3 center = EditorGUILayout.Vector3Field(L("center"), _settings.BoxCenter);
                bool centerResetClicked = GUILayout.Button(L("reset"), GradationBakerTheme.MiniButtonStyle, GUILayout.Width(50));
                if (centerResetClicked)
                {
                    _settings.FitToAllMeshBounds();
                    SceneView.RepaintAll();
                    GUI.FocusControl(null);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                Vector3 euler = _settings.BoxRotation.eulerAngles;
                euler = EditorGUILayout.Vector3Field(L("rotation"), euler);
                bool resetClicked = GUILayout.Button(L("reset"), GradationBakerTheme.MiniButtonStyle, GUILayout.Width(50));
                if (resetClicked)
                {
                    _settings.BoxRotation = Quaternion.identity;
                    SceneView.RepaintAll();
                    GUI.FocusControl(null);
                }
                EditorGUILayout.EndHorizontal();

                float height = _settings.BoxHeight;
                Vector3 size = _settings.BoxScale;

                if (_settings.Shape == GradationShape.Linear)
                    height = EditorGUILayout.FloatField(L("height"), _settings.BoxHeight);
                else
                    size = EditorGUILayout.Vector3Field(L("size"), _settings.BoxScale);

                if (EditorGUI.EndChangeCheck())
                {
                    if (!resetClicked && !centerResetClicked)
                    {
                        _settings.BoxCenter = RoundVector3(center, 2);
                        _settings.BoxRotation = Quaternion.Euler(RoundVector3(euler, 2));

                        if (_settings.Shape == GradationShape.Linear)
                        {
                            _settings.BoxHeight = Mathf.Max(0.01f, Mathf.Round(height * 100f) / 100f);
                        }
                        else
                        {
                            _settings.BoxWidth  = Mathf.Max(0.01f, Mathf.Round(size.x * 100f) / 100f);
                            _settings.BoxHeight = Mathf.Max(0.01f, Mathf.Round(size.y * 100f) / 100f);
                            _settings.BoxDepth  = Mathf.Max(0.01f, Mathf.Round(size.z * 100f) / 100f);
                        }
                    }
                    SceneView.RepaintAll();
                }
            });
        }

        private void DrawMirrorSection()
        {
            DrawSection(L("section_mirror"), () =>
            {
                EditorGUI.BeginChangeCheck();
                _settings.UseMirror = EditorGUILayout.Toggle(L("mirror"), _settings.UseMirror);

                if (_settings.UseMirror)
                {
                    string[] axisOptions = { L("mirror_none"), "X", "Y", "Z" };
                    _settings.MirrorAxis = (MirrorAxis)EditorGUILayout.Popup(
                        L("mirror_axis"), (int)_settings.MirrorAxis, axisOptions);

                    string[] blendOptions = { L("mirror_blend_max"), L("mirror_blend_min") };
                    _settings.MirrorBlend = (MirrorBlendMode)EditorGUILayout.Popup(
                        L("mirror_blend"), (int)_settings.MirrorBlend, blendOptions);

                    EditorGUILayout.Space(2);
                    GUILayout.Label(L("mirror_help"), GradationBakerTheme.CaptionStyle);
                }

                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            });
        }

        private void DrawOutputSection()
        {
            DrawSection(L("section_output"), () =>
            {
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.55f;

                string[] resOptions = { "128", "256", "512", "1024", "2048", "4096" };
                int[] resValues = { 128, 256, 512, 1024, 2048, 4096 };
                int currentResIndex = System.Array.IndexOf(resValues, _settings.Resolution);
                if (currentResIndex < 0) currentResIndex = 4;
                int newResIndex = EditorGUILayout.Popup(L("resolution"), currentResIndex, resOptions);
                _settings.Resolution = resValues[newResIndex];

                _settings.UseTextureFolder = EditorGUILayout.Toggle(L("use_texture_folder"), _settings.UseTextureFolder);

                if (!_settings.UseTextureFolder)
                {
                    EditorGUILayout.BeginHorizontal();
                    _settings.SavePath = EditorGUILayout.TextField(L("save_path"), _settings.SavePath);
                    if (GUILayout.Button("...", GUILayout.Width(30)))
                    {
                        string path = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (path.StartsWith(Application.dataPath))
                                _settings.SavePath = "Assets" + path.Substring(Application.dataPath.Length);
                            else
                                _settings.SavePath = path;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                string[] bgOptions = { L("bg_transparent"), L("bg_white"), L("bg_black") };
                _settings.BgColor = (BackgroundColor)EditorGUILayout.Popup(
                    L("bg_color"), (int)_settings.BgColor, bgOptions);

                _settings.EdgePaddingPixels = EditorGUILayout.IntSlider(
                    L("edge_padding"), _settings.EdgePaddingPixels, 0, 16);
                if (_settings.EdgePaddingPixels > 0)
                {
                    EditorGUILayout.Space(2);
                    GUILayout.Label(L("edge_padding_help"), GradationBakerTheme.CaptionStyle);
                }

                EditorGUIUtility.labelWidth = originalLabelWidth;
            });
        }

        private void DrawPreviewSection()
        {
            DrawSection(L("section_preview"), () =>
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(L("blend_mode"), GradationBakerTheme.SecondaryTextStyle, GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                _settings.BlendMode = (PreviewBlendMode)EditorGUILayout.EnumPopup(_settings.BlendMode);
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
                EditorGUILayout.EndHorizontal();
            });
        }

        // ─── Section Helpers ─────────────────────────────────────────────────

        private void DrawSection(string title, System.Action content)
        {
            GUILayout.BeginVertical(GradationBakerTheme.CardStyle);
            GUILayout.Label(title, GradationBakerTheme.SectionHeaderStyle);
            DrawSeparator();
            content?.Invoke();
            GUILayout.EndVertical();
        }

        private void DrawToggleSection(string title, ref bool toggle, System.Action content, System.Action onReset = null)
        {
            GUILayout.BeginVertical(GradationBakerTheme.CardStyle);

            GUILayout.BeginHorizontal();
            var headerStyle = toggle ? GradationBakerTheme.ToggleSectionOnStyle : GradationBakerTheme.ToggleSectionOffStyle;
            EditorGUI.BeginChangeCheck();
            bool newToggle = EditorGUILayout.ToggleLeft(title, toggle, headerStyle, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                toggle = newToggle;
                Repaint();
            }
            if (onReset != null && GUILayout.Button("Reset", GradationBakerTheme.MiniButtonStyle, GUILayout.Width(50)))
            {
                onReset.Invoke();
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            DrawSeparator();

            using (new EditorGUI.DisabledGroupScope(!toggle))
            {
                content?.Invoke();
            }

            GUILayout.EndVertical();
        }

        private void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, GradationBakerTheme.Outline);
            EditorGUILayout.Space(4);
        }

        // ─── Drag & Drop ─────────────────────────────────────────────────────

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    bool hasValidObject = false;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go && (go.GetComponent<Renderer>() != null))
                        {
                            hasValidObject = true;
                            break;
                        }
                        if (obj is Renderer)
                        {
                            hasValidObject = true;
                            break;
                        }
                    }

                    if (!hasValidObject)
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        bool isFirstMesh = _settings.MeshEntries.Count == 0;

                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            Renderer renderer = null;

                            if (obj is GameObject go)
                                renderer = go.GetComponent<Renderer>();
                            else if (obj is Renderer r)
                                renderer = r;

                            if (renderer != null)
                            {
                                bool alreadyExists = false;
                                foreach (var entry in _settings.MeshEntries)
                                {
                                    if (entry.SourceRenderer == renderer)
                                    {
                                        alreadyExists = true;
                                        break;
                                    }
                                }

                                if (!alreadyExists)
                                    _settings.MeshEntries.Add(new MeshEntry { SourceRenderer = renderer });
                            }
                        }

                        if (isFirstMesh && _settings.MeshEntries.Count > 0)
                            InitializeBoxFromRenderer(_settings.MeshEntries[0].SourceRenderer);

                        SetupMeshList();
                        SceneView.RepaintAll();
                    }

                    evt.Use();
                    break;
            }
        }

        // ─── Scene GUI ───────────────────────────────────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_settings.IsToolActive || _settings.MeshEntries.Count == 0)
            {
                _preview.ClearProxies();
                return;
            }

            _preview.UpdatePreviewAll(_settings);

            HandleChangeType changeType = _sceneHandle.DrawHandle(_settings, this);

            if (_settings.UseMirror && _settings.MirrorAxis != MirrorAxis.None)
                _sceneHandle.DrawMirrorHandle(_settings);

            if (changeType != HandleChangeType.None)
                Repaint();
        }

        // ─── Bake ────────────────────────────────────────────────────────────

        private void BakeAndSave()
        {
            FileLogger.Clear();
            FileLogger.Log("[GradationBakerWindow] Bake & Save clicked.");

            var results = _baker.BakeAll(_settings);
            int savedCount = 0;
            List<string> savedPaths = new List<string>();

            foreach (var result in results)
            {
                if (result.Texture == null && (result.SubMeshResults == null || result.SubMeshResults.Count == 0)) continue;

                string outputFolder = OutputPathResolver.ResolveOutputFolder(
                    result.SourceRenderer,
                    _settings.SavePath,
                    _settings.UseTextureFolder
                );

                OutputPathResolver.EnsureDirectoryExists(outputFolder);

                string fullDirPath = OutputPathResolver.ToFullPath(outputFolder);

                if (result.SubMeshResults != null && result.SubMeshResults.Count > 0)
                {
                    foreach (var subRes in result.SubMeshResults)
                    {
                        if (subRes.Texture == null) continue;

                        string safeMatName = string.Join("_", subRes.MaterialName.Split(Path.GetInvalidFileNameChars()));
                        string baseName = $"{result.RendererName}_{safeMatName}";

                        string fileName = OutputPathResolver.GenerateUniqueFilename(fullDirPath, baseName);
                        string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');

                        SaveTexture(subRes.Texture, fullPath);

                        FileLogger.Log($"[GradationBakerWindow] Saved: {fullPath}");
                        savedPaths.Add(fullPath);
                        savedCount++;
                    }
                }
                else if (result.Texture != null)
                {
                    string fileName = OutputPathResolver.GenerateUniqueFilename(fullDirPath, result.RendererName);
                    string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');

                    SaveTexture(result.Texture, fullPath);

                    FileLogger.Log($"[GradationBakerWindow] Saved: {fullPath}");
                    savedPaths.Add(fullPath);
                    savedCount++;
                }
            }

            AssetDatabase.Refresh();

            if (_settings.BgColor == BackgroundColor.Transparent)
            {
                foreach (string fullPath in savedPaths)
                {
                    string assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length);
                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                    }
                }
            }

            if (savedCount > 0)
            {
                string message = L("status_bake_success", savedCount);
                _statusBar.Show(message, StatusBar.StatusType.Success);

                if (savedPaths.Count > 0)
                {
                    string firstPath = savedPaths[0];
                    string assetPath = "Assets" + firstPath.Substring(Application.dataPath.Length);

                    Object textureAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (textureAsset != null)
                    {
                        EditorGUIUtility.PingObject(textureAsset);
                        Selection.activeObject = textureAsset;
                    }
                }
            }
            else
            {
                _statusBar.Show(L("status_bake_error"), StatusBar.StatusType.Error, 0);
            }
        }

        // ─── Mesh Helpers ────────────────────────────────────────────────────

        private void InitializeBoxFromRenderer(Renderer renderer)
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh != null)
            {
                MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);
                _settings.FitToAllMeshBounds();
                _settings.Resolution = TextureResolutionResolver.ResolveDefaultResolution(renderer);
                SceneView.RepaintAll();
            }
        }

        private bool HasAnyWorkMesh()
        {
            foreach (var entry in _settings.MeshEntries)
            {
                if (entry.HasWorkMesh) return true;
            }
            return false;
        }

        private void ToggleAllWorkMeshes()
        {
            bool hasAny = HasAnyWorkMesh();

            foreach (var entry in _settings.MeshEntries)
            {
                if (hasAny)
                {
                    if (entry.HasWorkMesh)
                    {
                        WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
                        entry.WorkMeshObject = null;
                    }
                }
                else
                {
                    if (!entry.HasWorkMesh && entry.SourceRenderer != null)
                        entry.WorkMeshObject = WorkMeshManager.CreateWorkMesh(entry.SourceRenderer);
                }
            }

            _settings.FitToAllMeshBounds();
            SceneView.RepaintAll();
        }

        private void ToggleWorkMesh(MeshEntry entry)
        {
            if (entry.HasWorkMesh)
            {
                WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
                entry.WorkMeshObject = null;
            }
            else if (entry.SourceRenderer != null)
            {
                entry.WorkMeshObject = WorkMeshManager.CreateWorkMesh(entry.SourceRenderer);
            }
            SceneView.RepaintAll();
        }

        private void RemoveMeshEntry(int index)
        {
            if (index < 0 || index >= _settings.MeshEntries.Count) return;

            var entry = _settings.MeshEntries[index];
            if (entry.HasWorkMesh)
                WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);

            _settings.MeshEntries.RemoveAt(index);
            SetupMeshList();
            SceneView.RepaintAll();
        }

        private void ClearAllMeshes()
        {
            CleanupAllWorkMeshes();
            _settings.MeshEntries.Clear();
            SetupMeshList();
            SceneView.RepaintAll();
        }

        private void CleanupAllWorkMeshes()
        {
            foreach (var entry in _settings.MeshEntries)
            {
                if (entry.HasWorkMesh)
                {
                    WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
                    entry.WorkMeshObject = null;
                }
            }
        }

        private Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (renderer is MeshRenderer mr)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                return mf ? mf.sharedMesh : null;
            }
            return null;
        }

        // ─── Gradient Save / Load ────────────────────────────────────────────

        private void SaveGradientAsTexture()
        {
            string gradientDir = "Assets/GeneratedGradation/gradation";
            if (!AssetDatabase.IsValidFolder(gradientDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeneratedGradation"))
                    AssetDatabase.CreateFolder("Assets", "GeneratedGradation");
                AssetDatabase.CreateFolder("Assets/GeneratedGradation", "gradation");
            }

            string fullDirPath = OutputPathResolver.ToFullPath(gradientDir);
            string fileName = GenerateUniqueGradientFilename(fullDirPath);
            string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');

            Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                tex.SetPixel(i, 0, _settings.Gradient.Evaluate(t));
            }
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.Refresh();

            string assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Object savedAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (savedAsset != null)
            {
                EditorGUIUtility.PingObject(savedAsset);
                Selection.activeObject = savedAsset;
            }

            FileLogger.Log($"[GradationBakerWindow] Gradient saved to: {fullPath}");
            _statusBar.Show(L("status_gradient_saved"), StatusBar.StatusType.Success);
        }

        private string GenerateUniqueGradientFilename(string folderPath)
        {
            string baseName = "gradient";
            string extension = ".png";

            string fullPath = Path.Combine(folderPath, baseName + extension).Replace('\\', '/');
            if (!File.Exists(fullPath))
                return baseName + extension;

            int counter = 1;
            while (true)
            {
                string numberedName = $"{baseName} {counter}{extension}";
                fullPath = Path.Combine(folderPath, numberedName).Replace('\\', '/');

                if (!File.Exists(fullPath))
                    return numberedName;

                counter++;
                if (counter > 9999)
                    return $"{baseName}_{System.DateTime.Now:yyyyMMddHHmmss}{extension}";
            }
        }

        private void LoadGradientFromTexture()
        {
            string gradientDir = Path.Combine(Application.dataPath, "GeneratedGradation/gradation").Replace('\\', '/');
            if (!Directory.Exists(gradientDir))
                gradientDir = Application.dataPath;

            string path = EditorUtility.OpenFilePanel("Load Gradient", gradientDir, "png");

            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
            }

            if (tex == null || tex.width < 2) return;

            Gradient gradient = new Gradient();
            int numKeys = Mathf.Min(8, tex.width);
            GradientColorKey[] colorKeys = new GradientColorKey[numKeys];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[numKeys];

            for (int i = 0; i < numKeys; i++)
            {
                float t = i / (float)(numKeys - 1);
                int x = Mathf.RoundToInt(t * (tex.width - 1));
                Color c = tex.GetPixel(x, 0);

                colorKeys[i] = new GradientColorKey(c, t);
                alphaKeys[i] = new GradientAlphaKey(c.a, t);
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            _settings.Gradient = gradient;

            SceneView.RepaintAll();
            FileLogger.Log($"[GradationBakerWindow] Gradient loaded from: {path}");
        }

        // ─── Utilities ───────────────────────────────────────────────────────

        private Vector3 RoundVector3(Vector3 v, int decimals)
        {
            float multiplier = Mathf.Pow(10f, decimals);
            return new Vector3(
                Mathf.Round(v.x * multiplier) / multiplier,
                Mathf.Round(v.y * multiplier) / multiplier,
                Mathf.Round(v.z * multiplier) / multiplier
            );
        }

        private string L(string key) => LocalizationManager.Get(key);
        private string L(string key, params object[] args) => LocalizationManager.Get(key, args);

        private void SaveTexture(Texture2D tex, string path)
        {
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);
        }
    }
}
