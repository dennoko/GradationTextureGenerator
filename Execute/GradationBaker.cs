using UnityEngine;
using System.Collections.Generic;
using GradationBaker.Data;

namespace GradationBaker.Execute
{
    public class GradationBakingExecutor
    {
        private const string ShaderPath = "Hidden/GradationBaker/Bake";

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
                
                // Bake main gradation (or split based on settings)
                BakeResult bakeResult = Bake(settings, entry, false);
                
                // If mirror is enabled, blend with mirrored gradation
                if (settings.UseMirror && settings.MirrorAxis != MirrorAxis.None)
                {
                    // For split results
                    if (bakeResult.SubMeshResults != null && bakeResult.SubMeshResults.Count > 0)
                    {
                        var mirrorResult = Bake(settings, entry, true);
                        if (mirrorResult != null && mirrorResult.SubMeshResults != null)
                        {
                            for (int i = 0; i < bakeResult.SubMeshResults.Count; i++)
                            {
                                var mainTex = bakeResult.SubMeshResults[i].Texture;
                                // Try to find matching submesh in mirror result
                                // Note: Assuming submesh order and count is identical
                                if (i < mirrorResult.SubMeshResults.Count)
                                {
                                    var mirrorTex = mirrorResult.SubMeshResults[i].Texture;
                                    if (mainTex != null && mirrorTex != null)
                                    {
                                        BlendTextures(mainTex, mirrorTex, settings.MirrorBlend);
                                    }
                                }
                            }

                            // Cleanup mirror textures immediately as they are blended in
                            foreach (var res in mirrorResult.SubMeshResults)
                            {
                                if (res.Texture != null) Object.DestroyImmediate(res.Texture);
                            }
                        }
                    }
                    // For single result
                    else if (bakeResult.Texture != null)
                    {
                        var mirrorResult = Bake(settings, entry, true);
                        if (mirrorResult != null && mirrorResult.Texture != null)
                        {
                            BlendTextures(bakeResult.Texture, mirrorResult.Texture, settings.MirrorBlend);
                            Object.DestroyImmediate(mirrorResult.Texture);
                        }
                    }
                }
                
                results.Add(bakeResult);
            }
            
