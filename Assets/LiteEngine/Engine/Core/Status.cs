using UnityEngine;

[System.Serializable]
public sealed class Status
{
    public float moveSpeedMult = 1f;
    public float verticalOffset = 0f;
    public Vector3 externalForce = Vector3.zero;
    public float externalForceDamping = 18f;

    public bool CanMove => true;

    public void Reset()
    {
        moveSpeedMult = 1f;
        verticalOffset = 0f;
        externalForce = Vector3.zero;
        externalForceDamping = 18f;
    }

    public void Tick(float dt, float gravity) { }
    public void ApplySlow(float multiplier, float duration) { }
    public void ApplyStun(float duration) { }
    public void ApplyKnockback(Vector3 force, float damping = 18f) { }
    public void ApplyKnockup(float initialUpVelocity, float duration) { }
}