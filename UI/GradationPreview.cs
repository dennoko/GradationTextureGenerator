using UnityEngine;
using UnityEditor;
using GradationBaker.Data;
using System.Collections.Generic;

namespace GradationBaker.UI
{
    public class GradationPreview
    {
        private const string ShaderPath = "Hidden/GradationBaker/Preview";
        private Material _previewMaterial;
        private Material _disabledMaterial;
        private Texture2D _lutTexture;

        // SceneView イベント毎の再生成を避けるためのキャッシュ。
        // MaterialPropertyBlock はネイティブ生成を伴うため、ScriptableObject の
        // コンストラクタ経由 (EditorWindow のフィールドイニシャライザ) では生成できない。
        // 初回使用時に遅延生成する。
        private MaterialPropertyBlock _propertyBlock;
        private readonly Color[] _lutPixels = new Color[LutWidth];
        private int _lastGradientHash;
        private const int LutWidth = 256;

        // Shader.PropertyToID は文字列ルックアップより高速
        private static readonly int PropMainTex           = Shader.PropertyToID("_MainTex");
        private static readonly int PropWorldToBox        = Shader.PropertyToID("_WorldToBox");
        private static readonly int PropBoxHeight         = Shader.PropertyToID("_BoxHeight");
        private static readonly int PropShape             = Shader.PropertyToID("_Shape");
        private static readonly int PropBlendMode         = Shader.PropertyToID("_BlendMode");
        private static readonly int PropUseMirror         = Shader.PropertyToID("_UseMirror");
        private static readonly int PropWorldToBoxMirror  = Shader.PropertyToID("_WorldToBoxMirror");
        private static readonly int PropMirrorBlendMode   = Shader.PropertyToID("_MirrorBlendMode");
        private static readonly int PropUVChannel         = Shader.PropertyToID("_UVChannel");
        private static readonly int PropMaskTex           = Shader.PropertyToID("_MaskTex");
        private static readonly int PropUseMaskTexture    = Shader.PropertyToID("_UseMaskTexture");
        private static readonly int PropUseVertexColorMask = Shader.PropertyToID("_UseVertexColorMask");
        private static readonly int PropInvertMask        = Shader.PropertyToID("_InvertMask");
        private static readonly int PropDitherMode        = Shader.PropertyToID("_DitherMode");
        private static readonly int PropDitherIntensity   = Shader.PropertyToID("_DitherIntensity");

        private class ProxyEntry
        {
            public GameObject ProxyObject;
            public MeshFilter MeshFilter;
            public MeshRenderer MeshRenderer;
            public SkinnedMeshRenderer SkinnedRenderer;

            // マテリアル配列とプロパティブロックの再設定はネイティブ呼び出しを伴うため、
            // 実際に内容が変わったときだけ行う。
            public Material[] AssignedMaterials;
            public int LastMaskStateHash = int.MinValue;
            public bool HasBlockState;
            public bool LastUseMirror;
            public Matrix4x4 LastWorldToBoxMirror;

            // ?? は Unity の破棄済みオブジェクト (fake null) を拾えないため明示的に比較する
            public Renderer Renderer => SkinnedRenderer != null ? (Renderer)SkinnedRenderer : MeshRenderer;
        }

        private readonly Dictionary<Renderer, ProxyEntry> _proxies = new Dictionary<Renderer, ProxyEntry>();

        // UpdatePreviewAll の毎イベント確保を避けるための再利用バッファ
        private readonly HashSet<Renderer> _validRenderers = new HashSet<Renderer>();
        private readonly List<Renderer> _proxiesToRemove = new List<Renderer>();

