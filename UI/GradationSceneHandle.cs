using UnityEngine;
using UnityEditor;

namespace GradationTextureGenerator.UI
{
    public class GradationSceneHandle
    {
        public void DrawHandle(Vector3 center, ref Vector3 direction)
        {
            if (direction == Vector3.zero) direction = Vector3.up;

            Handles.color = Color.cyan;
            
            // Visualize direction
            float size = HandleUtility.GetHandleSize(center);
            Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(direction), size, EventType.Repaint);
            
            // Rotation Handle
            EditorGUI.BeginChangeCheck();
            Quaternion rotation = Quaternion.LookRotation(direction);
            Quaternion newRotation = Handles.Disc(rotation, center, Vector3.right, size, false, 0.1f);
            newRotation = Handles.Disc(newRotation, center, Vector3.up, size, false, 0.1f);
            newRotation = Handles.Disc(newRotation, center, Vector3.forward, size, false, 0.1f);
            
            // Or use standard RotationHandle which is easier
            // newRotation = Handles.RotationHandle(rotation, center); 
            // RotationHandle is a bit intrusive if always on. Let's use free move handle logic or just rotation handle.
            // Let's stick to standard Rotation Handle for familiarity.
            newRotation = Handles.RotationHandle(rotation, center);

            if (EditorGUI.EndChangeCheck())
            {
                direction = newRotation * Vector3.forward;
            }
        }
    }
}
