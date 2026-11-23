using UnityEngine;

// Trilha sonora
public class BackgroundMusic : MonoBehaviour
{
    public AudioClip music;
    [Range(0f, 1f)] public float volume = 0.2f;
    public float fadeTime = 3f;

    AudioSource src;

    // Procura a musica em Resources/Audio/Trilha se o campo estiver vazio
    public bool HasMusic()
    {
        if (music == null)
        {
            AudioClip[] found = Resources.LoadAll<AudioClip>("Audio/Trilha");
            if (found.Length > 0) music = found[0];
        }
        return music != null;
    }

    // Inicia a musica em loop
    void Start()
    {
        if (!HasMusic()) return;
        src = gameObject.AddComponent<AudioSource>();
        src.clip = music;
        src.loop = true;
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        src.ignoreListenerPause = true;
        src.volume = 0f;
        src.Play();
    }

    // Fade de entrada
    void Update()
    {
        if (src == null) return;
        src.volume = Mathf.MoveTowards(src.volume, volume, Time.unscaledDeltaTime * Mathf.Max(volume, 0.01f) / Mathf.Max(fadeTime, 0.01f));
    }

    // Muda o volume da trilha
    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
    }
}
