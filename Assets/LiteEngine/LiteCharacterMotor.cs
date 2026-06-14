using System.Collections.Generic;
using UnityEngine;

public class LiteCharacterMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LiteCollider body;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float friction = 16f;
    [SerializeField] private int subSteps = 2;

    [Header("Collision")]
    [SerializeField] private float skinWidth = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool logMovement = false;

    public Vector3 Velocity => velocity;

    private Vector2 moveInput;
    private Vector3 velocity;
    private Vector3 externalForce;

    //  ешируем в Awake Ч GetComponent не вызываетс€ в гор€чем пути
    private ILiteTriggerHandler _myTriggerHandler;

    // ѕереиспользуемый буфер Ч без аллокаций в FixedUpdate
    private readonly List<LiteCircleCollider> _nearbyMobs = new List<LiteCircleCollider>(32);

    private void Reset()
    {
        body = GetComponent<LiteCollider>();
    }

    private void Awake()
    {
            body = GetComponent<LiteCollider>();

    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetMoveDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude > 0.0001f)
            worldDirection.Normalize();
        moveInput = new Vector2(worldDirection.x, worldDirection.z);
    }

    public void AddImpulse(Vector3 impulse)
    {
        impulse.y = 0f;
        externalForce += impulse;
    }

    public void Stop()
    {
        velocity = Vector3.zero;
        externalForce = Vector3.zero;
        moveInput = Vector2.zero;
    }

    private void FixedUpdate()
    {
        Tick(Time.fixedDeltaTime);
    }

    public void Tick(float dt)
    {
        if (body == null) return;

        Vector3 desiredDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (desiredDir.sqrMagnitude > 0.0001f)
            desiredDir.Normalize();

        velocity = Vector3.MoveTowards(velocity, desiredDir * moveSpeed, acceleration * dt);
        externalForce = Vector3.MoveTowards(externalForce, Vector3.zero, friction * dt);

        MoveAndResolve((velocity + externalForce) * dt);

        if (logMovement)
            Debug.Log($"[LiteKinematicMotor] pos={transform.position} vel={velocity} ext={externalForce}");
    }

    private void MoveAndResolve(Vector3 motion)
    {
        Vector3 position = transform.position;
        int steps = Mathf.Max(1, subSteps);
        Vector3 step = motion / steps;

        if (body.BlocksMovement)
        {
            for (int i = 0; i < steps; i++)
            {
                position += step;
                ResolvePhysicalCollisions(ref position);
            }
        }
        else
        {
            position += motion;
        }

        transform.position = position;
        body.NotifyMoved();
        CheckTriggers(position);
    }

    private void ResolvePhysicalCollisions(ref Vector3 position)
    {
        Vector3 center = GetBodyWorldCenter(position);
        float radius = Mathf.Max(0.001f, body.ApproxRadius - skinWidth);

        ResolveAgainstWalls(ref position, ref center, radius);
        ResolveAgainstMobs(ref position, ref center, radius);

        if (externalForce.sqrMagnitude < 0.0001f)
            externalForce = Vector3.zero;
    }

    // —тены: LiteWallGrid Ч O(€чейки) вместо O(всех коллайдеров сцены)
    private void ResolveAgainstWalls(ref Vector3 position, ref Vector3 center, float radius)
    {
        for (int iter = 0; iter < 4; iter++)
        {
            if (!LiteWallGrid.QueryDeepestPush(center, radius, out Vector3 push, out Vector3 n))
                break;

            push.y = 0f;
            position += push;
            center += push;

            if (n.sqrMagnitude > 0.000001f)
                velocity = Vector3.ProjectOnPlane(velocity, n);
        }
    }

    // “ела монстров: LiteMobManager Ч O(€чейки) вместо O(всех коллайдеров сцены)
    private void ResolveAgainstMobs(ref Vector3 position, ref Vector3 center, float radius)
    {
        if (LiteMobManager.Instance == null) return;

        LiteMobManager.Instance.QueryNearbyMobBodies(center, _nearbyMobs);

        for (int iter = 0; iter < 4; iter++)
        {
            Vector3 bestPush = Vector3.zero;
            float bestPen = 0f;
            bool found = false;

            for (int i = 0; i < _nearbyMobs.Count; i++)
            {
                LiteCircleCollider mob = _nearbyMobs[i];
                if (mob == null || mob == body) continue;

                if (mob.OverlapCircle(center, radius, out Vector3 pushOut))
                {
                    float pen = pushOut.magnitude;
                    if (pen > bestPen)
                    {
                        bestPen = pen;
                        bestPush = pushOut;
                        found = true;
                    }
                }
            }

            if (!found) break;

            bestPush.y = 0f;
            position += bestPush;
            center += bestPush;

            Vector3 n = bestPush.normalized;
            if (n.sqrMagnitude > 0.000001f)
                velocity = Vector3.ProjectOnPlane(velocity, n);
        }
    }

    private readonly List<LiteCollider> _nearbyTriggers = new List<LiteCollider>(64);

    private void CheckTriggers(Vector3 position)
    {
        if (body == null)
            return;

        Vector3 center = GetBodyWorldCenter(position);
        float radius = Mathf.Max(0.001f, body.ApproxRadius - skinWidth);

        LiteTriggerGrid.QueryNearbyColliders(center, radius, _nearbyTriggers);

        for (int i = 0; i < _nearbyTriggers.Count; i++)
        {
            LiteCollider other = _nearbyTriggers[i];
            if (other == null || other == body)
                continue;

            if (!other.IsTrigger)
                continue;

            if (!other.OverlapCircle(center, radius, out _))
                continue;

            LiteTriggerHandlerRegistry.InvokeStay(gameObject, other);

            if (other.gameObject != gameObject)
                LiteTriggerHandlerRegistry.InvokeStay(other.gameObject, body);
        }
    }
    private Vector3 GetBodyWorldCenter(Vector3 bodyPosition)
    {
        Vector3 offset = body.WorldCenter - transform.position;
        return bodyPosition + offset;
    }
}