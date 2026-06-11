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

        private class ProxyEntry
        {
            public GameObject ProxyObject;
            public MeshFilter MeshFilter;
            public MeshRenderer MeshRenderer;
            public SkinnedMeshRenderer SkinnedRenderer;
        }

        private readonly Dictionary<Renderer, ProxyEntry> _proxies = new Dictionary<Renderer, ProxyEntry>();

        public void UpdatePreview(GradationSettings settings, MeshEntry entry)
        {
            Renderer renderer = entry.ActiveRenderer;
            if (renderer == null) return;

            // Lazy Init Material
            if (_previewMaterial == null)
            {
                Shader shader = Shader.Find(ShaderPath);
                if (shader == null) return;
                _previewMaterial = new Material(shader);
                _previewMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_disabledMaterial == null)
            {
                _disabledMaterial = new Material(Shader.Find("Sprites/Default"));
                _disabledMaterial.color = Color.clear;
                _disabledMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            // Update LUT
            UpdateLUT(settings.Gradient);

            // Calculate Main Matrix
            Matrix4x4 boxMatrix = Matrix4x4.TRS(
                settings.BoxCenter,
                settings.BoxRotation,
                settings.BoxScale
            );
            Matrix4x4 worldToBox = boxMatrix.inverse;

            // Calculate Mirror Matrix
            bool isMirrorEnabled = settings.UseMirror && settings.MirrorAxis != MirrorAxis.None;
            Matrix4x4 worldToBoxMirror = Matrix4x4.identity;

            if (isMirrorEnabled)
            {
                var (mirrorCenter, mirrorRot) = settings.GetMirroredBox(renderer.transform);
                Matrix4x4 mirrorBoxMatrix = Matrix4x4.TRS(
                    mirrorCenter,
                    mirrorRot,
                    settings.BoxScale
                );
                worldToBoxMirror = mirrorBoxMatrix.inverse;
            }

            // Set global shader properties on Material
            _previewMaterial.SetTexture(PropMainTex, _lutTexture);
            _previewMaterial.SetMatrix(PropWorldToBox, worldToBox);
            _previewMaterial.SetFloat(PropBoxHeight, settings.BoxHeight);
            _previewMaterial.SetInt(PropShape, (int)settings.Shape);
            _previewMaterial.SetInt(PropBlendMode, (int)settings.BlendMode);

            _previewMaterial.SetInt(PropUseMirror, isMirrorEnabled ? 1 : 0);
            _previewMaterial.SetMatrix(PropWorldToBoxMirror, worldToBoxMirror);
            _previewMaterial.SetInt(PropMirrorBlendMode, (int)settings.MirrorBlend);

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

            // Update proxy materials per slot (enabled → preview, disabled → transparent)
            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            var proxyMats = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                proxyMats[i] = entry.IsMaterialSlotEnabled(i) ? _previewMaterial : _disabledMaterial;

            // Use MaterialPropertyBlock for per-mesh settings
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

            if (proxy.SkinnedRenderer != null)
            {
                proxy.SkinnedRenderer.sharedMaterials = proxyMats;
                proxy.SkinnedRenderer.SetPropertyBlock(block);
            }
            else if (proxy.MeshRenderer != null)
            {
                proxy.MeshRenderer.sharedMaterials = proxyMats;
                proxy.MeshRenderer.SetPropertyBlock(block);
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
                var smrMats = new Material[Mathf.Max(1, smr.sharedMaterials.Length)];
                for (int i = 0; i < smrMats.Length; i++) smrMats[i] = _previewMaterial;
                newSmr.sharedMaterials = smrMats;
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
                var mrMats = new Material[Mathf.Max(1, mr.sharedMaterials.Length)];
                for (int i = 0; i < mrMats.Length; i++) mrMats[i] = _previewMaterial;
                proxy.MeshRenderer.sharedMaterials = mrMats;
            }
            else
            {
                Object.DestroyImmediate(go);
                return null;
            }

            return proxy;
        }

        public void UpdatePreviewAll(GradationSettings settings)
        {
            // Remove unused proxies
            var validRenderers = new HashSet<Renderer>();
            foreach (var entry in settings.MeshEntries)
            {
                if (entry.ActiveRenderer != null) validRenderers.Add(entry.ActiveRenderer);
            }

            var toRemove = new List<Renderer>();
            foreach (var renderer in _proxies.Keys)
            {
                if (!validRenderers.Contains(renderer)) toRemove.Add(renderer);
            }
            foreach (var renderer in toRemove)
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
                UpdatePreview(settings, entry);
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
