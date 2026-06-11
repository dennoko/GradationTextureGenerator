using System;
using System.Reflection;
using UnityEngine;

namespace GradationBaker.UI
{
    /// <summary>
    /// NDMF (Non-Destructive Modification Framework) のプレビューシステムとの連携ブリッジ。
    /// NDMF がインストールされていない環境でもコンパイル・動作できるよう、
    /// 公開 API (nadena.dev.ndmf.preview.NDMFPreview) にリフレクション経由でアクセスする。
    ///
    /// 役割:
    /// 1. 本ツールのプレビュー表示中は NDMF プレビューを一時停止する
    ///    (DisablePreviewDepth をインクリメント)。NDMF のプロキシ表示と
    ///    本ツールのオーバーレイプレビューが二重に描画される競合を防ぎ、
    ///    ベイク結果(元メッシュ基準)とシーン上の見た目を一致させる。
    /// 2. NDMF プレビューが生成したプロキシオブジェクトが D&D された場合に
    ///    元のオブジェクトへ解決する (GetOriginalObjectForProxy)。
    /// </summary>
    internal static class NdmfPreviewBridge
    {
        private const string NdmfPreviewTypeName = "nadena.dev.ndmf.preview.NDMFPreview, nadena.dev.ndmf";

        private static bool _resolved;
        private static PropertyInfo _disablePreviewDepth;
        private static MethodInfo _getOriginalObjectForProxy;

        // 本ツールが停止要求を出しているか (二重インクリメント防止)
        private static bool _suppressing;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return _disablePreviewDepth != null;
            }
        }

        /// <summary>本ツールのプレビューが NDMF プレビューを停止中かどうか (UI 表示用)。</summary>
        public static bool IsSuppressing => _suppressing;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                Type type = Type.GetType(NdmfPreviewTypeName);
                if (type == null) return;

                _disablePreviewDepth = type.GetProperty(
                    "DisablePreviewDepth",
                    BindingFlags.Public | BindingFlags.Static);

                _getOriginalObjectForProxy = type.GetMethod(
                    "GetOriginalObjectForProxy",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(GameObject) }, null);
            }
            catch (Exception)
            {
                // NDMF 側の API 変更などで失敗しても本体機能には影響させない
                _disablePreviewDepth = null;
                _getOriginalObjectForProxy = null;
            }
        }

        /// <summary>
        /// NDMF プレビューを一時停止する。既に停止要求済みなら何もしない。
        /// 必ず RestorePreview と対で呼ぶこと (ウィンドウ閉鎖時など)。
        /// </summary>
        public static void SuppressPreview()
        {
            if (_suppressing || !IsAvailable) return;

            try
            {
                int depth = (int)_disablePreviewDepth.GetValue(null);
                _disablePreviewDepth.SetValue(null, depth + 1);
                _suppressing = true;
            }
            catch (Exception) { /* NDMF 側の問題は無視 */ }
        }

        /// <summary>SuppressPreview で停止した NDMF プレビューを再開する。</summary>
        public static void RestorePreview()
        {
            if (!_suppressing || !IsAvailable)
            {
                _suppressing = false;
                return;
            }

            try
            {
                int depth = (int)_disablePreviewDepth.GetValue(null);
                if (depth > 0) _disablePreviewDepth.SetValue(null, depth - 1);
            }
            catch (Exception) { /* NDMF 側の問題は無視 */ }
            finally
            {
                _suppressing = false;
            }
        }

        /// <summary>
        /// NDMF プレビューのプロキシなら元の GameObject を返す。
        /// プロキシでない・NDMF 不在の場合はそのまま返す。
        /// </summary>
        public static GameObject ResolveOriginal(GameObject obj)
        {
            if (obj == null) return null;
            Resolve();
            if (_getOriginalObjectForProxy == null) return obj;

            try
            {
                var original = _getOriginalObjectForProxy.Invoke(null, new object[] { obj }) as GameObject;
                return original != null ? original : obj;
            }
            catch (Exception)
            {
                return obj;
            }
        }
    }
}
