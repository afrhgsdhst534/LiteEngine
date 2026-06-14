using UnityEngine;

[ExecuteAlways]
public class LiteNavGrid : MonoBehaviour
{
    public static LiteNavGrid Instance { get; private set; }

    [Header("Grid Bounds (Editable)")]
    [Tooltip("Размер навигационной сетки (X и Z)")]
    public Vector2 gridSize = new Vector2(64f, 64f);
    
    [Tooltip("Размер одной ячейки (влияет на точность A* и Flow Field)")]
    [Min(0.2f)] public float cellSize = 1f;

    [Header("Debug")]
    public bool drawGridGizmos = true;

    private void OnEnable()
    {
        Instance = this;
    }

    public Vector3 GridOrigin => transform.position - new Vector3(gridSize.x * 0.5f, 0f, gridSize.y * 0.5f);

    // Перевод мировых координат в координаты сетки (для уровней 3 и 4)
    public bool WorldToGridCoords(Vector3 pos, out int x, out int z)
    {
        Vector3 local = pos - GridOrigin;
        x = Mathf.FloorToInt(local.x / cellSize);
        z = Mathf.FloorToInt(local.z / cellSize);

        int maxX = Mathf.CeilToInt(gridSize.x / cellSize);
        int maxZ = Mathf.CeilToInt(gridSize.y / cellSize);

        return x >= 0 && x < maxX && z >= 0 && z < maxZ;
    }

    private void OnDrawGizmos()
    {
        if (!drawGridGizmos) return;

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Vector3 center = transform.position;
        Vector3 size = new Vector3(gridSize.x, 0.1f, gridSize.y);
        Gizmos.DrawWireCube(center, size);

        // Отрисовка ячеек для понимания масштаба
        Vector3 origin = GridOrigin;
        int countX = Mathf.CeilToInt(gridSize.x / cellSize);
        int countZ = Mathf.CeilToInt(gridSize.y / cellSize);

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.1f);
        for (int x = 0; x <= countX; x++)
        {
            Vector3 p1 = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 p2 = p1 + new Vector3(0, 0, gridSize.y);
            Gizmos.DrawLine(p1, p2);
        }
        for (int z = 0; z <= countZ; z++)
        {
            Vector3 p1 = origin + new Vector3(0, 0, z * cellSize);
            Vector3 p2 = p1 + new Vector3(gridSize.x, 0, 0);
            Gizmos.DrawLine(p1, p2);
        }
    }
}