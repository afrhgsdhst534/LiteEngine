using UnityEngine;

public enum LiteIntelligence
{
    Primitive = 0,   // Тупой напролом (Уклонение лучами)
    WallHugger = 1,  // Запоминает стену и скользит вдоль нее
    Tactician = 2,   // Локальный A* при застревании
    Genius = 3       // Движение по глобальному Flow Field
}

public static class LitePathfinder
{
    // Главный метод навигации
    public static Vector3 GetMoveDirection(
        Vector3 pos, Vector3 target, float radius, LiteIntelligence intLevel,
        ref bool isHugging, ref Vector3 hugNormal, ref float hugSign)
    {
        switch (intLevel)
        {
            case LiteIntelligence.Primitive:
                return CalculatePrimitiveSteering(pos, target, radius);

            case LiteIntelligence.WallHugger:
            case LiteIntelligence.Tactician: // Временно используют улучшенный обход
            case LiteIntelligence.Genius:
                return CalculateWallHugging(pos, target, radius, ref isHugging, ref hugNormal, ref hugSign);

            default:
                return (target - pos).normalized;
        }
    }

    // =======================================================================
    // УРОВЕНЬ 1: Примитивный Steering (без памяти, сглаживает углы)
    // =======================================================================
    private static Vector3 CalculatePrimitiveSteering(Vector3 pos, Vector3 target, float radius)
    {
        Vector3 toTarget = target - pos;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return Vector3.zero;
        Vector3 desired = toTarget.normalized;

        // Очень короткий щуп прямо перед носом (на 0.1м дальше радиуса)
        float lookAhead = radius + 0.1f;
        Vector3 probe = pos + desired * lookAhead;

        if (LiteWallGrid.QueryDeepestPush(probe, radius, out _, out Vector3 normal))
        {
            Vector3 tangent = new Vector3(normal.z, 0f, -normal.x);
            float sign = Vector3.Dot(desired, tangent) > 0 ? 1f : -1f;
            return (tangent * sign).normalized;
        }

        return desired;
    }

    // =======================================================================
    // УРОВЕНЬ 2: Честный Wall Hugger (Алгоритм Жука с детекцией углов)
    // =======================================================================
    private static Vector3 CalculateWallHugging(
        Vector3 pos, Vector3 target, float radius,
        ref bool isHugging, ref Vector3 hugNormal, ref float hugSign)
    {
        Vector3 toTarget = target - pos;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return Vector3.zero;
        Vector3 desired = toTarget.normalized;

        float lookAhead = radius + 0.1f;
        Vector3 probeTarget = pos + desired * lookAhead;

        // Проверяем стену прямо по курсу на игрока
        bool hitTargetDir = LiteWallGrid.QueryDeepestPush(probeTarget, radius, out _, out Vector3 targetNormal);

        if (!isHugging)
        {
            if (hitTargetDir)
            {
                // ЛОВУШКА ИСПРАВЛЕНА: Впервые упёрлись в стену. 
                isHugging = true;
                hugNormal = targetNormal;

                // УМНЫЙ ВЫБОР СТОРОНЫ: Проверяем пространство слева и справа от моба
                Vector3 rightTangent = new Vector3(targetNormal.z, 0f, -targetNormal.x);

                Vector3 checkRight = pos + rightTangent * (radius + 0.4f);
                Vector3 checkLeft = pos - rightTangent * (radius + 0.4f);

                bool wallRight = LiteWallGrid.QueryDeepestPush(checkRight, radius, out _, out _);
                bool wallLeft = LiteWallGrid.QueryDeepestPush(checkLeft, radius, out _, out _);

                if (!wallRight && wallLeft) hugSign = 1f;       // Справа свободно — идем направо
                else if (wallRight && !wallLeft) hugSign = -1f;  // Слева свободно — идем налево
                else
                {
                    // Если везде одинаково, выбираем геометрически выгодное направление
                    hugSign = Vector3.Dot(desired, rightTangent) > 0f ? 1f : -1f;
                }
            }
        }
        else
        {
            // Моб УЖЕ находится в режиме обхода стены
            Vector3 currentTangent = new Vector3(hugNormal.z, 0f, -hugNormal.x) * hugSign;
            Vector3 probeTangent = pos + currentTangent * lookAhead;

            // 1. ПРОВЕРКА ВНУТРЕННЕГО УГЛА: Не упёрлись ли мы во встречную стену во время обхода?
            if (LiteWallGrid.QueryDeepestPush(probeTangent, radius, out _, out Vector3 tangentNormal))
            {
                // Меняем нормаль на новую стену, плавно перетекая на нее
                hugNormal = tangentNormal;
            }

            // 2. ПРОВЕРКА ВНЕШНЕГО УГЛА: Проверяем, есть ли еще стена под боком?
            // Смещаем датчик в сторону стены (обратно вектору нормали)
            Vector3 probeSide = pos - hugNormal * (radius + 0.3f);
            bool wallStillUnderSide = LiteWallGrid.QueryDeepestPush(probeSide, radius, out _, out _);

            // 3. УСЛОВИЕ ВЫХОДА: Путь на игрока чист И мы не упрёмся в эту же стену при развороте
            if (!hitTargetDir && (Vector3.Dot(desired, hugNormal) > 0.1f || !wallStillUnderSide))
            {
                isHugging = false;
            }
        }

        // Если режим обхода активен — строим вектор движения строго вдоль стены
        if (isHugging)
        {
            Vector3 tangent = new Vector3(hugNormal.z, 0f, -hugNormal.x) * hugSign;

            // Фикс отлипания: направляем вектор на 85% вперед по касательной 
            // и на 15% ВНУТРЬ стены, чтобы моб не "слетал" на внешних углах и шел вплотную
            Vector3 finalDir = (tangent - hugNormal * 0.15f).normalized;
            return finalDir;
        }

        // Если стены нет — просто бежим к игроку
        return desired;
    }
}