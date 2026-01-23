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
        }

        private void OnGUI()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            Renderer newRenderer = (Renderer)EditorGUILayout.ObjectField("Target Renderer", _settings.TargetRenderer, typeof(Renderer), true);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.TargetRenderer = newRenderer;
                if (newRenderer != null)
                {
                    _settings.Resolution = TextureResolutionResolver.ResolveDefaultResolution(newRenderer);
                }
            }

            _settings.GradientDirection = EditorGUILayout.Vector3Field("Direction", _settings.GradientDirection);
            _settings.Gradient = EditorGUILayout.GradientField("Gradient", _settings.Gradient);

            EditorGUILayout.Space();
            GUILayout.Label("Masking", EditorStyles.boldLabel);
            _settings.MaskTexture = (Texture2D)EditorGUILayout.ObjectField("Mask Texture", _settings.MaskTexture, typeof(Texture2D), false);
            _settings.UseVertexColorMask = EditorGUILayout.Toggle("Use Vertex Color", _settings.UseVertexColorMask);
            _settings.InvertMask = EditorGUILayout.Toggle("Invert Mask", _settings.InvertMask);

            EditorGUILayout.Space();
            GUILayout.Label("Output", EditorStyles.boldLabel);
            _settings.Resolution = EditorGUILayout.IntField("Resolution", _settings.Resolution);
            
            _settings.AutoNormalize = EditorGUILayout.Toggle("Auto Normalize", _settings.AutoNormalize);
            if (!_settings.AutoNormalize)
            {
                _settings.MinRange = EditorGUILayout.FloatField("Min Range", _settings.MinRange);
                _settings.MaxRange = EditorGUILayout.FloatField("Max Range", _settings.MaxRange);
            }
            else
            {
                if (GUILayout.Button("Calculate Range Preview"))
                {
                    if (_settings.TargetRenderer != null)
                    {
                         Mesh mesh = GetMesh(_settings.TargetRenderer);
                         if (mesh != null)
                         {
                             // Ensure accessible
                             MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);
                            (_settings.MinRange, _settings.MaxRange) = _baker.CalculateNormalizeRange(mesh, _settings.GradientDirection);
                         }
                    }
                }
                EditorGUILayout.HelpBox($"Current Range: {_settings.MinRange:F2} - {_settings.MaxRange:F2}", MessageType.Info);
            }

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

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake & Save", GUILayout.Height(40)))
            {
                BakeAndSave();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_settings.TargetRenderer == null) return;
            
            Bounds bounds = _settings.TargetRenderer.bounds;
            Vector3 center = bounds.center;

            _sceneHandle.DrawHandle(center, ref _settings.GradientDirection);
            
            if (GUI.changed)
            {
                Repaint();
            }
        }

        private void BakeAndSave()
        {
            FileLogger.Clear(); // Clear old logs
            FileLogger.Log("[GradationBakerWindow] Bake & Save button clicked.");
            Texture2D tex = _baker.Bake(_settings);
            if (tex != null)
            {
                // Ensure directory
                string dir = _settings.SavePath.Replace('\\', '/');
                FileLogger.Log($"[GradationBakerWindow] SavePath setting: {dir}");
                
                string fullDirPath = dir;
                
                // If it starts with Assets, mapped to project root
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
            }
            else
            {
                FileLogger.LogError("[GradationBakerWindow] Bake returned null.");
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
