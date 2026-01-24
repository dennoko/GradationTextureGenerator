using UnityEngine;
using UnityEditor;

namespace GradationTextureGenerator.UI
{
    public class StatusBar
    {
        public enum StatusType
        {
            Idle,
            Success,
            Error,
            Info
        }

        private string _message = "";
        private StatusType _type = StatusType.Idle;
        private double _clearTime = 0;
        private const float DefaultDuration = 5f;

        // Colors
        private static readonly Color SuccessColor = new Color(0.3f, 0.75f, 0.3f, 1f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        private static readonly Color InfoColor = new Color(0.3f, 0.6f, 0.9f, 1f);
        private static readonly Color IdleColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        private static readonly Color SuccessBg = new Color(0.2f, 0.35f, 0.2f, 1f);
        private static readonly Color ErrorBg = new Color(0.35f, 0.15f, 0.15f, 1f);
        private static readonly Color InfoBg = new Color(0.15f, 0.25f, 0.35f, 1f);
        private static readonly Color IdleBg = new Color(0.2f, 0.2f, 0.2f, 0.3f);

        /// <summary>
        /// Shows a status message
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="type">Status type</param>
        /// <param name="duration">Duration in seconds (0 = permanent until next message)</param>
        public void Show(string message, StatusType type, float duration = DefaultDuration)
        {
            _message = message;
            _type = type;
            
            if (duration > 0)
            {
                _clearTime = EditorApplication.timeSinceStartup + duration;
            }
            else
            {
                _clearTime = 0; // Permanent
            }
        }

        /// <summary>
        /// Clears the status message
        /// </summary>
        public void Clear()
        {
            _message = "";
            _type = StatusType.Idle;
            _clearTime = 0;
        }

        /// <summary>
        /// Draws the status bar at the current position
        /// Call this at the end of OnGUI
        /// </summary>
        public void Draw()
        {
            // Check if should auto-clear
            if (_clearTime > 0 && EditorApplication.timeSinceStartup > _clearTime)
            {
                Clear();
            }

            // Get colors based on type
            Color textColor, bgColor;
            string icon;
            
            switch (_type)
            {
                case StatusType.Success:
                    textColor = SuccessColor;
                    bgColor = SuccessBg;
                    icon = "✓ ";
                    break;
                case StatusType.Error:
                    textColor = ErrorColor;
                    bgColor = ErrorBg;
                    icon = "✗ ";
                    break;
                case StatusType.Info:
                    textColor = InfoColor;
                    bgColor = InfoBg;
                    icon = "ⓘ ";
                    break;
                default:
                    textColor = IdleColor;
                    bgColor = IdleBg;
                    icon = "";
                    break;
            }

            // Draw background
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(22), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, bgColor);

            // Draw text
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor },
                padding = new RectOffset(8, 8, 0, 0),
                fontStyle = _type != StatusType.Idle ? FontStyle.Bold : FontStyle.Normal
            };

            string displayText = string.IsNullOrEmpty(_message) ? "" : icon + _message;
            GUI.Label(rect, displayText, style);
        }

        /// <summary>
        /// Returns true if the status bar needs repainting (for auto-clear)
        /// </summary>
        public bool NeedsRepaint()
        {
            return _clearTime > 0 && EditorApplication.timeSinceStartup < _clearTime;
        }
    }
}
