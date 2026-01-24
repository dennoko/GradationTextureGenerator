using UnityEngine;
using UnityEditor;
using GradationTextureGenerator.Data;

namespace GradationTextureGenerator.UI
{
    public class GradationPreview
    {
        private const string ShaderPath = "Hidden/GradationTextureGenerator/Preview";
        private Material _previewMaterial;
        private Texture2D _lutTexture;

        public void UpdatePreview(GradationSettings settings, Renderer renderer, Matrix4x4 localToWorld, bool useMirror = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (renderer == null) return;
            
            Mesh mesh = GetMesh(renderer);
            if (mesh == null) return;
            
            // Lazy Init Material
            if (_previewMaterial == null)
            {
                Shader shader = Shader.Find(ShaderPath);
                if (shader == null) return;
                _previewMaterial = new Material(shader);
                _previewMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            // Update LUT
            UpdateLUT(settings.Gradient);

            // Get box parameters (mirrored if requested)
            Vector3 boxCenter = settings.BoxCenter;
            Quaternion boxRotation = settings.BoxRotation;
            
            if (useMirror)
            {
                (boxCenter, boxRotation) = settings.GetMirroredBox(renderer.transform);
            }

            // Calculate world-to-box transformation matrix
            Matrix4x4 boxMatrix = Matrix4x4.TRS(
                boxCenter, 
                boxRotation, 
                new Vector3(GradationSettings.BoxWidth, settings.BoxHeight, GradationSettings.BoxDepth)
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;

            // Set shader properties
            _previewMaterial.SetTexture("_MainTex", _lutTexture);
            _previewMaterial.SetMatrix("_WorldToBox", worldToBox);
            _previewMaterial.SetMatrix("_ObjectToWorld", localToWorld);
            _previewMaterial.SetFloat("_BoxHeight", settings.BoxHeight);
            _previewMaterial.SetFloat("_Opacity", settings.PreviewOpacity);

            // Draw mesh with preview material
            if (_previewMaterial.SetPass(0))
            {
                Graphics.DrawMeshNow(mesh, localToWorld);
            }
        }

        /// <summary>
        /// Updates preview for all mesh entries (with optional mirror)
        /// </summary>
        public void UpdatePreviewAll(GradationSettings settings)
        {
            foreach (var entry in settings.MeshEntries)
            {
                Renderer renderer = entry.ActiveRenderer;
                if (renderer == null) continue;
                
                // Draw main gradation
                UpdatePreview(settings, renderer, renderer.localToWorldMatrix, false);
                
                // Draw mirrored gradation if enabled
                if (settings.UseMirror && settings.MirrorAxis != MirrorAxis.None)
                {
                    UpdatePreview(settings, renderer, renderer.localToWorldMatrix, true);
                }
            }
        }
        
        private void UpdateLUT(Gradient gradient)
        {
            if (_lutTexture == null)
            {
                _lutTexture = new Texture2D(256, 1, TextureFormat.ARGB32, false);
                _lutTexture.wrapMode = TextureWrapMode.Clamp;
                _lutTexture.hideFlags = HideFlags.HideAndDontSave;
            }
            
            for (int i = 0; i < 256; i++)
            {
                _lutTexture.SetPixel(i, 0, gradient.Evaluate((float)i / 255f));
            }
            _lutTexture.Apply();
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

        public void Cleanup()
        {
            if (_previewMaterial != null) Object.DestroyImmediate(_previewMaterial);
            if (_lutTexture != null) Object.DestroyImmediate(_lutTexture);
        }
    }
}