            return results;
        }

        /// <summary>
        /// Bakes a single mesh entry to texture(s)
        /// </summary>
        public BakeResult Bake(GradationSettings settings, MeshEntry entry, bool useMirror = false)
        {
            Renderer renderer = entry.ActiveRenderer;
            FileLogger.Log($"[GradationBaker] Starting Bake for {renderer.name} (mirror={useMirror}) split={entry.SplitByMaterial}...");
            
            BakeResult result = new BakeResult
            {
                RendererName = entry.SourceRenderer != null ? entry.SourceRenderer.name : "Unknown",
                SourceRenderer = entry.SourceRenderer,
                SubMeshResults = new List<SubMeshResult>()
            };

            Mesh mesh = GetMesh(renderer);
            if (mesh == null)
            {
                FileLogger.LogError("[GradationBaker] Mesh not found.");
                return result;
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
                settings.BoxScale
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;
            Matrix4x4 objectToWorld = renderer.localToWorldMatrix;

            mat.SetMatrix("_WorldToBox", worldToBox);
            mat.SetMatrix("_ObjectToWorld", objectToWorld);
            mat.SetInt("_Shape", (int)settings.Shape);
            
            // UV Channel (per-mesh)
            mat.SetInt("_UVChannel", entry.UVChannel);

            // Mask settings (per-mesh)
            if (entry.MaskTexture != null)
            {
                mat.SetTexture("_MaskTex", entry.MaskTexture);
                mat.SetInt("_UseMaskTexture", 1);
            }
            else
            {
                mat.SetInt("_UseMaskTexture", 0);
            }

            mat.SetInt("_UseVertexColorMask", entry.UseVertexColorMask ? 1 : 0);
            mat.SetInt("_InvertMask", entry.InvertMask ? 1 : 0);

            // Setup RenderTexture
            int res = settings.Resolution;
            RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            
            // Clear color logic
            Color clearColor = Color.clear;
            switch (settings.BgColor)
            {
                case BackgroundColor.White:
                    clearColor = Color.white;
                    break;
                case BackgroundColor.Black:
                    clearColor = Color.black;
                    break;
                case BackgroundColor.Transparent:
                default:
                    clearColor = Color.clear;
                    break;
            }

            // Material and Submesh Handling
            Material[] sharedMaterials = renderer.sharedMaterials;

            if (entry.SplitByMaterial)
            {
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    GL.Clear(true, true, clearColor);

                    if (mat.SetPass(0))
                    {
                        Graphics.DrawMeshNow(mesh, Matrix4x4.identity, i);
                    }
                    else
                    {
                        FileLogger.LogError($"[GradationBaker] SetPass failed for submesh {i}.");
                    }
                    
                    Texture2D subTex = new Texture2D(res, res, TextureFormat.ARGB32, false);
                    subTex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                    subTex.Apply();
                    
                    string matName = (i < sharedMaterials.Length && sharedMaterials[i] != null) 
                        ? sharedMaterials[i].name 
                        : $"Submesh{i}";

                    result.SubMeshResults.Add(new SubMeshResult
                    {
                        Texture = subTex,
                        SubMeshIndex = i,
                        MaterialName = matName
                    });
                }
            }
            else
            {
                // Draw all submeshes into a single texture
                GL.Clear(true, true, clearColor);
                if (mat.SetPass(0))
                {
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        Graphics.DrawMeshNow(mesh, Matrix4x4.identity, i);
                    }
                }

                Texture2D mainTex = new Texture2D(res, res, TextureFormat.ARGB32, false);
                mainTex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                mainTex.Apply();

                result.Texture = mainTex;
            }

            // Cleanup
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(lut);

            // Apply Edge Padding if enabled
            if (settings.EdgePaddingPixels > 0)
            {
                if (result.SubMeshResults != null && result.SubMeshResults.Count > 0)
                {
                    foreach (var subRes in result.SubMeshResults)
                    {
                        if (subRes.Texture != null)
                        {
                            EdgePadding.Apply(subRes.Texture, settings.EdgePaddingPixels);
                        }
                    }
                }
                else if (result.Texture != null)
                {
                    EdgePadding.Apply(result.Texture, settings.EdgePaddingPixels);
                }
            }

            return result;
        }

        /// <summary>
        /// Blends two textures for mirror gradation overlay.
        /// Max: brighter value wins per channel. Min: darker value wins per channel.
        /// </summary>
        private void BlendTextures(Texture2D baseT, Texture2D overlayTex, MirrorBlendMode blendMode)
        {
            Color[] basePixels = baseT.GetPixels();
            Color[] overlayPixels = overlayTex.GetPixels();

            for (int i = 0; i < basePixels.Length; i++)
            {
                Color baseC = basePixels[i];
                Color overC = overlayPixels[i];

                if (blendMode == MirrorBlendMode.Min)
                {
                    basePixels[i] = new Color(
                        Mathf.Min(baseC.r, overC.r),
                        Mathf.Min(baseC.g, overC.g),
                        Mathf.Min(baseC.b, overC.b),
                        Mathf.Min(baseC.a, overC.a)
                    );
                }
                else // Max (default)
                {
                    basePixels[i] = new Color(
                        Mathf.Max(baseC.r, overC.r),
                        Mathf.Max(baseC.g, overC.g),
                        Mathf.Max(baseC.b, overC.b),
                        Mathf.Max(baseC.a, overC.a)
                    );
                }
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
        // Combined result (or main result if not split)
        public Texture2D Texture;
        
        // Split results (if applicable)
        public List<SubMeshResult> SubMeshResults;
        
        public string RendererName;
        public Renderer SourceRenderer;
    }

    public class SubMeshResult
    {
        public Texture2D Texture;
        public string MaterialName;
        public int SubMeshIndex;
    }
}
