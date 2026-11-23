using UnityEngine;

// Item coletavel
public class Collectible : MonoBehaviour
{
    public enum Tipo { Pilha, Prontuario, Fusivel, Chave, Bilhete }
    public Tipo tipo = Tipo.Pilha;

    [Header("Pilha")]
    public float cargaBateria = 30f;

    [Header("Prontuario / Bilhete")]
    [TextArea(3, 10)]
    public string textoProntuario = "Prontuario do paciente...";

    // Chamado pelo PlayerInteractor
    public void Collect()
    {
        switch (tipo)
        {
            case Tipo.Pilha:
                Flashlight f = (GameHUD.Instance != null) ? GameHUD.Instance.flashlight : null;
                if (f == null) f = Object.FindFirstObjectByType<Flashlight>();
                if (f != null) f.RecarregarBateria(cargaBateria);
                break;

            case Tipo.Prontuario:
                if (GameHUD.Instance != null) GameHUD.Instance.CollectRecord(textoProntuario);
                if (GameFlow.Instance != null) GameFlow.Instance.RecordCollected();
                break;

            case Tipo.Fusivel:
                if (GameFlow.Instance != null) GameFlow.Instance.FuseCollected();
                break;

            case Tipo.Chave:
                if (GameFlow.Instance != null) GameFlow.Instance.KeyCollected();
                break;

            case Tipo.Bilhete:
                if (GameHUD.Instance != null) GameHUD.Instance.ShowNote(textoProntuario);
                break;
        }

        Destroy(gameObject);
    }
}
