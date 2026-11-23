using UnityEngine;

// Porta de saida do predio
public class ExitDoor : Interactable
{
    public override void Interact()
    {
        if (GameFlow.Instance != null) GameFlow.Instance.TryExit();
    }
}
