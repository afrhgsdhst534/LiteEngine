using UnityEngine;

public class PoolSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimplePool pool;

    [Header("Spawn")]
    [SerializeField] private float spawnRadius = 20f;
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private bool autoSpawn = true;

    private float timer;

    private void Update()
    {
        if (!autoSpawn)
            return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            Spawn();
        }
    }

    public void Spawn()
    {
        Vector2 dir = Random.insideUnitCircle.normalized;

        Vector3 spawnPos = transform.position +
                           new Vector3(dir.x, 0f, dir.y) * spawnRadius;

        GameObject obj = pool.Spawn(spawnPos, Quaternion.identity);
        if (obj == null)
            return;

        if (obj.TryGetComponent(out LiteMob mob))
        {
            mob.Pool = pool;
            mob.OnSpawn(spawnPos);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}