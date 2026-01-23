using UnityEngine;
using UnityEditor;
using GradationTextureGenerator.Data;

namespace GradationTextureGenerator.UI
{
    public class GradationSceneHandle
    {
        private const string PROXY_NAME = "Gradation Handle [Temp]";
        private const string MIN_HANDLE_NAME = "Min Range";
        private const string MAX_HANDLE_NAME = "Max Range";
        
        private GameObject _proxyObject;
        private GameObject _minHandle;
        private GameObject _maxHandle;
        
        public void DrawHandle(Vector3 center, GradationSettings settings, Transform targetTransform)
        {
            Vector3 dir = settings.GradientDirection.normalized;
            Vector3 objectOrigin = targetTransform != null ? targetTransform.position : center;
            
            // Ensure Proxy Object exists
            if (_proxyObject == null)
            {
                _proxyObject = GameObject.Find(PROXY_NAME);
                
                if (_proxyObject == null)
                {
                    _proxyObject = new GameObject(PROXY_NAME);
                    _proxyObject.hideFlags = HideFlags.DontSave;
                }
            }
            
            // Ensure Min Handle exists (child of proxy)
            if (_minHandle == null)
            {
                var existing = _proxyObject.transform.Find(MIN_HANDLE_NAME);
                if (existing != null)
                {
                    _minHandle = existing.gameObject;
                }
                else
                {
                    _minHandle = new GameObject(MIN_HANDLE_NAME);
                    _minHandle.transform.SetParent(_proxyObject.transform);
                    _minHandle.hideFlags = HideFlags.DontSave;
                }
            }
            
            // Ensure Max Handle exists (child of proxy)
            if (_maxHandle == null)
            {
                var existing = _proxyObject.transform.Find(MAX_HANDLE_NAME);
                if (existing != null)
                {
                    _maxHandle = existing.gameObject;
                }
                else
                {
                    _maxHandle = new GameObject(MAX_HANDLE_NAME);
                    _maxHandle.transform.SetParent(_proxyObject.transform);
                    _maxHandle.hideFlags = HideFlags.DontSave;
                }
            }

            // === Sync Proxy -> Settings (User moved handles in Scene) ===
            
            // Direction from proxy rotation
            if (_proxyObject.transform.hasChanged)
            {
                settings.GradientDirection = _proxyObject.transform.up;
                _proxyObject.transform.hasChanged = false;
            }
            
            // Min Range from min handle position
            if (_minHandle.transform.hasChanged)
            {
                Vector3 minWorldPos = _minHandle.transform.position;
                Vector3 delta = minWorldPos - objectOrigin;
                settings.MinRange = Vector3.Dot(delta, dir);
                _minHandle.transform.hasChanged = false;
            }
            
            // Max Range from max handle position
            if (_maxHandle.transform.hasChanged)
            {
                Vector3 maxWorldPos = _maxHandle.transform.position;
                Vector3 delta = maxWorldPos - objectOrigin;
                settings.MaxRange = Vector3.Dot(delta, dir);
                _maxHandle.transform.hasChanged = false;
            }
            
            // === Sync Settings -> Proxy (UI changed or first run) ===
            
            // Proxy position and rotation
            _proxyObject.transform.position = center;
            Vector3 currentUp = _proxyObject.transform.up;
            if (Vector3.Dot(currentUp, dir) < 0.999f)
            {
                _proxyObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
            
            // Min/Max handle positions (only if not being dragged)
            Vector3 expectedMinPos = objectOrigin + dir * settings.MinRange;
            Vector3 expectedMaxPos = objectOrigin + dir * settings.MaxRange;
            
            if (!_minHandle.transform.hasChanged)
            {
                _minHandle.transform.position = expectedMinPos;
            }
            if (!_maxHandle.transform.hasChanged)
            {
                _maxHandle.transform.position = expectedMaxPos;
            }
            
            // === Draw Visualization ===
            float size = HandleUtility.GetHandleSize(center);
            
            // Direction Arrow
            Handles.color = Color.cyan;
            Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(dir), size * 1.5f, EventType.Repaint);
            
            // Visual discs at min/max positions
            Vector3 minPos = _minHandle.transform.position;
            Vector3 maxPos = _maxHandle.transform.position;
            
            Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.4f);
            Handles.DrawSolidDisc(minPos, dir, size * 0.3f);
            Handles.color = new Color(0.8f, 0.2f, 0.2f, 0.4f);
            Handles.DrawSolidDisc(maxPos, dir, size * 0.3f);
            
            // Line connecting min to max
            Handles.color = Color.yellow;
            Handles.DrawLine(minPos, maxPos);
            
            // Labels
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.green;
            Handles.Label(minPos + Vector3.up * size * 0.25f, $"Min: {settings.MinRange:F2}", style);
            style.normal.textColor = Color.red;
            Handles.Label(maxPos + Vector3.up * size * 0.25f, $"Max: {settings.MaxRange:F2}", style);
        }

        public void Cleanup()
        {
            if (_minHandle != null) Object.DestroyImmediate(_minHandle);
            if (_maxHandle != null) Object.DestroyImmediate(_maxHandle);
            if (_proxyObject != null) Object.DestroyImmediate(_proxyObject);
            
            _minHandle = null;
            _maxHandle = null;
            _proxyObject = null;
            
            // Double check search
            var obj = GameObject.Find(PROXY_NAME);
            if (obj != null) Object.DestroyImmediate(obj);
        }
    }
}

