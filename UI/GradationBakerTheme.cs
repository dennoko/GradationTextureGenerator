using UnityEngine;
using UnityEditor;

namespace GradationBaker.UI
{
    /// <summary>
    /// dennoko.dev カラースキーマに基づくテーマ定義。
    /// colors_spec.md / design_reference.md の仕様を Unity IMGUI に変換する。
    /// OnGUI の先頭で Initialize() → PushEditorTheme() を呼び、
    /// finally ブロックで必ず PopEditorTheme() を呼ぶこと。
    /// </summary>
    internal static class GradationBakerTheme
    {
        // ─── Colors ──────────────────────────────────────────────────────────

        // theme.surface (Neutral Layer)
        public static readonly Color Surface0 = Hex(0x121212); // app background
        public static readonly Color Surface1 = Hex(0x1e1e1e); // cards, inputs
        public static readonly Color Surface2 = Hex(0x2c2c2c); // hover, toolbar

        // theme.outline
        public static readonly Color Outline = Hex(0x3a3a3a);

        // theme.typography
        public static readonly Color TextPrimary   = Hex(0xffffff);
        public static readonly Color TextSecondary = Hex(0xcccccc);
        public static readonly Color TextTertiary  = Hex(0xaaaaaa);
        public static readonly Color TextDisabled  = Hex(0x555555);

        // theme.semantic
        public static readonly Color SemanticError   = Hex(0x9b1b30);
        public static readonly Color SemanticWarning = Hex(0xffb74d);
        public static readonly Color SemanticSuccess = Hex(0x4caf50);
        public static readonly Color SemanticInfo    = Hex(0x64b5f6);

        // theme.interaction
        public static readonly Color Accent       = Color.white;
        public static readonly Color HoverOverlay = new Color(1f, 1f, 1f, 0.05f);

        // ─── Cached Textures ─────────────────────────────────────────────────

        private static Texture2D _texSurface0;
        private static Texture2D _texSurface1;
        private static Texture2D _texSurface2;
        private static Texture2D _texCard;        // Surface1 fill + Outline border (3x3)
        private static Texture2D _texAccentCard;  // Surface2 fill + Outline border (3x3)
        private static Texture2D _texSearchField; // Input fields background (3x3 bordered)
        private static Texture2D _texHover;       // Surface2 → white 10%
        private static Texture2D _texActive;      // Surface2 → white 18%
        private static Texture2D _texButtonHover;
        private static Texture2D _texButtonActive;
        private static Texture2D _texSecondaryActive;
        private static Texture2D _texStatusSuccess;
        private static Texture2D _texStatusError;

        // ─── Styles ──────────────────────────────────────────────────────────

        private static bool _initialized;
        private static bool _lastIsProSkin;

        // Layout / Container
        public static GUIStyle CardStyle      { get; private set; } // sections (padding あり)
        public static GUIStyle CardOuterStyle { get; private set; } // ツールバー付き外枠 (padding なし)
        public static GUIStyle ToolbarStyle   { get; private set; } // ツールバー行

        // Typography
        public static GUIStyle TitleStyle            { get; private set; } // ウィンドウタイトル
        public static GUIStyle SectionHeaderStyle    { get; private set; } // 非トグルセクション見出し
        public static GUIStyle ToggleSectionOnStyle  { get; private set; } // トグル ON 時の見出し
        public static GUIStyle ToggleSectionOffStyle { get; private set; } // トグル OFF 時の見出し
        public static GUIStyle SecondaryTextStyle    { get; private set; } // 説明文
        public static GUIStyle CaptionStyle          { get; private set; } // 補足・メタデータ
        public static GUIStyle MiniLabelStyle        { get; private set; } // リスト内の小ラベル
        public static GUIStyle DropAreaLabelStyle    { get; private set; } // D&D エリアの中央ラベル

        // Buttons
        public static GUIStyle ActionButtonStyle     { get; private set; } // Primary Action
        public static GUIStyle SecondaryButtonStyle  { get; private set; } // Secondary Action
        public static GUIStyle MiniButtonStyle       { get; private set; }
        public static GUIStyle MiniButtonLeftStyle   { get; private set; }
        public static GUIStyle MiniButtonRightStyle  { get; private set; }

