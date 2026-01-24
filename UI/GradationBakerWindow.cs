using UnityEngine;
using UnityEditor;
using GradationTextureGenerator.Data;
using GradationTextureGenerator.Execute;
using System.IO;

namespace GradationTextureGenerator.UI
{
    public class GradationBakerWindow : EditorWindow
    {
        private GradationSettings _settings = new GradationSettings();
        private GradationBaker _baker = new GradationBaker();
        private GradationSceneHandle _sceneHandle = new GradationSceneHandle();
        private GradationPreview _preview = new GradationPreview();
        private bool _previewEnabled = true;
        
        // Foldout states
        private bool _boxSettingsFoldout = true;
        private bool _maskingFoldout = true;
        private bool _outputFoldout = true;
        
        [MenuItem("Tools/Gradation Texture Generator")]
        public static void ShowWindow()
        {
            GetWindow<GradationBakerWindow>("Gradation Gen");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _sceneHandle.Cleanup();
            _preview.Cleanup();
        }

        private void OnGUI()
        {
            // Master Toggle in Toolbar style
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _settings.IsToolActive = EditorGUILayout.ToggleLeft("Enable Tool", _settings.IsToolActive, GUILayout.Width(120));
            if (GUI.changed) SceneView.RepaintAll();
            EditorGUILayout.EndHorizontal();

            if (!_settings.IsToolActive)
            {
                EditorGUILayout.HelpBox("Tool is disabled. Enable to edit.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);
            
            // Target Renderer
            GUILayout.Label("Target", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Renderer newRenderer = (Renderer)EditorGUILayout.ObjectField("Target Renderer", _settings.TargetRenderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.TargetRenderer = newRenderer;
                if (newRenderer != null)
                {
                    _settings.Resolution = TextureResolutionResolver.ResolveDefaultResolution(newRenderer);
                    
                    // Initialize box to mesh bounds
                    Mesh mesh = GetMesh(newRenderer);
                    if (mesh != null)
                    {
                        MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);
                        _settings.FitToMeshBounds(mesh, newRenderer.transform);
                    }
                }
            }
            
            if (_settings.TargetRenderer == null)
            {
                EditorGUILayout.HelpBox("Please assign a Target Renderer to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);
            
            // Gradient
            GUILayout.Label("Gradient", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _settings.Gradient = EditorGUILayout.GradientField("Colors", _settings.Gradient);
            bool gradientChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(5);
            
            // Box Settings
            _boxSettingsFoldout = EditorGUILayout.Foldout(_boxSettingsFoldout, "Box Control", true, EditorStyles.foldoutHeader);
            if (_boxSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                
                // Fit to Mesh button - always available
                if (GUILayout.Button("Fit to Mesh Bounds"))
                {
                    Mesh mesh = GetMesh(_settings.TargetRenderer);
                    if (mesh != null)
                    {
                        MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);
                        _settings.FitToMeshBounds(mesh, _settings.TargetRenderer.transform);
                        SceneView.RepaintAll();
                    }
                }
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox("Use Scene View handles to adjust the gradation box:\n• Rotation: Change gradient direction\n• Position: Move the box\n• Red/Green cones: Adjust min/max range", MessageType.Info);
                
                EditorGUILayout.Space(3);
                
                // Manual box control - always visible
                EditorGUI.BeginChangeCheck();
                
                _settings.BoxCenter = EditorGUILayout.Vector3Field("Center", _settings.BoxCenter);
                
                // Rotation as Euler for easier editing
                Vector3 euler = _settings.BoxRotation.eulerAngles;
                euler = EditorGUILayout.Vector3Field("Rotation", euler);
                _settings.BoxRotation = Quaternion.Euler(euler);
                
                _settings.BoxHeight = EditorGUILayout.FloatField("Height", _settings.BoxHeight);
                _settings.BoxHeight = Mathf.Max(0.01f, _settings.BoxHeight);
                
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Masking
            _maskingFoldout = EditorGUILayout.Foldout(_maskingFoldout, "Masking", true, EditorStyles.foldoutHeader);
            if (_maskingFoldout)
            {
                EditorGUI.indentLevel++;
                _settings.MaskTexture = (Texture2D)EditorGUILayout.ObjectField("Mask Texture", _settings.MaskTexture, typeof(Texture2D), false);
                _settings.UseVertexColorMask = EditorGUILayout.Toggle("Use Vertex Color", _settings.UseVertexColorMask);
                _settings.InvertMask = EditorGUILayout.Toggle("Invert Mask", _settings.InvertMask);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Output
            _outputFoldout = EditorGUILayout.Foldout(_outputFoldout, "Output", true, EditorStyles.foldoutHeader);
            if (_outputFoldout)
            {
                EditorGUI.indentLevel++;
                _settings.Resolution = EditorGUILayout.IntField("Resolution", _settings.Resolution);
                _settings.Resolution = Mathf.Clamp(_settings.Resolution, 64, 8192);
                
                EditorGUILayout.BeginHorizontal();
                _settings.SavePath = EditorGUILayout.TextField("Save Path", _settings.SavePath);
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
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            
            // Preview toggle
            _previewEnabled = EditorGUILayout.Toggle("Realtime Preview", _previewEnabled);
            if (_previewEnabled)
            {
                EditorGUI.indentLevel++;
                _settings.PreviewOpacity = EditorGUILayout.Slider("Opacity", _settings.PreviewOpacity, 0f, 1f);
                EditorGUI.indentLevel--;
                
                if (GUI.changed) SceneView.RepaintAll();
            }

            EditorGUILayout.Space(10);
            
            // Bake button
            if (GUILayout.Button("Bake & Save", GUILayout.Height(40)))
            {
                BakeAndSave();
            }
            
            if (gradientChanged)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_settings.IsToolActive) return;
            if (_settings.TargetRenderer == null) return;
            
            Mesh mesh = GetMesh(_settings.TargetRenderer);
            if (mesh == null) return;

            // Preview
            if (_previewEnabled)
            {
                _preview.UpdatePreview(_settings, mesh, _settings.TargetRenderer.localToWorldMatrix);
            }

            // Handle - returns what type of change was made
            HandleChangeType changeType = _sceneHandle.DrawHandle(_settings, _settings.TargetRenderer.transform);
            
            if (changeType != HandleChangeType.None)
            {
                // Just repaint the window to reflect changes
                // FitToMeshBounds is only called on initialization or via button
                Repaint();
            }
        }

        private void BakeAndSave()
        {
            FileLogger.Clear();
            FileLogger.Log("[GradationBakerWindow] Bake & Save button clicked.");
            
            Texture2D tex = _baker.Bake(_settings);
            if (tex != null)
            {
                string dir = _settings.SavePath.Replace('\\', '/');
                FileLogger.Log($"[GradationBakerWindow] SavePath setting: {dir}");
                
                string fullDirPath = dir;
                
                if (dir.StartsWith("Assets"))
                {
                    fullDirPath = Path.Combine(Application.dataPath, dir.Substring("Assets".Length).TrimStart('/'));
                }
                fullDirPath = fullDirPath.Replace('\\', '/');
                
                FileLogger.Log($"[GradationBakerWindow] Full Directory Path: {fullDirPath}");

                if (!Directory.Exists(fullDirPath))
                {
                    FileLogger.Log("[GradationBakerWindow] Directory does not exist. Creating...");
                    Directory.CreateDirectory(fullDirPath);
                }

                string fileName = $"Gradation_{_settings.TargetRenderer.name}_{System.DateTime.Now:yyyyMMddHHmmss}.png";
                string fullPath = Path.Combine(fullDirPath, fileName).Replace('\\', '/');
                
                FileLogger.Log($"[GradationBakerWindow] Writing file to: {fullPath}");
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(fullPath, bytes);
                
                AssetDatabase.Refresh();
                FileLogger.Log($"[GradationTextureGenerator] Saved to {fullPath}");
                
                EditorUtility.DisplayDialog("Success", $"Gradation texture saved to:\n{fullPath}", "OK");
            }
            else
            {
                FileLogger.LogError("[GradationBakerWindow] Bake returned null.");
                EditorUtility.DisplayDialog("Error", "Bake failed. Check the console for details.", "OK");
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
    }
}
