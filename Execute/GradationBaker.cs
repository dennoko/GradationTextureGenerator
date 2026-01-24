using UnityEngine;
using System.Collections.Generic;
using GradationTextureGenerator.Data;

namespace GradationTextureGenerator.Execute
{
    public class GradationBaker
    {
        private const string ShaderPath = "Hidden/GradationTextureGenerator/Bake";

        /// <summary>
        /// Bakes gradation textures for all mesh entries
        /// </summary>
        public List<BakeResult> BakeAll(GradationSettings settings)
        {
            var results = new List<BakeResult>();
            
            foreach (var entry in settings.MeshEntries)
            {
                Renderer renderer = entry.ActiveRenderer;
                if (renderer == null) continue;
                
                Texture2D tex = Bake(settings, renderer);
                results.Add(new BakeResult
                {
                    Texture = tex,
                    RendererName = entry.SourceRenderer != null ? entry.SourceRenderer.name : "Unknown"
                });
            }
            
            return results;
        }

        /// <summary>
        /// Bakes a single renderer to texture
        /// </summary>
        public Texture2D Bake(GradationSettings settings, Renderer renderer)
        {
            FileLogger.Log($"[GradationBaker] Starting Bake for {renderer.name}...");
            
            Mesh mesh = GetMesh(renderer);
            if (mesh == null)
            {
                FileLogger.LogError("[GradationBaker] Mesh not found.");
                return null;
            }
            FileLogger.Log($"[GradationBaker] Target Mesh: {mesh.name}, Vertex Count: {mesh.vertexCount}");

            MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);

            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                FileLogger.LogError($"[GradationBaker] Shader not found at {ShaderPath}.");
                return null;
            }
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
            Matrix4x4 objectToWorld = renderer.localToWorldMatrix;

            mat.SetMatrix("_WorldToBox", worldToBox);
            mat.SetMatrix("_ObjectToWorld", objectToWorld);
            
            // UV Channel
            mat.SetInt("_UVChannel", settings.UVChannel);

            FileLogger.Log($"[GradationBaker] UV Channel: {settings.UVChannel}, Box Height: {settings.BoxHeight}");

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
            RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);

            // Draw Mesh
            if (mat.SetPass(0))
            {
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
            FileLogger.Log("[GradationBaker] Bake completed.");

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

    public class BakeResult
    {
        public Texture2D Texture;
        public string RendererName;
    }
}
