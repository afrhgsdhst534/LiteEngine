using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-100)]
public class LiteMobManager : MonoBehaviour
{
    public static LiteMobManager Instance { get; private set; }

    [Header("World")]
    [SerializeField] private Transform player;

    [Header("Mass AI")]
    [SerializeField] private float separationWeight = 1.2f;
    [SerializeField] private float hashCellSize = 2f;

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
    private readonly List<List<int>> _mobListPool = new List<List<int>>(128); // ���, ���� GC
    private readonly Dictionary<LiteMob, Transform> facingVisualRoots = new Dictionary<LiteMob, Transform>(1024);

    private readonly Dictionary<LiteMob, int> mobIndices = new Dictionary<LiteMob, int>(1024);

    private readonly List<LiteCollider> _nearbyTriggers = new List<LiteCollider>(64);
    private readonly Dictionary<LiteMob, Dictionary<int, LiteCollider>> _activeTriggersByMob = new Dictionary<LiteMob, Dictionary<int, LiteCollider>>(1024);
    private readonly Dictionary<LiteMob, Dictionary<int, LiteCollider>> _currentTriggersByMob = new Dictionary<LiteMob, Dictionary<int, LiteCollider>>(1024);
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
        mobIndices.Clear();
        facingVisualRoots.Clear();

        LiteMob[] found = FindObjectsByType<LiteMob>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            LiteMob mob = found[i];
            if (mob == null) continue;

