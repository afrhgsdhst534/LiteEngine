using System.Collections.Generic;
using UnityEngine;

// Один стартовый снимок стен сцены.
// Без version++, без постоянных rebuild'ов, без лишней работы на кадр.
public static class LiteWallGrid
{
    public const float CellSize = 2f;

    private static readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>(512);
    private static readonly List<List<int>> _pool = new List<List<int>>(64);
    private static readonly List<LiteWallCollider> _walls = new List<LiteWallCollider>(1024);


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        Initialize();
    }

    // if problem in walls this method he didnt inited
    public static void Initialize()
    {
        Rebuild();
    }

    public static bool QueryDeepestPush(
        Vector3 pos, float radius,
        out Vector3 push, out Vector3 normal)
    {
        return QueryDeepestPush(pos, radius, out push, out normal, out _);
    }

    public static bool QueryDeepestPush(
        Vector3 pos, float radius,
        out Vector3 push, out Vector3 normal,
        out LiteWallCollider hitWall)
    {

        int cx = Mathf.FloorToInt(pos.x / CellSize);
        int cz = Mathf.FloorToInt(pos.z / CellSize);

        float bestPen = 0f;
        push = Vector3.zero;
        normal = Vector3.zero;
        hitWall = null;
        bool found = false;

        for (int oz = -1; oz <= 1; oz++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                long key = Hash(cx + ox, cz + oz);
                if (!_buckets.TryGetValue(key, out List<int> list))
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    int wallIndex = list[i];
                    if ((uint)wallIndex >= (uint)_walls.Count)
                        continue;

                    LiteWallCollider wall = _walls[wallIndex];
                    if (wall == null || !wall.isActiveAndEnabled || !wall.BlocksMovement)
                        continue;

                    if (wall.OverlapCircle(pos, radius, out Vector3 p))
                    {
                        float pen = p.magnitude;
                        if (pen > bestPen)
                        {
                            bestPen = pen;
                            push = p;
                            normal = pen > 1e-6f ? p / pen : Vector3.zero;
                            hitWall = wall;
                            found = true;
                        }
                    }
                }
            }
        }

        return found;
    }

    private static void Rebuild()
    {
        foreach (var kv in _buckets)
        {
            kv.Value.Clear();
            _pool.Add(kv.Value);
        }

        _buckets.Clear();
        _walls.Clear();

        // Стены статичные: берём один снимок всех активных wall collider'ов на старте.
        for (int i = 0; i < LiteWallCollider.All.Count; i++)
        {
            LiteWallCollider wall = LiteWallCollider.All[i];
            if (wall == null || !wall.isActiveAndEnabled || !wall.BlocksMovement)
                continue;

            _walls.Add(wall);
        }

        for (int i = 0; i < _walls.Count; i++)
        {
            LiteWallCollider wall = _walls[i];

            Vector3 c = wall.WorldCenter;
            float r = wall.ApproxRadius;

            int x0 = Mathf.FloorToInt((c.x - r) / CellSize);
            int x1 = Mathf.FloorToInt((c.x + r) / CellSize);
            int z0 = Mathf.FloorToInt((c.z - r) / CellSize);
            int z1 = Mathf.FloorToInt((c.z + r) / CellSize);

            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    long key = Hash(cx, cz);
                    if (!_buckets.TryGetValue(key, out List<int> list))
                    {
                        list = Rent();
                        _buckets.Add(key, list);
                    }

                    list.Add(i);
                }
            }
        }
    }

    private static List<int> Rent()
    {
        if (_pool.Count == 0)
            return new List<int>(4);

        int last = _pool.Count - 1;
        var l = _pool[last];
        _pool.RemoveAt(last);
        return l;
    }

    private static long Hash(int x, int z)
    {
        unchecked
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
