using UnityEngine;
using UnityEditor;

namespace GradationTextureGenerator.Execute
{
    public static class MeshReadWriteEnabler
    {
        public static void EnsureReadWriteEnabled(Mesh mesh)
        {
            if (mesh == null) return;
            if (mesh.isReadable) return;

            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(assetPath)) return; // Procedural mesh or built-in

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                Debug.Log($"[GradationTextureGenerator] Automatically enabled Read/Write for {mesh.name} at {assetPath}");
            }
        }
    }
}