        // Status bar
        public static GUIStyle StatusInfoStyle    { get; private set; }
        public static GUIStyle StatusSuccessStyle { get; private set; }
        public static GUIStyle StatusErrorStyle   { get; private set; }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>OnGUI の先頭で呼び出す。初回のみスタイルを構築する。</summary>
        public static void Initialize()
        {
            // Editor テーマ (ライト/ダーク) が切り替わったら全テクスチャを再構築する
            bool currentProSkin = EditorGUIUtility.isProSkin;
            if (_initialized && _lastIsProSkin != currentProSkin)
            {
                DisposeTextures();
            }
            _lastIsProSkin = currentProSkin;

            // ドメインリロードでテクスチャだけ消えた場合にも復旧できるよう
            // Unity の null 比較で毎回確認する
            if (_initialized && _texCard) return;
            _initialized = true;
            EnsureTextures();
            BuildStyles();
        }

        private static void EnsureTextures()
        {
            if (!_texSurface0)        _texSurface0        = MakeTex(Surface0);
            if (!_texSurface1)        _texSurface1        = MakeTex(Surface1);
            if (!_texSurface2)        _texSurface2        = MakeTex(Surface2);
            if (!_texCard)            _texCard            = MakeBorderedTex(Surface1, Outline);
            if (!_texAccentCard)      _texAccentCard      = MakeBorderedTex(Surface2, Outline);
            if (!_texHover)           _texHover           = MakeTex(Color.Lerp(Surface2, Color.white, 0.10f));
            if (!_texActive)          _texActive          = MakeTex(Color.Lerp(Surface2, Color.white, 0.18f));
            if (!_texButtonHover)     _texButtonHover     = MakeTex(Color.Lerp(Surface2, Color.white, 0.07f));
            if (!_texButtonActive)    _texButtonActive    = MakeTex(Color.Lerp(Surface2, Color.white, 0.15f));
            if (!_texSecondaryActive) _texSecondaryActive = MakeTex(Color.Lerp(Surface1, Color.white, 0.10f));
            if (!_texStatusSuccess)   _texStatusSuccess   = MakeTex(Color.Lerp(Surface1, SemanticSuccess, 0.3f));
            if (!_texStatusError)     _texStatusError     = MakeTex(Color.Lerp(Surface1, SemanticError, 0.5f));
            if (!_texSearchField)     _texSearchField     = MakeBorderedTex(Surface2, Hex(0x5a5a5a));
        }

