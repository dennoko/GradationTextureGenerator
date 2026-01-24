using UnityEngine;
using UnityEditor;
using GradationBaker.Data;

namespace GradationBaker.UI
{
    public class GradationPreview
    {
        private const string ShaderPath = "Hidden/GradationBaker/Preview";
        private Material _previewMaterial;
        private Texture2D _lutTexture;


        public void UpdatePreview(GradationSettings settings, MeshEntry entry)
        {
            if (Event.current.type != EventType.Repaint) return;
            Renderer renderer = entry.ActiveRenderer;
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

            // Calculate Main Matrix
            Matrix4x4 boxMatrix = Matrix4x4.TRS(
                settings.BoxCenter, 
                settings.BoxRotation, 
                new Vector3(GradationSettings.BoxWidth, settings.BoxHeight, GradationSettings.BoxDepth)
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;
            Matrix4x4 localToWorld = renderer.localToWorldMatrix;

            // Calculate Mirror Matrix
            bool isMirrorEnabled = settings.UseMirror && settings.MirrorAxis != MirrorAxis.None;
            Matrix4x4 worldToBoxMirror = Matrix4x4.identity;
            
            if (isMirrorEnabled)
            {
                var (mirrorCenter, mirrorRot) = settings.GetMirroredBox(renderer.transform);
                Matrix4x4 mirrorBoxMatrix = Matrix4x4.TRS(
                    mirrorCenter, 
                    mirrorRot, 
                    new Vector3(GradationSettings.BoxWidth, settings.BoxHeight, GradationSettings.BoxDepth)
                );
                worldToBoxMirror = mirrorBoxMatrix.inverse;
            }

            // Set shader properties
            _previewMaterial.SetTexture("_MainTex", _lutTexture);
            _previewMaterial.SetMatrix("_WorldToBox", worldToBox);
            _previewMaterial.SetMatrix("_ObjectToWorld", localToWorld);
            _previewMaterial.SetFloat("_BoxHeight", settings.BoxHeight);
            _previewMaterial.SetFloat("_Opacity", settings.PreviewOpacity);
            
            // Mirror settings
            _previewMaterial.SetInt("_UseMirror", isMirrorEnabled ? 1 : 0);
            _previewMaterial.SetMatrix("_WorldToBoxMirror", worldToBoxMirror);

            // Mask settings (per-mesh)
            _previewMaterial.SetInt("_UVChannel", entry.UVChannel);
            if (entry.MaskTexture != null)
            {
                _previewMaterial.SetTexture("_MaskTex", entry.MaskTexture);
                _previewMaterial.SetInt("_UseMaskTexture", 1);
            }
            else
            {
                _previewMaterial.SetInt("_UseMaskTexture", 0);
            }
            _previewMaterial.SetInt("_UseVertexColorMask", entry.UseVertexColorMask ? 1 : 0);
            _previewMaterial.SetInt("_InvertMask", entry.InvertMask ? 1 : 0);

            // Draw mesh with preview material (Single Pass)
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
                UpdatePreview(settings, entry);
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
