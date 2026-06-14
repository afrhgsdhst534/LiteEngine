using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public abstract class LiteCollider : MonoBehaviour
{
    public static readonly List<LiteCollider> All = new List<LiteCollider>(1024);

    [Header("Flags")]
    [SerializeField] private bool blocksMovement = true;
    [SerializeField] private bool isTrigger = false;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawWhenSelected = true;
    [SerializeField] private bool drawWhenUnselected = true;
    [SerializeField] public Color gizmoColor = new Color(0.15f, 0.8f, 1f, 0.75f);
    [SerializeField] public Color selectedGizmoColor = new Color(0.15f, 0.8f, 1f, 1f);

    internal int TriggerGridStamp;

    public bool IsTrigger => isTrigger;
    public bool BlocksMovement => blocksMovement && !isTrigger;

    public abstract Vector3 LocalCenter { get; set; }
    public abstract Vector3 WorldCenter { get; }
    public abstract float ApproxRadius { get; }

    protected virtual void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);

        LiteTriggerGrid.Register(this);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);

        LiteTriggerGrid.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        All.Remove(this);

        LiteTriggerGrid.Unregister(this);
    }

    protected virtual void OnValidate()
    {
        LiteTriggerGrid.SyncCollider(this);
    }

    // Вызывай это из кода движения, а не через transform.hasChanged.
    public void NotifyMoved()
    {
        LiteTriggerGrid.SyncCollider(this);
    }

    public abstract bool OverlapCircle(Vector3 circleCenter, float circleRadius, out Vector3 pushOut);

    public virtual void DrawGizmo(bool selected) { }

    protected virtual void OnDrawGizmos()
    {
        if (!drawGizmos || !drawWhenUnselected) return;
        DrawGizmo(false);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !drawWhenSelected) return;
        DrawGizmo(true);
    }
}