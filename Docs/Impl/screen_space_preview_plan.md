現在のコードを詳しく確認してから計画書を作成します。

現在のコードを確認できたので、リファレンス兼要件定義・実装計画書をまとめます。

---

# 実装計画書: Blendコマンドによる合成モードプレビュー

## 1. 現状の整理

| 項目 | 現状 |
|---|---|
| Blendモード | `Blend SrcAlpha OneMinusSrcAlpha` 固定 |
| 透明度制御 | `_Opacity` をフラグメントシェーダーで `finalCol.a *= _Opacity` |
| 合成モード | なし（半透明オーバーレイのみ） | [1](#4-0) [2](#4-1) [3](#4-2) 

---

## 2. 要件定義

- `_Opacity` パラメーターを廃止する（合成モードの精度を優先）
- マスク（テクスチャ・頂点カラー）は引き続き合成強度の制御に使用する
- 合成モード（置き換え・加算・スクリーン・乗算）をUIで選択可能にする
- Blendコマンドを動的に切り替えることで、フレームバッファ上の対象メッシュのレンダリング結果に対してスクリーンスペース合成を行う

---

## 3. 各合成モードの設計

フラグメントシェーダーが返す値とBlend設定の組み合わせで各モードを実現します。

| モード | フラグメントシェーダーの出力 | Blend設定 | 精度 |
|---|---|---|---|
| 置き換え | `rgb=grad.rgb`, `a=mask` | `Blend SrcAlpha OneMinusSrcAlpha` | 正確 |
| 加算 | `rgb=grad.rgb`, `a=mask` | `Blend SrcAlpha One` | 正確 |
| スクリーン | `rgb=grad.rgb * mask`, `a=1` | `Blend One OneMinusSrcColor` | **近似**（mask=0,1では正確） |
| 乗算 | `rgb=lerp(1, grad.rgb, mask)`, `a=1` | `Blend DstColor Zero` | 正確 |

**スクリーンの近似について:**  
正確な式は `lerp(dst, 1-(1-grad)(1-dst), mask)` ですが、Blendコマンドのみでは表現不可能です。`src=grad*mask` として `Blend One OneMinusSrcColor` を使うと `grad*mask + dst*(1-grad*mask)` になり、mask=0（変化なし）とmask=1（正確なスクリーン）の両端では正確です。中間値は近似になります。

**乗算が正確な理由:**  
`src.rgb = lerp(1, grad.rgb, mask)` をシェーダーで計算し `Blend DstColor Zero` を使うと:  
`result = src * dst = lerp(1, grad, mask) * dst = dst*(1-mask) + grad*dst*mask = lerp(dst, grad*dst, mask)`  
これは正確な乗算の式と一致します。

---

## 4. 変更ファイル一覧

```
Data/GradationSettings.cs          ← PreviewBlendMode enum追加、PreviewOpacity削除
Shaders/GradationPreview.shader    ← Blend動的化、_BlendModeによるfrag分岐
UI/GradationPreview.cs             ← Blendパラメーター設定、_Opacity設定削除
UI/GradationBakerWindow.cs         ← UIに合成モード選択追加、Opacityスライダー削除
```

---

## 5. 各ファイルの変更内容

### `Data/GradationSettings.cs`

```csharp
// 追加
public enum PreviewBlendMode
{
    Replace  = 0,
    Additive = 1,
    Screen   = 2,
    Multiply = 3
}

// GradationSettings クラス内
// 削除: public float PreviewOpacity = 0.5f;
// 追加:
public PreviewBlendMode BlendMode = PreviewBlendMode.Replace;
``` [3](#4-2) 

---

### `Shaders/GradationPreview.shader`

**Properties に追加:**
```hlsl
[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5  // SrcAlpha
[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
int _BlendMode ("Blend Mode", Int) = 0
```

**SubShader の Blend を動的化:**
```hlsl
// 削除: Blend SrcAlpha OneMinusSrcAlpha
// 追加:
Blend [_SrcBlend] [_DstBlend]
```

**Properties から `_Opacity` を削除。**

**frag の出力を合成モードで分岐:**
```hlsl
// 削除: finalCol.a *= _Opacity;

// 追加（_BlendModeで分岐）:
fixed4 result;
if (_BlendMode == 1) // Additive: rgb=grad, a=mask
{
    result = fixed4(finalCol.rgb, finalCol.a); // Blend SrcAlpha One
}
else if (_BlendMode == 2) // Screen: rgb=grad*mask, a=1
{
    result = fixed4(finalCol.rgb * finalCol.a, 1.0); // Blend One OneMinusSrcColor
}
else if (_BlendMode == 3) // Multiply: rgb=lerp(1,grad,mask), a=1
{
    result = fixed4(lerp(fixed3(1,1,1), finalCol.rgb, finalCol.a), 1.0); // Blend DstColor Zero
}
else // Replace: rgb=grad, a=mask
{
    result = fixed4(finalCol.rgb, finalCol.a); // Blend SrcAlpha OneMinusSrcAlpha
}
return result;
``` [4](#4-3) [5](#4-4) 

---

### `UI/GradationPreview.cs`

`UpdatePreview()` 内で合成モードに応じてBlendパラメーターを設定します。

```csharp
// 削除:
_previewMaterial.SetFloat("_Opacity", settings.PreviewOpacity);

// 追加:
_previewMaterial.SetInt("_BlendMode", (int)settings.BlendMode);

// Blendモードに応じてSrcBlend/DstBlendを設定
switch (settings.BlendMode)
{
    case PreviewBlendMode.Additive:
        _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        break;
    case PreviewBlendMode.Screen:
        _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcColor);
        break;
    case PreviewBlendMode.Multiply:
        _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
        _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        break;
    default: // Replace
        _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        break;
}
``` [6](#4-5) 

---

### `UI/GradationBakerWindow.cs`

プレビュー設定のUI部分で `PreviewOpacity` スライダーを削除し、`BlendMode` の `EnumPopup` を追加します。

```csharp
// 削除: settings.PreviewOpacity = EditorGUILayout.Slider(...);
// 追加:
settings.BlendMode = (PreviewBlendMode)EditorGUILayout.EnumPopup("Blend Mode", settings.BlendMode);
``` [7](#4-6) 

---

## 6. 注意事項

- **乗算モードでは背景が黒い部分が黒くなる**: `Blend DstColor Zero` はフレームバッファの色に乗算するため、SceneViewの背景色（グレー等）も影響を受けます。対象メッシュが先にレンダリングされている領域のみ正確です（`ZTest LEqual` により保証されます）。
- **スクリーンモードはマスク中間値で近似**: 前述の通り、mask=0と1の両端では正確ですが中間値は近似です。実用上問題ないかは確認が必要です。
- **`_Opacity` 廃止の影響**: `GradationBakerWindow.cs` の `_Opacity` 参照箇所をすべて削除する必要があります。`GradationSettings.PreviewOpacity` を参照している箇所は `grep` で確認してください。

### Citations

**File:** Shaders/GradationPreview.shader (L1-17)
```text
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
```

**File:** Shaders/GradationPreview.shader (L119-126)
```text
                // --- Max Blend (Simulate Bake result) ---
                fixed4 finalCol = max(colMain, colMirror);
                
                // Apply global opacity for preview
                finalCol.a *= _Opacity;
                
                return finalCol;
            }
```

**File:** Data/GradationSettings.cs (L78-80)
```csharp
        // Preview Settings
        public bool IsToolActive = true;
        public float PreviewOpacity = 0.5f;
```

**File:** UI/GradationPreview.cs (L59-88)
```csharp
            // Set shader properties
            _previewMaterial.SetTexture("_MainTex", _lutTexture);
            _previewMaterial.SetMatrix("_WorldToBox", worldToBox);
            _previewMaterial.SetMatrix("_ObjectToWorld", localToWorld);
            _previewMaterial.SetFloat("_BoxHeight", settings.BoxHeight);
            _previewMaterial.SetFloat("_Opacity", settings.PreviewOpacity);
            
            // Mirror settings
            _previewMaterial.SetInt("_UseMirror", isMirrorEnabled ? 1 : 0);
            _previewMaterial.SetMatrix("_WorldToBoxMirror", worldToBoxMirror);

            // Mask settings (per-mesh)
            _previewMaterial.SetInt("_UVChannel", entry.UVChannel);
            if (entry.MaskTexture != null)
            {
                _previewMaterial.SetTexture("_MaskTex", entry.MaskTexture);
                _previewMaterial.SetInt("_UseMaskTexture", 1);
            }
            else
            {
                _previewMaterial.SetInt("_UseMaskTexture", 0);
            }
            _previewMaterial.SetInt("_UseVertexColorMask", entry.UseVertexColorMask ? 1 : 0);
            _previewMaterial.SetInt("_InvertMask", entry.InvertMask ? 1 : 0);

            // Draw mesh with preview material (Single Pass)
            if (_previewMaterial.SetPass(0))
            {
                Graphics.DrawMeshNow(mesh, localToWorld);
            }
```

**File:** UI/GradationBakerWindow.cs (L47-52)
```csharp
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LocalizationManager.Initialize();
            SetupMeshList();
        }
```
