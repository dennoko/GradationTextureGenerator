using UnityEngine;

namespace GradationBaker.Execute
{
    public static class TextureResolutionResolver
    {
        public static int ResolveDefaultResolution(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null) return 1024;

            Texture mainTex = renderer.sharedMaterial.mainTexture;
            if (mainTex != null)
            {
                return Mathf.Max(mainTex.width, mainTex.height);
            }
            return 1024;
        }
    }
}
