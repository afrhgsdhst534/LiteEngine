using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LiteCollider), true)]
public class LiteColliderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Lite Collider", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Кастомная коллизия для рогалика: видно в Scene View, редактируется ручками, не использует Rigidbody.",
            MessageType.Info
        );

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        LiteCollider col = (LiteCollider)target;
        Transform t = col.transform;

        DrawCenterHandle(col, t);

        if (col is LiteCircleCollider circle)
        {
            DrawCircleRadiusHandle(circle, t);
        }
        else if (col is LiteCapsuleCollider capsule)
        {
            DrawCapsuleRadiusHandle(capsule, t);
            DrawCapsuleHeightHandle(capsule, t);
        }
        else if (col is LiteWallCollider wall)
        {
            DrawWallHandles(wall, t);
        }
    }

    private void DrawCenterHandle(LiteCollider col, Transform t)
    {
        Vector3 worldCenter = col.WorldCenter;

        EditorGUI.BeginChangeCheck();
        Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, t.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(col, "Move Lite Collider Center");
            col.LocalCenter = t.InverseTransformPoint(newWorldCenter);
            EditorUtility.SetDirty(col);
        }
    }

    private void DrawCircleRadiusHandle(LiteCircleCollider circle, Transform t)
    {
        Vector3 center = circle.WorldCenter;
        Vector3 right = t.right.normalized;
        float worldRadius = circle.ApproxRadius;
        float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;

        Vector3 radiusHandlePos = center + right * worldRadius;

        EditorGUI.BeginChangeCheck();
        Vector3 newRadiusHandlePos = Handles.Slider(radiusHandlePos, right, handleSize, Handles.SphereHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(circle, "Resize Lite Circle Radius");

            float newWorldRadius = Mathf.Abs(Vector3.Dot(newRadiusHandlePos - center, right));
            float scale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z)));

            circle.radius = Mathf.Max(0.01f, newWorldRadius / scale);
            EditorUtility.SetDirty(circle);
        }

        Handles.Label(center + Vector3.up * (circle.ApproxRadius + 0.25f), $"Circle  R:{circle.radius:0.00}");
    }

    private void DrawCapsuleRadiusHandle(LiteCapsuleCollider capsule, Transform t)
    {
        Vector3 center = capsule.WorldCenter;
        Vector3 right = t.right.normalized;
        float worldRadius = capsule.ApproxRadius;
        float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;

        Vector3 radiusHandlePos = center + right * worldRadius;

        EditorGUI.BeginChangeCheck();
        Vector3 newRadiusHandlePos = Handles.Slider(radiusHandlePos, right, handleSize, Handles.SphereHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(capsule, "Resize Lite Capsule Radius");

            float newWorldRadius = Mathf.Abs(Vector3.Dot(newRadiusHandlePos - center, right));
            float scale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z)));

            capsule.radius = Mathf.Max(0.01f, newWorldRadius / scale);
            if (capsule.height < capsule.radius * 2f)
                capsule.height = capsule.radius * 2f;

            EditorUtility.SetDirty(capsule);
        }

        Handles.Label(center + Vector3.up * (capsule.WorldHeight + 0.25f), $"Capsule  R:{capsule.radius:0.00}  H:{capsule.height:0.00}");
    }

    private void DrawCapsuleHeightHandle(LiteCapsuleCollider capsule, Transform t)
    {
        Vector3 center = capsule.WorldCenter;
        Vector3 up = t.up.normalized;
        float worldHeight = capsule.WorldHeight;
        float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;

        Vector3 topHandle = center + up * (worldHeight * 0.5f);

        EditorGUI.BeginChangeCheck();
        Vector3 newTopHandle = Handles.Slider(topHandle, up, handleSize, Handles.SphereHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(capsule, "Resize Lite Capsule Height");

            float halfHeightWorld = Mathf.Abs(Vector3.Dot(newTopHandle - center, up));
            halfHeightWorld = Mathf.Max(halfHeightWorld, capsule.ApproxRadius);

            float newWorldHeight = halfHeightWorld * 2f;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(t.lossyScale.y));

            capsule.height = Mathf.Max(0.02f, newWorldHeight / scaleY);
            EditorUtility.SetDirty(capsule);
        }
    }

    private void DrawWallHandles(LiteWallCollider wall, Transform t)
    {
        Vector3 center = wall.WorldCenter;
        float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;

        // Оси объекта с учётом его поворота
        GetAxis(t, Vector3.right, out Vector3 xDir, out float xScale);
        GetAxis(t, Vector3.forward, out Vector3 zDir, out float zScale);

        Vector3 xHandle = center + xDir * (wall.size.x * xScale * 0.5f);
        Vector3 zHandle = center + zDir * (wall.size.y * zScale * 0.5f);

        DrawWallOutline(center, xDir, zDir, wall.size.x * xScale, wall.size.y * zScale);

        EditorGUI.BeginChangeCheck();
        Vector3 newXHandle = Handles.Slider(xHandle, xDir, handleSize, Handles.CubeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(wall, "Resize Lite Wall X");

            float halfWorld = Mathf.Abs(Vector3.Dot(newXHandle - center, xDir));
            float newLocalX = halfWorld * 2f / Mathf.Max(0.0001f, xScale);

            wall.size = new Vector2(Mathf.Max(0.1f, newLocalX), wall.size.y);
            EditorUtility.SetDirty(wall);
        }

        EditorGUI.BeginChangeCheck();
        Vector3 newZHandle = Handles.Slider(zHandle, zDir, handleSize, Handles.CubeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(wall, "Resize Lite Wall Z");

            float halfWorld = Mathf.Abs(Vector3.Dot(newZHandle - center, zDir));
            float newLocalZ = halfWorld * 2f / Mathf.Max(0.0001f, zScale);

            wall.size = new Vector2(wall.size.x, Mathf.Max(0.1f, newLocalZ));
            EditorUtility.SetDirty(wall);
        }

        Handles.Label(center + t.up * 0.25f, $"Wall X:{wall.size.x:0.0} Z:{wall.size.y:0.0}");
    }

    private void DrawWallOutline(Vector3 center, Vector3 xDir, Vector3 zDir, float worldX, float worldZ)
    {
        float halfX = worldX * 0.5f;
        float halfZ = worldZ * 0.5f;

        Vector3 p0 = center - xDir * halfX - zDir * halfZ;
        Vector3 p1 = center + xDir * halfX - zDir * halfZ;
        Vector3 p2 = center + xDir * halfX + zDir * halfZ;
        Vector3 p3 = center - xDir * halfX + zDir * halfZ;

        Handles.DrawAAPolyLine(2f, p0, p1, p2, p3, p0);
    }

    private void DrawWallOutlineXZ(Vector3 center, Vector3 xDir, Vector3 zDir, float worldX, float worldZ)
    {
        float halfX = worldX * 0.5f;
        float halfZ = worldZ * 0.5f;

        Vector3 p0 = center - xDir * halfX - zDir * halfZ;
        Vector3 p1 = center + xDir * halfX - zDir * halfZ;
        Vector3 p2 = center + xDir * halfX + zDir * halfZ;
        Vector3 p3 = center - xDir * halfX + zDir * halfZ;

        Handles.DrawAAPolyLine(2f, new Vector3[] { p0, p1, p2, p3, p0 });
    }

    private static void GetAxisXZ(Transform t, Vector3 localAxis, out Vector3 worldDir, out float worldScale)
    {
        Vector3 v = t.TransformVector(localAxis);
        v.y = 0f;
        worldScale = v.magnitude;

        if (worldScale < 0.000001f)
        {
            worldDir = localAxis.normalized;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.000001f) worldDir.Normalize();
            worldScale = 0.000001f;
            return;
        }

        worldDir = v / worldScale;
    }
    private static void GetAxis(Transform t, Vector3 localAxis, out Vector3 worldDir, out float worldScale)
    {
        Vector3 v = t.TransformVector(localAxis);
        worldScale = v.magnitude;

        if (worldScale < 0.000001f)
        {
            worldDir = localAxis.normalized;
            worldScale = 0.000001f;
            return;
        }

        worldDir = v / worldScale;
    }
}