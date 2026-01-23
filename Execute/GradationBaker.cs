using UnityEngine;
using UnityEngine.Rendering;
using GradationTextureGenerator.Data;

namespace GradationTextureGenerator.Execute
{
    public class GradationBaker
    {
        private const string ShaderPath = "Hidden/GradationTextureGenerator/Bake";

        public Texture2D Bake(GradationSettings settings)
        {
            FileLogger.Log("[GradationBaker] Starting Bake...");
            if (settings.TargetRenderer == null)
            {
                FileLogger.LogError("[GradationBaker] Target Renderer is null.");
                return null;
            }

            Mesh mesh = GetMesh(settings.TargetRenderer);
            if (mesh == null)
            {
                FileLogger.LogError("[GradationBaker] Mesh not found.");
                return null;
            }
            FileLogger.Log($"[GradationBaker] Target Mesh: {mesh.name}, Vertex Count: {mesh.vertexCount}");

            // Ensure Read/Write to access vertices for bounds calculation (and potentially bake)
            MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);

            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                FileLogger.LogError($"[GradationBaker] Shader not found at {ShaderPath}. Ensure the shader file is imported.");
                return null;
            }
            FileLogger.Log($"[GradationBaker] Shader found: {shader.name}");
            Material mat = new Material(shader);

            // Generate LUT
            Texture2D lut = CreateGradientLUT(settings.Gradient);
            mat.SetTexture("_MainTex", lut);

            // Set Properties
            mat.SetVector("_Direction", settings.GradientDirection.normalized);
            
            float min = settings.MinRange;
            float max = settings.MaxRange;

            if (settings.AutoNormalize)
            {
                (min, max) = CalculateNormalizeRange(mesh, settings.GradientDirection);
                FileLogger.Log($"[GradationBaker] AutoNormalize Range: {min} to {max}");
                // Note: We don't write back to settings here to avoid side effects during bake,
                // but the UI might want to pre-calculate this.
            }
            mat.SetFloat("_RangeMin", min);
            mat.SetFloat("_RangeMax", max);

            if (settings.MaskTexture != null)
            {
                mat.SetTexture("_MaskTex", settings.MaskTexture);
                mat.SetInt("_UseMaskTexture", 1);
            }
            else
            {
                mat.SetInt("_UseMaskTexture", 0);
            }

            mat.SetInt("_UseVertexColorMask", settings.UseVertexColorMask ? 1 : 0);
            mat.SetInt("_InvertMask", settings.InvertMask ? 1 : 0);

            // Setup RenderTexture
            int res = settings.Resolution;
            FileLogger.Log($"[GradationBaker] Resolution: {res}");
            RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);

            // Draw Mesh
            // SetPass(0) activates the first pass
            if (mat.SetPass(0))
            {
                FileLogger.Log("[GradationBaker] Drawing Mesh Now...");
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
            else
            {
                FileLogger.LogError("[GradationBaker] SetPass failed.");
            }
            
            // Read back
            Texture2D result = new Texture2D(res, res, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            result.Apply();
            FileLogger.Log("[GradationBaker] ReadPixels finished.");

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(lut);

            return result;
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

        public (float min, float max) CalculateNormalizeRange(Mesh mesh, Vector3 direction)
        {
            // Simple bound calculation in Object Space
            // Because we bake based on Object Space position in shader.
            if (mesh == null) return (0, 1);

            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0) return (0, 1);

            float min = float.MaxValue;
            float max = float.MinValue;
            
            Vector3 dir = direction.normalized;
            
            foreach (var v in vertices)
            {
                float t = Vector3.Dot(v, dir);
                if (t < min) min = t;
                if (t > max) max = t;
            }
            
            // Prevent zero division if flat
            if (Mathf.Abs(max - min) < 0.0001f) max = min + 1.0f;
            
            return (min, max);
        }

        private Texture2D CreateGradientLUT(Gradient gradient)
        {
            int width = 256;
            Texture2D tex = new Texture2D(width, 1, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            
            // Color[] pixels = new Color[width]; // Can use SetPixels for speed
            for (int i = 0; i < width; i++)
            {
                float t = (float)i / (width - 1);
                Color col = gradient.Evaluate(t);
                tex.SetPixel(i, 0, col);
            }
            tex.Apply();
            return tex;
        }
    }
}
