using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD: contador, objetivo, barra de bateria e leitura de prontuario
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance;

    [Header("Prontuarios")]
    public int totalRecords = 5;
    public TMP_Text recordCounterText;
    public GameObject recordPanel;
    public TMP_Text recordContentText;

    [Header("Bateria")]
    public Flashlight flashlight;
    public Image batteryBar;

    [Header("Leitura")]
    public KeyCode closeKey = KeyCode.Escape;

    private int recordsCollected;
    private bool reading;
    private string objective;

    public bool IsReading { get { return reading; } }
    public int ClosedFrame { get; private set; } = -1;

    void Awake() { Instance = this; }

    void Start()
    {
        UpdateRecordCounter();
        if (recordPanel != null) recordPanel.SetActive(false);
    }

    void Update()
    {
        // Barra de bateria
        if (batteryBar != null && flashlight != null && flashlight.maxBattery > 0f)
            batteryBar.fillAmount = flashlight.currentBattery / flashlight.maxBattery;

        // Fecha a leitura
        if (reading && Input.GetKeyDown(closeKey))
            CloseRecord();
    }

    // Conta o prontuario e abre a leitura
    public void CollectRecord(string texto)
    {
        recordsCollected++;
        UpdateRecordCounter();
        ShowRecord(texto);
    }

    // Abre um texto sem contar como prontuario
    public void ShowNote(string texto)
    {
        ShowRecord(texto);
    }

    // Troca o contador pelo objetivo da fase 2
    public void SetObjective(string text)
    {
        objective = text;
        UpdateRecordCounter();
    }

    void UpdateRecordCounter()
    {
        if (recordCounterText == null) return;
        if (!string.IsNullOrEmpty(objective)) recordCounterText.text = objective;
        else recordCounterText.text = "Prontuários: " + recordsCollected + "/" + totalRecords;
    }

    // Pausa o jogo e mostra o texto
    void ShowRecord(string texto)
    {
        if (recordPanel == null || recordContentText == null) return;

        recordContentText.text = texto;
        recordPanel.SetActive(true);
        reading = true;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetPlayerControlsEnabled(false);
    }

    // Fecha o texto e retoma o jogo
    void CloseRecord()
    {
        if (recordPanel != null) recordPanel.SetActive(false);
        reading = false;
        ClosedFrame = Time.frameCount;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetPlayerControlsEnabled(true);
    }

    void SetPlayerControlsEnabled(bool on)
    {
        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = on;

        PlayerInteractor pi = Object.FindFirstObjectByType<PlayerInteractor>();
        if (pi != null) pi.enabled = on;
    }
}
