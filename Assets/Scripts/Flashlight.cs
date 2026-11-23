using UnityEngine;

// Lanterna, liga e desliga com F
[RequireComponent(typeof(Light))]
public class Flashlight : MonoBehaviour
{
    [Header("Controle")]
    public KeyCode toggleKey = KeyCode.F;
    public bool startsOn = true;

    [Header("Bateria (opcional)")]
    public bool useBattery = false; 
    public float maxBattery = 100f;
    public float currentBattery = 100f;
    public float drainPerSecond = 2f;   // gasto por segundo com a luz ligada

    private Light lightSource;
    private bool isOn;

    void Start()
    {
        lightSource = GetComponent<Light>();
        isOn = startsOn;
        lightSource.enabled = isOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();

        // Gasta bateria enquanto ligada (so se a bateria estiver ativada)
        if (useBattery && isOn)
        {
            currentBattery -= drainPerSecond * Time.deltaTime;
            if (currentBattery <= 0f)
            {
                currentBattery = 0f;
                SetOn(false); // acabou a bateria: apaga sozinha
            }
        }
    }

    void Toggle()
    {
        // Nao deixa ligar sem bateria (quando a bateria esta ativada)
        if (!isOn && useBattery && currentBattery <= 0f) return;
        SetOn(!isOn);
    }

    void SetOn(bool on)
    {
        isOn = on;
        lightSource.enabled = on;
    }

    // Recarrega a bateria
    public void RecarregarBateria(float quantidade)
    {
        currentBattery = Mathf.Min(currentBattery + quantidade, maxBattery);
    }
}