            mobIndices[mob] = mobs.Count;
            mobs.Add(mob);
        }

    }
    public void Register(LiteMob mob)
    {
        if (mob == null) return;
        if (mobIndices.ContainsKey(mob)) return;

        mobIndices[mob] = mobs.Count;
        mobs.Add(mob);
    }
    public void Unregister(LiteMob mob)
    {
        if (mob == null) return;
        if (!mobIndices.TryGetValue(mob, out int idx)) return;

        int lastIndex = mobs.Count - 1;
        LiteMob lastMob = mobs[lastIndex];

        mobs[idx] = lastMob;
        mobIndices[lastMob] = idx;

        mobs.RemoveAt(lastIndex);
        mobIndices.Remove(mob);
        if (_activeTriggersByMob.TryGetValue(mob, out Dictionary<int, LiteCollider> active))
        {
            LiteCircleCollider body = mob.Body;

            if (body != null)
            {
                foreach (KeyValuePair<int, LiteCollider> pair in active)
                {
                    LiteCollider other = pair.Value;
                    if (other == null) continue;

                    LiteTriggerHandlerRegistry.InvokeExit(mob.gameObject, other);
                    if (other.gameObject != mob.gameObject)
                        LiteTriggerHandlerRegistry.InvokeExit(other.gameObject, body);
                }
            }

            _activeTriggersByMob.Remove(mob);
        }

        _currentTriggersByMob.Remove(mob);
        facingVisualRoots.Remove(mob);
    }
    public void Tick(float dt)
    {
        if (player == null) return;

        SnapshotMobs();
        RebuildMobBuckets();
        SimulateAll(dt);
        ApplyAll();
    }
    public void QueryNearbyMobBodies(Vector3 position, float radius, List<LiteCircleCollider> results)
    {
        results.Clear();

        int cx = Mathf.FloorToInt(position.x / hashCellSize);
        int cz = Mathf.FloorToInt(position.z / hashCellSize);
        int range = Mathf.Max(1, Mathf.CeilToInt(radius / hashCellSize) + 1);

        for (int oz = -range; oz <= range; oz++)
            for (int ox = -range; ox <= range; ox++)
            {
                long key = Hash(cx + ox, cz + oz);
                if (!mobBuckets.TryGetValue(key, out List<int> list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    int idx = list[i];
                    if (idx >= snapshots.Count) continue;
                    LiteCircleCollider body = snapshots[idx].mob?.Body;
                    if (body != null && body.BlocksMovement)
                        results.Add(body);
                }
            }
    }

    private void SnapshotMobs()
    {
        snapshots.Clear();
        nextPositions.Clear();
        nextVelocities.Clear();

        for (int i = 0; i < mobs.Count; i++)
        {
            LiteMob mob = mobs[i];
            if (mob == null || !mob.isActiveAndEnabled)
                continue;

            LiteCircleCollider body = mob.Body;
            float radius = body != null ? body.ApproxRadius : 0.25f;
            bool blocks = body != null && body.BlocksMovement;

            snapshots.Add(new MobSnapshot
            {
                mob = mob,
                position = body != null ? body.WorldCenter : mob.transform.position,
                velocity = mob.Velocity,
                radius = radius,
                moveSpeed = mob.MoveSpeed,
                acceleration = mob.Acceleration,
                groundY = mob.GroundY,
                status = mob.Status,
                blocksMovement = blocks
            });

            nextPositions.Add(Vector3.zero);
            nextVelocities.Add(Vector3.zero);
        }
    }
    private void RebuildMobBuckets()
    {
        foreach (var kv in mobBuckets)
        {
            kv.Value.Clear();
            _mobListPool.Add(kv.Value);
        }
        mobBuckets.Clear();

        for (int i = 0; i < snapshots.Count; i++)
        {
            Vector3 p = snapshots[i].position;
            long key = Hash(
                Mathf.FloorToInt(p.x / hashCellSize),
                Mathf.FloorToInt(p.z / hashCellSize));

            if (!mobBuckets.TryGetValue(key, out List<int> list))
            {
                list = RentMobList();
                mobBuckets.Add(key, list);
            }
            list.Add(i);
        }
    }

    private List<int> RentMobList()
    {
        if (_mobListPool.Count == 0) return new List<int>(8);
        int last = _mobListPool.Count - 1;
        var l = _mobListPool[last];
        _mobListPool.RemoveAt(last);
        return l;
    }

    private void SimulateAll(float dt)
    {
        Vector3 playerPos = player.position;

        for (int i = 0; i < snapshots.Count; i++)
        {
            MobSnapshot s = snapshots[i];
            Status status = s.status; 
            LiteMob lm = s.mob;

            Vector3 flatPos = s.position;
            flatPos.y = 0f;

            Vector3 desiredDir = Vector3.zero;

            if (status.CanMove)
            {
                desiredDir = LitePathfinder.GetMoveDirection(
                    flatPos,
                    playerPos,
                    s.radius,
                    lm.intelligenceLevel,
                    ref lm.isHuggingWall,
                    ref lm.hugNormal,
                    ref lm.hugSign 
                );
                Vector3 separation = ComputeSeparation(i, flatPos, s.radius);
                desiredDir += separation * separationWeight;

                if (desiredDir.sqrMagnitude > 0.0001f)
                    desiredDir.Normalize();
            }

            float speedMult = Mathf.Max(0f, status.moveSpeedMult);
            Vector3 targetVelocity = desiredDir * s.moveSpeed * speedMult + status.externalForce;
            Vector3 newVelocity = Vector3.MoveTowards(s.velocity, targetVelocity, s.acceleration * dt);

            Vector3 motion = newVelocity * dt;
            Vector3 newPosition = MoveWithWalls(flatPos, s.radius, motion, ref newVelocity);


            newPosition.y = s.groundY + status.verticalOffset;

            nextPositions[i] = newPosition;
            nextVelocities[i] = newVelocity;
        }
    }
    private Vector3 ComputeSeparation(int selfIndex, Vector3 position, float radius)
    {
        if (!snapshots[selfIndex].blocksMovement)
            return Vector3.zero;

        int cx = Mathf.FloorToInt(position.x / hashCellSize);
        int cz = Mathf.FloorToInt(position.z / hashCellSize);
        Vector3 force = Vector3.zero;

        for (int oz = -1; oz <= 1; oz++)
            for (int ox = -1; ox <= 1; ox++)
            {
                long key = Hash(cx + ox, cz + oz);
                if (!mobBuckets.TryGetValue(key, out List<int> list)) continue;

                for (int k = 0; k < list.Count; k++)
                {
                    int otherIndex = list[k];
                    if (otherIndex == selfIndex) continue;

                    MobSnapshot other = snapshots[otherIndex];
                    if (!other.blocksMovement) continue;

                    Vector3 otherPos = other.position;
                    otherPos.y = 0f;

                    Vector3 delta = position - otherPos;
                    float minDist = radius + other.radius;
                    float sqr = delta.sqrMagnitude;

                    if (sqr > 0.000001f && sqr < minDist * minDist)
                    {
                        float dist = Mathf.Sqrt(sqr);
                        force += (delta / dist) * ((minDist - dist) / minDist);
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
        for (int iter = 0; iter < wallResolveIterations; iter++)
        {
            if (!LiteWallGrid.QueryDeepestPush(position, radius, out Vector3 push, out Vector3 n))
                break;

            position += push;

            if (n.sqrMagnitude > 0.000001f)
                velocity = Vector3.ProjectOnPlane(velocity, n);
        }
    }

    private void ApplyAll()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            LiteMob mob = snapshots[i].mob;
            if (mob == null) continue;

            mob.SetVelocity(nextVelocities[i]);
            mob.SetPosition(nextPositions[i]);
            mob.Body?.NotifyMoved();
            CheckTriggersForMob(mob);
            ApplyFacingDirection(mob, nextVelocities[i]);
        }
    }
    private void ApplyFacingDirection(LiteMob mob, Vector3 velocity)
    {
        if (mob == null)
            return;

        Transform visualRoot = mob.visualRoot;
        if (visualRoot == null)
            return;

        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);

        if (flatVelocity.sqrMagnitude < 0.0001f)
            return; 

        Quaternion targetRotation = Quaternion.LookRotation(flatVelocity.normalized, Vector3.up);
        visualRoot.localRotation = targetRotation;
    }
    private void CheckTriggersForMob(LiteMob mob)
    {
        if (mob == null) return;

        LiteCircleCollider body = mob.Body;
        if (body == null) return;

        if (!_currentTriggersByMob.TryGetValue(mob, out Dictionary<int, LiteCollider> current))
        {
            current = new Dictionary<int, LiteCollider>(16);
            _currentTriggersByMob.Add(mob, current);
        }

        if (!_activeTriggersByMob.TryGetValue(mob, out Dictionary<int, LiteCollider> active))
        {
            active = new Dictionary<int, LiteCollider>(16);
            _activeTriggersByMob.Add(mob, active);
        }

        current.Clear();

        Vector3 center = body.WorldCenter;
        float radius = body.ApproxRadius;

        _nearbyTriggers.Clear();
        LiteTriggerGrid.QueryNearbyColliders(center, radius, _nearbyTriggers);

        for (int i = 0; i < _nearbyTriggers.Count; i++)
        {
            LiteCollider other = _nearbyTriggers[i];
            if (other == null || other == body || !other.IsTrigger)
                continue;

            if (!other.OverlapCircle(center, radius, out _))
                continue;

            current[other.GetInstanceID()] = other;
        }

        foreach (KeyValuePair<int, LiteCollider> pair in current)
        {
            LiteCollider other = pair.Value;
            bool wasActive = active.ContainsKey(pair.Key);

            if (!wasActive)
            {
                LiteTriggerHandlerRegistry.InvokeEnter(mob.gameObject, other);
                if (other.gameObject != mob.gameObject)
                    LiteTriggerHandlerRegistry.InvokeEnter(other.gameObject, body);
            }
            else
            {
                LiteTriggerHandlerRegistry.InvokeStay(mob.gameObject, other);
                if (other.gameObject != mob.gameObject)
                    LiteTriggerHandlerRegistry.InvokeStay(other.gameObject, body);
            }
        }

        foreach (KeyValuePair<int, LiteCollider> pair in active)
        {
            if (current.ContainsKey(pair.Key))
                continue;

            LiteCollider other = pair.Value;
            if (other == null)
                continue;

            LiteTriggerHandlerRegistry.InvokeExit(mob.gameObject, other);
            if (other.gameObject != mob.gameObject)
                LiteTriggerHandlerRegistry.InvokeExit(other.gameObject, body);
        }

        active.Clear();
        foreach (KeyValuePair<int, LiteCollider> pair in current)
            active.Add(pair.Key, pair.Value);
    }
    private long Hash(int x, int z)
    {
        unchecked { return ((long)x << 32) ^ (uint)z; }
    }
}