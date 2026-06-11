using System.Collections.Generic;
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

            // Color32 (byte) で処理し、未充填ピクセルだけを反復走査することで
            // 高解像度 (2048+) でも全ピクセル × 反復回数の走査を避ける
            Color32[] pixels = texture.GetPixels32();

            // 8-directional offsets for neighbor search
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            // 初回パス: 透明ピクセルの index を収集
            var transparent = new List<int>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0) transparent.Add(i);
            }

            var stillTransparent = new List<int>(transparent.Count);
            var filledThisPass = new List<(int index, Color32 color)>();

            for (int iteration = 0; iteration < paddingPixels && transparent.Count > 0; iteration++)
            {
                stillTransparent.Clear();
                filledThisPass.Clear();

                foreach (int index in transparent)
                {
                    int x = index % width;
                    int y = index / width;

                    bool found = false;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + dx[d];
                        int ny = y + dy[d];

                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                        int neighborIndex = ny * width + nx;
                        if (pixels[neighborIndex].a != 0)
                        {
                            // 同一パス内の伝播を防ぐため、書き込みはパス終了後にまとめて行う
                            filledThisPass.Add((index, pixels[neighborIndex]));
                            found = true;
                            break;
                        }
                    }

                    if (!found) stillTransparent.Add(index);
                }

                // Early exit if no changes
                if (filledThisPass.Count == 0) break;

                foreach (var (index, color) in filledThisPass)
                {
                    pixels[index] = color;
                }

                // swap lists
                var tmp = transparent;
                transparent = stillTransparent;
                stillTransparent = tmp;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }
    }
}
