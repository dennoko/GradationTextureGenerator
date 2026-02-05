using UnityEngine;

namespace GradationBaker.Execute
{
    /// <summary>
    /// Applies edge padding (dilation) to textures to prevent UV seam artifacts
    /// </summary>
    public static class EdgePadding
    {
        /// <summary>
        /// Applies dilation to transparent pixels by copying colors from nearby opaque pixels
        /// </summary>
        /// <param name="texture">The texture to process (modified in place)</param>
        /// <param name="paddingPixels">Number of pixels to expand (iterations)</param>
        public static void Apply(Texture2D texture, int paddingPixels)
        {
            if (texture == null || paddingPixels <= 0) return;

            int width = texture.width;
            int height = texture.height;
            
            Color[] pixels = texture.GetPixels();
            Color[] result = new Color[pixels.Length];
            
            // 8-directional offsets for neighbor search
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            
            // Perform dilation for specified number of iterations
            for (int iteration = 0; iteration < paddingPixels; iteration++)
            {
                System.Array.Copy(pixels, result, pixels.Length);
                bool anyChange = false;
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        
                        // Skip if already opaque
                        if (pixels[index].a > 0.001f) continue;
                        
                        // Search for nearest opaque neighbor
                        Color neighborColor = Color.clear;
                        bool found = false;
                        
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + dx[d];
                            int ny = y + dy[d];
                            
                            // Bounds check
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                            
                            int neighborIndex = ny * width + nx;
                            if (pixels[neighborIndex].a > 0.001f)
                            {
                                // Found opaque neighbor - use its color (keep alpha=0 or set to small value)
                                neighborColor = pixels[neighborIndex];
                                found = true;
                                break;
                            }
                        }
                        
                        if (found)
                        {
                            // Copy RGB but keep alpha low (or use neighbor's alpha)
                            // Using small alpha to mark as "extended" but not fully visible
                            result[index] = new Color(neighborColor.r, neighborColor.g, neighborColor.b, neighborColor.a);
                            anyChange = true;
                        }
                    }
                }
                
                // Copy result back to pixels for next iteration
                System.Array.Copy(result, pixels, pixels.Length);
                
                // Early exit if no changes
                if (!anyChange) break;
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
        }
    }
}
