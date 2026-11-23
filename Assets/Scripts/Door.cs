using UnityEngine;

// Abre e fecha a porta
public class Door : MonoBehaviour
{
    [Header("Configuracao")]
    public float openAngle = -90f;  // angulo de abertura
    public float speed = 1.5f;      // velocidade de abrir/fechar
    public bool startsOpen = false; // comeca aberta?

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;

    public bool IsOpen { get { return isOpen; } }

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        isOpen = startsOpen;

        // Desliga o recorte do NavMesh
        foreach (UnityEngine.AI.NavMeshObstacle o in GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            o.enabled = false;
    }

    void Update()
    {
        // Move suavemente ate o estado atual (aberta ou fechada)
        Quaternion target = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, speed * Time.deltaTime);
    }

    // Chamado pelo jogador quando ele interage com a porta
    public void Toggle()
    {
        isOpen = !isOpen;
    }

    // Usado pela criatura
    public void Open()
    {
        isOpen = true;
    }
}
