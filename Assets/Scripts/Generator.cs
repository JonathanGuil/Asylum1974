using UnityEngine;

// Gerador da fase 2
public class Generator : Interactable
{
    public string local = "casa de máquinas";
    public Light statusLight;
    public Color offColor = new Color(1f, 0.15f, 0.1f);
    public Color onColor = new Color(0.3f, 1f, 0.4f);
    public AudioClip startSound;

    public bool IsOn { get; private set; }

    void Start()
    {
        Refresh();
    }

    public override void Interact()
    {
        if (GameFlow.Instance != null) GameFlow.Instance.UseGenerator(this);
    }

    // Liga o gerador
    public void TurnOn()
    {
        if (IsOn) return;
        IsOn = true;
        Refresh();
        if (startSound == null)
        {
            AudioClip[] found = Resources.LoadAll<AudioClip>("Audio/Gerador");
            if (found.Length > 0) startSound = found[0];
        }
        if (startSound == null) return;

        // Motor em loop
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = startSound;
        src.loop = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 25f;
        src.Play();
    }

    // Atualiza a luz de status
    void Refresh()
    {
        if (statusLight != null) statusLight.color = IsOn ? onColor : offColor;
    }
}
