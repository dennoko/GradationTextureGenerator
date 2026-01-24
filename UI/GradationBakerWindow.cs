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
        private bool _previewEnabled = true;
        
        // ReorderableList for meshes
        private ReorderableList _meshList;
        
        // Foldout states
        private bool _meshFoldout = true;
        private bool _gradientFoldout = true;
        private bool _boxSettingsFoldout = true;
        private bool _maskingFoldout = false;
        private bool _outputFoldout = true;
        
        // Styles
        private GUIStyle _headerStyle;
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
            
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
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
                
                float buttonWidth = 60f;
                float removeWidth = 24f;
                float spacing = 4f;
                
                // Renderer field
                float fieldWidth = rect.width - buttonWidth - removeWidth - spacing * 2;
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
                
                // Work mesh toggle button
                Rect workRect = new Rect(rect.x + fieldWidth + spacing, rect.y + 2, buttonWidth, EditorGUIUtility.singleLineHeight);
                string workLabel = entry.HasWorkMesh ? "✓ " + L("work") : L("work");
                if (GUI.Button(workRect, workLabel))
                {
                    ToggleWorkMesh(entry);
                }
                
                // Remove button
                Rect removeRect = new Rect(rect.x + fieldWidth + buttonWidth + spacing * 2, rect.y + 2, removeWidth, EditorGUIUtility.singleLineHeight);
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
            
            // Target Meshes Section
            _meshFoldout = EditorGUILayout.Foldout(_meshFoldout, L("target_meshes"), true, EditorStyles.foldoutHeader);
            if (_meshFoldout)
            {
                EditorGUILayout.BeginVertical(_sectionStyle);
                
                if (_settings.MeshEntries.Count == 0)
                {
                    EditorGUILayout.HelpBox(L("no_mesh_assigned"), MessageType.Info);
                }
                else
                {
                    _meshList.DoLayoutList();
                }
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(L("add_mesh")))
                {
                    _settings.MeshEntries.Add(new MeshEntry());
                    SetupMeshList();
                }
                if (_settings.MeshEntries.Count > 0 && GUILayout.Button(L("clear_all")))
                {
                    ClearAllMeshes();
                }
                EditorGUILayout.EndHorizontal();
                
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
                
                // UV Channel
                string[] uvOptions = { "UV0", "UV1", "UV2", "UV3" };
                _settings.UVChannel = EditorGUILayout.Popup(L("uv_channel"), _settings.UVChannel, uvOptions);
                
                // Resolution
                _settings.Resolution = EditorGUILayout.IntField(L("resolution"), _settings.Resolution);
                _settings.Resolution = Mathf.Clamp(_settings.Resolution, 64, 8192);
                
                // Save Path
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
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);
            
            // Preview toggle
            EditorGUILayout.BeginHorizontal();
            _previewEnabled = EditorGUILayout.Toggle(L("preview"), _previewEnabled, GUILayout.Width(150));
            if (_previewEnabled)
            {
                _settings.PreviewOpacity = EditorGUILayout.Slider(_settings.PreviewOpacity, 0f, 1f);
            }
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

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_settings.IsToolActive) return;
            if (_settings.MeshEntries.Count == 0) return;

            // Preview all meshes
            if (_previewEnabled)
            {
                _preview.UpdatePreviewAll(_settings);
            }

            // Handle
            HandleChangeType changeType = _sceneHandle.DrawHandle(_settings, null);
            
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
            string lastPath = "";
            
            foreach (var result in results)
            {
                if (result.Texture == null) continue;
                
                string dir = _settings.SavePath.Replace('\\', '/');
                string fullDirPath = dir;
                
                if (dir.StartsWith("Assets"))
                {
                    fullDirPath = Path.Combine(Application.dataPath, dir.Substring("Assets".Length).TrimStart('/'));
                }
                fullDirPath = fullDirPath.Replace('\\', '/');

                if (!Directory.Exists(fullDirPath))
                {
                    Directory.CreateDirectory(fullDirPath);
                }

                string fileName = $"Gradation_{result.RendererName}_{System.DateTime.Now:yyyyMMddHHmmss}.png";
                string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');
                
                byte[] bytes = result.Texture.EncodeToPNG();
                File.WriteAllBytes(fullPath, bytes);
                Object.DestroyImmediate(result.Texture);
                
                FileLogger.Log($"[GradationBakerWindow] Saved: {fullPath}");
                lastPath = fullPath;
                savedCount++;
            }
            
            AssetDatabase.Refresh();
            
            if (savedCount > 0)
            {
                EditorUtility.DisplayDialog(L("success_title"), L("success_message", lastPath), "OK");
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
                _settings.FitToMeshBounds(mesh, renderer.transform);
                _settings.Resolution = TextureResolutionResolver.ResolveDefaultResolution(renderer);
                SceneView.RepaintAll();
            }
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
