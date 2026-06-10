using System.Collections.Generic;
using UnityEngine;

// Разделяемый пространственный хеш для стен.
// Используется и LiteMobManager, и LiteCharacterMotor.
// Пул List-ов: ноль аллокаций после прогрева.
public static class LiteWallGrid
{
    public const float CellSize = 2f;

    private static readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>(512);
    private static readonly List<List<int>> _pool = new List<List<int>>(64);
    private static int _cachedVersion = -1;

    public static void EnsureUpToDate()
    {
        if (_cachedVersion != LiteWallCollider.Version)
            Rebuild();
    }

    // Возвращает толчок от стены с наибольшей пенетрацией.
    // Проверяет только 9 ближайших ячеек — O(k) вместо O(N всех стен).
    public static bool QueryDeepestPush(
        Vector3 pos, float radius,
        out Vector3 push, out Vector3 normal)
    {
        EnsureUpToDate();

        int cx = Mathf.FloorToInt(pos.x / CellSize);
        int cz = Mathf.FloorToInt(pos.z / CellSize);

        float bestPen = 0f;
        push = Vector3.zero;
        normal = Vector3.zero;
        bool found = false;

        for (int oz = -1; oz <= 1; oz++)
            for (int ox = -1; ox <= 1; ox++)
            {
                long key = Hash(cx + ox, cz + oz);
                if (!_buckets.TryGetValue(key, out List<int> list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    LiteWallCollider wall = LiteWallCollider.All[list[i]];
                    if (wall == null || !wall.BlocksMovement) continue;

                    if (wall.OverlapCircle(pos, radius, out Vector3 p))
                    {
                        float pen = p.magnitude;
                        if (pen > bestPen)
                        {
                            bestPen = pen;
                            push = p;
                            normal = pen > 1e-6f ? p / pen : Vector3.zero;
                            found = true;
                        }
                    }
                }
            }
        return found;
    }

    private static void Rebuild()
    {
        // Возвращаем все живые списки в пул — без new
        foreach (var kv in _buckets)
        {
            kv.Value.Clear();
            _pool.Add(kv.Value);
        }
        _buckets.Clear();
        _cachedVersion = LiteWallCollider.Version;

        for (int i = 0; i < LiteWallCollider.All.Count; i++)
        {
            LiteWallCollider wall = LiteWallCollider.All[i];
            if (wall == null || !wall.BlocksMovement) continue;

            Vector3 c = wall.WorldCenter;
            float r = wall.ApproxRadius;

            int x0 = Mathf.FloorToInt((c.x - r) / CellSize);
            int x1 = Mathf.FloorToInt((c.x + r) / CellSize);
            int z0 = Mathf.FloorToInt((c.z - r) / CellSize);
            int z1 = Mathf.FloorToInt((c.z + r) / CellSize);

            for (int cz = z0; cz <= z1; cz++)
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

    private static List<int> Rent()
    {
        if (_pool.Count == 0) return new List<int>(4);
        int last = _pool.Count - 1;
        var l = _pool[last];
        _pool.RemoveAt(last);
        return l;
    }

    private static long Hash(int x, int z) =>
        unchecked(((long)x << 32) ^ (uint)z);
}