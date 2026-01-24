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
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        
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
                float3 worldPos : TEXCOORD0;
            };

            sampler2D _MainTex; // Gradient LUT
            
            // New cube-based parameters
            float4x4 _WorldToBox;     // World to box local space transform
            float4x4 _ObjectToWorld;  // Object to world transform
            float _BoxHeight;
            float _Opacity;
            
            // Legacy parameters (kept for compatibility)
            float3 _Direction;
            float _RangeMin;
            float _RangeMax;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Transform vertex to world space
                o.worldPos = mul(_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Transform world position to box local space
                float3 boxLocalPos = mul(_WorldToBox, float4(i.worldPos, 1.0)).xyz;
                
                // In box local space, Y goes from -0.5 to 0.5 (normalized box)
                // Map to 0-1 range for gradient sampling
                float t = saturate(boxLocalPos.y + 0.5);
                
                // Sample Gradient Color from LUT
                fixed4 col = tex2D(_MainTex, float2(t, 0.5));
                
                // Apply global opacity for preview
                col.a *= _Opacity;
                
                return col;
            }
            ENDCG
        }
    }
}
