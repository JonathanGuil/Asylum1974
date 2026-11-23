using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// Fluxo do jogo: menu, introducao, fases, morte e final
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }
    static bool skipMenuOnNextLoad = false;
    static bool startInPhase2 = false;

    [Header("Opcoes")]
    public bool mostrarBotaoFase2 = true;
    public bool teclasDeTeste = true;
    public float tempoDeRespiro = 15f;

    [Header("Fase 2")]
    public float raioDeAtracao = 18f;
    [Range(0.1f, 1f)] public float drenoFase2 = 0.6f;
    [Range(0f, 1f)] public float recargaFase2 = 0.5f;
    [Range(0f, 1f)] public float lampadasDoGerador = 0.5f;

    [Header("Iluminacao")]
    public bool ajustarIluminacao = true;
    [Range(0f, 1f)] public float lampadasAcesas = 0.3f;
    [Range(0f, 1f)] public float brilhoDasLampadas = 0.85f;
    public float lanternaAngulo = 34f;
    public float lanternaAnguloInterno = 20f;
    public float lanternaAlcance = 30f;
    public float lanternaIntensidade = 40f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        skipMenuOnNextLoad = false;
        startInPhase2 = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootFirstScene()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindFirstObjectByType<GameFlow>() != null) return;
        if (Object.FindFirstObjectByType<PlayerController>() == null) return;
        new GameObject("GameFlow").AddComponent<GameFlow>();
    }

    enum State { Menu, Intro, Playing, Paused, Dead, Won }
    enum Step { Fuses, Generator, Trap, Key, Exit }
    State state = State.Menu;
    Step step = Step.Fuses;
    int phase = 1;

    public bool IsPlaying { get { return state == State.Playing; } }
    public int Phase { get { return phase; } }

    PlayerController pc;
    PlayerInteractor pi;
    Flashlight fl;
    Transform cam;
    EnemyAI enemy;

    GameObject phase2Group;
    Generator generator;
    ShockTrap trap;
    Collectible keyItem;
    Collectible[] phase2Items = new Collectible[0];

    GameObject menuPanel, deathPanel, winPanel, introPanel, pausePanel, confirmPanel;
    TextMeshProUGUI winTitle, winText, winTimeText, introTitle, introText, messageText, confirmText;
    UnityEngine.Events.UnityAction confirmAction;

    class Lamp
    {
        public Renderer renderer;
        public Color glow;
        public System.Collections.Generic.List<Light> lights = new System.Collections.Generic.List<Light>();
    }
    System.Collections.Generic.List<Lamp> offLamps = new System.Collections.Generic.List<Lamp>();
    int totalRecords, recordsCollected, totalFuses, fusesCollected;
    bool pendingWin, hasKey;
    float playTime, messageUntil;
    Image fade;
    AudioSource sfx;
    AudioClip screamClip;

    static readonly Color Red = new Color(0.75f, 0.08f, 0.08f);
    static readonly Color LightGrey = new Color(0.85f, 0.85f, 0.85f);
    static readonly Color Grey = new Color(0.6f, 0.6f, 0.6f);

    bool HasPhase2 { get { return phase2Group != null && totalFuses > 0; } }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        pc = Object.FindFirstObjectByType<PlayerController>();
        pi = Object.FindFirstObjectByType<PlayerInteractor>();
        fl = Object.FindFirstObjectByType<Flashlight>();
        enemy = Object.FindFirstObjectByType<EnemyAI>();
        if (pc != null && pc.cameraTransform != null) cam = pc.cameraTransform;
        else if (Camera.main != null) cam = Camera.main.transform;

        FindPhase2Objects();
        SetControls(false);
        SetupAudio();
        BuildUI();

        bool beginInPhase2 = startInPhase2 && HasPhase2;
        startInPhase2 = false;

        if (skipMenuOnNextLoad)
        {
            skipMenuOnNextLoad = false;
            if (beginInPhase2) StartCoroutine(Phase2Sequence(false));
            else
            {
                StartGame();
                StartCoroutine(Fade(1f, 0f, 0.8f));
            }
        }
        else ShowMenu();
    }

    void Start()
    {
        foreach (Collectible c in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            if (c.tipo == Collectible.Tipo.Prontuario) totalRecords++;
        StyleRecordPanel();
        StylePrompt();
        AdjustLighting();
    }

    // Escurece as lampadas do predio e deixa o feixe da lanterna estreito e longo
    void AdjustLighting()
    {
        if (!ajustarIluminacao) return;

        Light flash = fl != null ? fl.GetComponent<Light>() : null;
        System.Collections.Generic.List<Light> lights = new System.Collections.Generic.List<Light>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l == flash || l.type == LightType.Directional) continue;
            if (l.GetComponentInParent<Collectible>(true) != null) continue;
            if (phase2Group != null && l.transform.IsChildOf(phase2Group.transform)) continue;
            lights.Add(l);
        }

        // Apaga parte das lampadas por inteiro: a luz e o brilho do modelo
        System.Collections.Generic.HashSet<Light> handled = new System.Collections.Generic.HashSet<Light>();
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Material m = r.sharedMaterial;
            if (m == null || !m.IsKeywordEnabled("_EMISSION")) continue;
            if (!m.name.Contains("Lamp") && !m.name.Contains("Light") && !m.name.Contains("Chandelier")) continue;

            Vector3 pos = r.bounds.center;
            float reach = Mathf.Max(2.5f, r.bounds.extents.magnitude + 1f);
            int hash = Mathf.Abs(Mathf.FloorToInt(pos.x * 7.3f + pos.y * 3.7f + pos.z * 13.1f)) % 100;
            bool on = hash < lampadasAcesas * 100f;
            Lamp lamp = null;
            if (!on)
            {
                lamp = new Lamp();
                lamp.renderer = r;
                lamp.glow = m.GetColor("_EmissionColor");
                offLamps.Add(lamp);
            }

            foreach (Light l in lights)
            {
                if (handled.Contains(l) || Vector3.Distance(l.transform.position, pos) > reach) continue;
                handled.Add(l);
                if (on) l.intensity *= brilhoDasLampadas;
                else { l.enabled = false; lamp.lights.Add(l); }
            }

            if (!on)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", Color.black);
                r.SetPropertyBlock(block);
            }
        }

        foreach (Light l in lights)
            if (!handled.Contains(l)) l.intensity *= brilhoDasLampadas;

        if (flash != null)
        {
            flash.spotAngle = lanternaAngulo;
            flash.innerSpotAngle = Mathf.Min(lanternaAnguloInterno, lanternaAngulo);
            flash.range = lanternaAlcance;
            flash.intensity = lanternaIntensidade;
        }
    }

    // Liga ou desliga uma lampada: a luz e o brilho do modelo
    void SetLamp(Lamp lamp, bool on)
    {
        foreach (Light l in lamp.lights)
            if (l != null) l.enabled = on;
        if (lamp.renderer == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        lamp.renderer.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", on ? lamp.glow : Color.black);
        lamp.renderer.SetPropertyBlock(block);
    }

    // Lampadas religadas pelo gerador, piscando
    IEnumerator FlickerLamps()
    {
        System.Collections.Generic.List<Lamp> lamps = new System.Collections.Generic.List<Lamp>();
        foreach (Lamp lamp in offLamps)
        {
            if (Random.value > lampadasDoGerador) continue;
            foreach (Light l in lamp.lights)
                if (l != null) l.intensity *= brilhoDasLampadas * 0.8f;
            lamps.Add(lamp);
        }

        bool[] lit = new bool[lamps.Count];
        float[] next = new float[lamps.Count];
        for (int i = 0; i < next.Length; i++) next[i] = Time.time + Random.Range(0f, 2.5f);

        while (true)
        {
            for (int i = 0; i < lamps.Count; i++)
            {
                if (Time.time < next[i]) continue;
                lit[i] = !lit[i];
                SetLamp(lamps[i], lit[i]);
                if (lit[i]) next[i] = Time.time + Random.Range(0.2f, 5f);
                else next[i] = Time.time + (Random.value < 0.15f ? Random.Range(2f, 6f) : Random.Range(0.05f, 0.5f));
            }
            yield return null;
        }
    }

    // Deixa o aviso de interacao logo abaixo da mira, em qualquer resolucao
    void StylePrompt()
    {
        if (pi == null || pi.interactPrompt == null) return;
        RectTransform rt = pi.interactPrompt.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -45f);
        rt.sizeDelta = new Vector2(400f, 50f);
        TMP_Text text = pi.interactPrompt.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    // Guarda os objetos da fase 2 e esconde os itens ate ela comecar
    void FindPhase2Objects()
    {
        phase2Group = GameObject.Find("_Fase2");
        if (phase2Group == null) return;

        generator = phase2Group.GetComponentInChildren<Generator>(true);
        trap = phase2Group.GetComponentInChildren<ShockTrap>(true);
        phase2Items = phase2Group.GetComponentsInChildren<Collectible>(true);

        foreach (Collectible c in phase2Items)
        {
            if (c.tipo == Collectible.Tipo.Fusivel) totalFuses++;
            else if (c.tipo == Collectible.Tipo.Chave) keyItem = c;
            c.gameObject.SetActive(false);
        }
    }

    // Ajusta o painel de leitura e o contador
    void StyleRecordPanel()
    {
        GameHUD hud = GameHUD.Instance;
        if (hud == null) return;
        if (hud.recordPanel != null)
        {
            Image bg = hud.recordPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);
        }
        if (hud.recordCounterText != null)
        {
            RectTransform c = hud.recordCounterText.rectTransform;
            c.anchorMin = new Vector2(0f, 1f);
            c.anchorMax = new Vector2(0f, 1f);
            c.pivot = new Vector2(0f, 1f);
            c.anchoredPosition = new Vector2(30f, -25f);
            c.sizeDelta = new Vector2(1500f, 60f);
            hud.recordCounterText.textWrappingMode = TextWrappingModes.NoWrap;
            hud.recordCounterText.alignment = TextAlignmentOptions.TopLeft;
        }
        if (hud.recordContentText != null)
        {
            RectTransform rt = hud.recordContentText.rectTransform;
            rt.anchorMin = new Vector2(0.15f, 0.1f);
            rt.anchorMax = new Vector2(0.85f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            hud.recordContentText.enableAutoSizing = true;
            hud.recordContentText.fontSizeMin = 12;
            hud.recordContentText.fontSizeMax = 34;
            hud.recordContentText.alignment = TextAlignmentOptions.TopLeft;
            hud.recordContentText.color = new Color(0.9f, 0.88f, 0.8f);
        }
    }

    void Update()
    {
        if (messageText != null && messageText.gameObject.activeSelf && Time.unscaledTime >= messageUntil)
            messageText.gameObject.SetActive(false);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == State.Paused)
            {
                if (confirmPanel.activeSelf) confirmPanel.SetActive(false);
                else Resume();
                return;
            }
            if (state == State.Playing && Time.timeScale > 0f && !Reading())
            {
                Pause();
                return;
            }
        }

        if (state == State.Playing && Time.timeScale > 0f)
        {
            playTime += Time.deltaTime;

            // Fim da fase 1
            if (pendingWin)
            {
                pendingWin = false;
                if (HasPhase2) StartCoroutine(Phase2Sequence(true));
                else StartCoroutine(EndSequence("VOCÊ SOBREVIVEU", "Todos os prontuários do Karlheinz Asylum foram encontrados.\nA verdade sobre Emil Brandt não ficará mais enterrada."));
                return;
            }

            if (teclasDeTeste && Input.GetKeyDown(KeyCode.F9)) SkipStep();
            if (teclasDeTeste && Input.GetKeyDown(KeyCode.F10)) ToggleEnemy();
        }

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (state == State.Menu && enter)
            StartFromMenu();
        else if (state == State.Won && winPanel.activeSelf && (enter || Input.GetKeyDown(KeyCode.R)))
            RestartGame();
        else if (state == State.Dead && deathPanel.activeSelf && (enter || Input.GetKeyDown(KeyCode.R)))
            Retry();
    }

    // ---------- Estados ----------

    void ShowMenu()
    {
        state = State.Menu;
        menuPanel.SetActive(true);
        deathPanel.SetActive(false);
        Time.timeScale = 0f;
        SetControls(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---------- Pausa ----------

    bool Reading()
    {
        GameHUD hud = GameHUD.Instance;
        return hud != null && (hud.IsReading || hud.ClosedFrame == Time.frameCount);
    }

    void Pause()
    {
        state = State.Paused;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        SetControls(false);
        confirmPanel.SetActive(false);
        pausePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (state != State.Paused) return;
        state = State.Playing;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetControls(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Pede confirmacao antes de executar a acao
    void AskConfirm(string question, UnityEngine.Events.UnityAction action)
    {
        confirmText.text = question;
        confirmAction = action;
        confirmPanel.SetActive(true);
    }

    void ConfirmYes()
    {
        confirmPanel.SetActive(false);
        if (confirmAction != null) confirmAction();
    }

    void ConfirmNo()
    {
        confirmPanel.SetActive(false);
    }

    void AskRestart() { AskConfirm(phase == 2 ? "Reiniciar a fase 2?" : "Reiniciar a partida?", Retry); }
    void AskMenu() { AskConfirm("Voltar ao menu principal?", BackToMenu); }
    void AskQuit() { AskConfirm("Sair do jogo?", Quit); }

    // Botao JOGAR
    public void StartFromMenu()
    {
        if (state != State.Menu) return;
        StartCoroutine(IntroSequence());
    }

    // Botao FASE 2
    public void StartPhase2FromMenu()
    {
        if (state != State.Menu || !HasPhase2) return;
        StartCoroutine(Phase2Sequence(false));
    }

    public void StartGame()
    {
        state = State.Playing;
        menuPanel.SetActive(false);
        deathPanel.SetActive(false);
        introPanel.SetActive(false);
        Time.timeScale = 1f;
        SetControls(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void SetControls(bool on)
    {
        if (pc != null) pc.enabled = on;
        if (pi != null)
        {
            pi.enabled = on;
            if (!on && pi.interactPrompt != null) pi.interactPrompt.SetActive(false);
        }
        if (fl != null) fl.enabled = on;
    }

    // Texto de abertura: o personagem acorda no saguao
    IEnumerator IntroSequence()
    {
        state = State.Intro;
        menuPanel.SetActive(false);
        introTitle.text = "KARLHEINZ ASYLUM, 1974";
        introText.text = "Você acorda no chão frio do saguão.\nNão lembra como chegou aqui.\n\nA porta principal está trancada.\nEm algum lugar, algo se arrasta pelos corredores.";
        introPanel.SetActive(true);
        yield return null;
        yield return WaitOrSkip(10f);

        fade.gameObject.SetActive(true);
        SetFade(1f);
        StartGame();
        yield return Fade(1f, 0f, 1.5f);
        ShowMessage("Encontre os prontuários.", 4f);
    }

    // Espera o tempo acabar ou o jogador pular
    IEnumerator WaitOrSkip(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) break;
            yield return null;
        }
    }

    // ---------- Fase 2 ----------

    // Tela de transicao e inicio da fase 2
    IEnumerator Phase2Sequence(bool fromPhase1)
    {
        state = State.Intro;
        SetControls(false);
        menuPanel.SetActive(false);
        deathPanel.SetActive(false);

        fade.gameObject.SetActive(true);
        if (fromPhase1) yield return Fade(0f, 1f, 1f);
        else SetFade(1f);

        Time.timeScale = 0f;
        introTitle.text = "FASE 2";
        introText.text = "Os prontuários contam o que aconteceu, mas não abrem a porta.\n\nSó o choque fazia a criatura recuar, e o gerador está sem fusíveis.\nEncontre os " + totalFuses + " fusíveis espalhados pelo prédio.";
        introPanel.SetActive(true);
        yield return null;
        yield return WaitOrSkip(8f);

        EnterPhase2();
        StartGame();
        yield return Fade(1f, 0f, 1.2f);
    }

    void EnterPhase2()
    {
        phase = 2;
        step = Step.Fuses;
        fusesCollected = 0;
        hasKey = false;
        pendingWin = false;

        foreach (Collectible c in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            if (c.tipo == Collectible.Tipo.Prontuario) Destroy(c.gameObject);

        foreach (Collectible c in phase2Items)
            if (c != null) c.gameObject.SetActive(c.tipo != Collectible.Tipo.Chave);

        if (enemy != null)
        {
            if (trap != null) enemy.SetLure(trap.Center, raioDeAtracao);
            enemy.SetHuntLevel(1);
            enemy.Rest(tempoDeRespiro);
        }

        if (fl != null)
        {
            fl.drainPerSecond *= drenoFase2;
            fl.RecarregarBateria(fl.maxBattery * recargaFase2);
        }

        BackgroundMusic music = Object.FindFirstObjectByType<BackgroundMusic>();
        if (music != null) music.SetVolume(music.volume * 1.5f);

        UpdateObjective();
    }

    // Atualiza o objetivo mostrado no HUD
    void UpdateObjective()
    {
        if (GameHUD.Instance == null) return;
        string text = "";
        switch (step)
        {
            case Step.Fuses: text = "Fusíveis: " + fusesCollected + "/" + totalFuses; break;
            case Step.Generator: text = "Ligue o gerador (" + (generator != null ? generator.local : "?") + ")"; break;
            case Step.Trap: text = "Atraia a criatura e puxe a alavanca (" + (trap != null ? trap.local : "?") + ")"; break;
            case Step.Key: text = "Pegue a chave que a criatura deixou cair"; break;
            case Step.Exit: text = "Fuja pela porta principal"; break;
        }
        GameHUD.Instance.SetObjective(text);
    }

    // Chamado pelo Collectible ao pegar um fusivel
    public void FuseCollected()
    {
        fusesCollected++;
        if (phase == 2 && step == Step.Fuses && fusesCollected >= totalFuses)
        {
            step = Step.Generator;
            ShowMessage("Todos os fusíveis encontrados. Agora o gerador.", 4f);
        }
        UpdateObjective();
    }

    // Chamado pelo Generator
    public void UseGenerator(Generator g)
    {
        if (state != State.Playing || g == null || g.IsOn) return;
        if (phase != 2)
        {
            ShowMessage("O gerador está sem fusíveis.", 3f);
            return;
        }
        if (step == Step.Fuses)
        {
            ShowMessage("Faltam fusíveis: " + fusesCollected + "/" + totalFuses, 3f);
            return;
        }

        g.TurnOn();
        StartCoroutine(FlickerLamps());
        if (trap != null) trap.SetPowered(true);
        if (step == Step.Generator)
        {
            step = Step.Trap;
            if (enemy != null) enemy.SetHuntLevel(2);
            ShowMessage("Gerador ligado. Alguma coisa ouviu o barulho.", 4f);
            UpdateObjective();
        }
    }

    // Chamado pela ShockTrap antes de disparar
    public bool CanUseTrap()
    {
        if (state != State.Playing) return false;
        if (phase == 2 && step == Step.Trap) return true;
        if (phase == 2 && step > Step.Trap) return false;
        ShowMessage("Sem energia. O gerador está desligado.", 3f);
        return false;
    }

    // Chamado pela ShockTrap quando a criatura morre
    public void CreatureKilled(EnemyAI e)
    {
        if (step > Step.Trap) return;
        step = Step.Key;

        if (sfx != null && screamClip != null)
        {
            sfx.pitch = 0.8f;
            sfx.PlayOneShot(screamClip, 0.9f);
        }

        if (keyItem != null && e != null)
        {
            Vector3 pos = e.transform.position;
            if (pc != null)
            {
                Vector3 dir = pc.transform.position - pos;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) pos += dir.normalized * 1.2f;
            }
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
            keyItem.transform.position = pos + Vector3.up * 0.03f;
            keyItem.gameObject.SetActive(true);
            ShowMessage("A criatura caiu. Algo brilha perto do corpo.", 4f);
        }
        else
        {
            hasKey = true;
            step = Step.Exit;
        }
        UpdateObjective();
    }

    // Chamado pelo Collectible ao pegar a chave
    public void KeyCollected()
    {
        hasKey = true;
        step = Step.Exit;
        ShowMessage("Você pegou a chave da porta principal.", 4f);
        UpdateObjective();
    }

    // Chamado pela ExitDoor
    public void TryExit()
    {
        if (state != State.Playing) return;
        if (!hasKey)
        {
            ShowMessage(phase == 2 ? "Trancada. Preciso da chave." : "Trancada.", 3f);
            return;
        }
        StartCoroutine(EndSequence("VOCÊ ESCAPOU", "A chave gira e a porta principal se abre.\nO que restou de Emil Brandt ficou para trás, no Karlheinz Asylum."));
    }

    // Atalho de teste: liga ou desliga a criatura
    void ToggleEnemy()
    {
        if (enemy == null || enemy.IsDead) return;
        enemy.SetDisabled(!enemy.IsDisabled);
        ShowMessage(enemy.IsDisabled ? "Criatura desativada" : "Criatura ativada", 2f);
    }

    // Atalho de teste: conclui a etapa atual
    void SkipStep()
    {
        if (!HasPhase2 || state != State.Playing) return;
        if (phase == 1)
        {
            StartCoroutine(Phase2Sequence(true));
            return;
        }

        switch (step)
        {
            case Step.Fuses:
                foreach (Collectible c in phase2Items)
                    if (c != null && c.tipo == Collectible.Tipo.Fusivel) Destroy(c.gameObject);
                fusesCollected = totalFuses;
                step = Step.Generator;
                UpdateObjective();
                break;
            case Step.Generator:
                UseGenerator(generator);
                break;
            case Step.Trap:
                if (enemy != null && trap != null)
                {
                    enemy.WarpTo(trap.Center);
                    trap.ForceFire();
                }
                break;
            case Step.Key:
                if (keyItem != null) keyItem.Collect();
                break;
            case Step.Exit:
                TryExit();
                break;
        }
    }

    // ---------- Morte e final ----------

    public void PlayerCaught(EnemyAI enemy)
    {
        if (state != State.Playing) return;
        state = State.Dead;
        SetControls(false);
        StartCoroutine(DeathSequence(enemy));
    }

    IEnumerator DeathSequence(EnemyAI enemy)
    {
        // Camera vira para a criatura
        if (cam != null && enemy != null)
        {
            Quaternion from = cam.rotation;
            Vector3 dir = enemy.HeadPosition - cam.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion to = Quaternion.LookRotation(dir);
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    cam.rotation = Quaternion.Slerp(from, to, t / 0.35f);
                    yield return null;
                }
                cam.rotation = to;
            }
        }

        // Grito e tremor
        sfx.pitch = 1f;
        if (screamClip != null) sfx.PlayOneShot(screamClip, 1f);
        if (cam != null)
        {
            Vector3 basePos = cam.localPosition;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                cam.localPosition = basePos + Random.insideUnitSphere * 0.08f * (1f - t / 0.6f);
                yield return null;
            }
            cam.localPosition = basePos;
        }

        // Tela de morte
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Fade(0f, 1f, 0.8f);
        deathPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    // Chamado pelo Collectible ao pegar um prontuario
    public void RecordCollected()
    {
        recordsCollected++;
        if (phase == 1 && totalRecords > 0 && recordsCollected >= totalRecords) pendingWin = true;
    }

    // Tela final
    IEnumerator EndSequence(string title, string text)
    {
        state = State.Won;
        SetControls(false);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Fade(0f, 1f, 1.5f);
        int min = (int)(playTime / 60f), sec = (int)(playTime % 60f);
        winTitle.text = title;
        winText.text = text;
        winTimeText.text = "Tempo: " + min + ":" + sec.ToString("00");
        winPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    // Recomeca na mesma fase
    public void Retry()
    {
        skipMenuOnNextLoad = true;
        startInPhase2 = (phase == 2);
        Reload();
    }

    // Recomeca do inicio
    public void RestartGame()
    {
        skipMenuOnNextLoad = true;
        startInPhase2 = false;
        Reload();
    }

    public void BackToMenu()
    {
        skipMenuOnNextLoad = false;
        startInPhase2 = false;
        Reload();
    }

    void Reload()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Scene s = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(s.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(s.buildIndex);
#endif
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Audio ----------

    void SetupAudio()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.ignoreListenerPause = true;
        AudioClip[] found = Resources.LoadAll<AudioClip>("Audio/Captura");
        screamClip = found.Length > 0 ? found[0] : null;

        if (Object.FindFirstObjectByType<BackgroundMusic>() == null) gameObject.AddComponent<BackgroundMusic>();
        if (Object.FindFirstObjectByType<RandomSounds>() == null) gameObject.AddComponent<RandomSounds>();

        if (pc != null && pc.GetComponent<PlayerFootsteps>() == null)
            pc.gameObject.AddComponent<PlayerFootsteps>();
    }

    // ---------- Interface ----------

    void BuildUI()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject cgo = new GameObject("GameFlowUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;

        // Mensagens curtas
        messageText = Label(cgo.transform, "", 34, new Vector2(0, -380), LightGrey, 1500f, 60f);
        messageText.gameObject.SetActive(false);

        // Menu inicial
        RectTransform menu = FullPanel("MenuPanel", cgo.transform, new Color(0f, 0f, 0f, 0.82f));
        menuPanel = menu.gameObject;
        Label(menu, "ASYLUM 1974", 120, new Vector2(0, 260), Red);
        Label(menu, "Karlheinz Asylum", 40, new Vector2(0, 160), LightGrey);
        Label(menu, "Encontre os prontuários e sobreviva. Não deixe a criatura te pegar.", 30, new Vector2(0, 90), Grey);
        if (mostrarBotaoFase2 && HasPhase2)
        {
            MakeButton(menu, "JOGAR", new Vector2(0, -20), StartFromMenu);
            MakeButton(menu, "FASE 2", new Vector2(0, -115), StartPhase2FromMenu);
            MakeButton(menu, "SAIR", new Vector2(0, -210), Quit);
        }
        else
        {
            MakeButton(menu, "JOGAR", new Vector2(0, -40), StartFromMenu);
            MakeButton(menu, "SAIR", new Vector2(0, -140), Quit);
        }
        Label(menu, "WASD mover  |  Shift correr  |  E interagir  |  F lanterna  |  ESC pausar", 26, new Vector2(0, -330), Grey);

        // Fade preto
        RectTransform fr = FullPanel("Fade", cgo.transform, new Color(0f, 0f, 0f, 0f));
        fade = fr.GetComponent<Image>();
        fade.raycastTarget = false;
        fr.gameObject.SetActive(false);

        // Tela de texto (introducao e fase 2)
        RectTransform intro = FullPanel("IntroPanel", cgo.transform, Color.black);
        introPanel = intro.gameObject;
        introTitle = Label(intro, "", 70, new Vector2(0, 230), Red);
        introText = Label(intro, "", 36, new Vector2(0, -30), LightGrey, 1500f, 360f);
        Label(intro, "[Enter] continuar", 24, new Vector2(0, -430), Grey);
        introPanel.SetActive(false);

        // Tela de morte
        RectTransform death = FullPanel("DeathPanel", cgo.transform, new Color(0f, 0f, 0f, 0f));
        death.GetComponent<Image>().raycastTarget = false;
        deathPanel = death.gameObject;
        Label(death, "VOCÊ MORREU", 130, new Vector2(0, 170), Red);
        MakeButton(death, "TENTAR NOVAMENTE", new Vector2(0, -20), Retry);
        MakeButton(death, "MENU PRINCIPAL", new Vector2(0, -120), BackToMenu);
        Label(death, "[Enter] tentar novamente", 26, new Vector2(0, -240), Grey);
        deathPanel.SetActive(false);

        // Tela final
        RectTransform win = FullPanel("WinPanel", cgo.transform, new Color(0f, 0f, 0f, 0f));
        win.GetComponent<Image>().raycastTarget = false;
        winPanel = win.gameObject;
        winTitle = Label(win, "", 120, new Vector2(0, 230), new Color(0.85f, 0.8f, 0.65f));
        winText = Label(win, "", 32, new Vector2(0, 110), LightGrey, 1700f, 110f);
        winTimeText = Label(win, "", 30, new Vector2(0, 30), Grey);
        MakeButton(win, "JOGAR NOVAMENTE", new Vector2(0, -80), RestartGame);
        MakeButton(win, "MENU PRINCIPAL", new Vector2(0, -180), BackToMenu);
        winPanel.SetActive(false);

        // Pausa
        RectTransform pause = FullPanel("PausePanel", cgo.transform, new Color(0f, 0f, 0f, 0.8f));
        pausePanel = pause.gameObject;
        Label(pause, "PAUSADO", 100, new Vector2(0, 280), Red);
        MakeButton(pause, "CONTINUAR", new Vector2(0, 100), Resume);
        MakeButton(pause, "REINICIAR", new Vector2(0, 0), AskRestart);
        MakeButton(pause, "MENU PRINCIPAL", new Vector2(0, -100), AskMenu);
        MakeButton(pause, "SAIR", new Vector2(0, -200), AskQuit);
        Label(pause, "[ESC] continuar", 26, new Vector2(0, -320), Grey);

        // Confirmacao
        RectTransform confirm = FullPanel("ConfirmPanel", pause, new Color(0f, 0f, 0f, 0.96f));
        confirmPanel = confirm.gameObject;
        confirmText = Label(confirm, "", 56, new Vector2(0, 110), LightGrey);
        MakeButton(confirm, "SIM", new Vector2(-240, -40), ConfirmYes);
        MakeButton(confirm, "NÃO", new Vector2(240, -40), ConfirmNo);
        confirmPanel.SetActive(false);
        pausePanel.SetActive(false);
    }

    // Mostra uma mensagem por alguns segundos
    public void ShowMessage(string text, float seconds)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        messageUntil = Time.unscaledTime + seconds;
    }

    IEnumerator Fade(float a, float b, float dur)
    {
        fade.gameObject.SetActive(true);
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(a, b, t / dur));
            yield return null;
        }
        SetFade(b);
        if (b <= 0f) fade.gameObject.SetActive(false);
    }

    void SetFade(float a)
    {
        Color c = fade.color;
        c.a = a;
        fade.color = c;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static RectTransform FullPanel(string name, Transform parent, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return rt;
    }

    static TextMeshProUGUI Label(Transform parent, string text, float size, Vector2 pos, Color color, float width = 1700f, float height = 0f)
    {
        RectTransform rt = NewRect("Text", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height > 0f ? height : size * 1.6f);
        rt.anchoredPosition = pos;
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeButton(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = NewRect("Button " + text, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(440, 80);
        rt.anchoredPosition = pos;
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.55f, 0.55f);
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.7f, 0.3f, 0.3f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        Label(rt, text, 40, Vector2.zero, LightGrey, 420f, 80f);
        return b;
    }
}
