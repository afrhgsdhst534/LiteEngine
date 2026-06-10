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

    private void Reset()
    {
        body = GetComponent<LiteCollider>();
    }

    private void Awake()
    {
        if (body == null)
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

    // Убедись, что этот скрипт не тикает дважды, если им уже управляет LiteMobManager!
    private void FixedUpdate()
    {
        Tick(Time.fixedDeltaTime);
    }

    public void Tick(float dt)
    {
        if (body == null)
            return;

        Vector3 desiredDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (desiredDir.sqrMagnitude > 0.0001f)
            desiredDir.Normalize();

        Vector3 desiredVelocity = desiredDir * moveSpeed;

        velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * dt);
        externalForce = Vector3.MoveTowards(externalForce, Vector3.zero, friction * dt);

        Vector3 totalMotion = (velocity + externalForce) * dt;
        MoveAndResolve(totalMotion);

        if (logMovement)
            Debug.Log($"[LiteKinematicMotor] pos={transform.position} vel={velocity} ext={externalForce}");
    }

    private void MoveAndResolve(Vector3 motion)
    {
        Vector3 position = transform.position;
        int steps = Mathf.Max(1, subSteps);
        Vector3 step = motion / steps;

        // ЭТАП 1: Физическое движение и выталкивание (несколько шагов для точности)
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
            // Если мы сами просто триггер, нам выталкивание не нужно, просто летим
            position += motion;
        }

        transform.position = position;

        // ЭТАП 2: Проверка триггеров (ОДИН РАЗ за кадр, после всего движения)
        CheckTriggers(position);
    }

    private void ResolvePhysicalCollisions(ref Vector3 position)
    {
        Vector3 center = GetBodyWorldCenter(position);
        float radius = Mathf.Max(0.001f, body.ApproxRadius - skinWidth);

        // Сначала стены, потом остальные динамические объекты
        ResolvePass(ref position, ref center, radius, ref velocity, wallsOnly: true);
        ResolvePass(ref position, ref center, radius, ref velocity, wallsOnly: false);

        if (externalForce.sqrMagnitude < 0.0001f)
            externalForce = Vector3.zero;
    }

    private void ResolvePass(ref Vector3 position, ref Vector3 center, float radius, ref Vector3 velocity, bool wallsOnly)
    {
        for (int iter = 0; iter < 4; iter++)
        {
            Vector3 bestPush = Vector3.zero;
            float bestPen = 0f;
            bool found = false;

            for (int i = 0; i < LiteCollider.All.Count; i++)
            {
                LiteCollider other = LiteCollider.All[i];
                if (other == null || other == body || !other.BlocksMovement)
                    continue;

                bool isWall = other is LiteWallCollider;
                if (wallsOnly != isWall)
                    continue;

                if (other.OverlapCircle(center, radius, out Vector3 pushOut))
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

            // Жестко блокируем выталкивание по высоте
            bestPush.y = 0f;

            position += bestPush;
            center += bestPush;

            Vector3 n = bestPush.normalized;
            if (n.sqrMagnitude > 0.000001f)
            {
                velocity = Vector3.ProjectOnPlane(velocity, n);
            }
        }
    }

    private void CheckTriggers(Vector3 position)
    {
        Vector3 center = GetBodyWorldCenter(position);
        float radius = Mathf.Max(0.001f, body.ApproxRadius - skinWidth);
        bool bodyIsTrigger = !body.BlocksMovement;

        for (int i = 0; i < LiteCollider.All.Count; i++)
        {
            LiteCollider other = LiteCollider.All[i];
            if (other == null || other == body)
                continue;

            if (!other.BlocksMovement || bodyIsTrigger)
            {
                if (other.OverlapCircle(center, radius, out _))
                {
                    // Замена медленного SendMessage на быстрый интерфейс
                    var myTriggerHandler = GetComponent<ILiteTriggerHandler>();
                    if (myTriggerHandler != null)
                        myTriggerHandler.OnLiteTriggerStay(other);

                    var otherTriggerHandler = other.GetComponent<ILiteTriggerHandler>();
                    if (otherTriggerHandler != null)
                        otherTriggerHandler.OnLiteTriggerStay(body);
                }
            }
        }
    }

    private Vector3 GetBodyWorldCenter(Vector3 bodyPosition)
    {
        Vector3 localOffset = body.LocalCenter;
        return bodyPosition + (transform.rotation * localOffset);
    }
}