Shader "Hidden/GradationTextureGenerator/Preview"
{
    Properties
    {
        _MainTex ("LUT", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        // Non-destructive overlay settings
        ZWrite Off
        ZTest LEqual // Draw only if not occluded (or Always if user prefers seeing through)
        Blend SrcAlpha OneMinusSrcAlpha // Alpha Blending
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPos : TEXCOORD0;
            };

            sampler2D _MainTex; // Gradient LUT
            
            float3 _Direction; // Gradient Direction (Normalized)
            float _RangeMin;
            float _RangeMax;
            float _Opacity;
            
            // Mask props (Optional for preview, can keep simple initially)
            // To support masks in preview, we need UVs and mask texture prop.
            // Let's implement full parity with Bake shader for accuracy.

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.objectPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate gradient factor t based on object position and direction
                float t = dot(i.objectPos, _Direction);
                
                // Optimize t to 0-1 range based on Min/Max
                float normalizedT = saturate((t - _RangeMin) / (_RangeMax - _RangeMin));
                
                // Sample Gradient Color from LUT
                fixed4 col = tex2D(_MainTex, float2(normalizedT, 0.5));
                
                // Apply global opacity for preview
                col.a *= _Opacity;
                
                return col;
            }
            ENDCG
        }
    }
}