        private static void BuildStyles()
        {
            // ── Container ────────────────────────────────────────────────────

            CardStyle = new GUIStyle();
            CardStyle.normal.background = _texCard;
            CardStyle.border  = new RectOffset(1, 1, 1, 1);
            CardStyle.padding = new RectOffset(10, 10, 8, 8);
            CardStyle.margin  = new RectOffset(8, 8, 8, 8);

            CardOuterStyle = new GUIStyle();
            CardOuterStyle.normal.background = _texCard;
            CardOuterStyle.border  = new RectOffset(1, 1, 1, 1);
            CardOuterStyle.padding = new RectOffset(0, 0, 0, 0);
            CardOuterStyle.margin  = new RectOffset(8, 8, 8, 8);

            ToolbarStyle = new GUIStyle();
            ToolbarStyle.normal.background = _texSurface2;
            ToolbarStyle.padding = new RectOffset(6, 6, 4, 4);
            ToolbarStyle.margin  = new RectOffset(0, 0, 0, 0);

            // ── Typography ───────────────────────────────────────────────────
            // new GUIStyle() から構築してテーマ非依存とする。
            // EditorStyles.* を継承すると未設定の state にライトモード色が混入するため使用しない。

            TitleStyle = new GUIStyle();
            TitleStyle.fontStyle = FontStyle.Bold;
            TitleStyle.fontSize  = 14;
            TitleStyle.alignment = TextAnchor.MiddleLeft;
            FixAllTextColors(TitleStyle, TextPrimary);

            SectionHeaderStyle = new GUIStyle();
            SectionHeaderStyle.fontStyle = FontStyle.Bold;
            SectionHeaderStyle.fontSize  = 10;
            SectionHeaderStyle.margin    = new RectOffset(0, 0, 0, 2);
            FixAllTextColors(SectionHeaderStyle, TextTertiary);

            ToggleSectionOnStyle = new GUIStyle();
            ToggleSectionOnStyle.fontStyle = FontStyle.Bold;
            ToggleSectionOnStyle.fontSize  = 10;
            ToggleSectionOnStyle.margin    = new RectOffset(0, 0, 0, 2);
            FixAllTextColors(ToggleSectionOnStyle, TextPrimary);

            ToggleSectionOffStyle = new GUIStyle();
            ToggleSectionOffStyle.fontStyle = FontStyle.Bold;
            ToggleSectionOffStyle.fontSize  = 10;
            ToggleSectionOffStyle.margin    = new RectOffset(0, 0, 0, 2);
            FixAllTextColors(ToggleSectionOffStyle, TextTertiary);

            SecondaryTextStyle = new GUIStyle();
            SecondaryTextStyle.wordWrap = true;
            FixAllTextColors(SecondaryTextStyle, TextSecondary);

            CaptionStyle = new GUIStyle();
            CaptionStyle.fontSize = 9;
            CaptionStyle.wordWrap = true;
            FixAllTextColors(CaptionStyle, TextTertiary);

            MiniLabelStyle = new GUIStyle();
            MiniLabelStyle.fontSize = 10;
            FixAllTextColors(MiniLabelStyle, TextSecondary);

            DropAreaLabelStyle = new GUIStyle();
            DropAreaLabelStyle.fontSize  = 10;
            DropAreaLabelStyle.fontStyle = FontStyle.Italic;
            DropAreaLabelStyle.alignment = TextAnchor.MiddleCenter;
            FixAllTextColors(DropAreaLabelStyle, TextTertiary);

            // ── Buttons ──────────────────────────────────────────────────────

            // GUI.skin.button / EditorStyles.miniButton* を継承すると Unity の角丸・グラデーション・
            // scaledBackgrounds が引き継がれてフラットなテクスチャと混ざる。
            // そのため new GUIStyle() から全プロパティを明示的に構築する。

            ActionButtonStyle = new GUIStyle();
            ActionButtonStyle.normal.background  = _texAccentCard;
            ActionButtonStyle.hover.background   = _texButtonHover;
            ActionButtonStyle.active.background  = _texButtonActive;
            ActionButtonStyle.border       = new RectOffset(1, 1, 1, 1);
            ActionButtonStyle.margin       = new RectOffset(4, 4, 2, 2);
            ActionButtonStyle.padding      = new RectOffset(6, 6, 3, 3);
            ActionButtonStyle.fontSize     = 13;
            ActionButtonStyle.fontStyle    = FontStyle.Bold;
            ActionButtonStyle.fixedHeight  = 34;
            ActionButtonStyle.alignment    = TextAnchor.MiddleCenter;
            ActionButtonStyle.stretchWidth = true;
            FixAllTextColors(ActionButtonStyle, TextPrimary);

            SecondaryButtonStyle = new GUIStyle();
            SecondaryButtonStyle.normal.background = _texCard;
            SecondaryButtonStyle.hover.background  = _texAccentCard;
            SecondaryButtonStyle.active.background = _texSecondaryActive;
            SecondaryButtonStyle.border       = new RectOffset(1, 1, 1, 1);
            SecondaryButtonStyle.margin       = new RectOffset(4, 4, 2, 2);
            SecondaryButtonStyle.padding      = new RectOffset(6, 6, 3, 3);
            SecondaryButtonStyle.fontSize     = 11;
            SecondaryButtonStyle.fixedHeight  = 26;
            SecondaryButtonStyle.alignment    = TextAnchor.MiddleCenter;
            SecondaryButtonStyle.stretchWidth = true;
            SecondaryButtonStyle.normal.textColor    = TextSecondary;
            SecondaryButtonStyle.hover.textColor     = TextPrimary;
            SecondaryButtonStyle.active.textColor    = TextPrimary;
            SecondaryButtonStyle.focused.textColor   = TextSecondary;
            SecondaryButtonStyle.onNormal.textColor  = TextSecondary;
            SecondaryButtonStyle.onHover.textColor   = TextPrimary;
            SecondaryButtonStyle.onActive.textColor  = TextPrimary;
            SecondaryButtonStyle.onFocused.textColor = TextSecondary;

            MiniButtonStyle      = BuildMiniButtonStyle();
            MiniButtonLeftStyle  = BuildMiniButtonStyle();
            MiniButtonRightStyle = BuildMiniButtonStyle();

            // ── Status Bar ───────────────────────────────────────────────────

            var statusBase = new GUIStyle();
            statusBase.border    = new RectOffset(1, 1, 1, 1);
            statusBase.padding   = new RectOffset(10, 10, 6, 6);
            statusBase.margin    = new RectOffset(4, 4, 2, 2);
            statusBase.fontSize  = 11;
            statusBase.wordWrap  = true;
            statusBase.alignment = TextAnchor.MiddleLeft;

            StatusInfoStyle = new GUIStyle(statusBase);
            StatusInfoStyle.normal.background = _texSurface1;
            FixAllTextColors(StatusInfoStyle, TextSecondary);

            StatusSuccessStyle = new GUIStyle(statusBase);
            StatusSuccessStyle.normal.background = _texStatusSuccess;
            FixAllTextColors(StatusSuccessStyle, SemanticSuccess);

            StatusErrorStyle = new GUIStyle(statusBase);
            StatusErrorStyle.normal.background = _texStatusError;
            FixAllTextColors(StatusErrorStyle, new Color(1f, 0.65f, 0.65f));
        }

