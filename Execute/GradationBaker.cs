using UnityEngine;
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

            // Ensure Read/Write to access vertices
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

            // Calculate world-to-box transformation matrix
            Matrix4x4 boxMatrix = Matrix4x4.TRS(
                settings.BoxCenter, 
                settings.BoxRotation, 
                new Vector3(GradationSettings.BoxWidth, settings.BoxHeight, GradationSettings.BoxDepth)
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;
            
            // For baking, we use object space directly (identity transform)
            // But we need to account for the renderer's transform
            Matrix4x4 objectToWorld = settings.TargetRenderer.localToWorldMatrix;

            mat.SetMatrix("_WorldToBox", worldToBox);
            mat.SetMatrix("_ObjectToWorld", objectToWorld);

            FileLogger.Log($"[GradationBaker] Box Center: {settings.BoxCenter}, Height: {settings.BoxHeight}");

            // Mask settings
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

        /// <summary>
        /// Fits the box settings to mesh bounds along the current gradient direction
        /// </summary>
        public void FitBoxToMesh(GradationSettings settings, Mesh mesh, Transform transform)
        {
            settings.FitToMeshBounds(mesh, transform);
        }

        private Texture2D CreateGradientLUT(Gradient gradient)
        {
            int width = 256;
            Texture2D tex = new Texture2D(width, 1, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            
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
