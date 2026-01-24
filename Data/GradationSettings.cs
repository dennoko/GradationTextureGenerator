using UnityEngine;
using System.Collections.Generic;

namespace GradationTextureGenerator.Data
{
    [System.Serializable]
    public class MeshEntry
    {
        public Renderer SourceRenderer;
        public GameObject WorkMeshObject;
        
        public Renderer ActiveRenderer => WorkMeshObject != null 
            ? WorkMeshObject.GetComponent<Renderer>() 
            : SourceRenderer;
            
        public bool HasWorkMesh => WorkMeshObject != null;
    }

    [System.Serializable]
    public class GradationSettings
    {
        // Multiple mesh support
        public List<MeshEntry> MeshEntries = new List<MeshEntry>();
        
        public Gradient Gradient = new Gradient();
        public Texture2D MaskTexture;
        public bool UseVertexColorMask = false;
        public bool InvertMask = false;
        public int Resolution = 1024;
        
        // UV Channel selection (0-3)
        public int UVChannel = 0;
        
        // Cube-based gradation control
        public Vector3 BoxCenter = Vector3.zero;
        public Quaternion BoxRotation = Quaternion.identity;
        public float BoxHeight = 1f;
        
        // Fixed visual dimensions for the cube handle
        public const float BoxWidth = 0.5f;
        public const float BoxDepth = 0.5f;

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
        /// Gets the first valid renderer for Box fitting
        /// </summary>
        public Renderer GetPrimaryRenderer()
        {
            foreach (var entry in MeshEntries)
            {
                if (entry.ActiveRenderer != null)
                    return entry.ActiveRenderer;
            }
            return null;
        }
        
        /// <summary>
        /// Fits the box to the combined mesh bounds of all entries
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
            
            foreach (var v in vertices)
            {
                Vector3 worldV = transform != null ? transform.TransformPoint(v) : v;
                worldCenter += worldV;
                float t = Vector3.Dot(worldV, dir);
                if (t < min) min = t;
                if (t > max) max = t;
            }
            worldCenter /= vertices.Length;
            
            if (Mathf.Abs(max - min) < 0.0001f) max = min + 1.0f;
            
            BoxHeight = max - min;
            
            float midT = (min + max) / 2f;
            BoxCenter = dir * midT;
            
            Vector3 projectedCenter = Vector3.Project(worldCenter, dir);
            Vector3 perpendicularOffset = worldCenter - projectedCenter;
            BoxCenter = dir * midT + perpendicularOffset;
        }

        /// <summary>
        /// Fits box to all mesh entries combined bounds
        /// </summary>
        public void FitToAllMeshBounds()
        {
            if (MeshEntries.Count == 0) return;
            
            Vector3 dir = GradientDirection.normalized;
            float globalMin = float.MaxValue;
            float globalMax = float.MinValue;
            Vector3 globalCenter = Vector3.zero;
            int totalVertices = 0;
            
            foreach (var entry in MeshEntries)
            {
                Renderer renderer = entry.ActiveRenderer;
                if (renderer == null) continue;
                
                Mesh mesh = GetMesh(renderer);
                if (mesh == null) continue;
                
                Vector3[] vertices = mesh.vertices;
                Transform transform = renderer.transform;
                
                foreach (var v in vertices)
                {
                    Vector3 worldV = transform.TransformPoint(v);
                    globalCenter += worldV;
                    totalVertices++;
                    
                    float t = Vector3.Dot(worldV, dir);
                    if (t < globalMin) globalMin = t;
                    if (t > globalMax) globalMax = t;
                }
            }
            
            if (totalVertices == 0) return;
            globalCenter /= totalVertices;
            
            if (Mathf.Abs(globalMax - globalMin) < 0.0001f) globalMax = globalMin + 1.0f;
            
            BoxHeight = globalMax - globalMin;
            
            float midT = (globalMin + globalMax) / 2f;
            Vector3 projectedCenter = Vector3.Project(globalCenter, dir);
            Vector3 perpendicularOffset = globalCenter - projectedCenter;
            BoxCenter = dir * midT + perpendicularOffset;
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (renderer is MeshRenderer mr)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                return mf ? mf.sharedMesh : null;
            }
            return null;
        }
    }
}
