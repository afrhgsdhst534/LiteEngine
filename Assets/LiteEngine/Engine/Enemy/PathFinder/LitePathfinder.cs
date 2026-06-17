using UnityEngine;

public enum LiteIntelligence
{
    Primitive = 0,   // Тупой напролом (Уклонение лучами)
    WallHugger = 1,  // Запоминает стену и скользит вдоль нее
    Tactician = 2,   // Локальный A* при застревании - do it later
    Genius = 3       // Движение по глобальному Flow Field- do it later
}

public static class LitePathfinder
{
    public static Vector3 GetMoveDirection(
        Vector3 pos, Vector3 target, float radius, LiteIntelligence intLevel,
        ref bool isHugging, ref Vector3 hugNormal, ref float hugSign)
    {
        switch (intLevel)
        {
            case LiteIntelligence.Primitive:
                return CalculatePrimitiveSteering(pos, target, radius);

            case LiteIntelligence.WallHugger:
            case LiteIntelligence.Tactician:
            case LiteIntelligence.Genius:
                return CalculateWallHugging(pos, target, radius, ref isHugging, ref hugNormal, ref hugSign);

            default:
                return (target - pos).normalized;
        }
    }

    private static Vector3 CalculatePrimitiveSteering(Vector3 pos, Vector3 target, float radius)
    {
        Vector3 toTarget = target - pos;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 desired = toTarget.normalized;
        float lookAhead = radius + 0.1f;
        Vector3 probe = pos + desired * lookAhead;

        if (LiteWallGrid.QueryDeepestPush(probe, radius, out _, out Vector3 normal))
        {
            Vector3 tangent = new Vector3(normal.z, 0f, -normal.x);
            float sign = Vector3.Dot(desired, tangent) >= 0f ? 1f : -1f;
            return (tangent * sign).normalized;
        }

        return desired;
    }

    private static Vector3 CalculateWallHugging(
        Vector3 pos, Vector3 target, float radius,
        ref bool isHugging, ref Vector3 hugNormal, ref float hugSign)
    {
        Vector3 toTarget = target - pos;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 desired = toTarget.normalized;
        float lookAhead = radius + 0.1f;

        if (!isHugging)
        {
            Vector3 probeTarget = pos + desired * lookAhead;
            if (!LiteWallGrid.QueryDeepestPush(probeTarget, radius, out _, out Vector3 targetNormal))
                return desired;

            isHugging = true;
            hugNormal = targetNormal;

            Vector3 tangent = new Vector3(targetNormal.z, 0f, -targetNormal.x);
            hugSign = Vector3.Dot(desired, tangent) >= 0f ? 1f : -1f;
            return (tangent * hugSign).normalized;
        }

        Vector3 currentTangent = new Vector3(hugNormal.z, 0f, -hugNormal.x) * hugSign;
        Vector3 probeAhead = pos + currentTangent * lookAhead;

        if (!LiteWallGrid.QueryDeepestPush(probeAhead, radius, out _, out Vector3 newNormal))
        {
            isHugging = false;
            return desired;
        }

        if (Vector3.Dot(newNormal, hugNormal) < 0.95f)
            hugNormal = newNormal;

        return currentTangent.normalized;
    }
}
