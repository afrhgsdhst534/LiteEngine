using System.Collections.Generic;
using UnityEngine;

public static class LiteTriggerHandlerRegistry
{
    private sealed class Entry
    {
        public int RefCount;
        public readonly List<ILiteTriggerHandler> Handlers = new List<ILiteTriggerHandler>(4);
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

        entry = new Entry();
        entry.RefCount = 1;

        RebuildHandlers(go, entry);

        if (entry.Handlers.Count == 0)
            return;

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

    public static void InvokeStay(GameObject go, LiteCollider cal)
    {
        if (go == null || cal == null) return;

        int id = go.GetInstanceID();
        if (!_entries.TryGetValue(id, out Entry entry))
            return;

        // Если список пустой — пробуем один раз собрать его снова.
        // Это позволяет подхватить компоненты, которые были добавлены после старта.
        if (entry.Handlers.Count == 0)
            RebuildHandlers(go, entry);

        for (int i = 0; i < entry.Handlers.Count; i++)
        {
            ILiteTriggerHandler handler = entry.Handlers[i];
            if (handler == null)
                continue;

            if (handler is MonoBehaviour mb && mb.isActiveAndEnabled)
                handler.OnLiteTriggerStay(cal);
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