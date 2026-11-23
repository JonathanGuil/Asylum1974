using System.Collections;
using UnityEngine;

// Armadilha de choque da fase 2
public class ShockTrap : Interactable
{
    public string local = "sala de cirurgia";
    public Transform center;
    public float radius = 5f;
    public float activeTime = 4f;
    public float cooldown = 6f;
    public Light shockLight;
    public float idleIntensity = 0.6f;
    public Transform lever;
    public AudioClip shockSound;
    public AudioClip leverSound;

    [Header("Poca")]
    public float puddleRadius = 3.5f;
    public int wires = 3;
    public int sparks = 6;

    bool powered;
    bool busy;
    Material puddleMat;
    LineRenderer[] sparkLines = new LineRenderer[0];
    float nextIdleSpark;
    float idleSparkOff;

    static readonly Color Blue = new Color(0.45f, 0.7f, 1f);

    public Vector3 Center { get { return center != null ? center.position : transform.position; } }

    void Start()
    {
        if (shockLight != null)
        {
            shockLight.enabled = false;
            shockLight.color = Blue;
            shockLight.range = Mathf.Max(shockLight.range, radius + 3f);
        }
        if (shockSound == null) shockSound = FirstClip("Audio/Choque");
        if (leverSound == null) leverSound = FirstClip("Audio/Alavanca");
        BuildPuddle();
    }

    static AudioClip FirstClip(string folder)
    {
        AudioClip[] found = Resources.LoadAll<AudioClip>(folder);
        return found.Length > 0 ? found[0] : null;
    }

    // Faisca solta enquanto a armadilha esta energizada
    void Update()
    {
        if (!powered || busy || sparkLines.Length == 0) return;
        if (idleSparkOff > 0f && Time.time >= idleSparkOff)
        {
            sparkLines[0].enabled = false;
            idleSparkOff = 0f;
        }
        if (Time.time < nextIdleSpark) return;
        nextIdleSpark = Time.time + Random.Range(1.5f, 4f);
        idleSparkOff = Time.time + 0.08f;
        Arc(sparkLines[0]);
    }

