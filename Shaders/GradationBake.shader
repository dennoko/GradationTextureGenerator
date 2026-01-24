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
            
            // Cube-based parameters
            float4x4 _WorldToBox;     // World to box local space transform
            float4x4 _ObjectToWorld;  // Object to world transform (identity for bake)
            
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

                o.objectPos = v.vertex.xyz;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Transform object position to world space, then to box local space
                float3 worldPos = mul(_ObjectToWorld, float4(i.objectPos, 1.0)).xyz;
                float3 boxLocalPos = mul(_WorldToBox, float4(worldPos, 1.0)).xyz;
                
                // In box local space, Y goes from -0.5 to 0.5 (normalized box)
                // Map to 0-1 range for gradient sampling
                float t = saturate(boxLocalPos.y + 0.5);
                
                // Sample Gradient
                fixed4 col = tex2D(_MainTex, float2(t, 0.5));

                // Calculate Mask
                float mask = 1.0;
                
                if (_UseMaskTexture == 1)
                {
                    float4 maskSample = tex2D(_MaskTex, i.uv);
                    mask *= maskSample.r; 
                }

                if (_UseVertexColorMask == 1)
                {
                    mask *= i.color.r; 
                }

                if (_InvertMask == 1)
                {
                    mask = 1.0 - mask;
                }

                col.a *= mask;

                return col;
            }
            ENDCG
        }
    }
}
