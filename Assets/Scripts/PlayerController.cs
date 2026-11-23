using UnityEngine;

// Controlador de jogador em primeira pessoa.
// Anda com WASD, olha com o mouse, corre com Shift e colide com o cenario.
// Requer um componente Character Controller no mesmo objeto (adicionado automaticamente).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 4f;      // velocidade andando
    public float sprintSpeed = 7f;    // velocidade correndo (Shift)
    public float gravity = -9.81f;    // gravidade que mantem o jogador no chao

    [Header("Camera / Mouse")]
    public Transform cameraTransform; // arraste a Main Camera aqui no Inspector
    public float mouseSensitivity = 2f;
    public float pitchMin = -80f;     // limite de olhar pra baixo
    public float pitchMax = 80f;      // limite de olhar pra cima

    private CharacterController controller;
    private float verticalVelocity;   // velocidade vertical acumulada (gravidade)
    private float pitch;              // rotacao vertical acumulada da camera

    void Start()
    {
        controller = GetComponent<CharacterController>();
        LockCursor(true);
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0)) LockCursor(true);
            return; // com o mouse solto, nao mexe na camera nem anda
        }

        LookAround();
        Move();
    }

    void LookAround()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Gira o corpo do jogador na horizontal
        transform.Rotate(Vector3.up * mouseX);

        // Gira a camera na vertical, travando pra nao virar de cabeca pra baixo
        pitch = Mathf.Clamp(pitch - mouseY, pitchMin, pitchMax);
        if (cameraTransform != null)
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void Move()
    {
        float x = Input.GetAxis("Horizontal"); // A/D
        float z = Input.GetAxis("Vertical");   // W/S

        // Direcao relativa a pra onde o jogador esta olhando (sem acelerar na diagonal)
        Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        // Gravidade: mantem o jogador colado no chao
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
