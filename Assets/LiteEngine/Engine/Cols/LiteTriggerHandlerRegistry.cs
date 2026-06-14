using System.Collections.Generic;
using UnityEngine;

public interface ILiteTriggerHandler
{
    void OnLiteTriggerEnter(LiteCollider other);
    void OnLiteTriggerStay(LiteCollider other);
    void OnLiteTriggerExit(LiteCollider other);
}

public static class LiteTriggerHandlerRegistry
{
    private sealed class Entry
    {
        public int RefCount;
        public readonly List<ILiteTriggerHandler> Handlers = new List<ILiteTriggerHandler>(4);
        public bool HandlersBuilt;
    }

    private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(512);
    private static readonly List<MonoBehaviour> _componentBuffer = new List<MonoBehaviour>(16);

    public static void Register(GameObject go)
    {
        if (go == null) return;

        int id = go.GetInstanceID();
        if (_entries.TryGetValue(id, out Entry entry))
        {
            entry.RefCount++;
            return;
        }

        entry = new Entry
        {
            RefCount = 1
        };

        RebuildHandlers(go, entry);
        entry.HandlersBuilt = true;
        _entries.Add(id, entry);
    }

    public static void Unregister(GameObject go)
    {
        if (go == null) return;

        int id = go.GetInstanceID();
        if (!_entries.TryGetValue(id, out Entry entry))
            return;

        entry.RefCount--;
        if (entry.RefCount > 0)
            return;

        _entries.Remove(id);
    }

    public static void InvokeEnter(GameObject go, LiteCollider cal)
    {
        Invoke(go, cal, (handler, collider) => handler.OnLiteTriggerEnter(collider));
    }

    public static void InvokeStay(GameObject go, LiteCollider cal)
    {
        Invoke(go, cal, (handler, collider) => handler.OnLiteTriggerStay(collider));
    }

    public static void InvokeExit(GameObject go, LiteCollider cal)
    {
        Invoke(go, cal, (handler, collider) => handler.OnLiteTriggerExit(collider));
    }

    private static void Invoke(GameObject go, LiteCollider cal, System.Action<ILiteTriggerHandler, LiteCollider> callback)
    {
        if (go == null || cal == null) return;

        int id = go.GetInstanceID();
        if (!_entries.TryGetValue(id, out Entry entry))
        {
            Register(go);
            if (!_entries.TryGetValue(id, out entry))
                return;
        }

        // Перестраиваем список только один раз, а затем используем кэш.
        if (!entry.HandlersBuilt)
        {
            RebuildHandlers(go, entry);
            entry.HandlersBuilt = true;
        }

        for (int i = 0; i < entry.Handlers.Count; i++)
        {
            ILiteTriggerHandler handler = entry.Handlers[i];
            if (handler == null)
                continue;

            if (handler is MonoBehaviour mb && mb.isActiveAndEnabled)
                callback(handler, cal);
        }
    }

    private static void RebuildHandlers(GameObject go, Entry entry)
    {
        entry.Handlers.Clear();

        _componentBuffer.Clear();
        go.GetComponents(_componentBuffer);

        for (int i = 0; i < _componentBuffer.Count; i++)
        {
            MonoBehaviour mb = _componentBuffer[i];
            if (mb == null)
                continue;

            if (mb is ILiteTriggerHandler handler)
                entry.Handlers.Add(handler);
        }
    }
}
