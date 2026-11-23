using UnityEngine;

// Passos/arrasto da criatura, som 3D. Adicionado automaticamente pelo EnemyAI.
public class CreatureFootsteps : MonoBehaviour
{
    public float stepLength = 1.1f;   // metros por passo
    public float volume = 1f;
    public float maxDistance = 30f;

    AudioSource src;
    public AudioClip[] clips;
    Vector3 lastPos;
    float acc;
    int last = -1;

    void Start()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 2f;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;
        if (clips == null || clips.Length == 0) clips = Resources.LoadAll<AudioClip>("Audio/Criatura");
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
        if (dist / dt < 0.2f || dist > 3f) return;

        acc += dist;
        if (acc >= stepLength)
        {
            acc = 0f;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
            last = i;
            src.pitch = Random.Range(0.85f, 1.1f);
            src.PlayOneShot(clips[i], volume);
        }
    }
}
