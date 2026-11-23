using UnityEngine;

// Interacao do jogador: olhar + E
public class PlayerInteractor : MonoBehaviour
{
    public Transform cameraTransform;
    public float reach = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("UI")]
    public GameObject interactPrompt;

    void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    // Procura algo para interagir na frente da camera
    void Update()
    {
        Interactable other = null;
        Door door = null;
        Collectible item = null;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, reach))
        {
            other = hit.collider.GetComponentInParent<Interactable>();
            door = hit.collider.GetComponentInParent<Door>();
            item = hit.collider.GetComponentInParent<Collectible>();
        }

        bool canInteract = (other != null || door != null || item != null);
        if (interactPrompt != null)
            interactPrompt.SetActive(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
        {
            if (other != null) other.Interact();
            else if (door != null) door.Toggle();
            else if (item != null) item.Collect();
        }
    }
}
