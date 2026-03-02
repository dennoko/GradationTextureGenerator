Shader "Hidden/GradationBaker/Bake"
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
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
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
            float4 _MaskTex_ST; // Mask texture tiling/offset
            
            // Cube-based parameters
            float4x4 _WorldToBox;
            float4x4 _ObjectToWorld;
            
            // UV Channel selection
            int _UVChannel;
            
            // Shape: 0=Linear, 1=Spherical
            int _Shape;
            
            int _UseMaskTexture;
            int _UseVertexColorMask;
            int _InvertMask;

            v2f vert (appdata v)
            {
                v2f o;
                
                // Select UV channel
                float2 selectedUV = v.uv0;
                if (_UVChannel == 1) selectedUV = v.uv1;
                else if (_UVChannel == 2) selectedUV = v.uv2;
                else if (_UVChannel == 3) selectedUV = v.uv3;
                
                // UV to Clip Space mapping
                float2 uvClip = selectedUV * 2.0 - 1.0;
                o.vertex = float4(uvClip.x, -uvClip.y, 0, 1);

                o.objectPos = v.vertex.xyz;
                o.color = v.color;
                o.uv = selectedUV;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Transform object position to world space, then to box local space
                float3 worldPos = mul(_ObjectToWorld, float4(i.objectPos, 1.0)).xyz;
                float3 boxLocalPos = mul(_WorldToBox, float4(worldPos, 1.0)).xyz;
                
                // Calculate t based on shape
                float t;
                if (_Shape == 1)
                {
                    // Spherical: distance from center, scaled so box surface = 1.0
                    t = saturate(length(boxLocalPos) * 2.0);
                }
                else
                {
                    // Linear: Y axis from -0.5 to 0.5
                    t = saturate(boxLocalPos.y + 0.5);
                }
                
                // Sample Gradient
                fixed4 col = tex2D(_MainTex, float2(t, 0.5));

                // Calculate Mask
                float mask = 1.0;
                
                if (_UseMaskTexture == 1)
                {
                    // Use the same UV channel as output for mask sampling
                    float2 maskUV = TRANSFORM_TEX(i.uv, _MaskTex);
                    float4 maskSample = tex2D(_MaskTex, maskUV);
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
