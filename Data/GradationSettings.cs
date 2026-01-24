using UnityEngine;

namespace GradationTextureGenerator.Data
{
    [System.Serializable]
    public class GradationSettings
    {
        public Renderer TargetRenderer;
        public Gradient Gradient = new Gradient();
        public Texture2D MaskTexture;
        public bool UseVertexColorMask = false;
        public bool InvertMask = false;
        public int Resolution = 1024;
        
        // Cube-based gradation control
        public Vector3 BoxCenter = Vector3.zero;
        public Quaternion BoxRotation = Quaternion.identity;
        public float BoxHeight = 1f;
        
        // Fixed visual dimensions for the cube handle
        public const float BoxWidth = 0.5f;
        public const float BoxDepth = 0.5f;

        // AutoNormalize - fits the box to mesh bounds
        public bool AutoNormalize = true;

        // Computed properties for shader compatibility
        public Vector3 GradientDirection => BoxRotation * Vector3.up;
        
        // Min/Max in world space along the gradient direction from box center
        public float MinRange => Vector3.Dot(BoxCenter - BoxRotation * Vector3.up * (BoxHeight / 2f), GradientDirection);
        public float MaxRange => Vector3.Dot(BoxCenter + BoxRotation * Vector3.up * (BoxHeight / 2f), GradientDirection);

        // Preview Settings
        public bool IsToolActive = true;
        public float PreviewOpacity = 0.5f;

        public string SavePath = "Assets/";
        
        /// <summary>
        /// Fits the box to the mesh bounds along the current gradient direction
        /// </summary>
        public void FitToMeshBounds(Mesh mesh, Transform transform)
        {
            if (mesh == null) return;
            
            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0) return;
            
            Vector3 dir = GradientDirection.normalized;
            
            float min = float.MaxValue;
            float max = float.MinValue;
            Vector3 worldCenter = Vector3.zero;
            
            // Calculate bounds in object space along direction
            foreach (var v in vertices)
            {
                Vector3 worldV = transform != null ? transform.TransformPoint(v) : v;
                worldCenter += worldV;
                float t = Vector3.Dot(worldV, dir);
                if (t < min) min = t;
                if (t > max) max = t;
            }
            worldCenter /= vertices.Length;
            
            // Prevent zero height
            if (Mathf.Abs(max - min) < 0.0001f) max = min + 1.0f;
            
            // Set box height and center
            BoxHeight = max - min;
            
            // Position box center at the midpoint along the direction
            float midT = (min + max) / 2f;
            BoxCenter = dir * midT;
            
            // Adjust center to be on the mesh center projected onto the gradient axis
            Vector3 projectedCenter = Vector3.Project(worldCenter, dir);
            Vector3 perpendicularOffset = worldCenter - projectedCenter;
            BoxCenter = dir * midT + perpendicularOffset;
        }
    }
}