        private static GUIStyle BuildMiniButtonStyle()
        {
            var style = new GUIStyle();
            style.normal.background = _texAccentCard;
            style.hover.background  = _texHover;
            style.active.background = _texActive;
            style.border      = new RectOffset(1, 1, 1, 1);
            style.margin      = new RectOffset(2, 2, 1, 1);
            style.padding     = new RectOffset(4, 4, 1, 2);
            style.fontSize    = 10;
            style.fixedHeight = 16;
            style.alignment   = TextAnchor.MiddleCenter;
            style.normal.textColor    = TextTertiary;
            style.hover.textColor     = TextSecondary;
            style.active.textColor    = TextPrimary;
            style.focused.textColor   = TextTertiary;
            style.onNormal.textColor  = TextPrimary;
            style.onHover.textColor   = TextPrimary;
            style.onActive.textColor  = TextPrimary;
            style.onFocused.textColor = TextPrimary;
            return style;
        }

        // ─── Editor Style Override (Light Mode Fix) ──────────────────────────

        private static bool _overrideActive;
        public static bool IsOverrideActive => _overrideActive;

        private static Color _backupCursorColor;
        private static Color _backupSelectionColor;
        private static bool _settingsBackupActive;

        private class GUIStyleBackup
        {
            private readonly GUIStyle _style;
            private readonly Color _normalColor, _hoverColor, _activeColor, _focusedColor;
            private readonly Color _onNormalColor, _onHoverColor, _onActiveColor, _onFocusedColor;
            private readonly Texture2D _normalBg, _hoverBg, _activeBg, _focusedBg;
            private readonly Texture2D _onNormalBg, _onHoverBg, _onActiveBg, _onFocusedBg;
            private readonly RectOffset _border;
            private readonly RectOffset _padding;

            public GUIStyleBackup(GUIStyle style)
            {
                _style = style;
                _normalColor    = style.normal.textColor;
                _hoverColor     = style.hover.textColor;
                _activeColor    = style.active.textColor;
                _focusedColor   = style.focused.textColor;
                _onNormalColor  = style.onNormal.textColor;
                _onHoverColor   = style.onHover.textColor;
                _onActiveColor  = style.onActive.textColor;
                _onFocusedColor = style.onFocused.textColor;

                _normalBg    = style.normal.background;
                _hoverBg     = style.hover.background;
                _activeBg    = style.active.background;
                _focusedBg   = style.focused.background;
                _onNormalBg  = style.onNormal.background;
                _onHoverBg   = style.onHover.background;
                _onActiveBg  = style.onActive.background;
                _onFocusedBg = style.onFocused.background;

                _border  = new RectOffset(style.border.left, style.border.right, style.border.top, style.border.bottom);
                _padding = new RectOffset(style.padding.left, style.padding.right, style.padding.top, style.padding.bottom);
            }

