Shader "Hidden/GradationBaker/Preview"
{
    Properties
    {
        _MainTex ("LUT", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        ZWrite Off
        ZTest LEqual

        CGINCLUDE
        #include "UnityCG.cginc"

        // プロパティとして宣言
        int _BlendMode;
        sampler2D _BackgroundTexture;

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
            float4 screenPos : TEXCOORD4;
        };

        sampler2D _MainTex;
        sampler2D _MaskTex;
        float4 _MaskTex_ST;

        // ... その他の変数はそのまま ...
        float4x4 _WorldToBox;
        float4x4 _ObjectToWorld; // ※Proxy描画では不要になりますが互換性のため残す
        float _BoxHeight;

        int _UseMirror;
        float4x4 _WorldToBoxMirror;
        int _MirrorBlendMode;

        int _Shape;

        int _UVChannel;
        int _UseMaskTexture;
        int _UseVertexColorMask;
        int _InvertMask;

        v2f vert(appdata v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            
            // Proxy対応: unity_ObjectToWorldを優先使用。もし固定マトリクスがあれば手動計算。
            // ここでは従来の_ObjectToWorldを使いますが、Proxy運用時はC#側でMatrix設定をスキップできます
            // 今回はProxyObjectのTransformが正しく同期されるため、本来の unity_ObjectToWorld が使えます。
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

            float2 selectedUV = v.uv0;
            if (_UVChannel == 1) selectedUV = v.uv1;
            else if (_UVChannel == 2) selectedUV = v.uv2;
            else if (_UVChannel == 3) selectedUV = v.uv3;

            o.uv = selectedUV;
            o.color = v.color;
            o.screenPos = ComputeGrabScreenPos(o.vertex);
            return o;
        }

        fixed4 CalcColor(v2f i)
        {
            // === 従来のCalcColorと同じ ===
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
                fixed4 colMirror = tex2D(_MainTex, float2(tMirror, 0.5));
                colMirror.a *= mask;

                if (_MirrorBlendMode == 1)
                    return min(colMain, colMirror);
                return max(colMain, colMirror);
            }

            return colMain;
        }
        ENDCG

        GrabPass { "_BackgroundTexture" }

        Pass
        {
            Blend Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            fixed4 frag(v2f i) : SV_Target
            {
                // 背景色の取得 (DstColor)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                fixed3 dst = tex2D(_BackgroundTexture, screenUV).rgb;

                // グラデーション色の取得 (SrcColor)
                fixed4 grad = CalcColor(i);
                fixed3 src = grad.rgb;
                float mask = grad.a;

                // ブレンドの計算 (Photoshop式に基づく)
                fixed3 resultColor;
                if (_BlendMode == 1)      // Additive
                {
                    resultColor = saturate(dst + src);
                }
                else if (_BlendMode == 2) // Screen
                {
                    resultColor = 1.0 - (1.0 - dst) * (1.0 - src);
                }
                else if (_BlendMode == 3) // Multiply
                {
                    resultColor = dst * src;
                }
                else                      // Replace (0)
                {
                    resultColor = src;
                }

                // マスク(不透明度)を使った合成
                // mask=0 (グラデーションなし)の時は元の背景色(dst)を出力する
                fixed3 finalColor = lerp(dst, resultColor, mask);

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
