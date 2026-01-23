using UnityEngine;
using UnityEditor;
using GradationTextureGenerator.Data;

namespace GradationTextureGenerator.UI
{
    public class GradationSceneHandle
    {
        private const string PROXY_NAME = "Gradation Handle [Temp]";
        private GameObject _proxyObject;
        
        public void DrawHandle(Vector3 center, GradationSettings settings)
        {
            // Ensure Proxy Object exists
            if (_proxyObject == null)
            {
                // Try find existing
                _proxyObject = GameObject.Find(PROXY_NAME);
                
                if (_proxyObject == null)
                {
                    _proxyObject = new GameObject(PROXY_NAME);
                    _proxyObject.hideFlags = HideFlags.DontSave; // Don't save to scene
                }
            }

            // Sync Proxy -> Settings (User moved handle)
            if (_proxyObject.transform.hasChanged)
            {
                 // We use the proxy's Up vector as direction
                 settings.GradientDirection = _proxyObject.transform.up;
                 
                 // Reset flag
                 _proxyObject.transform.hasChanged = false;
            }
            
            // Sync Settings -> Proxy (User changed settings UI or first run)
            // But we must be careful not to overwrite user's handle movement immediately if we just synced from it.
            // Check if direction is significantly different from proxy up
            Vector3 currentUp = _proxyObject.transform.up;
            if (Vector3.Dot(currentUp, settings.GradientDirection) < 0.999f)
            {
                // Align proxy to direction
                _proxyObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, settings.GradientDirection);
            }
            
            // Always keep proxy at center of mesh (or let user move it? Start with center)
            _proxyObject.transform.position = center;
            
            // Draw Visualization (Visual feedback of the direction)
            Handles.color = Color.cyan;
            float size = HandleUtility.GetHandleSize(center);
            Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(settings.GradientDirection), size * 1.5f, EventType.Repaint);
        }

        public void Cleanup()
        {
            if (_proxyObject != null)
            {
                Object.DestroyImmediate(_proxyObject);
                _proxyObject = null;
            }
            
            // Double check search
            var obj = GameObject.Find(PROXY_NAME);
            if (obj != null) Object.DestroyImmediate(obj);
        }
    }
}
