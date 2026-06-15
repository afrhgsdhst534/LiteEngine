using UnityEngine;
using UnityEngine.Rendering;

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

    [Header("PathFinder")]
    public bool isHuggingWall;
    public Vector3 hugNormal;
    public LiteIntelligence intelligenceLevel;
    public float hugSign = 1f;
    public Status Status { get; private set; } = new Status();

    public Vector3 Velocity { get; private set; }
    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;
    public bool IsAlive => health > 0f;
    public LiteCircleCollider Body => body;
    public float GroundY { get; private set; }
    public Transform visualRoot;
    private void Reset()
    {
        body = GetComponent<LiteCircleCollider>();
    }

    private void Awake()
    {
        if (body == null)
            body = GetComponent<LiteCircleCollider>();
        visualRoot = GetComponentInChildren<MeshRenderer>().transform;
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

    public void SetPosition(Vector3 value) => SetWorldCenterPosition(value);

    public void SetWorldCenterPosition(Vector3 worldCenter)
    {
        if (body == null)
        {
            transform.position = worldCenter;
            return;
        }

        Vector3 offset = transform.TransformVector(body.LocalCenter);
        transform.position = worldCenter - offset;
    }


    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        health = Mathf.Max(0f, health - amount);
    }

    public void Kill() => health = 0f;
}