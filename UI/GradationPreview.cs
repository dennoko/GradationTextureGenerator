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

        public void UpdatePreview(GradationSettings settings, Mesh mesh, Matrix4x4 localToWorld)
        {
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
            // For performance, maybe only update when gradient changes?
            // For now, update every frame for simplicity in "interactive" mode
            UpdateLUT(settings.Gradient);

            // Set Props
            _previewMaterial.SetTexture("_MainTex", _lutTexture);
            _previewMaterial.SetVector("_Direction", settings.GradientDirection.normalized);
            _previewMaterial.SetFloat("_RangeMin", settings.MinRange);
            _previewMaterial.SetFloat("_RangeMax", settings.MaxRange);
            _previewMaterial.SetFloat("_Opacity", 0.7f); // Fixed opacity for now
            
            // Draw via Graphics
            // DrawMesh draws immediately for the current camera.
            // But usually this is called inside OnSceneGUI which is a Repaint event.
            // However, Graphics.DrawMesh draws into the scene.
            // Let's use pass 0
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
            
            // Simple bake
            for (int i = 0; i < 256; i++)
            {
                _lutTexture.SetPixel(i, 0, gradient.Evaluate((float)i/255f));
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
