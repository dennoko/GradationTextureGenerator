using UnityEngine;
using System.Collections.Generic;
using GradationBaker.Data;

namespace GradationBaker.Execute
{
    public class GradationBakingExecutor
    {
        private const string ShaderPath = "Hidden/GradationBaker/Bake";

        /// <summary>
        /// シェーダー・マテリアル・LUT はグラデーション設定が共通なので
        /// メッシュ毎に作り直さず 1 回だけ生成して使い回す。
        /// </summary>
        private class BakeContext : System.IDisposable
        {
            public Material Material;
            public Texture2D Lut;

            public bool IsValid => Material != null;

            public static BakeContext Create(GradationSettings settings)
            {
                var ctx = new BakeContext();

                Shader shader = Shader.Find(ShaderPath);
                if (shader == null)
                {
                    FileLogger.LogError($"[GradationBaker] Shader not found at {ShaderPath}.");
                    return ctx; // IsValid == false
                }

                ctx.Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                ctx.Lut = CreateGradientLUT(settings.Gradient);
                ctx.Material.SetTexture("_MainTex", ctx.Lut);
                return ctx;
            }

            public void Dispose()
            {
                if (Material != null) Object.DestroyImmediate(Material);
                if (Lut != null) Object.DestroyImmediate(Lut);
                Material = null;
                Lut = null;
            }
        }

        /// <summary>
        /// Bakes gradation textures for all mesh entries (with optional mirror)
        /// </summary>
        /// <param name="onProgress">進捗通知 (current, total, name)。プログレスバー表示用 (省略可)</param>
        public List<BakeResult> BakeAll(GradationSettings settings, System.Action<int, int, string> onProgress = null)
        {
            var results = new List<BakeResult>();
            int total = settings.MeshEntries.Count;
            int current = 0;

            using (var ctx = BakeContext.Create(settings))
            {
                if (!ctx.IsValid) return results;

                foreach (var entry in settings.MeshEntries)
                {
                    current++;
                    Renderer renderer = entry.ActiveRenderer;
                    if (renderer == null) continue;

                    onProgress?.Invoke(current, total, renderer.name);

                    // Bake main gradation (or split based on settings)
                    BakeResult bakeResult = Bake(settings, entry, ctx, false);

                    // If mirror is enabled, blend with mirrored gradation
                    if (settings.UseMirror && settings.MirrorAxis != MirrorAxis.None)
                    {
                        BlendMirrorInto(bakeResult, settings, entry, ctx);
                    }

                    // エッジパディングはミラー合成が終わった最終ピクセルに対してかける。
                    // (合成前にかけると、膨張済みピクセル同士が Max/Min されて
                    //  UV アイランド外周の色が合成結果と食い違う)
                    ApplyEdgePadding(bakeResult, settings.EdgePaddingPixels);

                    results.Add(bakeResult);
                }
            }

            return results;
        }

        /// <summary>
        /// ミラーベイクを実行し、メインの結果へ Max/Min 合成する。
        /// </summary>
        private void BlendMirrorInto(BakeResult bakeResult, GradationSettings settings, MeshEntry entry, BakeContext ctx)
        {
            // For split results
            if (bakeResult.SubMeshResults != null && bakeResult.SubMeshResults.Count > 0)
            {
                var mirrorResult = Bake(settings, entry, ctx, true);
                if (mirrorResult?.SubMeshResults == null) return;

                // SubMeshIndex (マテリアルグループの代表サブメッシュ) で対応付ける。
                // リスト順で対応付けると、グループ化の結果が両者でずれた場合に
                // 別マテリアルのテクスチャ同士を合成してしまう。
                var mirrorByIndex = new Dictionary<int, Texture2D>();
                foreach (var res in mirrorResult.SubMeshResults)
                {
                    mirrorByIndex[res.SubMeshIndex] = res.Texture;
                }

                foreach (var mainRes in bakeResult.SubMeshResults)
                {
                    if (mainRes.Texture == null) continue;
                    if (!mirrorByIndex.TryGetValue(mainRes.SubMeshIndex, out Texture2D mirrorTex)) continue;
                    if (mirrorTex == null) continue;

                    BlendTextures(mainRes.Texture, mirrorTex, settings.MirrorBlend);
                }

                // Cleanup mirror textures immediately as they are blended in
                foreach (var res in mirrorResult.SubMeshResults)
                {
                    if (res.Texture != null) Object.DestroyImmediate(res.Texture);
                }
            }
            // For single result
            else if (bakeResult.Texture != null)
            {
                var mirrorResult = Bake(settings, entry, ctx, true);
                if (mirrorResult != null && mirrorResult.Texture != null)
                {
                    BlendTextures(bakeResult.Texture, mirrorResult.Texture, settings.MirrorBlend);
                    Object.DestroyImmediate(mirrorResult.Texture);
                }
            }
        }

