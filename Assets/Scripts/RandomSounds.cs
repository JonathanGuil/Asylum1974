using UnityEngine;

// Sons aleatorios de ambiente
public class RandomSounds : MonoBehaviour
{
    public AudioClip[] clips;
    public float minInterval = 25f;
    public float maxInterval = 60f;
    public float firstDelay = 15f;
    public float minDistance = 8f;
    public float maxDistance = 20f;
    [Range(0f, 1f)] public float volume = 0.9f;
    public KeyCode testKey = KeyCode.F8;

    float next = -1f;
    int last = -1;

    // Carrega os sons de Resources/Audio/Aleatorios se a lista estiver vazia
    void Start()
    {
        if (clips == null || clips.Length == 0)
            clips = Resources.LoadAll<AudioClip>("Audio/Aleatorios");
    }

    // Controla o tempo entre os sons
    void Update()
    {
        if (clips == null || clips.Length == 0) return;

        bool playing = Time.timeScale > 0f && (GameFlow.Instance == null || GameFlow.Instance.IsPlaying);
        if (!playing) { next = -1f; return; }

        if (Input.GetKeyDown(testKey)) { PlayRandom(); next = Time.time + Random.Range(minInterval, maxInterval); return; }

        if (next < 0f) next = Time.time + firstDelay;
        if (Time.time >= next)
        {
            PlayRandom();
            next = Time.time + Random.Range(minInterval, maxInterval);
        }
    }

    // Toca um som aleatorio em volta do jogador
    void PlayRandom()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
        last = i;
        if (clips[i] == null) return;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float dist = Random.Range(minDistance, maxDistance);
        Vector3 pos = cam.transform.position + new Vector3(dir.x, 0f, dir.y) * dist;

        GameObject go = new GameObject("RandomSound_" + clips[i].name);
        go.transform.position = pos;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clips[i];
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 4f;
        src.maxDistance = 45f;
        src.dopplerLevel = 0f;
        src.pitch = Random.Range(0.92f, 1.06f);
        src.volume = volume;
        src.Play();
        Destroy(go, clips[i].length / src.pitch + 0.2f);
    }
}
