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

    public enum MirrorBlendMode
    {
        Max = 0,
        Min = 1
    }

    public enum BackgroundColor
    {
        Transparent = 0,
        White = 1,
        Black = 2
    }

    public enum GradationShape
    {
        Linear = 0,
        Spherical = 1
    }

    public enum PreviewBlendMode
    {
        Replace  = 0,
        Additive = 1,
        Screen   = 2,
        Multiply = 3
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
        
        // Multi-material support
        public bool SplitByMaterial = true;
        public List<bool> EnabledMaterialSlots = new List<bool>();

        // UI state
        public bool ShowDetails = false;

        public bool IsMaterialSlotEnabled(int index)
        {
            if (index < 0 || index >= EnabledMaterialSlots.Count) return true;
            return EnabledMaterialSlots[index];
        }

        public void SyncMaterialSlots(Renderer renderer)
        {
            int count = renderer != null ? renderer.sharedMaterials.Length : 0;
            while (EnabledMaterialSlots.Count < count) EnabledMaterialSlots.Add(true);
            if (EnabledMaterialSlots.Count > count)
                EnabledMaterialSlots.RemoveRange(count, EnabledMaterialSlots.Count - count);
        }
        
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
        public MirrorBlendMode MirrorBlend = MirrorBlendMode.Max;
        
        // Gradation shape (Linear or Spherical)
        public GradationShape Shape = GradationShape.Linear;
        
        // Cube-based gradation control
        public Vector3 BoxCenter = Vector3.zero;
        public Quaternion BoxRotation = Quaternion.identity;
        public float BoxHeight = 1f;
        
        // Box dimensions (mutable for 3-axis ellipsoidal control)
        public float BoxWidth = 0.5f;
        public float BoxDepth = 0.5f;

        // Computed properties for shader compatibility
        public Vector3 GradientDirection => BoxRotation * Vector3.up;
        
        /// <summary>
        /// Returns the box scale vector used for matrix construction.
        /// </summary>
        public Vector3 BoxScale => new Vector3(BoxWidth, BoxHeight, BoxDepth);
        
        // Min/Max in world space along the gradient direction from box center
        public float MinRange => Vector3.Dot(BoxCenter - BoxRotation * Vector3.up * (BoxHeight / 2f), GradientDirection);
        public float MaxRange => Vector3.Dot(BoxCenter + BoxRotation * Vector3.up * (BoxHeight / 2f), GradientDirection);

        // Preview Settings
        public bool IsToolActive = true;
        public PreviewBlendMode BlendMode = PreviewBlendMode.Replace;

        // Output Settings
        public string SavePath = "Assets/GeneratedGradation/output/";
        public bool UseTextureFolder = true; // Output to material's main texture folder
        public BackgroundColor BgColor = BackgroundColor.Transparent;
        
        // Edge Padding (UV island dilation)
        public int EdgePaddingPixels = 4; // Default: 4, recommended: 1-16 pixels

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
            
            if (Shape == GradationShape.Spherical)
            {
                FitSphericalBounds();
                return;
            }
            
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
        
        /// <summary>
        /// Fits a bounding sphere to all mesh vertices for Spherical mode.
        /// Computes the centroid and the maximum distance from it, then sets
        /// BoxWidth/BoxHeight/BoxDepth to the diameter (uniform sphere).
        /// </summary>
        private void FitSphericalBounds()
        {
            Vector3 globalCenter = Vector3.zero;
            int totalVertices = 0;
            
            // First pass: compute centroid
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
                    globalCenter += transform.TransformPoint(v);
                    totalVertices++;
                }
            }
            
            if (totalVertices == 0) return;
            globalCenter /= totalVertices;
            
            // Second pass: find maximum distance from centroid
            float maxDist = 0f;
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
                    float dist = Vector3.Distance(globalCenter, transform.TransformPoint(v));
                    if (dist > maxDist) maxDist = dist;
                }
            }
            
            if (maxDist < 0.0001f) maxDist = 1f;
            
            // Diameter = maxDist * 2, sets uniform scale
            float diameter = maxDist * 2f;
            BoxCenter = globalCenter;
            BoxWidth = diameter;
            BoxHeight = diameter;
            BoxDepth = diameter;
            BoxRotation = Quaternion.identity;
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
