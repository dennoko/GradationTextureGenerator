using UnityEngine;
using UnityEditor;
using GradationBaker.Data;

namespace GradationBaker.UI
{
    /// <summary>
    /// Indicates what type of change was made via the scene handle
    /// </summary>
    [System.Flags]
    public enum HandleChangeType
    {
        None = 0,
        Rotation = 1,
        Position = 2,
        Height = 4
    }
    
    public class GradationSceneHandle
    {
        private Quaternion _lastRotation;
        private static readonly Color BoxColor = new Color(1f, 1f, 0f, 0.3f);
        private static readonly Color BoxOutlineColor = new Color(1f, 1f, 0f, 0.8f);
        private static readonly Color TopHandleColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color BottomHandleColor = new Color(0.3f, 1f, 0.3f, 1f);

        public HandleChangeType DrawHandle(GradationSettings settings, Transform targetTransform)
        {
            HandleChangeType changeType = HandleChangeType.None;
            
            Vector3 center = settings.BoxCenter;
            Quaternion rotation = settings.BoxRotation;
            
            float handleSize = HandleUtility.GetHandleSize(center);

            // 1. Position Handle at center
            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(center, rotation);
            if (EditorGUI.EndChangeCheck())
            {
                if (targetTransform != null) Undo.RecordObject(targetTransform, "Move Gradation Box");
                settings.BoxCenter = newCenter;
                changeType |= HandleChangeType.Position;
            }
            
            // 2. Rotation Handle at center
            EditorGUI.BeginChangeCheck();
            Quaternion newRotation = Handles.RotationHandle(rotation, center);
            if (EditorGUI.EndChangeCheck())
            {
                if (targetTransform != null) Undo.RecordObject(targetTransform, "Rotate Gradation Box");
                settings.BoxRotation = newRotation;
                changeType |= HandleChangeType.Rotation;
            }

            if (settings.Shape == GradationShape.Linear)
            {
                changeType |= DrawDimensionHandles(settings, targetTransform, Vector3.up, settings.BoxHeight, val => settings.BoxHeight = val, handleSize, TopHandleColor, BottomHandleColor);
                
                // Draw labels for Linear
                Vector3 upDir = rotation * Vector3.up;
                Vector3 topPos = center + upDir * (settings.BoxHeight / 2f);
                Vector3 bottomPos = center - upDir * (settings.BoxHeight / 2f);
                DrawLabels(settings, topPos, bottomPos, handleSize);
            }
            else
            {
                // Spherical mode uses 6 slider handles for 3 axes
                changeType |= DrawDimensionHandles(settings, targetTransform, Vector3.right, settings.BoxWidth, val => settings.BoxWidth = val, handleSize, TopHandleColor, BottomHandleColor);
                changeType |= DrawDimensionHandles(settings, targetTransform, Vector3.up, settings.BoxHeight, val => settings.BoxHeight = val, handleSize, TopHandleColor, BottomHandleColor);
                changeType |= DrawDimensionHandles(settings, targetTransform, Vector3.forward, settings.BoxDepth, val => settings.BoxDepth = val, handleSize, TopHandleColor, BottomHandleColor);
            }

            // 5. Draw visualization
            DrawVisualization(settings);
            
            return changeType;
        }

        private HandleChangeType DrawDimensionHandles(GradationSettings settings, Transform targetTransform, Vector3 localAxis, float currentDimension, System.Action<float> setDimension, float handleSize, Color posColor, Color negColor)
        {
            HandleChangeType changeType = HandleChangeType.None;
            Vector3 center = settings.BoxCenter;
            Vector3 worldAxis = settings.BoxRotation * localAxis;
            float halfDim = currentDimension / 2f;
            Vector3 posPos = center + worldAxis * halfDim;
            Vector3 negPos = center - worldAxis * halfDim;
            
            // Positive side slider
            Handles.color = posColor;
            EditorGUI.BeginChangeCheck();
            Vector3 newPosPos = Handles.Slider(posPos, worldAxis, handleSize * 0.15f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                if (targetTransform != null) Undo.RecordObject(targetTransform, "Adjust Gradation Handle");
                float newDistance = Vector3.Dot(newPosPos - negPos, worldAxis);
                if (newDistance > 0.01f)
                {
                    setDimension(newDistance);
                    settings.BoxCenter = negPos + worldAxis * (newDistance / 2f);
                    changeType |= HandleChangeType.Height;
                }
            }

            // Negative side slider
            Handles.color = negColor;
            EditorGUI.BeginChangeCheck();
            Vector3 newNegPos = Handles.Slider(negPos, -worldAxis, handleSize * 0.15f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                if (targetTransform != null) Undo.RecordObject(targetTransform, "Adjust Gradation Handle");
                float newDistance = Vector3.Dot(posPos - newNegPos, worldAxis);
                if (newDistance > 0.01f)
                {
                    setDimension(newDistance);
                    settings.BoxCenter = newNegPos + worldAxis * (newDistance / 2f);
                    changeType |= HandleChangeType.Height;
                }
            }
            return changeType;
        }

