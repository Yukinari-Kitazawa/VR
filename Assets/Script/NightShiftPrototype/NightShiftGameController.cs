using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NightShiftGameController : MonoBehaviour
{
    private enum NightState
    {
        Title,
        Playing,
        Paused,
        Won,
        Lost
    }

    [Header("Rules")]
    [SerializeField] private float nightLengthSeconds = 300f;
    [SerializeField] private float startingPower = 100f;
    [SerializeField] private float basePowerDrainPerSecond = 0.025f;
    [SerializeField] private float doorPowerDrainPerSecond = 0.14f;
    [SerializeField] private float monitorPowerDrainPerSecond = 0.06f;
    [SerializeField] private float officeLightPowerDrainPerSecond = 0.08f;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactableLayers = ~0;

    [Header("Scene")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private NightShiftVRRigController vrRig;
    [SerializeField] private Transform interactionRayOrigin;
    [SerializeField] private NightShiftOfficeDoor officeDoor;
    [SerializeField] private NightShiftEnemyStalker enemyStalker;
    [SerializeField] private Light officeLight;
    [SerializeField] private Light hallwayLight;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI dangerText;
    [SerializeField] private TextMeshProUGUI monitorFeedText;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultBodyText;
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject monitorPanel;
    [SerializeField] private GameObject jumpscarePanel;
    [SerializeField] private Image staticOverlay;

    [Header("World UI")]
    [SerializeField] private Transform monitorUiAnchor;
    [SerializeField] private float monitorUiSurfaceOffset = 0.096f;
    [SerializeField] private Vector3 monitorUiLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 monitorUiLocalEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] private Vector3 monitorUiLocalScale = Vector3.one * 0.0009f;
    [SerializeField] private Vector2 monitorUiSize = new Vector2(1200f, 680f);

    [Header("Menu UI")]
    [SerializeField] private Vector3 menuUiLocalPosition = new Vector3(0f, -0.04f, 2.35f);
    [SerializeField] private Vector3 menuUiLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 menuUiLocalScale = Vector3.one * 0.0018f;
    [SerializeField] private Vector2 menuUiSize = new Vector2(1600f, 900f);

    private NightState state = NightState.Title;
    private float elapsedNightTime;
    private float power;
    private bool monitorOpen;
    private bool powerOut;
    private NightShiftInteractable focusedInteractable;

    public bool IsPlaying => state == NightState.Playing;
    public bool IsMonitorOpen => monitorOpen;
    public bool IsDoorClosed => officeDoor != null && officeDoor.IsClosed;
    public bool PlayerHasControl => state == NightState.Playing && !monitorOpen;

    private void Awake()
    {
        power = startingPower;
        SetPanelActive(titlePanel, true);
        SetPanelActive(pausePanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(monitorPanel, false);
        SetPanelActive(jumpscarePanel, false);
        Time.timeScale = 1f;
    }

    private void Start()
    {
        ConfigureUiCanvases();
        ResetNightObjects();
        ShowTitle();
    }

    private void Update()
    {
        if (vrRig != null && vrRig.PausePressedThisFrame)
            HandlePauseInput();

        if (state == NightState.Title)
        {
            if (vrRig != null && vrRig.ConfirmPressedThisFrame)
                BeginNight();
            return;
        }

        if (state == NightState.Won || state == NightState.Lost)
        {
            if (vrRig != null && vrRig.ConfirmPressedThisFrame)
                RestartScene();
            return;
        }

        if (state == NightState.Paused)
        {
            if (vrRig != null && vrRig.ConfirmPressedThisFrame)
                ResumeNight();
            return;
        }

        UpdatePlayingInput();
        UpdateNightTimer();
        UpdatePower();
        UpdateInteractionPrompt();
        UpdateHud();
    }

    public void UseInteraction(NightShiftInteractionAction action)
    {
        if (state != NightState.Playing)
            return;

        switch (action)
        {
            case NightShiftInteractionAction.ToggleDoor:
                if (officeDoor != null)
                    officeDoor.SetClosed(!officeDoor.IsClosed);
                break;
            case NightShiftInteractionAction.ToggleMonitor:
                SetMonitorOpen(!monitorOpen);
                break;
            case NightShiftInteractionAction.ToggleOfficeLight:
                ToggleOfficeLight();
                break;
        }
    }

    public void SetDanger(string message)
    {
        if (dangerText == null)
            return;

        dangerText.text = message;
        dangerText.gameObject.SetActive(true);
    }

    public void ClearDanger()
    {
        if (dangerText != null)
            dangerText.gameObject.SetActive(false);
    }

    public void BlockEnemyAtDoor()
    {
        if (hallwayLight != null)
            hallwayLight.intensity = 2.8f;

        SetDanger("Impact blocked. It retreated.");
        Invoke(nameof(ClearDanger), 2.25f);
    }

    public void LoseNight(string reason)
    {
        if (state == NightState.Lost || state == NightState.Won)
            return;

        state = NightState.Lost;
        SetMonitorOpen(false);
        SetPanelActive(jumpscarePanel, true);
        ShowResult("YOU DIED", reason);
    }

    private void BeginNight()
    {
        state = NightState.Playing;
        elapsedNightTime = 0f;
        power = startingPower;
        powerOut = false;

        ResetNightObjects();
        SetPanelActive(titlePanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(jumpscarePanel, false);
        UpdateHud();
    }

    private void ShowTitle()
    {
        state = NightState.Title;
        SetPanelActive(titlePanel, true);
        SetPanelActive(pausePanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(monitorPanel, false);
        SetPanelActive(jumpscarePanel, false);
        UpdateHud();
    }

    private void ConfigureUiCanvases()
    {
        Canvas sourceCanvas = GetUiCanvas();
        if (sourceCanvas == null)
            return;

        Canvas menuCanvas = GetOrCreateMenuCanvas();
        if (menuCanvas != null)
        {
            ConfigureMenuCanvas(menuCanvas);
            MoveMenuUi(menuCanvas.transform);
        }

        Canvas monitorCanvas = GetOrCreateMonitorCanvas(sourceCanvas);
        if (monitorCanvas != null)
        {
            ConfigureMonitorCanvas(monitorCanvas);
            MoveMonitorUi(monitorCanvas.transform);
        }
    }

    private Canvas GetOrCreateMenuCanvas()
    {
        Canvas existing = FindCanvas("Menu UI Canvas");
        if (existing != null)
            return existing;

        Transform parent = playerCamera != null ? playerCamera.transform : transform;
        return CreateWorldCanvas("Menu UI Canvas", parent);
    }

    private Canvas GetOrCreateMonitorCanvas(Canvas sourceCanvas)
    {
        Canvas existing = FindCanvas("Monitor Camera Canvas");
        if (existing != null)
            return existing;

        Canvas currentMonitorCanvas = monitorPanel != null ? monitorPanel.GetComponentInParent<Canvas>(true) : null;
        if (currentMonitorCanvas != null && currentMonitorCanvas != sourceCanvas)
            return currentMonitorCanvas;

        if (sourceCanvas != null)
        {
            sourceCanvas.name = "Monitor Camera Canvas";
            return sourceCanvas;
        }

        Transform anchor = ResolveMonitorUiAnchor();
        return anchor != null ? CreateWorldCanvas("Monitor Camera Canvas", anchor) : null;
    }

    private void ConfigureMenuCanvas(Canvas canvas)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = playerCamera;
        EnsureCanvasComponents(canvas);

        RectTransform rect = canvas.GetComponent<RectTransform>();
        Transform parent = playerCamera != null ? playerCamera.transform : transform;
        rect.SetParent(parent, false);
        rect.localPosition = menuUiLocalPosition;
        rect.localRotation = Quaternion.Euler(menuUiLocalEulerAngles);
        rect.localScale = menuUiLocalScale == Vector3.zero ? Vector3.one * 0.0018f : menuUiLocalScale;
        rect.sizeDelta = menuUiSize == Vector2.zero ? new Vector2(1600f, 900f) : menuUiSize;
    }

    private void ConfigureMonitorCanvas(Canvas canvas)
    {
        Transform anchor = ResolveMonitorUiAnchor();
        if (anchor == null)
            return;

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = playerCamera;
        EnsureCanvasComponents(canvas);

        Vector3 localScale = monitorUiLocalScale;
        if (localScale == Vector3.zero || localScale.x < 0.00085f || localScale.y < 0.00085f)
            localScale = Vector3.one * 0.0009f;

        Vector2 size = monitorUiSize;
        if (size == Vector2.zero || size.x > 1400f || size.y > 800f)
            size = new Vector2(1200f, 680f);

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.SetParent(anchor, false);
        rect.localPosition = monitorUiLocalPosition;
        rect.localRotation = Quaternion.Euler(GetMonitorUiLocalEulerAngles());
        rect.localScale = localScale;
        rect.sizeDelta = size;
    }

    private void MoveMenuUi(Transform parent)
    {
        MoveToCanvas(timerText, parent);
        MoveToCanvas(powerText, parent);
        MoveToCanvas(statusText, parent);
        MoveToCanvas(promptText, parent);
        MoveToCanvas(dangerText, parent);
        MoveToCanvas(titlePanel, parent);
        MoveToCanvas(pausePanel, parent);
        MoveToCanvas(resultPanel, parent);
        MoveToCanvas(jumpscarePanel, parent);

        GameObject reticle = GameObject.Find("Reticle");
        if (reticle != null)
            MoveToCanvas(reticle, parent);
    }

    private void MoveMonitorUi(Transform parent)
    {
        MoveToCanvas(monitorPanel, parent);

        if (monitorFeedText != null)
        {
            monitorFeedText.fontSize = Mathf.Max(monitorFeedText.fontSize, 72f);
            monitorFeedText.alignment = TextAlignmentOptions.Center;
            monitorFeedText.enableWordWrapping = true;

            RectTransform rect = monitorFeedText.rectTransform;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1060f, 520f);
        }
    }

    private Transform ResolveMonitorUiAnchor()
    {
        if (monitorUiAnchor != null && monitorUiAnchor.name != "Security Monitor")
            return monitorUiAnchor;

        GameObject existingAnchor = GameObject.Find("Monitor UI Anchor");
        if (existingAnchor != null)
        {
            monitorUiAnchor = existingAnchor.transform;
            return monitorUiAnchor;
        }

        Transform monitor = monitorUiAnchor;
        if (monitor == null)
        {
            GameObject monitorObject = GameObject.Find("Security Monitor");
            if (monitorObject != null)
                monitor = monitorObject.transform;
        }

        if (monitor == null)
            return null;

        GameObject anchorObject = new GameObject("Monitor UI Anchor");
        monitorUiAnchor = anchorObject.transform;
        monitorUiAnchor.SetParent(monitor.parent, false);
        monitorUiAnchor.position = monitor.position + monitor.forward * GetMonitorUiSurfaceOffset();
        monitorUiAnchor.rotation = monitor.rotation;
        monitorUiAnchor.localScale = Vector3.one;
        return monitorUiAnchor;
    }

    private float GetMonitorUiSurfaceOffset()
    {
        return monitorUiSurfaceOffset > 0f ? monitorUiSurfaceOffset : 0.096f;
    }

    private Vector3 GetMonitorUiLocalEulerAngles()
    {
        return monitorUiLocalEulerAngles == Vector3.zero ? new Vector3(0f, 180f, 0f) : monitorUiLocalEulerAngles;
    }

    private Canvas FindCanvas(string canvasName)
    {
        GameObject canvasObject = GameObject.Find(canvasName);
        return canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
    }

    private Canvas CreateWorldCanvas(string canvasName, Transform parent)
    {
        GameObject canvasObject = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = playerCamera;

        EnsureCanvasComponents(canvas);
        return canvas;
    }

    private static void EnsureCanvasComponents(Canvas canvas)
    {
        if (canvas.GetComponent<CanvasScaler>() == null)
            canvas.gameObject.AddComponent<CanvasScaler>();

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;
    }

    private static void MoveToCanvas(Component component, Transform parent)
    {
        if (component != null)
            component.transform.SetParent(parent, false);
    }

    private static void MoveToCanvas(GameObject gameObject, Transform parent)
    {
        if (gameObject != null)
            gameObject.transform.SetParent(parent, false);
    }

    private Canvas GetUiCanvas()
    {
        if (timerText != null)
            return timerText.GetComponentInParent<Canvas>(true);

        if (monitorFeedText != null)
            return monitorFeedText.GetComponentInParent<Canvas>(true);

        if (titlePanel != null)
            return titlePanel.GetComponentInParent<Canvas>(true);

        return null;
    }

    private void PauseNight()
    {
        if (state != NightState.Playing)
            return;

        state = NightState.Paused;
        Time.timeScale = 0f;
        SetPanelActive(pausePanel, true);
    }

    private void ResumeNight()
    {
        if (state != NightState.Paused)
            return;

        state = NightState.Playing;
        Time.timeScale = 1f;
        SetPanelActive(pausePanel, false);
    }

    private void WinNight()
    {
        state = NightState.Won;
        SetMonitorOpen(false);
        ShowResult("6:00 AM", "You survived the shift.");
    }

    private void ShowResult(string title, string body)
    {
        Time.timeScale = 1f;
        SetPanelActive(resultPanel, true);

        if (resultPanel != null)
            resultPanel.transform.SetAsLastSibling();

        if (resultTitleText != null)
            resultTitleText.text = title;

        if (resultBodyText != null)
            resultBodyText.text = body + "\n\n" + ConfirmLabel + ": Restart";
    }

    private void HandlePauseInput()
    {
        if (state == NightState.Playing)
        {
            PauseNight();
            return;
        }

        if (state == NightState.Paused)
            ResumeNight();
    }

    private void UpdatePlayingInput()
    {
        if (vrRig == null)
            return;

        if (vrRig.MonitorPressedThisFrame)
            SetMonitorOpen(!monitorOpen);

        if (vrRig.InteractPressedThisFrame)
            TryInteract();
    }

    private void TryInteract()
    {
        Transform rayOrigin = GetInteractionRayOrigin();
        if (rayOrigin == null || monitorOpen)
            return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayers, QueryTriggerInteraction.Collide))
            return;

        NightShiftInteractable interactable = hit.collider.GetComponentInParent<NightShiftInteractable>();
        if (interactable != null)
            interactable.Interact();
    }

    private void UpdateNightTimer()
    {
        elapsedNightTime += Time.deltaTime;
        if (elapsedNightTime >= nightLengthSeconds)
            WinNight();
    }

    private void UpdatePower()
    {
        if (powerOut)
            return;

        float drain = basePowerDrainPerSecond;
        if (officeDoor != null && officeDoor.IsClosed)
            drain += doorPowerDrainPerSecond;
        if (monitorOpen)
            drain += monitorPowerDrainPerSecond;
        if (officeLight != null && officeLight.enabled)
            drain += officeLightPowerDrainPerSecond;

        power = Mathf.Max(0f, power - drain * Time.deltaTime);
        if (power <= 0f)
            TriggerPowerOut();
    }

    private void TriggerPowerOut()
    {
        powerOut = true;
        SetMonitorOpen(false);

        if (officeDoor != null)
        {
            officeDoor.SetPowered(false);
            officeDoor.ForceOpen();
        }

        if (officeLight != null)
            officeLight.enabled = false;

        if (hallwayLight != null)
            hallwayLight.color = Color.red;

        if (enemyStalker != null)
            enemyStalker.BeginPowerOutRush();

        SetDanger("Power is out.");
    }

    private void UpdateInteractionPrompt()
    {
        focusedInteractable = null;
        Transform rayOrigin = GetInteractionRayOrigin();

        if (promptText == null || rayOrigin == null || monitorOpen)
        {
            if (promptText != null)
                promptText.text = "";
            return;
        }

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayers, QueryTriggerInteraction.Collide))
            focusedInteractable = hit.collider.GetComponentInParent<NightShiftInteractable>();

        promptText.text = focusedInteractable != null ? InteractLabel + ": " + focusedInteractable.Prompt : "";
    }

    private void UpdateHud()
    {
        float remaining = Mathf.Max(0f, nightLengthSeconds - elapsedNightTime);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);

        if (timerText != null)
            timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");

        if (powerText != null)
            powerText.text = powerOut ? "POWER: OUT" : "POWER: " + Mathf.CeilToInt(power) + "%";

        if (statusText != null)
        {
            string door = IsDoorClosed ? "DOOR CLOSED" : "DOOR OPEN";
            string light = officeLight != null && officeLight.enabled ? "LIGHT ON" : "LIGHT OFF";
            statusText.text = door + " / " + light + "\n" + MonitorLabel + ": Monitor  " + PauseLabel + ": Pause";
        }

        if (monitorFeedText != null && enemyStalker != null)
            monitorFeedText.text = enemyStalker.GetFeedText() + "\n\n" + MonitorLabel + ": Close monitor";

        if (staticOverlay != null)
        {
            float alpha = monitorOpen ? Random.Range(0.03f, 0.13f) : 0f;
            staticOverlay.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void SetMonitorOpen(bool open)
    {
        if (open && powerOut)
            open = false;

        monitorOpen = open;
        SetPanelActive(monitorPanel, monitorOpen);
        UpdateHud();
    }

    private void ToggleOfficeLight()
    {
        if (officeLight == null || powerOut)
            return;

        officeLight.enabled = !officeLight.enabled;
    }

    private void ResetNightObjects()
    {
        CancelInvoke();
        ClearDanger();
        SetMonitorOpen(false);

        if (officeDoor != null)
        {
            officeDoor.SetPowered(true);
            officeDoor.SetClosed(false);
        }

        if (officeLight != null)
            officeLight.enabled = true;

        if (hallwayLight != null)
        {
            hallwayLight.enabled = true;
            hallwayLight.color = new Color(0.7f, 0.04f, 0.03f);
        }

        if (enemyStalker != null)
            enemyStalker.ResetForNight();
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private Transform GetInteractionRayOrigin()
    {
        if (interactionRayOrigin != null)
            return interactionRayOrigin;

        if (vrRig != null)
            return vrRig.InteractionRayOrigin;

        return playerCamera != null ? playerCamera.transform : null;
    }

    private bool IsDesktopFallback => vrRig != null && vrRig.IsUsingDesktopInputFallback;

    private string InteractLabel => IsDesktopFallback ? "Left click / Enter" : "Right trigger";

    private string ConfirmLabel => IsDesktopFallback ? "Left click / Enter / Space" : "Right trigger";

    private string MonitorLabel => IsDesktopFallback ? "Tab / M" : "Left primary";

    private string PauseLabel => IsDesktopFallback ? "Esc" : "Menu";

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
