using UnityEngine;
using UnityEditor;
using System.IO;

namespace GradationTextureGenerator.Execute
{
    /// <summary>
    /// Utilities for handling output file paths and naming
    /// </summary>
    public static class OutputPathResolver
    {
        /// <summary>
        /// Gets the output folder path, considering UseTextureFolder option
        /// </summary>
        public static string ResolveOutputFolder(Renderer renderer, string defaultPath, bool useTextureFolder)
        {
            if (useTextureFolder && renderer != null)
            {
                string texturePath = GetMainTexturePath(renderer);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    return Path.GetDirectoryName(texturePath).Replace('\\', '/');
                }
            }
            
            return defaultPath;
        }

        /// <summary>
        /// Gets the main texture path from renderer's material
        /// </summary>
        private static string GetMainTexturePath(Renderer renderer)
        {
            if (renderer == null) return null;
            
            Material mat = renderer.sharedMaterial;
            if (mat == null) return null;
            
            // Try common main texture property names
            string[] textureProps = { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" };
            
            foreach (var prop in textureProps)
            {
                if (mat.HasProperty(prop))
                {
                    Texture tex = mat.GetTexture(prop);
                    if (tex != null)
                    {
                        string path = AssetDatabase.GetAssetPath(tex);
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Generates a unique filename with Unity-style duplicate numbering
        /// Format: {meshname}_gradation.png, {meshname}_gradation 1.png, etc.
        /// </summary>
        public static string GenerateUniqueFilename(string folderPath, string meshName)
        {
            string baseName = $"{meshName}_gradation";
            string extension = ".png";
            
            // Ensure folder path ends properly
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            
            // Check if base name exists
            string fullPath = Path.Combine(folderPath, baseName + extension).Replace('\\', '/');
            if (!File.Exists(fullPath))
            {
                return baseName + extension;
            }
            
            // Find next available number
            int counter = 1;
            while (true)
            {
                string numberedName = $"{baseName} {counter}{extension}";
                fullPath = Path.Combine(folderPath, numberedName).Replace('\\', '/');
                
                if (!File.Exists(fullPath))
                {
                    return numberedName;
                }
                
                counter++;
                
                // Safety limit
                if (counter > 9999)
                {
                    return $"{baseName}_{System.DateTime.Now:yyyyMMddHHmmss}{extension}";
                }
            }
        }

        /// <summary>
        /// Ensures the directory exists, creating it if necessary
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            string fullPath = path.Replace('\\', '/');
            
            if (fullPath.StartsWith("Assets"))
            {
                fullPath = Path.Combine(Application.dataPath, fullPath.Substring("Assets".Length).TrimStart('/'));
            }
            fullPath = fullPath.Replace('\\', '/');
            
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }

        /// <summary>
        /// Converts Unity asset path to full system path
        /// </summary>
        public static string ToFullPath(string assetPath)
        {
            string path = assetPath.Replace('\\', '/');
            
            if (path.StartsWith("Assets"))
            {
                return Path.Combine(Application.dataPath, path.Substring("Assets".Length).TrimStart('/')).Replace('\\', '/');
            }
            
            return path;
        }
    }
}
