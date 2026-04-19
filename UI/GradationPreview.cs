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
            _previewMaterial.SetTexture("_MainTex", _lutTexture);
            _previewMaterial.SetMatrix("_WorldToBox", worldToBox);
            _previewMaterial.SetFloat("_BoxHeight", settings.BoxHeight);
            _previewMaterial.SetInt("_Shape", (int)settings.Shape);
            _previewMaterial.SetInt("_BlendMode", (int)settings.BlendMode);
            
            _previewMaterial.SetInt("_UseMirror", isMirrorEnabled ? 1 : 0);
            _previewMaterial.SetMatrix("_WorldToBoxMirror", worldToBoxMirror);
            _previewMaterial.SetInt("_MirrorBlendMode", (int)settings.MirrorBlend);

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

            // Update proxy materials per slot (enabled → preview, disabled → transparent)
            int slotCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            var proxyMats = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                proxyMats[i] = entry.IsMaterialSlotEnabled(i) ? _previewMaterial : _disabledMaterial;

            // Use MaterialPropertyBlock for per-mesh settings
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetInt("_UVChannel", entry.UVChannel);
            if (entry.MaskTexture != null)
            {
                block.SetTexture("_MaskTex", entry.MaskTexture);
                block.SetInt("_UseMaskTexture", 1);
            }
            else
            {
                block.SetInt("_UseMaskTexture", 0);
            }
            block.SetInt("_UseVertexColorMask", entry.UseVertexColorMask ? 1 : 0);
            block.SetInt("_InvertMask", entry.InvertMask ? 1 : 0);

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
            if (_lutTexture == null)
            {
                _lutTexture = new Texture2D(256, 1, TextureFormat.ARGB32, false);
                _lutTexture.wrapMode = TextureWrapMode.Clamp;
                _lutTexture.hideFlags = HideFlags.HideAndDontSave;
            }
            
            for (int i = 0; i < 256; i++)
            {
                _lutTexture.SetPixel(i, 0, gradient.Evaluate((float)i / 255f));
            }
            _lutTexture.Apply();
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
