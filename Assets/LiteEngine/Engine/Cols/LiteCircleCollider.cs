using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class LiteCircleCollider : LiteCollider
{
    [Header("Shape")]
    [Min(0.01f)] public float radius = 0.25f;
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

    public override bool OverlapCircle(Vector3 circleCenter, float circleRadius, out Vector3 pushOut)
    {
        pushOut = Vector3.zero;

        Vector3 a = WorldCenter;
        Vector3 b = circleCenter;

        a.y = b.y;

        Vector3 delta = b - a;
        float rr = ApproxRadius + circleRadius;
        float sqr = delta.sqrMagnitude;

        if (sqr >= rr * rr)
            return false;

        if (sqr <= 0.000001f)
        {
            Vector3 fallback = transform.right;
            fallback.y = 0f;
            if (fallback.sqrMagnitude <= 0.000001f)
            {
                fallback = transform.forward;
                fallback.y = 0f;
            }
            if (fallback.sqrMagnitude <= 0.000001f)
                fallback = Vector3.right;

            fallback.Normalize();
            pushOut = fallback * rr;
            return true;
        }

        float dist = Mathf.Sqrt(sqr);
        Vector3 dir = delta / dist;
        float penetration = rr - dist;

        pushOut = dir * penetration;
        return true;
    }
    public override void DrawGizmo(bool selected)
    {
        Gizmos.color = selected ? selectedGizmoColor : gizmoColor;
        Gizmos.DrawWireSphere(WorldCenter, ApproxRadius);
        Gizmos.DrawSphere(WorldCenter, Mathf.Min(ApproxRadius * 0.15f, 0.08f));
    }
}