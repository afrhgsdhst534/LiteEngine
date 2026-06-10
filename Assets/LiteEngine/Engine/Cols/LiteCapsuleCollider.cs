using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class LiteCapsuleCollider : LiteCollider
{
    [Header("Shape")]
    [Min(0.01f)] public float radius = 0.4f;
    [Min(0.02f)] public float height = 1.8f;
    public Vector3 center = Vector3.zero;

    public override Vector3 LocalCenter
    {
        get => center;
        set => center = value;
    }

    public override Vector3 WorldCenter => transform.TransformPoint(center);

    public override float ApproxRadius
    {
        get
        {
            float sx = Mathf.Abs(transform.lossyScale.x);
            float sz = Mathf.Abs(transform.lossyScale.z);
            return radius * Mathf.Max(sx, sz);
        }
    }

    public float WorldHeight
    {
        get
        {
            float sy = Mathf.Abs(transform.lossyScale.y);
            float h = height * sy;
            return Mathf.Max(h, ApproxRadius * 2f);
        }
    }


    public override bool OverlapCircle(Vector3 circleCenter, float circleRadius, out Vector3 pushOut)
    {
        pushOut = Vector3.zero;

        // 1. Вычисляем WorldCenter и Radius прямо здесь
        Vector3 centerWorld = WorldCenter;
        float r = ApproxRadius;

        // 2. Вычисляем позиции верха и низа капсулы (как это сделано в твоём GetWorldCapsule)
        float halfSegment = Mathf.Max(0f, WorldHeight * 0.5f - r);
        Vector3 up = transform.up.normalized;

        Vector3 pointA = centerWorld + up * halfSegment;
        Vector3 pointB = centerWorld - up * halfSegment;

        // 3. "Сплющиваем" всё по плоскости XZ
        pointA.y = 0f;
        pointB.y = 0f;
        Vector3 centerFlat = circleCenter;
        centerFlat.y = 0f;

        // 4. Логика поиска ближайшей точки на отрезке
        Vector3 ab = pointB - pointA;
        float t = 0f;

        if (ab.sqrMagnitude > 0.000001f)
        {
            t = Vector3.Dot(centerFlat - pointA, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
        }

        Vector3 closestPoint = pointA + t * ab;

        // 5. Проверка дистанции
        Vector3 delta = centerFlat - closestPoint;
        float rr = r + circleRadius;
        float sqr = delta.sqrMagnitude;

        if (sqr >= rr * rr || sqr <= 0.000001f)
            return false;

        float dist = Mathf.Sqrt(sqr);
        Vector3 dir = delta / dist;
        float penetration = rr - dist;

        pushOut = dir * penetration;
        return true;
    }
    public void GetWorldCapsule(out Vector3 pointA, out Vector3 pointB, out float worldRadius)
    {
        Vector3 centerWorld = WorldCenter;
        worldRadius = ApproxRadius;

        float halfSegment = Mathf.Max(0f, WorldHeight * 0.5f - worldRadius);
        Vector3 up = transform.up.normalized;

        pointA = centerWorld + up * halfSegment;
        pointB = centerWorld - up * halfSegment;
    }

    public override void DrawGizmo(bool selected)
    {
        Gizmos.color = selected ? SelectedGizmoColor : GizmoColor;

        GetWorldCapsule(out Vector3 top, out Vector3 bottom, out float r);

        Gizmos.DrawWireSphere(top, r);
        Gizmos.DrawWireSphere(bottom, r);

        Vector3 right = transform.right.normalized * r;
        Vector3 forward = transform.forward.normalized * r;

        Gizmos.DrawLine(top + right, bottom + right);
        Gizmos.DrawLine(top - right, bottom - right);
        Gizmos.DrawLine(top + forward, bottom + forward);
        Gizmos.DrawLine(top - forward, bottom - forward);
    }
}