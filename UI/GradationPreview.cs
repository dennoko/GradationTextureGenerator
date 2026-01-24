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
        private Gradient _cachedGradient;

        public void UpdatePreview(GradationSettings settings, Mesh mesh, Matrix4x4 localToWorld)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (mesh == null) return;
            
            // Lazy Init Material
            if (_previewMaterial == null)
            {
                Shader shader = Shader.Find(ShaderPath);
                if (shader == null) return;
                _previewMaterial = new Material(shader);
                _previewMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            // Update LUT only when gradient changes (optimization)
            UpdateLUT(settings.Gradient);

            // Calculate world-to-box transformation matrix
            Matrix4x4 boxMatrix = Matrix4x4.TRS(
                settings.BoxCenter, 
                settings.BoxRotation, 
                new Vector3(GradationSettings.BoxWidth, settings.BoxHeight, GradationSettings.BoxDepth)
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;

            // Set shader properties
            _previewMaterial.SetTexture("_MainTex", _lutTexture);
            _previewMaterial.SetMatrix("_WorldToBox", worldToBox);
            _previewMaterial.SetMatrix("_ObjectToWorld", localToWorld);
            _previewMaterial.SetFloat("_BoxHeight", settings.BoxHeight);
            _previewMaterial.SetFloat("_Opacity", settings.PreviewOpacity);
            
            // Legacy properties for compatibility (shader will use new method primarily)
            _previewMaterial.SetVector("_Direction", settings.GradientDirection.normalized);
            _previewMaterial.SetFloat("_RangeMin", settings.MinRange);
            _previewMaterial.SetFloat("_RangeMax", settings.MaxRange);

            // Draw mesh with preview material
            if (_previewMaterial.SetPass(0))
            {
                Graphics.DrawMeshNow(mesh, localToWorld);
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
            
            // Bake gradient to LUT texture
            for (int i = 0; i < 256; i++)
            {
                _lutTexture.SetPixel(i, 0, gradient.Evaluate((float)i / 255f));
            }
            _lutTexture.Apply();
        }

        public void Cleanup()
        {
            if (_previewMaterial != null) Object.DestroyImmediate(_previewMaterial);
            if (_lutTexture != null) Object.DestroyImmediate(_lutTexture);
        }
    }
}