        private static void ApplyEdgePadding(BakeResult result, int paddingPixels)
        {
            if (paddingPixels <= 0 || result == null) return;

            if (result.SubMeshResults != null && result.SubMeshResults.Count > 0)
            {
                foreach (var subRes in result.SubMeshResults)
                {
                    if (subRes.Texture != null) EdgePadding.Apply(subRes.Texture, paddingPixels);
                }
            }
            else if (result.Texture != null)
            {
                EdgePadding.Apply(result.Texture, paddingPixels);
            }
        }

        /// <summary>
        /// Bakes a single mesh entry to texture(s)
        /// </summary>
        public BakeResult Bake(GradationSettings settings, MeshEntry entry, bool useMirror = false)
        {
            using (var ctx = BakeContext.Create(settings))
            {
                if (!ctx.IsValid)
                {
                    return new BakeResult
                    {
                        RendererName = entry.SourceRenderer != null ? entry.SourceRenderer.name : "Unknown",
                        SourceRenderer = entry.SourceRenderer,
                        SubMeshResults = new List<SubMeshResult>()
                    };
                }

                BakeResult result = Bake(settings, entry, ctx, useMirror);
                ApplyEdgePadding(result, settings.EdgePaddingPixels);
                return result;
            }
        }

        private BakeResult Bake(GradationSettings settings, MeshEntry entry, BakeContext ctx, bool useMirror)
        {
            BakeResult result = new BakeResult
            {
                RendererName = entry.SourceRenderer != null ? entry.SourceRenderer.name : "Unknown",
                SourceRenderer = entry.SourceRenderer,
                SubMeshResults = new List<SubMeshResult>()
            };

            Renderer renderer = entry.ActiveRenderer;
            if (renderer == null)
            {
                FileLogger.LogError("[GradationBaker] Renderer not found.");
                return result;
            }

            FileLogger.Log($"[GradationBaker] Starting Bake for {renderer.name} (mirror={useMirror}) split={entry.SplitByMaterial}...");

            Mesh mesh = GetMesh(renderer);
            if (mesh == null)
            {
                FileLogger.LogError("[GradationBaker] Mesh not found.");
                return result;
            }

            MeshReadWriteEnabler.EnsureReadWriteEnabled(mesh);

            Material mat = ctx.Material;

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
            mat.SetInt("_DitherMode", (int)settings.DitherMode);
            mat.SetFloat("_DitherIntensity", settings.DitherIntensity);

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

            // Clear color logic
            Color clearColor;
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

            // Setup RenderTexture
            // 例外が出ても RenderTexture.active を元に戻さないと以降のエディタ描画が壊れるため
            // 保存 → try/finally で復元する
            int res = settings.Resolution;
            RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = rt;

                // Material and Submesh Handling
                Material[] sharedMaterials = renderer.sharedMaterials;

                if (entry.SplitByMaterial)
                {
                    // 同じマテリアルが複数スロットに割り当てられている場合、
                    // そのマテリアルが参照できるテクスチャは 1 枚だけなので
                    // サブメッシュ単位ではなくマテリアル単位でまとめて 1 枚に焼く。
                    foreach (var group in BuildMaterialGroups(mesh, sharedMaterials, entry))
                    {
                        GL.Clear(true, true, clearColor);

                        if (mat.SetPass(0))
                        {
                            foreach (int subMeshIndex in group.SubMeshIndices)
                            {
                                Graphics.DrawMeshNow(mesh, Matrix4x4.identity, subMeshIndex);
                            }
                        }
                        else
                        {
                            FileLogger.LogError($"[GradationBaker] SetPass failed for material '{group.MaterialName}'.");
                        }

                        Texture2D subTex = new Texture2D(res, res, TextureFormat.ARGB32, false);
                        subTex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                        subTex.Apply();

                        result.SubMeshResults.Add(new SubMeshResult
                        {
                            Texture = subTex,
                            SubMeshIndex = group.SubMeshIndices[0],
                            MaterialName = group.MaterialName
                        });
                    }
                }
                else
                {
                    // Draw all enabled submeshes into a single texture
                    GL.Clear(true, true, clearColor);
                    if (mat.SetPass(0))
                    {
                        for (int i = 0; i < mesh.subMeshCount; i++)
                        {
                            if (!entry.IsMaterialSlotEnabled(i)) continue;
                            Graphics.DrawMeshNow(mesh, Matrix4x4.identity, i);
                        }
                    }

                    Texture2D mainTex = new Texture2D(res, res, TextureFormat.ARGB32, false);
                    mainTex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                    mainTex.Apply();

                    result.Texture = mainTex;
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
            }

            return result;
        }

