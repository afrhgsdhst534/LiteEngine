using System.Collections.Generic;
using UnityEngine;

public static class LiteTriggerGrid
{
    public const float CellSize = 2f;

    private sealed class Entry
    {
        public int X0;
        public int X1;
        public int Z0;
        public int Z1;
        public bool Registered;
    }

    private static readonly Dictionary<LiteCollider, Entry> _entries = new Dictionary<LiteCollider, Entry>(1024);
    private static readonly Dictionary<long, List<LiteCollider>> _cells = new Dictionary<long, List<LiteCollider>>(1024);
    private static readonly Stack<List<LiteCollider>> _pool = new Stack<List<LiteCollider>>(128);

    private static int _queryStamp = 1;

    public static void Register(LiteCollider col)
    {
        if (col == null) return;

        if (!_entries.TryGetValue(col, out Entry entry))
        {
            entry = new Entry();
            _entries.Add(col, entry);
        }

        SyncCollider(col);
    }

    public static void Unregister(LiteCollider col)
    {
        if (col == null) return;

        if (!_entries.TryGetValue(col, out Entry entry))
            return;

        if (entry.Registered)
            RemoveFromCells(col, entry);

        _entries.Remove(col);
    }

    public static void SyncCollider(LiteCollider col)
    {
        if (col == null) return;

        if (!_entries.TryGetValue(col, out Entry entry))
        {
            entry = new Entry();
            _entries.Add(col, entry);
        }

        // Если это не trigger-коллайдер, он в триггерной сетке не нужен.
        if (!col.IsTrigger)
        {
            if (entry.Registered)
                RemoveFromCells(col, entry);

            entry.Registered = false;
            return;
        }

        Vector3 center = col.WorldCenter;
        float r = Mathf.Max(0.001f, col.ApproxRadius);

        int x0 = Mathf.FloorToInt((center.x - r) / CellSize);
        int x1 = Mathf.FloorToInt((center.x + r) / CellSize);
        int z0 = Mathf.FloorToInt((center.z - r) / CellSize);
        int z1 = Mathf.FloorToInt((center.z + r) / CellSize);

        if (entry.Registered &&
            entry.X0 == x0 && entry.X1 == x1 &&
            entry.Z0 == z0 && entry.Z1 == z1)
        {
            return;
        }

        if (entry.Registered)
            RemoveFromCells(col, entry);

        entry.X0 = x0;
        entry.X1 = x1;
        entry.Z0 = z0;
        entry.Z1 = z1;
        entry.Registered = true;

        AddToCells(col, x0, x1, z0, z1);
    }

    public static void QueryNearbyColliders(Vector3 position, float radius, List<LiteCollider> results)
    {
        results.Clear();

        int stamp = NextStamp();

        int cx = Mathf.FloorToInt(position.x / CellSize);
        int cz = Mathf.FloorToInt(position.z / CellSize);

        // Запас по ячейкам, чтобы не терять большие триггеры.
        int range = Mathf.Max(1, Mathf.CeilToInt(radius / CellSize) + 1);

        for (int oz = -range; oz <= range; oz++)
        {
            for (int ox = -range; ox <= range; ox++)
            {
                long key = Hash(cx + ox, cz + oz);
                if (!_cells.TryGetValue(key, out List<LiteCollider> list))
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    LiteCollider col = list[i];
                    if (col == null)
                        continue;

                    if (col.TriggerGridStamp == stamp)
                        continue;

                    col.TriggerGridStamp = stamp;
                    results.Add(col);
                }
            }
        }
    }

    private static void AddToCells(LiteCollider col, int x0, int x1, int z0, int z1)
    {
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                long key = Hash(x, z);

                if (!_cells.TryGetValue(key, out List<LiteCollider> list))
                {
                    list = RentList();
                    _cells.Add(key, list);
                }

                list.Add(col);
            }
        }
    }

    private static void RemoveFromCells(LiteCollider col, Entry entry)
    {
        for (int z = entry.Z0; z <= entry.Z1; z++)
        {
            for (int x = entry.X0; x <= entry.X1; x++)
            {
                long key = Hash(x, z);
                if (!_cells.TryGetValue(key, out List<LiteCollider> list))
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], col))
                    {
                        int last = list.Count - 1;
                        list[i] = list[last];
                        list.RemoveAt(last);
                        break;
                    }
                }

                if (list.Count == 0)
                {
                    _cells.Remove(key);
                    ReturnList(list);
                }
            }
        }
    }

    private static List<LiteCollider> RentList()
    {
        if (_pool.Count > 0)
            return _pool.Pop();

        return new List<LiteCollider>(8);
    }

    private static void ReturnList(List<LiteCollider> list)
    {
        list.Clear();
        _pool.Push(list);
    }

    private static int NextStamp()
    {
        if (_queryStamp == int.MaxValue)
            _queryStamp = 1;
        else
            _queryStamp++;

        return _queryStamp;
    }

    private static long Hash(int x, int z)
    {
        unchecked
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}