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
        private GradationSettings _settings = new GradationSettings();
        private GradationBakingExecutor _baker = new GradationBakingExecutor();
        private GradationSceneHandle _sceneHandle = new GradationSceneHandle();
        private GradationPreview _preview = new GradationPreview();
        private StatusBar _statusBar = new StatusBar();
        
        // ReorderableList for meshes
        private ReorderableList _meshList;
        
        // Foldout states
        private bool _meshFoldout = true;
        private bool _gradientFoldout = true;
        private bool _boxSettingsFoldout = true;
        private bool _mirrorFoldout = false;
        private bool _helpFoldout = false;
        private bool _outputFoldout = true;
        
        // Scroll position
        private Vector2 _scrollPosition;
        
        // Styles
        private GUIStyle _dropAreaStyle;
        private GUIStyle _sectionStyle;
        private bool _stylesInitialized = false;
        
        [MenuItem("Tools/Gradation Baker")]
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

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _dropAreaStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Italic
            };
            
            _sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };
            
            _stylesInitialized = true;
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
                
                // Renderer field
                float fieldWidth = rect.width - removeWidth - statusWidth - spacing * 3 - 20; // 20 for foldout
                Rect foldoutRect = new Rect(rect.x, headerRect.y, 20, lineHeight);
                Rect fieldRect = new Rect(rect.x + 20, headerRect.y, fieldWidth, lineHeight);
                
                entry.ShowDetails = EditorGUI.Foldout(foldoutRect, entry.ShowDetails, "");
                
                EditorGUI.BeginChangeCheck();
                entry.SourceRenderer = (Renderer)EditorGUI.ObjectField(fieldRect, entry.SourceRenderer, typeof(Renderer), true);
                if (EditorGUI.EndChangeCheck() && entry.SourceRenderer != null)
                {
                    // Initialize box on first mesh add
                    if (_settings.MeshEntries.Count == 1 && index == 0)
                    {
                        InitializeBoxFromRenderer(entry.SourceRenderer);
                    }
                    // Initialize default settings from global settings
                    entry.UVChannel = _settings.UVChannel;
                    entry.MaskTexture = _settings.MaskTexture;
                    entry.UseVertexColorMask = _settings.UseVertexColorMask;
                    entry.InvertMask = _settings.InvertMask;
                }
                
                // Work mesh status label
                Rect statusRect = new Rect(rect.x + 20 + fieldWidth + spacing, headerRect.y, statusWidth, lineHeight);
                if (entry.HasWorkMesh)
                {
                    GUI.Label(statusRect, "[" + L("work") + "]");
                }
                
                // Remove button
                Rect removeRect = new Rect(rect.x + rect.width - removeWidth, headerRect.y, removeWidth, lineHeight);
                if (GUI.Button(removeRect, "×"))
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
                    
                    // UV Channel
                    Rect uvLabelRect = new Rect(rect.x + indent, y, labelW, lineHeight);
                    Rect uvFieldRect = new Rect(rect.x + indent + labelW, y, contentW, lineHeight);
                    GUI.Label(uvLabelRect, L("uv_channel"));
                    string[] uvOptions = { "UV0", "UV1", "UV2", "UV3" };
                    entry.UVChannel = EditorGUI.Popup(uvFieldRect, entry.UVChannel, uvOptions);
                    
                    y += lineHeight + 2;
                    
                    // Mask Texture
                    Rect maskLabelRect = new Rect(rect.x + indent, y, labelW, lineHeight);
                    Rect maskFieldRect = new Rect(rect.x + indent + labelW, y, contentW, lineHeight);
                    GUI.Label(maskLabelRect, L("mask_texture"));
                    entry.MaskTexture = (Texture2D)EditorGUI.ObjectField(maskFieldRect, entry.MaskTexture, typeof(Texture2D), false);
                    
                    y += lineHeight + 2;
                    
                    // Mask Options
                    Rect optRect1 = new Rect(rect.x + indent + labelW, y, 120, lineHeight);
                    Rect optRect2 = new Rect(rect.x + indent + labelW + 120, y, 100, lineHeight);
                    entry.UseVertexColorMask = EditorGUI.ToggleLeft(optRect1, L("use_vertex_color"), entry.UseVertexColorMask);
                    entry.InvertMask = EditorGUI.ToggleLeft(optRect2, L("invert_mask"), entry.InvertMask);
                    
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
                    return (EditorGUIUtility.singleLineHeight + 2) * 4 + 10; // Header + 3 rows + padding
                }
                return EditorGUIUtility.singleLineHeight + 6;
            };
        }

        private void OnGUI()
        {
            InitStyles();
            
            // Header with language selector
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _settings.IsToolActive = EditorGUILayout.ToggleLeft(L("enable_tool"), _settings.IsToolActive, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            if (LocalizationManager.DrawLanguageSelector())
            {
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            if (!_settings.IsToolActive)
            {
                EditorGUILayout.HelpBox(L("tool_disabled"), MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(5);
            
            // Target Meshes Section with Drag & Drop
            _meshFoldout = EditorGUILayout.Foldout(_meshFoldout, L("target_meshes"), true, EditorStyles.foldoutHeader);
            if (_meshFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle, GUILayout.ExpandWidth(true));
                
                // Drag & Drop Area
                Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true), GUILayout.MinWidth(100));
                GUI.Box(dropArea, L("drop_meshes_here"), _dropAreaStyle);
                HandleDragAndDrop(dropArea);
                
                if (_settings.MeshEntries.Count > 0)
                {
                    EditorGUILayout.Space(3);
                    _meshList.DoLayoutList();
                    
                    // Work mesh buttons (all at once)
                    EditorGUILayout.BeginHorizontal();
                    bool hasAnyWorkMesh = HasAnyWorkMesh();
                    if (GUILayout.Button(hasAnyWorkMesh ? L("delete_work_mesh") : L("create_work_mesh")))
                    {
                        ToggleAllWorkMeshes();
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    if (GUILayout.Button(L("clear_all")))
                    {
                        ClearAllMeshes();
                    }
                }
                
                EditorGUILayout.EndVertical();
            }

            if (_settings.MeshEntries.Count == 0 || _settings.GetPrimaryRenderer() == null)
            {
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(L("help_title"), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(L("help_usage"), MessageType.Info);
                EditorGUILayout.HelpBox(L("help_warning"), MessageType.Warning);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
                _statusBar.Draw();
                if (_statusBar.NeedsRepaint()) Repaint();
                return;
            }

            EditorGUILayout.Space(5);
            
            // Gradient Section
            _gradientFoldout = EditorGUILayout.Foldout(_gradientFoldout, L("gradient"), true, EditorStyles.foldoutHeader);
            if (_gradientFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle, GUILayout.ExpandWidth(true));
                EditorGUI.BeginChangeCheck();
                _settings.Gradient = EditorGUILayout.GradientField(L("colors"), _settings.Gradient);
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
                
                // Gradient save/load buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(L("save_gradient")))
                {
                    SaveGradientAsTexture();
                }
                if (GUILayout.Button(L("load_gradient")))
                {
                    LoadGradientFromTexture();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            
            // Box Control Section
            _boxSettingsFoldout = EditorGUILayout.Foldout(_boxSettingsFoldout, L("box_control"), true, EditorStyles.foldoutHeader);
            if (_boxSettingsFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle, GUILayout.ExpandWidth(true));
                
                if (GUILayout.Button(L("fit_to_bounds")))
                {
                    _settings.FitToAllMeshBounds();
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(L("box_help"), MessageType.Info);
                EditorGUILayout.Space(3);
                
                EditorGUI.BeginChangeCheck();
                Vector3 center = EditorGUILayout.Vector3Field(L("center"), _settings.BoxCenter);
                
                EditorGUILayout.BeginHorizontal();
                Vector3 euler = _settings.BoxRotation.eulerAngles;
                euler = EditorGUILayout.Vector3Field(L("rotation"), euler);
                
                bool resetClicked = GUILayout.Button(L("reset"), GUILayout.Width(50));
                if (resetClicked)
                {
                    _settings.BoxRotation = Quaternion.identity;
                    SceneView.RepaintAll();
                    GUI.FocusControl(null); // Clear focus to update fields
                }
                EditorGUILayout.EndHorizontal();
                
                if (resetClicked) return; // Skip updating from fields this frame to prevent overwriting with old values

                float height = EditorGUILayout.FloatField(L("height"), _settings.BoxHeight);
                
                if (EditorGUI.EndChangeCheck())
                {
                    _settings.BoxCenter = RoundVector3(center, 2);
                    _settings.BoxRotation = Quaternion.Euler(RoundVector3(euler, 2));
                    _settings.BoxHeight = Mathf.Max(0.01f, Mathf.Round(height * 100f) / 100f);
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            
            // Mirror Section
            _mirrorFoldout = EditorGUILayout.Foldout(_mirrorFoldout, L("mirror"), true, EditorStyles.foldoutHeader);
            if (_mirrorFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle, GUILayout.ExpandWidth(true));
                
                EditorGUI.BeginChangeCheck();
                _settings.UseMirror = EditorGUILayout.Toggle(L("mirror"), _settings.UseMirror);
                
                if (_settings.UseMirror)
                {
                    string[] axisOptions = { L("mirror_none"), "X", "Y", "Z" };
                    _settings.MirrorAxis = (MirrorAxis)EditorGUILayout.Popup(L("mirror_axis"), (int)_settings.MirrorAxis, axisOptions);
                    EditorGUILayout.HelpBox(L("mirror_help"), MessageType.Info);
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);



            EditorGUILayout.Space(5);

            // Output Settings Section
            _outputFoldout = EditorGUILayout.Foldout(_outputFoldout, L("output_settings"), true, EditorStyles.foldoutHeader);
            if (_outputFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle, GUILayout.ExpandWidth(true));
                
                // Set label width for 2:1 ratio
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.55f;
                
                // Resolution (dropdown)
                string[] resOptions = { "128", "256", "512", "1024", "2048", "4096" };
                int[] resValues = { 128, 256, 512, 1024, 2048, 4096 };
                int currentResIndex = System.Array.IndexOf(resValues, _settings.Resolution);
                if (currentResIndex < 0) currentResIndex = 4; // Default to 2048
                int newResIndex = EditorGUILayout.Popup(L("resolution"), currentResIndex, resOptions);
                _settings.Resolution = resValues[newResIndex];
                
                // Use Texture Folder option
                _settings.UseTextureFolder = EditorGUILayout.Toggle(L("use_texture_folder"), _settings.UseTextureFolder);
                
                // Save Path (only show if not using texture folder)
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
                            {
                                _settings.SavePath = "Assets" + path.Substring(Application.dataPath.Length);
                            }
                            else
                            {
                                _settings.SavePath = path;
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                // Background Color
                string[] bgOptions = { L("bg_transparent"), L("bg_white"), L("bg_black") };
                _settings.BgColor = (BackgroundColor)EditorGUILayout.Popup(L("bg_color"), (int)_settings.BgColor, bgOptions);
                
                // Restore label width
                EditorGUIUtility.labelWidth = originalLabelWidth;
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            
            // Opacity slider (preview is always on)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L("opacity"), GUILayout.Width(60));
            _settings.PreviewOpacity = EditorGUILayout.Slider(_settings.PreviewOpacity, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            if (GUI.changed) SceneView.RepaintAll();

            EditorGUILayout.Space(10);
            
            // Bake button
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("🎨 " + L("bake_and_save"), GUILayout.Height(45)))
            {
                BakeAndSave();
            }
            GUI.backgroundColor = Color.white;
            
            // Help Section (Collapsed by default)
            EditorGUILayout.Space(10);
            _helpFoldout = EditorGUILayout.Foldout(_helpFoldout, L("help_title"), true, EditorStyles.foldoutHeader);
            if (_helpFoldout)
            {
                EditorGUILayout.HelpBox(L("help_usage"), MessageType.Info);
                EditorGUILayout.HelpBox(L("help_warning"), MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
            
            // Status bar at bottom
            _statusBar.Draw();
            
            // Request repaint for auto-clear
            if (_statusBar.NeedsRepaint())
            {
                Repaint();
            }
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;
                    
                    // Check if any dragged objects are valid
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
                            {
                                renderer = go.GetComponent<Renderer>();
                            }
                            else if (obj is Renderer r)
                            {
                                renderer = r;
                            }
                            
                            if (renderer != null)
                            {
                                // Check if already added
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
                                {
                                    _settings.MeshEntries.Add(new MeshEntry { SourceRenderer = renderer });
                                }
                            }
                        }
                        
                        // Initialize box if first mesh added
                        if (isFirstMesh && _settings.MeshEntries.Count > 0)
                        {
                            InitializeBoxFromRenderer(_settings.MeshEntries[0].SourceRenderer);
                        }
                        
                        SetupMeshList();
                        SceneView.RepaintAll();
                    }
                    
                    evt.Use();
                    break;
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_settings.IsToolActive) return;
            if (_settings.MeshEntries.Count == 0) return;

            // Preview all meshes (always on)
            _preview.UpdatePreviewAll(_settings);

            // Main Handle
            HandleChangeType changeType = _sceneHandle.DrawHandle(_settings, null);
            
            // Mirror handle visualization
            if (_settings.UseMirror && _settings.MirrorAxis != MirrorAxis.None)
            {
                _sceneHandle.DrawMirrorHandle(_settings);
            }
            
            if (changeType != HandleChangeType.None)
            {
                Repaint();
            }
        }

        private void BakeAndSave()
        {
            FileLogger.Clear();
            FileLogger.Log("[GradationBakerWindow] Bake & Save clicked.");
            
            var results = _baker.BakeAll(_settings);
            int savedCount = 0;
            List<string> savedPaths = new List<string>();
            
            foreach (var result in results)
            {
                if (result.Texture == null) continue;
                
                // Resolve output folder
                string outputFolder = OutputPathResolver.ResolveOutputFolder(
                    result.SourceRenderer, 
                    _settings.SavePath, 
                    _settings.UseTextureFolder
                );
                
                // Ensure directory exists
                OutputPathResolver.EnsureDirectoryExists(outputFolder);
                
                // Generate unique filename
                string fullDirPath = OutputPathResolver.ToFullPath(outputFolder);
                string fileName = OutputPathResolver.GenerateUniqueFilename(fullDirPath, result.RendererName);
                string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');
                
                // Save
                byte[] bytes = result.Texture.EncodeToPNG();
                File.WriteAllBytes(fullPath, bytes);
                Object.DestroyImmediate(result.Texture);
                
                FileLogger.Log($"[GradationBakerWindow] Saved: {fullPath}");
                savedPaths.Add(fullPath);
                savedCount++;
            }
            
            AssetDatabase.Refresh();
            
            // Set texture importer settings for transparent textures
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
                
                // Open the first saved texture in Project tab
                if (savedPaths.Count > 0)
                {
                    string firstPath = savedPaths[0];
                    string assetPath = "Assets" + firstPath.Substring(Application.dataPath.Length);
                    
                    // Ping the texture to reveal it in Project tab
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
                    // Delete all work meshes
                    if (entry.HasWorkMesh)
                    {
                        WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
                        entry.WorkMeshObject = null;
                    }
                }
                else
                {
                    // Create all work meshes
                    if (!entry.HasWorkMesh && entry.SourceRenderer != null)
                    {
                        entry.WorkMeshObject = WorkMeshManager.CreateWorkMesh(entry.SourceRenderer);
                    }
                }
            }
            
            // Fit to mesh bounds after creating or deleting work meshes
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
            {
                WorkMeshManager.DeleteWorkMesh(entry.WorkMeshObject);
            }
            
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

        private void SaveGradientAsTexture()
        {
            // Ensure gradient directory exists
            string gradientDir = "Assets/GeneratedGradation/gradation";
            if (!AssetDatabase.IsValidFolder(gradientDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeneratedGradation"))
                {
                    AssetDatabase.CreateFolder("Assets", "GeneratedGradation");
                }
                AssetDatabase.CreateFolder("Assets/GeneratedGradation", "gradation");
            }
            
            // Generate unique filename
            string fullDirPath = OutputPathResolver.ToFullPath(gradientDir);
            string fileName = GenerateUniqueGradientFilename(fullDirPath);
            string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');
            
            // Create gradient texture (256x1)
            Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                tex.SetPixel(i, 0, _settings.Gradient.Evaluate(t));
            }
            tex.Apply();
            
            // Save as PNG
            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            
            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.Refresh();
            
            // Enable Read/Write on the saved texture
            string assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            
            // Highlight saved file in Project tab
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
            
            // Check if base name exists
            string fullPath = Path.Combine(folderPath, baseName + extension).Replace('\\', '/');
            if (!File.Exists(fullPath))
            {
                return baseName + extension;
            }
            
            // Find next available number
            int counter = 1;
            while (true)
            {
                string numberedName = $"{baseName} {counter}{extension}";
                fullPath = Path.Combine(folderPath, numberedName).Replace('\\', '/');
                
                if (!File.Exists(fullPath))
                {
                    return numberedName;
                }
                
                counter++;
                
                if (counter > 9999)
                {
                    return $"{baseName}_{System.DateTime.Now:yyyyMMddHHmmss}{extension}";
                }
            }
        }

        private void LoadGradientFromTexture()
        {
            // Default to gradient directory
            string gradientDir = Path.Combine(Application.dataPath, "GeneratedGradation/gradation").Replace('\\', '/');
            if (!Directory.Exists(gradientDir))
            {
                gradientDir = Application.dataPath;
            }
            
            string path = EditorUtility.OpenFilePanel(
                "Load Gradient",
                gradientDir,
                "png"
            );
            
            if (string.IsNullOrEmpty(path)) return;
            
            // Convert to relative path if inside Assets
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }
            
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                // Try loading from file directly
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
            }
            
            if (tex == null || tex.width < 2) return;
            
            // Create gradient from texture
            Gradient gradient = new Gradient();
            
            // Sample colors at key points
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

        /// <summary>
        /// Rounds a Vector3 to the specified number of decimal places
        /// </summary>
        private Vector3 RoundVector3(Vector3 v, int decimals)
        {
            float multiplier = Mathf.Pow(10f, decimals);
            return new Vector3(
                Mathf.Round(v.x * multiplier) / multiplier,
                Mathf.Round(v.y * multiplier) / multiplier,
                Mathf.Round(v.z * multiplier) / multiplier
            );
        }

        /// <summary>
        /// Shorthand for localization
        /// </summary>
        private string L(string key) => LocalizationManager.Get(key);
        private string L(string key, params object[] args) => LocalizationManager.Get(key, args);
    }
}
