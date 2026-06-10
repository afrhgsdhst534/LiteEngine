using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class LiteMobManager : MonoBehaviour
{
    public static LiteMobManager Instance { get; private set; }

    [Header("World")]
    [SerializeField] private Transform player;

    [Header("Mass AI")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float separationWeight = 1.2f;
    [SerializeField] private float hashCellSize = 2f;
    [SerializeField] private float wallHashCellSize = 2f;

    [Header("Collision")]
    [SerializeField] private int movementSubSteps = 1;
    [SerializeField] private int wallResolveIterations = 3;

    [Header("Debug")]
    [SerializeField] private bool autoScanSceneOnAwake = true;

    private readonly List<LiteMob> mobs = new List<LiteMob>(1024);
    private readonly List<MobSnapshot> snapshots = new List<MobSnapshot>(1024);
    private readonly List<Vector3> nextPositions = new List<Vector3>(1024);
    private readonly List<Vector3> nextVelocities = new List<Vector3>(1024);

    private readonly Dictionary<long, List<int>> mobBuckets = new Dictionary<long, List<int>>(2048);
    private readonly Dictionary<long, List<int>> wallBuckets = new Dictionary<long, List<int>>(2048);

    private int cachedWallVersion = -1;

    private struct MobSnapshot
    {
        public LiteMob mob;
        public Vector3 position;
        public Vector3 velocity;
        public float radius;
        public float moveSpeed;
        public float acceleration;
        public float groundY;
        public Status status;
        public bool blocksMovement;
    }
    private void Awake()
    {
        Instance = this;

        if (autoScanSceneOnAwake)
            ScanScene();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void FixedUpdate()
    {
        Tick(Time.fixedDeltaTime);
    }

    public void ScanScene()
    {
        mobs.Clear();
        mobs.AddRange(FindObjectsByType<LiteMob>(FindObjectsSortMode.None));
        RebuildWallGrid(force: true);
    }

    public void Register(LiteMob mob)
    {
        if (mob == null) return;
        if (!mobs.Contains(mob))
            mobs.Add(mob);
    }

    public void Unregister(LiteMob mob)
    {
        if (mob == null) return;
        mobs.Remove(mob);
    }

    public void Tick(float dt)
    {
        if (player == null)
            return;

        RebuildWallGrid(force: false);
        SnapshotMobs();
        RebuildMobBuckets();
        SimulateAll(dt);
        ApplyAll();
    }

    private void SnapshotMobs()
    {
        snapshots.Clear();
        nextPositions.Clear();
        nextVelocities.Clear();

        for (int i = 0; i < mobs.Count; i++)
        {
            LiteMob mob = mobs[i];
            if (mob == null || !mob.isActiveAndEnabled || !mob.IsAlive)
                continue;

            LiteCircleCollider body = mob.Body;
            float radius = body != null ? body.ApproxRadius : 0.25f;

            // [ИСПРАВЛЕНИЕ] Узнаем, является ли моб физическим препятствием
            bool blocks = body != null && body.BlocksMovement;

            snapshots.Add(new MobSnapshot
            {
                mob = mob,
                position = mob.transform.position,
                velocity = mob.Velocity,
                radius = radius,
                moveSpeed = mob.MoveSpeed,
                acceleration = mob.Acceleration,
                groundY = mob.GroundY,
                status = mob.Status,
                blocksMovement = blocks // <-- передаем флаг
            });

            nextPositions.Add(Vector3.zero);
            nextVelocities.Add(Vector3.zero);
        }
    }
    private void RebuildMobBuckets()
    {
        mobBuckets.Clear();

        for (int i = 0; i < snapshots.Count; i++)
        {
            Vector3 p = snapshots[i].position;
            Vector2Int cell = ToCell(p, hashCellSize);
            long key = Hash(cell.x, cell.y);

            if (!mobBuckets.TryGetValue(key, out List<int> list))
            {
                list = new List<int>(8);
                mobBuckets.Add(key, list);
            }

            list.Add(i);
        }
    }

    private void SimulateAll(float dt)
    {
        Vector3 playerPos = player.position;

        for (int i = 0; i < snapshots.Count; i++)
        {
            MobSnapshot s = snapshots[i];
            Status status = s.status;

            status.Tick(dt, gravity);

            Vector3 flatPos = s.position;
            flatPos.y = 0f;

            Vector3 desiredDir = Vector3.zero;

            if (status.CanMove)
            {
                Vector3 toPlayer = playerPos - flatPos;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude > 0.0001f)
                    toPlayer.Normalize();

                Vector3 separation = ComputeSeparation(i, flatPos, s.radius);
                desiredDir = toPlayer + separation * separationWeight;

                if (desiredDir.sqrMagnitude > 0.0001f)
                    desiredDir.Normalize();
            }

            float speedMult = Mathf.Max(0f, status.moveSpeedMult);
            Vector3 targetVelocity = desiredDir * s.moveSpeed * speedMult;
            targetVelocity += status.externalForce;

            Vector3 newVelocity = Vector3.MoveTowards(
                s.velocity,
                targetVelocity,
                s.acceleration * dt
            );

            Vector3 motion = newVelocity * dt;
            Vector3 newPosition = MoveWithWalls(flatPos, s.radius, motion, ref newVelocity);

            newPosition.y = s.groundY + status.verticalOffset;

            nextPositions[i] = newPosition;
            nextVelocities[i] = newVelocity;
        }
    }

    private Vector3 ComputeSeparation(int selfIndex, Vector3 position, float radius)
    {
        // [ИСПРАВЛЕНИЕ] Если я сам триггер, меня никто не выталкивает
        if (!snapshots[selfIndex].blocksMovement)
            return Vector3.zero;

        Vector2Int cell = ToCell(position, hashCellSize);
        Vector3 force = Vector3.zero;

        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                long key = Hash(cell.x + ox, cell.y + oy);
                if (!mobBuckets.TryGetValue(key, out List<int> list))
                    continue;

                for (int k = 0; k < list.Count; k++)
                {
                    int otherIndex = list[k];
                    if (otherIndex == selfIndex)
                        continue;

                    MobSnapshot other = snapshots[otherIndex];

                    // [ИСПРАВЛЕНИЕ] Если другой моб - триггер, он меня не толкает
                    if (!other.blocksMovement)
                        continue;

                    Vector3 otherPos = other.position;
                    otherPos.y = 0f;

                    Vector3 delta = position - otherPos;
                    float minDist = radius + other.radius;
                    float sqr = delta.sqrMagnitude;

                    if (sqr > 0.000001f && sqr < minDist * minDist)
                    {
                        float dist = Mathf.Sqrt(sqr);
                        float push = (minDist - dist) / minDist;
                        force += (delta / dist) * push;
                    }
                }
            }
        }

        return force;
    }
    private Vector3 MoveWithWalls(Vector3 position, float radius, Vector3 motion, ref Vector3 velocity)
    {
        Vector3 remaining = new Vector3(motion.x, 0f, motion.z);
        int steps = Mathf.Max(1, movementSubSteps);
        Vector3 step = remaining / steps;

        for (int s = 0; s < steps; s++)
        {
            position += step;
            ResolveAgainstWalls(ref position, radius, ref velocity);
        }

        return position;
    }

    private void ResolveAgainstWalls(ref Vector3 position, float radius, ref Vector3 velocity)
    {
        Vector3 center = position;

        for (int iter = 0; iter < wallResolveIterations; iter++)
        {
            Vector2Int cell = ToCell(center, wallHashCellSize);

            float bestPen = 0f;
            Vector3 bestPush = Vector3.zero;
            bool found = false;

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    long key = Hash(cell.x + ox, cell.y + oy);
                    if (!wallBuckets.TryGetValue(key, out List<int> list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        LiteWallCollider wall = LiteWallCollider.All[list[i]];
                        if (wall == null || !wall.BlocksMovement)
                            continue;

                        if (wall.OverlapCircle(center, radius, out Vector3 pushOut))
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
                }
            }

            if (!found)
                break;

            position += bestPush;
            center = position;

            Vector3 n = bestPush.normalized;
            if (n.sqrMagnitude > 0.000001f)
                velocity = Vector3.ProjectOnPlane(velocity, n);
        }
    }

    private void ApplyAll()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            MobSnapshot s = snapshots[i];
            LiteMob mob = s.mob;
            if (mob == null)
                continue;

            mob.SetVelocity(nextVelocities[i]);
            mob.SetPosition(nextPositions[i]);
            mob.FaceVelocity();
        }
    }

    private void RebuildWallGrid(bool force)
    {
        if (!force && cachedWallVersion == LiteWallCollider.Version)
            return;

        wallBuckets.Clear();
        cachedWallVersion = LiteWallCollider.Version;

        for (int i = 0; i < LiteWallCollider.All.Count; i++)
        {
            LiteWallCollider wall = LiteWallCollider.All[i];
            if (wall == null || !wall.BlocksMovement)
                continue;

            Vector3 center = wall.WorldCenter;
            float radius = wall.ApproxRadius;

            Vector2Int min = ToCell(center - new Vector3(radius, 0f, radius), wallHashCellSize);
            Vector2Int max = ToCell(center + new Vector3(radius, 0f, radius), wallHashCellSize);

            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    long key = Hash(x, y);

                    if (!wallBuckets.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>(4);
                        wallBuckets.Add(key, list);
                    }

                    list.Add(i);
                }
            }
        }
    }

    private Vector2Int ToCell(Vector3 pos, float cellSize)
    {
        int x = Mathf.FloorToInt(pos.x / cellSize);
        int y = Mathf.FloorToInt(pos.z / cellSize);
        return new Vector2Int(x, y);
    }

    private long Hash(int x, int y)
    {
        unchecked { return ((long)x << 32) ^ (uint)y; }
    }
}