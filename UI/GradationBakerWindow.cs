using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using GradationTextureGenerator.Data;
using GradationTextureGenerator.Execute;
using GradationTextureGenerator.Localization;
using System.IO;
using System.Collections.Generic;

namespace GradationTextureGenerator.UI
{
    public class GradationBakerWindow : EditorWindow
    {
        private GradationSettings _settings = new GradationSettings();
        private GradationBaker _baker = new GradationBaker();
        private GradationSceneHandle _sceneHandle = new GradationSceneHandle();
        private GradationPreview _preview = new GradationPreview();
        
        // ReorderableList for meshes
        private ReorderableList _meshList;
        
        // Foldout states
        private bool _meshFoldout = true;
        private bool _gradientFoldout = true;
        private bool _boxSettingsFoldout = true;
        private bool _mirrorFoldout = false;
        private bool _maskingFoldout = false;
        private bool _outputFoldout = true;
        
        // Styles
        private GUIStyle _dropAreaStyle;
        private GUIStyle _sectionStyle;
        private bool _stylesInitialized = false;
        
        [MenuItem("Tools/Gradation Texture Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<GradationBakerWindow>();
            window.titleContent = new GUIContent("Gradation Gen");
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
                
                // Renderer field
                float fieldWidth = rect.width - removeWidth - statusWidth - spacing * 2;
                Rect fieldRect = new Rect(rect.x, rect.y + 2, fieldWidth, EditorGUIUtility.singleLineHeight);
                
                EditorGUI.BeginChangeCheck();
                entry.SourceRenderer = (Renderer)EditorGUI.ObjectField(fieldRect, entry.SourceRenderer, typeof(Renderer), true);
                if (EditorGUI.EndChangeCheck() && entry.SourceRenderer != null)
                {
                    // Initialize box on first mesh add
                    if (_settings.MeshEntries.Count == 1 && index == 0)
                    {
                        InitializeBoxFromRenderer(entry.SourceRenderer);
                    }
                }
                
                // Work mesh status label
                Rect statusRect = new Rect(rect.x + fieldWidth + spacing, rect.y + 2, statusWidth, EditorGUIUtility.singleLineHeight);
                if (entry.HasWorkMesh)
                {
                    GUI.Label(statusRect, "[" + L("work") + "]");
                }
                
                // Remove button
                Rect removeRect = new Rect(rect.x + fieldWidth + statusWidth + spacing * 2, rect.y + 2, removeWidth, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(removeRect, "×"))
                {
                    RemoveMeshEntry(index);
                }
            };
            
            _meshList.elementHeight = EditorGUIUtility.singleLineHeight + 6;
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

            EditorGUILayout.Space(5);
            
            // Target Meshes Section with Drag & Drop
            _meshFoldout = EditorGUILayout.Foldout(_meshFoldout, L("target_meshes"), true, EditorStyles.foldoutHeader);
            if (_meshFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                
                // Drag & Drop Area
                Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
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
                return;
            }

            EditorGUILayout.Space(5);
            
            // Gradient Section
            _gradientFoldout = EditorGUILayout.Foldout(_gradientFoldout, L("gradient"), true, EditorStyles.foldoutHeader);
            if (_gradientFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                EditorGUI.BeginChangeCheck();
                _settings.Gradient = EditorGUILayout.GradientField(L("colors"), _settings.Gradient);
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            
            // Box Control Section
            _boxSettingsFoldout = EditorGUILayout.Foldout(_boxSettingsFoldout, L("box_control"), true, EditorStyles.foldoutHeader);
            if (_boxSettingsFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                
                if (GUILayout.Button(L("fit_to_bounds")))
                {
                    _settings.FitToAllMeshBounds();
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(L("box_help"), MessageType.Info);
                EditorGUILayout.Space(3);
                
                EditorGUI.BeginChangeCheck();
                _settings.BoxCenter = EditorGUILayout.Vector3Field(L("center"), _settings.BoxCenter);
                
                Vector3 euler = _settings.BoxRotation.eulerAngles;
                euler = EditorGUILayout.Vector3Field(L("rotation"), euler);
                _settings.BoxRotation = Quaternion.Euler(euler);
                
                _settings.BoxHeight = EditorGUILayout.FloatField(L("height"), _settings.BoxHeight);
                _settings.BoxHeight = Mathf.Max(0.01f, _settings.BoxHeight);
                
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            
            // Mirror Section
            _mirrorFoldout = EditorGUILayout.Foldout(_mirrorFoldout, L("mirror"), true, EditorStyles.foldoutHeader);
            if (_mirrorFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                
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

            // Masking Section
            _maskingFoldout = EditorGUILayout.Foldout(_maskingFoldout, L("masking"), true, EditorStyles.foldoutHeader);
            if (_maskingFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                _settings.MaskTexture = (Texture2D)EditorGUILayout.ObjectField(L("mask_texture"), _settings.MaskTexture, typeof(Texture2D), false);
                _settings.UseVertexColorMask = EditorGUILayout.Toggle(L("use_vertex_color"), _settings.UseVertexColorMask);
                _settings.InvertMask = EditorGUILayout.Toggle(L("invert_mask"), _settings.InvertMask);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);

            // Output Settings Section
            _outputFoldout = EditorGUILayout.Foldout(_outputFoldout, L("output_settings"), true, EditorStyles.foldoutHeader);
            if (_outputFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                
                // Set label width for 2:1 ratio
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.55f;
                
                // UV Channel
                string[] uvOptions = { "UV0", "UV1", "UV2", "UV3" };
                _settings.UVChannel = EditorGUILayout.Popup(L("uv_channel"), _settings.UVChannel, uvOptions);
                
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
            
            if (savedCount > 0)
            {
                string message = string.Join("\n", savedPaths);
                EditorUtility.DisplayDialog(L("success_title"), L("success_message", message), "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(L("error_title"), L("error_message"), "OK");
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

        /// <summary>
        /// Shorthand for localization
        /// </summary>
        private string L(string key) => LocalizationManager.Get(key);
        private string L(string key, params object[] args) => LocalizationManager.Get(key, args);
    }
}
