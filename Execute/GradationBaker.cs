using UnityEngine;
using System.Collections.Generic;
using GradationTextureGenerator.Data;

namespace GradationTextureGenerator.Execute
{
    public class GradationBaker
    {
        private const string ShaderPath = "Hidden/GradationTextureGenerator/Bake";

        /// <summary>
        /// Bakes gradation textures for all mesh entries (with optional mirror)
        /// </summary>
        public List<BakeResult> BakeAll(GradationSettings settings)
        {
            var results = new List<BakeResult>();
            
            foreach (var entry in settings.MeshEntries)
            {
                Renderer renderer = entry.ActiveRenderer;
                if (renderer == null) continue;
                
                // Bake main gradation
                Texture2D tex = Bake(settings, renderer, false);
                
                // If mirror is enabled, blend with mirrored gradation
                if (settings.UseMirror && settings.MirrorAxis != MirrorAxis.None && tex != null)
                {
                    Texture2D mirrorTex = Bake(settings, renderer, true);
                    if (mirrorTex != null)
                    {
                        // Blend textures (max blend for gradation)
                        BlendTextures(tex, mirrorTex);
                        Object.DestroyImmediate(mirrorTex);
                    }
                }
                
                results.Add(new BakeResult
                {
                    Texture = tex,
                    RendererName = entry.SourceRenderer != null ? entry.SourceRenderer.name : "Unknown",
                    SourceRenderer = entry.SourceRenderer
                });
            }
            
            return results;
        }

        /// <summary>
        /// Bakes a single renderer to texture
        /// </summary>
        public Texture2D Bake(GradationSettings settings, Renderer renderer, bool useMirror = false)
        {
            FileLogger.Log($"[GradationBaker] Starting Bake for {renderer.name} (mirror={useMirror})...");
            
            Mesh mesh = GetMesh(renderer);
            if (mesh == null)
            {
                FileLogger.LogError("[GradationBaker] Mesh not found.");
                return null;
            }

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
            Matrix4x4 objectToWorld = renderer.localToWorldMatrix;

            mat.SetMatrix("_WorldToBox", worldToBox);
            mat.SetMatrix("_ObjectToWorld", objectToWorld);
            
            // UV Channel
            mat.SetInt("_UVChannel", settings.UVChannel);

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

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(lut);

            return result;
        }

        /// <summary>
        /// Blends two textures using max blend (for gradation overlay)
        /// </summary>
        private void BlendTextures(Texture2D baseT, Texture2D overlayTex)
        {
            Color[] basePixels = baseT.GetPixels();
            Color[] overlayPixels = overlayTex.GetPixels();
            
            for (int i = 0; i < basePixels.Length; i++)
            {
                // Max blend - take the brighter value (or higher alpha)
                Color baseC = basePixels[i];
                Color overC = overlayPixels[i];
                
                // For gradation, we want to combine both - use additive or max
                basePixels[i] = new Color(
                    Mathf.Max(baseC.r, overC.r),
                    Mathf.Max(baseC.g, overC.g),
                    Mathf.Max(baseC.b, overC.b),
                    Mathf.Max(baseC.a, overC.a)
                );
            }
            
            baseT.SetPixels(basePixels);
            baseT.Apply();
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
        public Renderer SourceRenderer;
    }
}
