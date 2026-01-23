Shader "Hidden/GradationTextureGenerator/Bake"
{
    Properties
    {
        _MainTex ("LUT", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPos : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
            };

            sampler2D _MainTex; // Gradient LUT
            sampler2D _MaskTex; // Mask Texture (optional)
            
            float3 _Direction; // Gradient Direction (Normalized)
            float _RangeMin;
            float _RangeMax;
            
            int _UseMaskTexture;
            int _UseVertexColorMask;
            int _InvertMask;

            v2f vert (appdata v)
            {
                v2f o;
                
                // UV to Clip Space mapping
                float2 uvClip = v.uv * 2.0 - 1.0;
                
                // Flip Y to fix upside down issue
                o.vertex = float4(uvClip.x, -uvClip.y, 0, 1);
                
                #if UNITY_UV_STARTS_AT_TOP
                // In rendering into RenderTexture, we might need to handle flip depending on platform
                // But usually Graphics.Blit handles it. Here we are drawing mesh.
                // Standard UV space rendering usually doesn't need flip if we map 0,0 to bottom-left.
                // However, Unity's UV usually starts bottom-left.
                #endif

                o.objectPos = v.vertex.xyz;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Calculate T
                float t = dot(i.objectPos, _Direction);
                // Linear Remap
                float normalizedT = saturate((t - _RangeMin) / (_RangeMax - _RangeMin));
                
                // 2. Sample Gradient
                fixed4 col = tex2D(_MainTex, float2(normalizedT, 0.5));

                // 3. Calculate Mask
                float mask = 1.0;
                
                if (_UseMaskTexture == 1)
                {
                    // Sample mask using original UV
                    float4 maskSample = tex2D(_MaskTex, i.uv);
                    // Use max of RGB or Alpha? Usually grayscale mask uses R or Gray.
                    mask *= maskSample.r; 
                }

                if (_UseVertexColorMask == 1)
                {
                    // Vertex color usually R channel for masks, or use Alpha?
                    // Let's assume R channel or luminance for now, but usually vertex color is multiplied directly.
                    // If it's a mask for "strength", purely multiplying might be right.
                    mask *= i.color.r; 
                }

                if (_InvertMask == 1)
                {
                    mask = 1.0 - mask;
                }

                // Apply Mask to Alpha or modify color?
                // Typically "Masking the effect" means checking if we apply the gradient or keep original?
                // But this tool generates a NEW texture.
                // If mask is 0, what should be the color? Transparent? Or Black?
                // The requirements say: "control the application intensity of the gradation".
                // And "Vertex color mask... widely used in VRChat".
                // Usually this means Alpha masking or returning clear color.
                // Let's assume the output texture will be composited or used as a layer.
                // If the user wants a standalone texture, maybe (0,0,0,0) for masked out areas?
                
                col.a *= mask;

                return col;
            }
            ENDCG
        }
    }
}