            public void Restore()
            {
                _style.normal.textColor    = _normalColor;
                _style.hover.textColor     = _hoverColor;
                _style.active.textColor    = _activeColor;
                _style.focused.textColor   = _focusedColor;
                _style.onNormal.textColor  = _onNormalColor;
                _style.onHover.textColor   = _onHoverColor;
                _style.onActive.textColor  = _onActiveColor;
                _style.onFocused.textColor = _onFocusedColor;

                _style.normal.background    = _normalBg;
                _style.hover.background     = _hoverBg;
                _style.active.background    = _activeBg;
                _style.focused.background   = _focusedBg;
                _style.onNormal.background  = _onNormalBg;
                _style.onHover.background   = _onHoverBg;
                _style.onActive.background  = _onActiveBg;
                _style.onFocused.background = _onFocusedBg;

                _style.border  = _border;
                _style.padding = _padding;
            }
        }

        private static GUIStyleBackup[] _backups;

        /// <summary>
        /// OnGUI 先頭で Initialize() の直後に呼ぶ。
        /// ライト/ダーク両モードで EditorStyles をテーマ定義色に一時上書きする。
        /// PopEditorTheme を finally ブロックで必ず呼ぶこと。
        /// </summary>
        public static void PushEditorTheme()
        {
            if (_overrideActive) return;
            _overrideActive = true;

            // 初回のみ元の状態をキャプチャ (Pop で完全に復元されるため再キャプチャ不要)
            if (_backups == null)
            {
                _backups = new[]
                {
                    new GUIStyleBackup(EditorStyles.label),
                    new GUIStyleBackup(EditorStyles.miniLabel),
                    new GUIStyleBackup(EditorStyles.boldLabel),
                    new GUIStyleBackup(EditorStyles.foldout),
                    new GUIStyleBackup(EditorStyles.objectField),
                    new GUIStyleBackup(EditorStyles.numberField),
                    new GUIStyleBackup(EditorStyles.textField),
                    new GUIStyleBackup(EditorStyles.popup),
                    new GUIStyleBackup(EditorStyles.toggle),
                    new GUIStyleBackup(GUI.skin.textField),
                    new GUIStyleBackup(GUI.skin.label)
                };
            }

            if (!_settingsBackupActive)
            {
                _backupCursorColor = GUI.skin.settings.cursorColor;
                _backupSelectionColor = GUI.skin.settings.selectionColor;
                _settingsBackupActive = true;
            }

            // ─ テキスト色を固定
            FixAllTextColors(EditorStyles.label,       TextSecondary);
            FixAllTextColors(EditorStyles.miniLabel,   TextTertiary);
            FixAllTextColors(EditorStyles.boldLabel,   TextPrimary);
            FixAllTextColors(EditorStyles.foldout,     TextSecondary);
            FixAllTextColors(EditorStyles.objectField, TextSecondary);
            FixAllTextColors(EditorStyles.numberField, TextSecondary);
            FixAllTextColors(EditorStyles.textField,   TextSecondary);
            FixAllTextColors(EditorStyles.popup,       TextSecondary);
            FixAllTextColors(EditorStyles.toggle,      TextSecondary);
            FixAllTextColors(GUI.skin.textField,       TextSecondary);
            FixAllTextColors(GUI.skin.label,           TextSecondary);

            // ─ 背景テクスチャをすべての状態でダーク色＋ボーダーに固定
            FixAllStateBackgrounds(EditorStyles.objectField, _texSearchField);
            EditorStyles.objectField.border = new RectOffset(1, 1, 1, 1);

            FixAllStateBackgrounds(EditorStyles.numberField, _texSearchField);
            EditorStyles.numberField.border = new RectOffset(1, 1, 1, 1);

            FixAllStateBackgrounds(EditorStyles.textField,   _texSearchField);
            EditorStyles.textField.border  = new RectOffset(1, 1, 1, 1);
            EditorStyles.textField.padding = new RectOffset(6, 6, 3, 3);

            FixAllStateBackgrounds(GUI.skin.textField,       _texSearchField);
            GUI.skin.textField.border  = new RectOffset(1, 1, 1, 1);
            GUI.skin.textField.padding = new RectOffset(6, 6, 3, 3);

            // ── カーソルと選択範囲の色を固定 (ライトモードの黒カーソル等を防止)
            GUI.skin.settings.cursorColor = TextPrimary;
            GUI.skin.settings.selectionColor = new Color(1f, 1f, 1f, 0.25f);

            // ポップアップは枠線付きカードテクスチャを使用し、9スライス境界を1pxに設定して引き伸ばし縞ノイズを解消
            FixAllStateBackgrounds(EditorStyles.popup, _texCard);
            EditorStyles.popup.border  = new RectOffset(1, 1, 1, 1);
            EditorStyles.popup.padding = new RectOffset(6, 18, 4, 4);
        }