        /// <summary>
        /// 同一マテリアルを共有するサブメッシュの集合。1 グループ = 出力テクスチャ 1 枚。
        /// </summary>
        private class MaterialGroup
        {
            public string MaterialName;
            public List<int> SubMeshIndices = new List<int>();
        }

        /// <summary>
        /// サブメッシュをマテリアル単位でグループ化する。
        ///
        /// Unity のマテリアルは 1 アセットにつきテクスチャ 1 枚しか参照できないため、
        /// 同じマテリアルが複数スロットに割り当てられている場合にサブメッシュ毎へ
        /// 分割して出力すると、どちらを割り当てても他方の UV アイランドが
        /// 背景色のまま残ってしまう。それを避けるためにここでまとめる。
        ///
        /// マテリアル未設定 (スロット数がサブメッシュ数より少ない場合を含む) の
        /// サブメッシュはまとめようがないので単独グループとして扱う。
        /// スロットの有効/無効はサブメッシュ単位で適用され、グループ内の全スロットが
        /// 無効ならそのマテリアルのテクスチャ自体が出力されない。
        /// </summary>
        private static List<MaterialGroup> BuildMaterialGroups(Mesh mesh, Material[] sharedMaterials, MeshEntry entry)
        {
            var groups = new List<MaterialGroup>();
            var groupByMaterial = new Dictionary<Material, MaterialGroup>();

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (!entry.IsMaterialSlotEnabled(i)) continue;

                Material material = (i < sharedMaterials.Length) ? sharedMaterials[i] : null;

                if (material == null)
                {
                    groups.Add(new MaterialGroup
                    {
                        MaterialName = $"Submesh{i}",
                        SubMeshIndices = { i }
                    });
                    continue;
                }

                if (groupByMaterial.TryGetValue(material, out MaterialGroup existing))
                {
                    existing.SubMeshIndices.Add(i);
                    continue;
                }

                var group = new MaterialGroup
                {
                    MaterialName = material.name,
                    SubMeshIndices = { i }
                };
                groupByMaterial.Add(material, group);
                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Blends two textures for mirror gradation overlay.
        /// Max: brighter value wins per channel. Min: darker value wins per channel.
        /// </summary>
        private void BlendTextures(Texture2D baseT, Texture2D overlayTex, MirrorBlendMode blendMode)
        {
            // Color32 (byte) で処理することで float 変換コストとメモリを削減する
            Color32[] basePixels = baseT.GetPixels32();
            Color32[] overlayPixels = overlayTex.GetPixels32();
            bool useMin = blendMode == MirrorBlendMode.Min;

            int count = Mathf.Min(basePixels.Length, overlayPixels.Length);
            for (int i = 0; i < count; i++)
            {
                Color32 baseC = basePixels[i];
                Color32 overC = overlayPixels[i];

                if (useMin)
                {
                    basePixels[i] = new Color32(
                        baseC.r < overC.r ? baseC.r : overC.r,
                        baseC.g < overC.g ? baseC.g : overC.g,
                        baseC.b < overC.b ? baseC.b : overC.b,
                        baseC.a < overC.a ? baseC.a : overC.a
                    );
                }
                else // Max (default)
                {
                    basePixels[i] = new Color32(
                        baseC.r > overC.r ? baseC.r : overC.r,
                        baseC.g > overC.g ? baseC.g : overC.g,
                        baseC.b > overC.b ? baseC.b : overC.b,
                        baseC.a > overC.a ? baseC.a : overC.a
                    );
                }
            }

            baseT.SetPixels32(basePixels);
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

        private static Texture2D CreateGradientLUT(Gradient gradient)
        {
            const int width = 256;
            Texture2D tex = new Texture2D(width, 1, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            // SetPixel を 256 回呼ぶより Color[] を一括転送する方が速い
            var pixels = new Color[width];
            for (int i = 0; i < width; i++)
            {
                pixels[i] = gradient.Evaluate(i / (float)(width - 1));
            }
            tex.SetPixels(pixels);
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