        private void DrawVisualization(GradationSettings settings)
        {
            Vector3 center = settings.BoxCenter;
            Quaternion rotation = settings.BoxRotation;
            Vector3 size = settings.BoxScale;
            
            Matrix4x4 oldMatrix = Handles.matrix;
            
            if (settings.Shape == GradationShape.Linear)
            {
                Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                
                // Draw semi-transparent cube
                Handles.color = BoxColor;
                Handles.DrawWireCube(Vector3.zero, size);
                
                // Draw solid outline
                Handles.color = BoxOutlineColor;
                DrawBoxEdges(size);
                
                // Draw gradient direction arrow
                Handles.color = Color.yellow;
                float arrowLength = settings.BoxHeight * 0.3f;
                Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.LookRotation(Vector3.up), arrowLength, EventType.Repaint);
            }
            else
            {
                // Draw Ellipsoid (Spherical mode)
                // Use scale of bounds, radius 0.5 makes diameter = bounds
                Handles.matrix = Matrix4x4.TRS(center, rotation, size);
                
                Handles.color = BoxOutlineColor;
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, 0.5f);
                Handles.DrawWireDisc(Vector3.zero, Vector3.right, 0.5f);
                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 0.5f);
                
                Handles.color = new Color(BoxOutlineColor.r, BoxOutlineColor.g, BoxOutlineColor.b, 0.2f);
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
                
                // Draw "Up" arrow indicator for primary gradient direction from center
                Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                Handles.color = Color.yellow;
                Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.LookRotation(Vector3.up), size.y * 0.3f, EventType.Repaint);
            }
            
            Handles.matrix = oldMatrix;
        }

        private void DrawBoxEdges(Vector3 size)
        {
            float hw = size.x / 2f;
            float hh = size.y / 2f;
            float hd = size.z / 2f;
            
            // Top face
            Vector3 t0 = new Vector3(-hw, hh, -hd);
            Vector3 t1 = new Vector3(hw, hh, -hd);
            Vector3 t2 = new Vector3(hw, hh, hd);
            Vector3 t3 = new Vector3(-hw, hh, hd);
            
            // Bottom face
            Vector3 b0 = new Vector3(-hw, -hh, -hd);
            Vector3 b1 = new Vector3(hw, -hh, -hd);
            Vector3 b2 = new Vector3(hw, -hh, hd);
            Vector3 b3 = new Vector3(-hw, -hh, hd);
            
            // Top face edges
            Handles.DrawLine(t0, t1);
            Handles.DrawLine(t1, t2);
            Handles.DrawLine(t2, t3);
            Handles.DrawLine(t3, t0);
            
            // Bottom face edges
            Handles.DrawLine(b0, b1);
            Handles.DrawLine(b1, b2);
            Handles.DrawLine(b2, b3);
            Handles.DrawLine(b3, b0);
            
            // Vertical edges
            Handles.DrawLine(t0, b0);
            Handles.DrawLine(t1, b1);
            Handles.DrawLine(t2, b2);
            Handles.DrawLine(t3, b3);
        }

        private void DrawLabels(GradationSettings settings, Vector3 topPos, Vector3 bottomPos, float handleSize)
        {
            GUIStyle labelStyle = new GUIStyle
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            
            // Add background for better visibility
            GUIStyle bgStyle = new GUIStyle(labelStyle)
            {
                normal = { background = MakeTexture(1, 1, new Color(0, 0, 0, 0.5f)) }
            };
            
            Vector3 offset = settings.BoxRotation * Vector3.right * handleSize * 0.5f;
            Handles.Label(topPos + offset, $"Max (1.0)", bgStyle);
            Handles.Label(bottomPos + offset, $"Min (0.0)", bgStyle);
            
            // Height label
            Vector3 midPoint = (topPos + bottomPos) / 2f + settings.BoxRotation * Vector3.right * handleSize * 0.8f;
            Handles.Label(midPoint, $"Height: {settings.BoxHeight:F2}", bgStyle);
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        /// <summary>
        /// Draws a visualization of the mirrored gradation box
        /// </summary>
        public void DrawMirrorHandle(GradationSettings settings)
        {
            if (!settings.UseMirror || settings.MirrorAxis == MirrorAxis.None)
                return;
            
            // Get primary renderer for origin reference
            Renderer primaryRenderer = settings.GetPrimaryRenderer();
            Transform transform = primaryRenderer != null ? primaryRenderer.transform : null;
            
            var (mirrorCenter, mirrorRotation) = settings.GetMirroredBox(transform);
            
            Vector3 size = settings.BoxScale;
            
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(mirrorCenter, mirrorRotation, Vector3.one);
            
            // Draw mirrored cube with different color (cyan)
            Handles.color = new Color(0f, 1f, 1f, 0.3f);
            Handles.DrawWireCube(Vector3.zero, size);
            
            // Draw edges
            Handles.color = new Color(0f, 1f, 1f, 0.6f);
            DrawBoxEdges(size);
            
            // Draw mirror arrow
            Handles.color = Color.cyan;
            float arrowLength = settings.BoxHeight * 0.3f;
            Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.LookRotation(Vector3.up), arrowLength, EventType.Repaint);
            
            Handles.matrix = oldMatrix;
            
            // Draw mirror axis indicator line
            Vector3 origin = transform != null ? transform.position : Vector3.zero;
            Handles.color = new Color(1f, 0f, 1f, 0.5f);
            
            Vector3 axisDir = Vector3.zero;
            switch (settings.MirrorAxis)
            {
                case MirrorAxis.X: axisDir = Vector3.right; break;
                case MirrorAxis.Y: axisDir = Vector3.up; break;
                case MirrorAxis.Z: axisDir = Vector3.forward; break;
            }
            
            float lineLength = 2f;
            Handles.DrawDottedLine(origin - axisDir * lineLength, origin + axisDir * lineLength, 5f);
        }

        public void Cleanup()
        {
            // No persistent objects to cleanup
        }
    }
}
