using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using GradationBaker;

namespace GradationBaker.UI
{
    // インポート時（コンパイル完了時）や起動時に自動的にアップデートチェックを実行する
    [InitializeOnLoad]
    internal static class GradationBakerVersion
    {
        // version.json の GUID
        private const string VersionJsonGuid = "a0c3b88b0ea74d309087c53d9c3ab2a1";
        // version.json をどうしても読めなかった場合の最終フォールバック
        private const string FallbackVersion = "1.0.0";
        private static string _currentCache = null;

        internal static string Current
        {
            get
            {
                if (string.IsNullOrEmpty(_currentCache))
                {
                    _currentCache = LoadLocalVersion();
                }
                return string.IsNullOrEmpty(_currentCache) ? FallbackVersion : _currentCache;
            }
        }

        // チェック先
        internal const string RepoOwner       = "dennoko";
        internal const string RepoName        = "GradationTextureGenerator";
        internal const string RepoBranch      = "main";
        internal const string VersionFilePath = "version.json";

        // セッションキー
        internal const string VerCheckDoneKey   = "GradGen_VerCheck_Done";
        internal const string VerCheckErrorKey  = "GradGen_VerCheck_Error";
        internal const string VerCheckLatestKey = "GradGen_VerCheck_Latest";
        internal const string VerCheckUrlKey    = "GradGen_VerCheck_Url";
        internal const string VerCheckMessageKey = "GradGen_VerCheck_Message";

        static GradationBakerVersion()
        {
            EditorApplication.delayCall += StartCheckBackgroundTask;
        }

        private static bool _checking;

        internal static void StartCheckBackgroundTask()
        {
            bool done  = SessionState.GetBool(VerCheckDoneKey, false);
            bool error = SessionState.GetBool(VerCheckErrorKey, false);
            if (done && !error) return;
            if (_checking) return;
            _checking = true;

            DennokoVersionChecker.CheckAsync(
                RepoOwner, RepoName, RepoBranch, VersionFilePath, Current, OnVersionChecked);
        }

        private static void OnVersionChecked(DennokoVersionChecker.Result result)
        {
            _checking = false;
            SessionState.SetBool(VerCheckDoneKey, true);
            SessionState.SetBool(VerCheckErrorKey, result.State == DennokoVersionChecker.State.Error);
            SessionState.SetString(VerCheckLatestKey, result.LatestVersion ?? string.Empty);
            SessionState.SetString(VerCheckUrlKey, result.Url ?? string.Empty);
            SessionState.SetString(VerCheckMessageKey, result.Message ?? string.Empty);

            // すでにエディタウィンドウが開かれている場合は再描画を促す
            var windows = Resources.FindObjectsOfTypeAll<GradationBakerWindow>();
            if (windows != null && windows.Length > 0)
            {
                foreach (var w in windows)
                {
                    if (w != null)
                    {
                        w.LoadVersionResultFromSessionState();
                    }
                }
            }
        }

        [Serializable]
        private class VersionInfo
        {
            public string version;
        }

        private static string LoadLocalVersion()
        {
            // 1) GUID 経由
            var v = TryReadVersion(AssetDatabase.GUIDToAssetPath(VersionJsonGuid));
            if (v != null) return v;

            // 2) スクリプト位置からの相対探索
            return TryReadVersion(ResolveVersionJsonByScriptPath());
        }

        private static string TryReadVersion(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var info = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(path));
                if (info != null && !string.IsNullOrEmpty(info.version)) return info.version;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GradationBakerVersion] Failed to read version.json ({path}): {e.Message}");
            }
            return null;
        }

        private static string ResolveVersionJsonByScriptPath([CallerFilePath] string scriptPath = null)
        {
            if (string.IsNullOrEmpty(scriptPath)) return null;
            var dir = Path.GetDirectoryName(scriptPath);
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, "version.json");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        internal static void ForceRecheck()
        {
            if (_checking) return;
            _currentCache = null;
            SessionState.SetBool(VerCheckDoneKey, false);
            SessionState.SetBool(VerCheckErrorKey, false);
            StartCheckBackgroundTask();
        }
    }
}
