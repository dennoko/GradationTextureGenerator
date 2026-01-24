using UnityEngine;
using System.Collections.Generic;

namespace GradationBaker.Data
{
    public enum MirrorAxis
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 3
    }

    public enum BackgroundColor
    {
        Transparent = 0,
        White = 1,
        Black = 2
    }

    [System.Serializable]
    public class MeshEntry
    {
        public Renderer SourceRenderer;
        public GameObject WorkMeshObject;
        
        // Per-mesh settings
        public int UVChannel = 0;
        public Texture2D MaskTexture;
        public bool UseVertexColorMask = false;
        public bool InvertMask = false;
        
        // UI state
        public bool ShowDetails = false;
        
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
        public int Resolution = 2048;
        
        // UV Channel selection (0-3)
        public int UVChannel = 0;
        
        // Mirror settings
        public bool UseMirror = false;
        public MirrorAxis MirrorAxis = MirrorAxis.X;
        
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

        // Output Settings
        public string SavePath = "Assets/GeneratedGradation/output/";
        public bool UseTextureFolder = true; // Output to material's main texture folder
        public BackgroundColor BgColor = BackgroundColor.Transparent;

        /// <summary>
        /// Gets mirrored box settings
        /// </summary>
        public (Vector3 center, Quaternion rotation) GetMirroredBox(Transform meshTransform)
        {
            if (!UseMirror || MirrorAxis == Data.MirrorAxis.None)
                return (BoxCenter, BoxRotation);
            
            Vector3 origin = meshTransform != null ? meshTransform.position : Vector3.zero;
            Vector3 mirroredCenter = BoxCenter;
            Quaternion mirroredRotation = BoxRotation;
            
            // Mirror center position
            Vector3 relativePos = BoxCenter - origin;
            switch (MirrorAxis)
            {
                case Data.MirrorAxis.X:
                    relativePos.x = -relativePos.x;
                    break;
                case Data.MirrorAxis.Y:
                    relativePos.y = -relativePos.y;
                    break;
                case Data.MirrorAxis.Z:
                    relativePos.z = -relativePos.z;
                    break;
            }
            mirroredCenter = origin + relativePos;
            
            // Mirror rotation
            Vector3 eulerAngles = BoxRotation.eulerAngles;
            switch (MirrorAxis)
            {
                case Data.MirrorAxis.X:
                    eulerAngles.y = -eulerAngles.y;
                    eulerAngles.z = -eulerAngles.z;
                    break;
                case Data.MirrorAxis.Y:
                    eulerAngles.x = -eulerAngles.x;
                    eulerAngles.z = -eulerAngles.z;
                    break;
                case Data.MirrorAxis.Z:
                    eulerAngles.x = -eulerAngles.x;
                    eulerAngles.y = -eulerAngles.y;
                    break;
            }
            mirroredRotation = Quaternion.Euler(eulerAngles);
            
            return (mirroredCenter, mirroredRotation);
        }
        
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