    Material MakeMaterial(Color color, float smoothness, Color glow)
    {
        Renderer r = GetComponentInChildren<Renderer>();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material m = r != null && r.sharedMaterial != null ? new Material(r.sharedMaterial) : new Material(lit);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0.1f);
        if (glow.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", glow);
        }
        return m;
    }

    LineRenderer NewLine(string name, Material mat, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = mat;
        line.widthMultiplier = width;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return line;
    }

    // Poca d'agua com fios, marcando o centro da armadilha
    void BuildPuddle()
    {
        Vector3 c = Center;
        puddleMat = MakeMaterial(new Color(0.02f, 0.035f, 0.045f), 0.85f, Color.black);
        puddleMat.EnableKeyword("_EMISSION");

        GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        puddle.name = "Poca";
        Destroy(puddle.GetComponent<Collider>());
        puddle.transform.SetParent(transform, true);
        puddle.transform.position = c + Vector3.up * 0.012f;
        puddle.transform.localScale = new Vector3(puddleRadius * 2f, 0.006f, puddleRadius * 2f);
        Renderer pr = puddle.GetComponent<Renderer>();
        pr.sharedMaterial = puddleMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Fios da alavanca ate a agua
        Material wireMat = MakeMaterial(new Color(0.05f, 0.04f, 0.04f), 0.3f, Color.black);
        Vector3 from = transform.position;
        Vector3 side = Vector3.Cross(Vector3.up, (c - from).normalized);
        for (int w = 0; w < wires; w++)
        {
            Vector2 r = Random.insideUnitCircle * puddleRadius * 0.6f;
            Vector3 to = c + new Vector3(r.x, 0f, r.y);
            LineRenderer line = NewLine("Fio " + (w + 1), wireMat, 0.035f);
            int count = 10;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f);
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < count - 1) p += side * Random.Range(-0.35f, 0.35f);
                p.y = Mathf.Lerp(from.y, to.y, t) + 0.03f;
                line.SetPosition(i, p);
            }
        }

        // Faiscas
        Material sparkMat = MakeMaterial(Color.white, 0f, Blue * 6f);
        sparkLines = new LineRenderer[Mathf.Max(1, sparks)];
        for (int i = 0; i < sparkLines.Length; i++)
        {
            sparkLines[i] = NewLine("Faisca " + (i + 1), sparkMat, 0.03f);
            sparkLines[i].enabled = false;
        }
    }

    // Desenha um arco eletrico sobre a agua
    void Arc(LineRenderer line)
    {
        Vector3 c = Center;
        Vector2 a = Random.insideUnitCircle * puddleRadius;
        Vector2 b = a + Random.insideUnitCircle * 1.8f;
        int count = 6;
        line.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = i / (count - 1f);
            Vector2 p = Vector2.Lerp(a, b, t);
            float h = (i == 0 || i == count - 1) ? 0.03f : Random.Range(0.05f, 0.7f);
            line.SetPosition(i, c + new Vector3(p.x, h, p.y));
        }
        line.enabled = true;
    }

    void SetSparks(bool on)
    {
        foreach (LineRenderer line in sparkLines)
        {
            if (on && Random.value < 0.7f) Arc(line);
            else line.enabled = false;
        }
        if (puddleMat != null) puddleMat.SetColor("_EmissionColor", on ? Blue * Random.Range(0.2f, 2.5f) : Color.black);
    }

    public override void Interact()
    {
        if (busy) return;
        if (GameFlow.Instance == null || !GameFlow.Instance.CanUseTrap()) return;
        StartCoroutine(Fire());
    }

    // Chamado quando o gerador liga
    public void SetPowered(bool on)
    {
        powered = on;
        if (shockLight != null && !busy)
        {
            shockLight.enabled = on;
            shockLight.intensity = idleIntensity;
        }
    }

    // Dispara sem checar as condicoes
    public void ForceFire()
    {
        if (!busy) StartCoroutine(Fire());
    }

    // Verifica se um ponto esta dentro da area de choque
    public bool InZone(Vector3 p)
    {
        Vector3 d = p - Center;
        if (Mathf.Abs(d.y) > 2.5f) return false;
        d.y = 0f;
        return d.magnitude <= radius;
    }

    // Mantem a area eletrificada por alguns segundos
    IEnumerator Fire()
    {
        busy = true;
        if (lever != null) lever.localRotation = Quaternion.Euler(50f, 0f, 0f);
        if (leverSound != null) AudioSource.PlayClipAtPoint(leverSound, transform.position);
        if (shockSound != null) AudioSource.PlayClipAtPoint(shockSound, Center);

        EnemyAI enemy = Object.FindFirstObjectByType<EnemyAI>();
        bool killed = false;
        float end = activeTime;

        for (float t = 0f; t < end; t += Time.deltaTime)
        {
            if (shockLight != null)
            {
                shockLight.enabled = true;
                shockLight.intensity = Random.Range(3f, 30f);
            }
            SetSparks(true);

            if (!killed && enemy != null && !enemy.IsDead && InZone(enemy.transform.position))
            {
                killed = true;
                end = t + 1.5f;
                enemy.Die();
                if (GameFlow.Instance != null) GameFlow.Instance.CreatureKilled(enemy);
            }
            yield return null;
        }

        SetSparks(false);
        if (shockLight != null)
        {
            shockLight.enabled = powered;
            shockLight.intensity = idleIntensity;
        }
        if (lever != null) lever.localRotation = Quaternion.Euler(-50f, 0f, 0f);

        if (!killed)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.ShowMessage("A criatura não estava na sala. Recarregando...", 3f);
            yield return new WaitForSeconds(cooldown);
        }
        busy = false;
    }
}
