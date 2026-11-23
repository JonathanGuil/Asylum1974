using UnityEngine;

// Passos do jogador. Adicionado automaticamente pelo GameFlow.
public class PlayerFootsteps : MonoBehaviour
{
    public float walkStep = 1.6f;          // metros por passo andando
    public float runStep = 2.1f;           // metros por passo correndo
    public float runSpeedThreshold = 4.5f; // acima disso conta como correndo
    public float volume = 0.5f;

    AudioSource src;
    public AudioClip[] clips;
    Vector3 lastPos;
    float acc;
    int last = -1;

    void Start()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (clips == null || clips.Length == 0) clips = Resources.LoadAll<AudioClip>("Audio/Passos");
        lastPos = transform.position;
    }

    void Update()
    {
        if (clips.Length == 0) return;
        float dt = Time.deltaTime;
        Vector3 d = transform.position - lastPos;
        lastPos = transform.position;
        if (dt <= 0f) return;
        d.y = 0f;
        float dist = d.magnitude;
        float speed = dist / dt;
        if (speed < 0.3f || dist > 3f) { acc = walkStep * 0.6f; return; }

        bool running = speed > runSpeedThreshold;
        acc += dist;
        if (acc >= (running ? runStep : walkStep))
        {
            acc = 0f;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
            last = i;
            src.pitch = Random.Range(0.9f, 1.1f);
            src.PlayOneShot(clips[i], running ? volume * 1.3f : volume);
        }
    }
}
