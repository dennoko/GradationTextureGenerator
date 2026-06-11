using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace GradationBaker.Localization
{
    public static class LocalizationManager
    {
        public enum Language
        {
            Japanese = 0,
            English = 1
        }

        private const string PrefsKey = "GradGen_Language";
        private static Language _currentLanguage = Language.Japanese;
        private static Dictionary<string, string> _strings = new Dictionary<string, string>();
        private static bool _isInitialized = false;

        public static Language CurrentLanguage => _currentLanguage;

        public static void Initialize()
        {
            if (_isInitialized) return;
            
            int savedLang = EditorPrefs.GetInt(PrefsKey, 0);
            _currentLanguage = (Language)savedLang;
            LoadStrings();
            _isInitialized = true;
        }

        public static void SetLanguage(Language lang)
        {
            if (_currentLanguage == lang && _isInitialized) return;
            
            _currentLanguage = lang;
            EditorPrefs.SetInt(PrefsKey, (int)lang);
            LoadStrings();
        }

        public static string Get(string key)
        {
            if (!_isInitialized) Initialize();
            
            if (_strings.TryGetValue(key, out var value))
            {
                return value;
            }
            return key; // Return key as fallback
        }

        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        private static void LoadStrings()
        {
            _strings.Clear();
            
            string fileName = _currentLanguage == Language.Japanese ? "ja.json" : "en.json";
            string basePath = GetLocalizationFolderPath();
            string filePath = Path.Combine(basePath, fileName);
            
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[LocalizationManager] Localization file not found: {filePath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                _strings = ParseJson(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LocalizationManager] Failed to load localization: {e.Message}");
            }
        }

        private static string GetLocalizationFolderPath()
        {
            // 1) このソースファイル自身のパスから導出する (最も確実)。
            //    FindAssets による名前検索は、他ツール (例: FastAOBaker) の同名スクリプトが
            //    先にヒットすると誤ったフォルダを返すため使わない。
            string sourcePath = GetSourceFilePath();
            if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
            {
                return Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            }

            // 2) AssetDatabase 検索 (パスにこのツールのフォルダが含まれるものだけ採用)
            string[] guids = AssetDatabase.FindAssets("LocalizationManager t:Script");
            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (scriptPath.Contains("GradationBaker"))
                {
                    return Path.GetDirectoryName(scriptPath).Replace('\\', '/');
                }
            }

            // 3) Fallback path
            return "Assets/Editor/GradationBaker/Localization";
        }

        /// <summary>コンパイル時にこのファイルの絶対パスが埋め込まれる。</summary>
        private static string GetSourceFilePath(
            [System.Runtime.CompilerServices.CallerFilePath] string path = null) => path;

        /// <summary>
        /// Simple JSON parser for flat string dictionaries
        /// </summary>
        private static Dictionary<string, string> ParseJson(string json)
        {
            var result = new Dictionary<string, string>();
            
            // Remove whitespace and braces
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);
            
            // Split by lines and parse key-value pairs
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed == "{" || trimmed == "}") continue;
                
                // Remove trailing comma
                if (trimmed.EndsWith(",")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                
                // Find the first colon (key-value separator)
                int colonIndex = trimmed.IndexOf(':');
                if (colonIndex < 0) continue;
                
                string key = trimmed.Substring(0, colonIndex).Trim().Trim('"');
                string value = trimmed.Substring(colonIndex + 1).Trim().Trim('"');
                
                // Unescape special characters
                value = value.Replace("\\n", "\n").Replace("\\\"", "\"");
                
                result[key] = value;
            }
            
            return result;
        }

        /// <summary>
        /// Draw language toggle checkbox and return true if changed
        /// Shows "Enable English Mode" when in Japanese, "英語表記を有効匁E when in English
        /// </summary>
        public static bool DrawLanguageSelector()
        {
            if (!_isInitialized) Initialize();
            
            EditorGUI.BeginChangeCheck();
            
            // Show opposite language label for intuitive switching
            string label = _currentLanguage == Language.Japanese 
                ? "Enable English Mode" 
                : "英語表記を有効化";
            
            bool isEnglish = _currentLanguage == Language.English;
            bool newIsEnglish = EditorGUILayout.ToggleLeft(label, isEnglish, GUILayout.Width(140));
            
            if (EditorGUI.EndChangeCheck() && newIsEnglish != isEnglish)
            {
                SetLanguage(newIsEnglish ? Language.English : Language.Japanese);
                return true;
            }
            
            return false;
        }
    }
}
