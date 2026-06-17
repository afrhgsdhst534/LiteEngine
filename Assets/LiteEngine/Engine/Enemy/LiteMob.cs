using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(LiteCircleCollider))]
public class LiteMob : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LiteCircleCollider body;

    [Header("Stats")]
    [Min(0)] [SerializeField] private int moveSpeed = 3;
    [Min(0.01f)] [SerializeField] private float acceleration = 18f;
    [Min(0)] [SerializeField] private int health = 10;

    [Header("PathFinder")]
    public bool isHuggingWall;
    public Vector3 hugNormal;
    public LiteIntelligence intelligenceLevel;
    public float hugSign = 1f;
    public Status Status { get; private set; } = new Status();
    public int Health
    {
        get => health;
        set
        {
            health = Mathf.Max(0, value);
            if (health <= 0)
            {
                Die();
            }
        }
    }
    public Vector3 Velocity { get; private set; }
    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;

    public LiteCircleCollider Body => body;
    public float GroundY { get; private set; }
    public Transform visualRoot;
    public int maxHealth;

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


    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;

        Health -= amount;
    }
    public void Kill() => Health = 0;
    public void Die()
    {
        OnDespawn();
        Pool.Despawn(gameObject);
    }
    public SimplePool Pool { get; set; }

    public void OnSpawn(Vector3 position)
    {
        transform.position = position;

        health = maxHealth;

        Velocity = Vector3.zero;

        Status.Reset();

        isHuggingWall = false;
        hugNormal = Vector3.zero;
        hugSign = 1f;

        gameObject.SetActive(true);
    }
    public void OnDespawn()
    {

        ResetMob();
        gameObject.SetActive(false);
    }
    private void ResetMob()
    {
        Status.Reset();
        Velocity = Vector3.zero;
        hugNormal = Vector3.zero;
        isHuggingWall = false;
        hugSign = 0;
    }
}