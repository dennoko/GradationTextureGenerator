using UnityEngine;
using UnityEditor;

namespace GradationTextureGenerator.Execute
{
    /// <summary>
    /// Manages work mesh objects for avoiding coordinate issues with non-destructive components
    /// </summary>
    public static class WorkMeshManager
    {
        private const string WorkMeshPrefix = "[GradGen_Work] ";
        private static readonly Vector3 DefaultOffset = new Vector3(1.5f, 0f, 0f);

        /// <summary>
        /// Creates a work mesh copy of the source renderer using MeshFilter + MeshRenderer
        /// </summary>
        public static GameObject CreateWorkMesh(Renderer sourceRenderer)
        {
            if (sourceRenderer == null) return null;

            Mesh sourceMesh = GetMesh(sourceRenderer);
            if (sourceMesh == null) return null;

            // Create new GameObject
            string name = WorkMeshPrefix + sourceRenderer.name;
            GameObject workObj = new GameObject(name);
            
            // Position with offset
            workObj.transform.position = sourceRenderer.transform.position + DefaultOffset;
            workObj.transform.rotation = sourceRenderer.transform.rotation;
            workObj.transform.localScale = Vector3.one; // Reset scale to 1

            // Always use MeshFilter + MeshRenderer for work mesh
            var mf = workObj.AddComponent<MeshFilter>();
            mf.sharedMesh = sourceMesh;
            
            var mr = workObj.AddComponent<MeshRenderer>();
            mr.sharedMaterials = sourceRenderer.sharedMaterials;

            // Register undo
            Undo.RegisterCreatedObjectUndo(workObj, "Create Work Mesh");

            FileLogger.Log($"[WorkMeshManager] Created work mesh: {name}");
            return workObj;
        }

        /// <summary>
        /// Deletes a work mesh object
        /// </summary>
        public static void DeleteWorkMesh(GameObject workMesh)
        {
            if (workMesh == null) return;

            string name = workMesh.name;
            Undo.DestroyObjectImmediate(workMesh);
            FileLogger.Log($"[WorkMeshManager] Deleted work mesh: {name}");
        }

        /// <summary>
        /// Checks if an object is a work mesh
        /// </summary>
        public static bool IsWorkMesh(GameObject obj)
        {
            return obj != null && obj.name.StartsWith(WorkMeshPrefix);
        }

        /// <summary>
        /// Gets the renderer from a work mesh
        /// </summary>
        public static Renderer GetRenderer(GameObject workMesh)
        {
            if (workMesh == null) return null;
            return workMesh.GetComponent<MeshRenderer>();
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
