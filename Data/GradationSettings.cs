using UnityEngine;

namespace GradationTextureGenerator.Data
{
    [System.Serializable]
    public class GradationSettings
    {
        public Renderer TargetRenderer;
        public Vector3 GradientDirection = Vector3.up;
        public Gradient Gradient = new Gradient();
        public Texture2D MaskTexture;
        public bool UseVertexColorMask = false;
        public bool InvertMask = false;
        public int Resolution = 1024;
        
        // Normalization
        public bool AutoNormalize = true;
        public float MinRange = 0f;
        public float MaxRange = 1f;

        public string SavePath = "Assets/";
    }
}
