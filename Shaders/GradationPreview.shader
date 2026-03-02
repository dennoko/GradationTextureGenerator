Shader "Hidden/GradationBaker/Preview"
{
    Properties
    {
        _MainTex ("LUT", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
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
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            sampler2D _MainTex; // Gradient LUT
            sampler2D _MaskTex; // Mask Texture
            float4 _MaskTex_ST;
            
            // Cube-based parameters
            float4x4 _WorldToBox;
            float4x4 _ObjectToWorld;
            float _BoxHeight;
            float _Opacity;
            
            // Mirror parameters
            int _UseMirror;
            float4x4 _WorldToBoxMirror;
            
            // Shape: 0=Linear, 1=Spherical
            int _Shape;
            
            // UV Channel and Mask settings
            int _UVChannel;
            int _UseMaskTexture;
            int _UseVertexColorMask;
            int _InvertMask;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(_ObjectToWorld, v.vertex).xyz;
                
                // Select UV channel
                float2 selectedUV = v.uv0;
                if (_UVChannel == 1) selectedUV = v.uv1;
                else if (_UVChannel == 2) selectedUV = v.uv2;
                else if (_UVChannel == 3) selectedUV = v.uv3;
                
                o.uv = selectedUV;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate Mask first (common for both)
                float mask = 1.0;
                
                if (_UseMaskTexture == 1)
                {
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

                // --- Main Gradient ---
                float3 boxLocalPos = mul(_WorldToBox, float4(i.worldPos, 1.0)).xyz;
                float t;
                if (_Shape == 1)
                {
                    t = saturate(length(boxLocalPos) * 2.0);
                }
                else
                {
                    t = saturate(boxLocalPos.y + 0.5);
                }
                fixed4 colMain = tex2D(_MainTex, float2(t, 0.5));
                colMain.a *= mask;

                // --- Mirror Gradient ---
                fixed4 colMirror = fixed4(0,0,0,0);
                if (_UseMirror == 1)
                {
                    float3 boxLocalPosMirror = mul(_WorldToBoxMirror, float4(i.worldPos, 1.0)).xyz;
                    float tMirror;
                    if (_Shape == 1)
                    {
                        tMirror = saturate(length(boxLocalPosMirror) * 2.0);
                    }
                    else
                    {
                        tMirror = saturate(boxLocalPosMirror.y + 0.5);
                    }
                    colMirror = tex2D(_MainTex, float2(tMirror, 0.5));
                    colMirror.a *= mask;
                }

                // --- Max Blend (Simulate Bake result) ---
                fixed4 finalCol = max(colMain, colMirror);
                
                // Apply global opacity for preview
                finalCol.a *= _Opacity;
                
                return finalCol;
            }
            ENDCG
        }
    }
}
