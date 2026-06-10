using UnityEngine;

[RequireComponent(typeof(LiteCharacterMotor))]
public class CharacterControllerLite : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LiteCharacterMotor motor;

    [Header("Settings")]
    [SerializeField] private bool rotateToMoveDirection = true;
    [SerializeField] private float rotationSpeed = 720f;


    private void Reset()
    {
        motor = GetComponent<LiteCharacterMotor>();
    }

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<LiteCharacterMotor>();

    }

    private void Update()
    {
        Vector2 input = ReadInput();

        motor.SetMoveInput(input);

        if (rotateToMoveDirection)
            UpdateRotation(input);
    }

    private Vector2 ReadInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(x, y);

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        return input;
    }

    private void UpdateRotation(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f)
            return;

        Vector3 dir = new Vector3(input.x, 0f, input.y);

        Quaternion targetRotation = Quaternion.LookRotation(dir);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}