        /// <summary>OnGUI 末尾の finally ブロックで必ず呼ぶ。EditorStyles を元に戻す。</summary>
        public static void PopEditorTheme()
        {
            if (!_overrideActive) return;
            _overrideActive = false;

            if (_backups != null)
            {
                foreach (var backup in _backups)
                {
                    backup.Restore();
                }
            }

            if (_settingsBackupActive)
            {
                GUI.skin.settings.cursorColor = _backupCursorColor;
                GUI.skin.settings.selectionColor = _backupSelectionColor;
                _settingsBackupActive = false;
            }
        }

        /// <summary>テクスチャと状態を明示破棄する（テーマ切り替えやドメインリロード時に安全にクリーンアップするため）。</summary>
        internal static void DisposeTextures()
        {
            PopEditorTheme();

            if (_texSurface0)        Object.DestroyImmediate(_texSurface0);
            if (_texSurface1)        Object.DestroyImmediate(_texSurface1);
            if (_texSurface2)        Object.DestroyImmediate(_texSurface2);
            if (_texCard)            Object.DestroyImmediate(_texCard);
            if (_texAccentCard)      Object.DestroyImmediate(_texAccentCard);
            if (_texSearchField)     Object.DestroyImmediate(_texSearchField);
            if (_texHover)           Object.DestroyImmediate(_texHover);
            if (_texActive)          Object.DestroyImmediate(_texActive);
            if (_texButtonHover)     Object.DestroyImmediate(_texButtonHover);
            if (_texButtonActive)    Object.DestroyImmediate(_texButtonActive);
            if (_texSecondaryActive) Object.DestroyImmediate(_texSecondaryActive);
            if (_texStatusSuccess)   Object.DestroyImmediate(_texStatusSuccess);
            if (_texStatusError)     Object.DestroyImmediate(_texStatusError);

            _texSurface0        = null;
            _texSurface1        = null;
            _texSurface2        = null;
            _texCard            = null;
            _texAccentCard      = null;
            _texSearchField     = null;
            _texHover           = null;
            _texActive          = null;
            _texButtonHover     = null;
            _texButtonActive    = null;
            _texSecondaryActive = null;
            _texStatusSuccess   = null;
            _texStatusError     = null;
            _initialized        = false;
            _backups            = null;
        }

        private static void FixAllStateBackgrounds(GUIStyle style, Texture2D tex)
        {
            style.normal.background    = tex;
            style.hover.background     = tex;
            style.active.background    = tex;
            style.focused.background   = tex;
            style.onNormal.background  = tex;
            style.onHover.background   = tex;
            style.onActive.background  = tex;
            style.onFocused.background = tex;
        }

        // ─── Style Utilities ─────────────────────────────────────────────────

        /// <summary>
        /// GUIStyle の全 state の textColor を同一色に固定する。
        /// hover/active/focused/on* を含む全 state を明示設定して
        /// Unity スキン由来の色の混入を防ぐ。
        /// </summary>
        private static void FixAllTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor    = color;
            style.hover.textColor     = color;
            style.active.textColor    = color;
            style.focused.textColor   = color;
            style.onNormal.textColor  = color;
            style.onHover.textColor   = color;
            style.onActive.textColor  = color;
            style.onFocused.textColor = color;
        }

        // ─── Texture Utilities ───────────────────────────────────────────────

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static Texture2D MakeBorderedTex(Color fillColor, Color borderColor)
        {
            const int size = 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y,
                        (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                            ? borderColor
                            : fillColor);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.hideFlags  = HideFlags.HideAndDontSave;
            return tex;
        }

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >>  8) & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f);
    }
}