        /// <summary>
        /// 全メッシュ共通のシェーダープロパティ (Box 行列 / シェイプ / ディザ等) を更新する。
        /// エントリ毎ではなく 1 フレームに 1 回で足りる。
        /// ミラー行列はメッシュの Transform 基準で決まるため、ここではなく
        /// エントリ毎の MaterialPropertyBlock で渡す (ベイク側と同じ基準にするため)。
        /// </summary>
        private bool UpdateSharedState(GradationSettings settings)
        {
            // Lazy Init Material
            if (_previewMaterial == null)
            {
                Shader shader = Shader.Find(ShaderPath);
                if (shader == null) return false;
                _previewMaterial = new Material(shader);
                _previewMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_disabledMaterial == null)
            {
                _disabledMaterial = new Material(Shader.Find("Sprites/Default"));
                _disabledMaterial.color = Color.clear;
                _disabledMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            UpdateLUT(settings.Gradient);

            Matrix4x4 boxMatrix = Matrix4x4.TRS(
                settings.BoxCenter,
                settings.BoxRotation,
                settings.BoxScale
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;

            _previewMaterial.SetTexture(PropMainTex, _lutTexture);
            _previewMaterial.SetMatrix(PropWorldToBox, worldToBox);
            _previewMaterial.SetFloat(PropBoxHeight, settings.BoxHeight);
            _previewMaterial.SetInt(PropShape, (int)settings.Shape);
            _previewMaterial.SetInt(PropBlendMode, (int)settings.BlendMode);
            _previewMaterial.SetInt(PropMirrorBlendMode, (int)settings.MirrorBlend);

            _previewMaterial.SetInt(PropDitherMode, (int)settings.DitherMode);
            _previewMaterial.SetFloat(PropDitherIntensity, settings.DitherIntensity);

            return true;
        }

        public void UpdatePreview(GradationSettings settings, MeshEntry entry)
        {
            if (!UpdateSharedState(settings)) return;
            UpdateProxy(settings, entry);
        }

        private void UpdateProxy(GradationSettings settings, MeshEntry entry)
        {
            Renderer renderer = entry.ActiveRenderer;
            if (renderer == null) return;

            // Fetch or create Proxy
            if (!_proxies.TryGetValue(renderer, out var proxy) || proxy.ProxyObject == null)
            {
                proxy = CreateProxy(renderer);
                if (proxy == null) return;
                _proxies[renderer] = proxy;
            }

            // Sync Transform for MeshRenderer only (Skinned is child)
            if (proxy.MeshRenderer != null)
            {
                proxy.ProxyObject.transform.position = renderer.transform.position;
                proxy.ProxyObject.transform.rotation = renderer.transform.rotation;
                proxy.ProxyObject.transform.localScale = renderer.transform.lossyScale;
            }

            // SkinnedMesh はブレンドシェイプの値も元レンダラーに同期させる
            if (proxy.SkinnedRenderer != null && renderer is SkinnedMeshRenderer sourceSmr)
            {
                SyncBlendShapes(sourceSmr, proxy.SkinnedRenderer);
            }

            Renderer proxyRenderer = proxy.Renderer;
            if (proxyRenderer == null) return;

            UpdateProxyMaterials(entry, renderer, proxy, proxyRenderer);
            UpdateProxyPropertyBlock(settings, entry, renderer, proxy, proxyRenderer);
        }

        /// <summary>
        /// スロットの有効/無効に応じてプレビュー用/透明マテリアルを割り当てる。
        /// sharedMaterials への代入はネイティブ側のコストがあるため、内容が変わったときだけ行う。
        /// </summary>
        private void UpdateProxyMaterials(MeshEntry entry, Renderer source, ProxyEntry proxy, Renderer proxyRenderer)
        {
            int slotCount = Mathf.Max(1, source.sharedMaterials.Length);

            Material[] mats = proxy.AssignedMaterials;
            bool changed = mats == null || mats.Length != slotCount;
            if (changed) mats = new Material[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                Material desired = entry.IsMaterialSlotEnabled(i) ? _previewMaterial : _disabledMaterial;
                if (mats[i] != desired)
                {
                    mats[i] = desired;
                    changed = true;
                }
            }

            if (!changed) return;

            proxy.AssignedMaterials = mats;
            proxyRenderer.sharedMaterials = mats;
        }

        /// <summary>
        /// メッシュ毎のマスク設定とミラー行列を MaterialPropertyBlock で渡す。
        /// 内容が変わったときだけ再設定する。
        /// </summary>
        private void UpdateProxyPropertyBlock(GradationSettings settings, MeshEntry entry, Renderer source,
                                              ProxyEntry proxy, Renderer proxyRenderer)
        {
            bool useMirror = settings.UseMirror && settings.MirrorAxis != MirrorAxis.None;

            // ミラーの基準はベイクと同じくそのメッシュ自身の Transform
            Matrix4x4 worldToBoxMirror = Matrix4x4.identity;
            if (useMirror)
            {
                var (mirrorCenter, mirrorRot) = settings.GetMirroredBox(source.transform);
                worldToBoxMirror = Matrix4x4.TRS(mirrorCenter, mirrorRot, settings.BoxScale).inverse;
            }

            int hash = ComputeMaskStateHash(entry);
            if (proxy.HasBlockState &&
                hash == proxy.LastMaskStateHash &&
                useMirror == proxy.LastUseMirror &&
                worldToBoxMirror == proxy.LastWorldToBoxMirror)
            {
                return;
            }

            proxy.HasBlockState = true;
            proxy.LastMaskStateHash = hash;
            proxy.LastUseMirror = useMirror;
            proxy.LastWorldToBoxMirror = worldToBoxMirror;

            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            MaterialPropertyBlock block = _propertyBlock;
            block.Clear();

            block.SetInt(PropUVChannel, entry.UVChannel);
            if (entry.MaskTexture != null)
            {
                block.SetTexture(PropMaskTex, entry.MaskTexture);
                block.SetInt(PropUseMaskTexture, 1);
            }
            else
            {
                block.SetInt(PropUseMaskTexture, 0);
            }
            block.SetInt(PropUseVertexColorMask, entry.UseVertexColorMask ? 1 : 0);
            block.SetInt(PropInvertMask, entry.InvertMask ? 1 : 0);

            block.SetInt(PropUseMirror, useMirror ? 1 : 0);
            block.SetMatrix(PropWorldToBoxMirror, worldToBoxMirror);

            proxyRenderer.SetPropertyBlock(block);
        }

        private static int ComputeMaskStateHash(MeshEntry entry)
        {
            unchecked
            {
                int hash = entry.UVChannel;
                hash = hash * 31 + (entry.MaskTexture != null ? entry.MaskTexture.GetInstanceID() : 0);
                hash = hash * 31 + (entry.UseVertexColorMask ? 1 : 0);
                hash = hash * 31 + (entry.InvertMask ? 1 : 0);
                return hash;
            }
        }

        private static void SyncBlendShapes(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
        {
            Mesh mesh = source.sharedMesh;
            if (mesh == null || target.sharedMesh != mesh) return;

            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                float weight = source.GetBlendShapeWeight(i);
                if (!Mathf.Approximately(target.GetBlendShapeWeight(i), weight))
                    target.SetBlendShapeWeight(i, weight);
            }
        }

        private ProxyEntry CreateProxy(Renderer target)
        {
            GameObject go = new GameObject("GradationPreviewProxy_" + target.name);
            go.hideFlags = HideFlags.HideAndDontSave;
            // Raycast等の邪魔にならないよう Ignore Raycast(2) にする
            go.layer = 2;

            var proxy = new ProxyEntry { ProxyObject = go };

            if (target is SkinnedMeshRenderer smr)
            {
                // SkinnedMeshの場合はターゲットの子にしてTransformを一致させ、Bonesをコピーする
                go.transform.SetParent(target.transform.parent, false);
                go.transform.localPosition = target.transform.localPosition;
                go.transform.localRotation = target.transform.localRotation;
                go.transform.localScale = target.transform.localScale;

                var newSmr = go.AddComponent<SkinnedMeshRenderer>();
                newSmr.sharedMesh = smr.sharedMesh;
                newSmr.bones = smr.bones;
                newSmr.rootBone = smr.rootBone;
                newSmr.updateWhenOffscreen = true;
                proxy.SkinnedRenderer = newSmr;

                SyncBlendShapes(smr, newSmr);
            }
            else if (target is MeshRenderer mr)
            {
                // 通常Meshの場合はワールド座標で同期するため親は設定不要か、もしくは同じようにする
                go.transform.position = target.transform.position;
                go.transform.rotation = target.transform.rotation;
                go.transform.localScale = target.transform.lossyScale;

                proxy.MeshFilter = go.AddComponent<MeshFilter>();
                var sourceMf = target.GetComponent<MeshFilter>();
                if (sourceMf != null) proxy.MeshFilter.sharedMesh = sourceMf.sharedMesh;

                proxy.MeshRenderer = go.AddComponent<MeshRenderer>();
            }
            else
            {
                Object.DestroyImmediate(go);
                return null;
            }

            // マテリアル割り当ては UpdateProxyMaterials が行う (AssignedMaterials == null なので初回で必ず走る)
            return proxy;
        }

        public void UpdatePreviewAll(GradationSettings settings)
        {
            if (!UpdateSharedState(settings)) return;

            // Remove unused proxies
            _validRenderers.Clear();
            foreach (var entry in settings.MeshEntries)
            {
                Renderer active = entry.ActiveRenderer;
                if (active != null) _validRenderers.Add(active);
            }

            _proxiesToRemove.Clear();
            foreach (var renderer in _proxies.Keys)
            {
                if (!_validRenderers.Contains(renderer)) _proxiesToRemove.Add(renderer);
            }
            foreach (var renderer in _proxiesToRemove)
            {
                if (_proxies[renderer].ProxyObject != null)
                {
                    Object.DestroyImmediate(_proxies[renderer].ProxyObject);
                }
                _proxies.Remove(renderer);
            }

            // Update all valid entries
            foreach (var entry in settings.MeshEntries)
            {
                UpdateProxy(settings, entry);
            }
        }

        public void ClearProxies()
        {
            foreach (var proxy in _proxies.Values)
            {
                if (proxy.ProxyObject != null) Object.DestroyImmediate(proxy.ProxyObject);
            }
            _proxies.Clear();
        }

        private void UpdateLUT(Gradient gradient)
        {
            bool created = false;
            if (_lutTexture == null)
            {
                _lutTexture = new Texture2D(LutWidth, 1, TextureFormat.ARGB32, false);
                _lutTexture.wrapMode = TextureWrapMode.Clamp;
                _lutTexture.hideFlags = HideFlags.HideAndDontSave;
                created = true;
            }

            // グラデーションが変わっていなければ再生成しない
            // (SceneView イベント毎に 256 回の Evaluate + GPU 転送が走るのを防ぐ)
            int hash = ComputeGradientHash(gradient);
            if (!created && hash == _lastGradientHash) return;
            _lastGradientHash = hash;

            for (int i = 0; i < LutWidth; i++)
            {
                _lutPixels[i] = gradient.Evaluate(i / (float)(LutWidth - 1));
            }
            _lutTexture.SetPixels(_lutPixels);
            _lutTexture.Apply();
        }

        private static int ComputeGradientHash(Gradient gradient)
        {
            unchecked
            {
                int hash = (int)gradient.mode;
                foreach (var key in gradient.colorKeys)
                {
                    hash = hash * 31 + key.color.GetHashCode();
                    hash = hash * 31 + key.time.GetHashCode();
                }
                foreach (var key in gradient.alphaKeys)
                {
                    hash = hash * 31 + key.alpha.GetHashCode();
                    hash = hash * 31 + key.time.GetHashCode();
                }
                return hash;
            }
        }

        public void Cleanup()
        {
            ClearProxies();

            if (_previewMaterial != null) Object.DestroyImmediate(_previewMaterial);
            if (_disabledMaterial != null) Object.DestroyImmediate(_disabledMaterial);
            if (_lutTexture != null) Object.DestroyImmediate(_lutTexture);
        }
    }
}
