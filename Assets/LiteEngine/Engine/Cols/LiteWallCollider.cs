using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class LiteWallCollider : LiteCollider
{
    public static new readonly List<LiteWallCollider> All = new List<LiteWallCollider>(1024);
    private static readonly HashSet<LiteWallCollider> _allSet = new HashSet<LiteWallCollider>();
    public static int Version { get; private set; }

    // X = ширина, Y = высота. Глубина не редактируется в инспекторе.
    [Header("Shape")]
    [Tooltip("X = ширина, Y = высота. Глубина скрыта и фиксирована внутри компонента.")]
    public Vector2 size = new Vector2(4f, 2f);

    public Vector3 center = Vector3.zero;

    // Скрытая физическая толщина по оси глубины.
    private const float Thickness = 0.2f;

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

            float halfW = size.x * sx * 0.5f;
            float halfD = Thickness * sz * 0.5f;

            return Mathf.Sqrt(halfW * halfW + halfD * halfD);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_allSet.Add(this))
            All.Add(this);
        Version++;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_allSet.Remove(this))
            All.Remove(this);
        Version++;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        Version++;
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            transform.hasChanged = false;
            Version++;
        }
    }

    public override bool OverlapCircle(Vector3 circleCenter, float circleRadius, out Vector3 pushOut)
    {
        pushOut = Vector3.zero;

        Vector3 c = WorldCenter;
        c.y = circleCenter.y;

        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude > 0.000001f) right.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.000001f) forward.Normalize();

        float sx = Mathf.Abs(transform.lossyScale.x);
        float sz = Mathf.Abs(transform.lossyScale.z);

        float halfW = size.x * sx * 0.5f;
        float halfD = Thickness * sz * 0.5f;

        Vector3 delta = circleCenter - c;
        float localX = Vector3.Dot(delta, right);
        float localZ = Vector3.Dot(delta, forward);

        float clampedX = Mathf.Clamp(localX, -halfW, halfW);
        float clampedZ = Mathf.Clamp(localZ, -halfD, halfD);

        Vector3 closest = c + right * clampedX + forward * clampedZ;
        Vector3 diff = circleCenter - closest;

        float sqr = diff.sqrMagnitude;
        if (sqr > 0.000001f)
        {
            float dist = Mathf.Sqrt(sqr);
            if (dist >= circleRadius)
                return false;

            pushOut = diff / dist * (circleRadius - dist);
            return true;
        }

        // Центр внутри объёма — выталкиваем к ближайшей грани
        float toRight = halfW - localX;
        float toLeft = halfW + localX;
        float toFront = halfD - localZ;
        float toBack = halfD + localZ;

        float minPenetration = toRight;
        Vector3 normal = right;

        if (toLeft < minPenetration) { minPenetration = toLeft; normal = -right; }
        if (toFront < minPenetration) { minPenetration = toFront; normal = forward; }
        if (toBack < minPenetration) { minPenetration = toBack; normal = -forward; }

        pushOut = normal * (circleRadius + minPenetration);
        return true;
    }

}
