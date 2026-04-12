using UnityEngine;
using UnityEditor;

namespace GradationBaker.UI
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

        /// <summary>
        /// Shows a status message
        /// </summary>
        public void Show(string message, StatusType type, float duration = DefaultDuration)
        {
            _message = message;
            _type = type;
            _clearTime = duration > 0
                ? EditorApplication.timeSinceStartup + duration
                : 0;
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
        /// Draws the status bar using the GradationBakerTheme styles.
        /// Call this at the end of OnGUI.
        /// </summary>
        public void Draw()
        {
            // Auto-clear
            if (_clearTime > 0 && EditorApplication.timeSinceStartup > _clearTime)
                Clear();

            GradationBakerTheme.Initialize();

            string displayText;
            GUIStyle style;

            switch (_type)
            {
                case StatusType.Success:
                    displayText = string.IsNullOrEmpty(_message) ? "" : _message;
                    style = GradationBakerTheme.StatusSuccessStyle;
                    break;
                case StatusType.Error:
                    displayText = string.IsNullOrEmpty(_message) ? "" : _message;
                    style = GradationBakerTheme.StatusErrorStyle;
                    break;
                case StatusType.Info:
                    displayText = string.IsNullOrEmpty(_message) ? "" : _message;
                    style = GradationBakerTheme.StatusInfoStyle;
                    break;
                default: // Idle
                    displayText = "Ready";
                    style = GradationBakerTheme.StatusInfoStyle;
                    break;
            }

            GUILayout.Box(displayText, style, GUILayout.ExpandWidth(true));
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
