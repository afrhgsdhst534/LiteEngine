using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LiteCircleCollider))]
public class LiteMob : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LiteCircleCollider body;

    [Header("Stats")]
    [Min(0.01f)] [SerializeField] private float moveSpeed = 3.5f;
    [Min(0.01f)] [SerializeField] private float acceleration = 18f;
    [Min(0f)] [SerializeField] private float friction = 14f;
    [Min(0.01f)] [SerializeField] private float health = 10f;

    [Header("Behavior")]
    [SerializeField] private bool faceMovementDirection = false;

    public Status Status { get; private set; } = new Status();

    public Vector3 Velocity { get; private set; }
    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;
    public float Friction => friction;
    public float Health => health;
    public bool IsAlive => health > 0f;
    public bool FaceMovementDirection => faceMovementDirection;
    public LiteCircleCollider Body => body;
    public float GroundY { get; private set; }

    private void Reset()
    {
        body = GetComponent<LiteCircleCollider>();
    }

    private void Awake()
    {
        if (body == null)
            body = GetComponent<LiteCircleCollider>();

        GroundY = transform.position.y;
        Status.Reset();
    }

    private void OnEnable()
    {
        if (LiteMobManager.Instance != null)
            LiteMobManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (LiteMobManager.Instance != null)
            LiteMobManager.Instance.Unregister(this);
    }

    public void SetVelocity(Vector3 value) => Velocity = value;
    public void SetPosition(Vector3 value) => transform.position = value;

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        health = Mathf.Max(0f, health - amount);
    }

    public void Kill() => health = 0f;

    public void FaceVelocity()
    {
        if (!faceMovementDirection) return;

        Vector3 dir = Velocity;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}