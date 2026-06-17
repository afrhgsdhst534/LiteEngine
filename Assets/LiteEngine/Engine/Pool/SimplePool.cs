using System.Collections.Generic;
using UnityEngine;
public class SimplePool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int prewarmCount;
    private int spawnedCounter; 
    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private void Awake()
    {
        for (int i = 0; i < prewarmCount; i++)
        {
            var obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }
    public GameObject Spawn(Vector3 position, Quaternion rotation)
    {
        if (pool.Count == 0)
            return null;

        GameObject obj = pool.Dequeue();
        spawnedCounter++;

        obj.name = $"{prefab.name} {spawnedCounter}";
        obj.transform.SetPositionAndRotation(position, rotation);

        if (obj.TryGetComponent(out LiteMob mob))
            mob.Pool = this;

        obj.SetActive(true);
        return obj;
    }
    public void Despawn(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        pool.Enqueue(obj);
    }
}