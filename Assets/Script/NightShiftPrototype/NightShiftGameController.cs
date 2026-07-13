using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NightShiftGameController : MonoBehaviour
{
    private static readonly string[] CameraNames =
    {
        "CAM 04 / STORAGE",
        "CAM 03 / BACK HALL",
        "CAM 02 / MAIN HALL",
        "CAM 01 / OFFICE DOOR"
    };

    private enum NightState
    {
        Title,
        Playing,
        Paused,
        Won,
        Lost
    }

    [Header("Rules")]
    [SerializeField, Min(10f), Tooltip("Length of one night in real-time seconds.")]
    private float nightLengthSeconds = 180f;
    [SerializeField] private float startingPower = 100f;
    [SerializeField] private float basePowerDrainPerSecond = 0.06f;
    [SerializeField] private float doorPowerDrainPerSecond = 0.22f;
    [SerializeField] private float monitorPowerDrainPerSecond = 0.14f;
    [SerializeField] private float officeLightPowerDrainPerSecond = 0.18f;
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
    private int selectedCameraIndex;
    private NightShiftInteractable focusedInteractable;
    private Light officeFillLight;
    private Renderer lightControlRenderer;

    public bool IsPlaying => state == NightState.Playing;
    public bool IsMonitorOpen => monitorOpen;
    public bool IsDoorClosed => officeDoor != null && officeDoor.IsClosed;
    public bool PlayerHasControl => state == NightState.Playing && !monitorOpen;
    public int SelectedCameraIndex => selectedCameraIndex;
    public int CurrentHourIndex => GetCurrentHourIndex();
    public float NightProgress => Mathf.Clamp01(elapsedNightTime / Mathf.Max(1f, nightLengthSeconds));

    public bool IsCameraWatching(int cameraIndex)
    {
        return state == NightState.Playing && monitorOpen && selectedCameraIndex == cameraIndex;
    }

    private void Awake()
    {
        NormalizeRuleTuning();
        power = startingPower;
        SetPanelActive(titlePanel, true);
        SetPanelActive(pausePanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(monitorPanel, false);
        SetPanelActive(jumpscarePanel, false);
        Time.timeScale = 1f;
    }

    private void NormalizeRuleTuning()
    {
        nightLengthSeconds = Mathf.Max(10f, nightLengthSeconds);
        startingPower = Mathf.Max(1f, startingPower);

        if (basePowerDrainPerSecond <= 0.03f)
            basePowerDrainPerSecond = 0.06f;
        if (doorPowerDrainPerSecond <= 0.15f)
            doorPowerDrainPerSecond = 0.22f;
        if (monitorPowerDrainPerSecond <= 0.07f)
            monitorPowerDrainPerSecond = 0.14f;
        if (officeLightPowerDrainPerSecond <= 0.09f)
            officeLightPowerDrainPerSecond = 0.18f;
    }

    private void Start()
    {
        ResetPlayerRigToOffice();
        ConfigureUiCanvases();
        PlaceReachableControls();
        ConfigureWorldVisibility();
        ResetNightObjects();
        ShowTitle();
    }

    private void ResetPlayerRigToOffice()
    {
        if (vrRig == null || officeDoor == null)
            return;

        Vector3 officeForward = officeDoor.transform.forward;
        Vector3 rigPosition = officeDoor.transform.position + officeForward * 4.83f;
        rigPosition.y = officeDoor.transform.position.y;
        Quaternion rigRotation = Quaternion.LookRotation(-officeForward, Vector3.up);
        vrRig.SetFixedPose(rigPosition, rigRotation);
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
        if (state != NightState.Playing)
            return;

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
            hallwayLight.intensity = 1.1f;

        SetDanger("Impact blocked. It retreated.");
        Invoke(nameof(RestoreHallwayLight), 0.45f);
        Invoke(nameof(ClearDanger), 2.25f);
    }

    private void RestoreHallwayLight()
    {
        if (hallwayLight != null)
            hallwayLight.intensity = 0.65f;
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
        selectedCameraIndex = 0;

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
            UpdateMenuCopy(menuCanvas.transform);
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
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        EnsureCanvasComponents(canvas);

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
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

        if (powerText != null)
            powerText.rectTransform.sizeDelta = new Vector2(420f, 110f);

        ConfigureVrHudLayout();
    }

    private void ConfigureVrHudLayout()
    {
        SetHudRect(timerText, new Vector2(100f, -70f), new Vector2(300f, 90f));
        SetHudRect(powerText, new Vector2(-100f, -70f), new Vector2(440f, 120f));
        SetHudRect(statusText, new Vector2(100f, 90f), new Vector2(700f, 120f));
        SetHudRect(promptText, new Vector2(0f, 150f), new Vector2(800f, 60f));
        SetHudRect(dangerText, new Vector2(0f, -145f), new Vector2(900f, 80f));
    }

    private static void SetHudRect(TMP_Text text, Vector2 anchoredPosition, Vector2 size)
    {
        if (text == null)
            return;

        text.rectTransform.anchoredPosition = anchoredPosition;
        text.rectTransform.sizeDelta = size;
    }

    private void MoveMonitorUi(Transform parent)
    {
        MoveToCanvas(monitorPanel, parent);

        if (monitorFeedText != null)
        {
            monitorFeedText.fontSize = 64f;
            monitorFeedText.alignment = TextAlignmentOptions.Center;
            monitorFeedText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform rect = monitorFeedText.rectTransform;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1120f, 560f);
        }
    }

    private void UpdateMenuCopy(Transform menuRoot)
    {
        TextMeshProUGUI[] labels = menuRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label.name == "Title Body")
            {
                label.text = "Survive from 12 AM to 6 AM.\n"
                    + ConfirmLabel + ": Start\n"
                    + MonitorLabel + ": Monitor  /  " + CameraSwitchLabel + ": Change camera";
                label.fontSize = 30f;
                label.rectTransform.sizeDelta = new Vector2(1250f, 220f);
            }
            else if (label.name == "Pause Body")
            {
                label.text = PauseLabel + " or " + ConfirmLabel + ": Resume";
            }
        }
    }

    private void PlaceReachableControls()
    {
        Transform player = vrRig != null ? vrRig.transform : playerCamera != null ? playerCamera.transform : null;
        if (player == null)
            return;

        Vector3 left = -player.right;
        Vector3 forward = player.forward;
        Quaternion controlRotation = Quaternion.LookRotation(left, Vector3.up);

        Vector3 doorPosition = player.position + left * 1.15f + forward * 0.3f;
        doorPosition.y = 1.45f;
        PlaceSceneObject("Door Control", doorPosition, controlRotation);

        Vector3 lightPosition = player.position + left * 1.15f + forward * 0.87f;
        lightPosition.y = 1.45f;
        PlaceSceneObject("Light Control", lightPosition, controlRotation);

        Vector3 indicatorPosition = player.position + left * 1.05f + forward * 0.3f;
        indicatorPosition.y = 1.85f;
        PlaceSceneObject("Door Indicator Light", indicatorPosition, null);
    }

    private static void PlaceSceneObject(string objectName, Vector3 position, Quaternion? rotation)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        if (sceneObject == null)
            return;

        sceneObject.transform.position = position;
        if (rotation.HasValue)
            sceneObject.transform.rotation = rotation.Value;
    }

    private void ConfigureWorldVisibility()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.048f, 0.055f);
        if (RenderSettings.fog)
            RenderSettings.fogDensity = Mathf.Min(RenderSettings.fogDensity, 0.02f);

        if (officeLight != null)
        {
            officeLight.range = 7f;
            officeLight.intensity = 1.7f;
            officeLight.color = new Color(1f, 0.82f, 0.58f);
            officeLight.shadows = LightShadows.None;
        }

        Transform environment = officeDoor != null && officeDoor.transform.parent != null
            ? officeDoor.transform.parent
            : transform;

        Vector3 fillPosition = officeLight != null
            ? new Vector3(officeLight.transform.position.x, 2.4f, officeLight.transform.position.z + 0.25f)
            : officeDoor.transform.position + officeDoor.transform.forward * 4.1f + Vector3.up * 2.4f;
        officeFillLight = FindOrCreatePointLight(
            "Office Fill Light",
            environment,
            fillPosition,
            new Color(0.84f, 0.86f, 0.9f),
            0.85f,
            8f);

        RemoveControlDecoration("Control Panel Backplate");
        RemoveControlDecoration("Door Control Label");
        RemoveControlDecoration("Light Control Label");
        RemoveControlDecoration("Control Panel Light");

        GameObject doorControl = GameObject.Find("Door Control");
        GameObject lightControl = GameObject.Find("Light Control");
        if (doorControl == null || lightControl == null)
            return;

        doorControl.transform.localScale = new Vector3(0.42f, 0.62f, 0.2f);
        lightControl.transform.localScale = new Vector3(0.42f, 0.62f, 0.2f);

        Light doorIndicator = GameObject.Find("Door Indicator Light")?.GetComponent<Light>();
        if (doorIndicator != null)
        {
            doorIndicator.range = 1.5f;
            doorIndicator.intensity = 0.45f;
        }

        if (hallwayLight != null)
        {
            hallwayLight.range = 7f;
            hallwayLight.intensity = 0.65f;
            hallwayLight.shadows = LightShadows.None;
        }

        lightControlRenderer = lightControl.GetComponent<Renderer>();
        UpdateLightControlVisual();
    }

    private static Light FindOrCreatePointLight(string objectName, Transform parent, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightObject = GameObject.Find(objectName);
        if (lightObject == null)
        {
            lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(parent);
        }

        lightObject.transform.position = position;
        Light light = lightObject.GetComponent<Light>();
        if (light == null)
            light = lightObject.AddComponent<Light>();

        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.enabled = true;
        return light;
    }

    private static void RemoveControlDecoration(string objectName)
    {
        GameObject decoration = GameObject.Find(objectName);
        if (decoration != null)
            Object.Destroy(decoration);
    }

    private void UpdateLightControlVisual()
    {
        if (lightControlRenderer == null)
            return;

        if (powerOut)
        {
            SetRendererColorAndEmission(lightControlRenderer, new Color(0.08f, 0.08f, 0.08f), Color.black);
            return;
        }

        bool lightOn = officeLight != null && officeLight.enabled;
        Color baseColor = lightOn ? new Color(1f, 0.72f, 0.12f) : new Color(0.45f, 0.18f, 0.025f);
        Color emission = lightOn ? new Color(1.1f, 0.55f, 0.06f) : new Color(0.28f, 0.08f, 0.01f);
        SetRendererColorAndEmission(lightControlRenderer, baseColor, emission);
    }

    private static void SetRendererColorAndEmission(Renderer targetRenderer, Color baseColor, Color emission)
    {
        if (targetRenderer == null)
            return;

        Material material = targetRenderer.material;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
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

        if (monitorOpen && vrRig.CameraSwitchDirectionThisFrame != 0)
            CycleCamera(vrRig.CameraSwitchDirectionThisFrame);

        if (vrRig.InteractPressedThisFrame)
            TryInteract();
    }

    private void CycleCamera(int direction)
    {
        int cameraCount = enemyStalker != null ? enemyStalker.CameraCount : CameraNames.Length;
        if (cameraCount <= 0)
            return;

        selectedCameraIndex = (selectedCameraIndex + direction) % cameraCount;
        if (selectedCameraIndex < 0)
            selectedCameraIndex += cameraCount;

        UpdateHud();
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
        RenderSettings.ambientLight = new Color(0.012f, 0.012f, 0.016f);
        SetMonitorOpen(false);

        if (officeDoor != null)
        {
            officeDoor.SetPowered(false);
            officeDoor.ForceOpen();
        }

        if (officeLight != null)
            officeLight.enabled = false;

        if (officeFillLight != null)
            officeFillLight.enabled = false;

        UpdateLightControlVisual();

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
        if (timerText != null)
            timerText.text = GetClockLabel();

        if (powerText != null)
        {
            string powerLabel = powerOut ? "POWER: OUT" : "POWER: " + Mathf.CeilToInt(power) + "%";
            powerText.text = powerLabel + "\nUSAGE " + GetUsageMeter();
        }

        if (statusText != null)
        {
            string door = IsDoorClosed ? "DOOR CLOSED" : "DOOR OPEN";
            string light = officeLight != null && officeLight.enabled ? "LIGHT ON" : "LIGHT OFF";
            statusText.text = door + " / " + light + "\n" + MonitorLabel + ": Monitor  " + PauseLabel + ": Pause";
        }

        if (monitorFeedText != null)
        {
            string feed = enemyStalker != null
                ? enemyStalker.GetCameraFeedText(selectedCameraIndex)
                : GetCameraName(selectedCameraIndex) + "\nSIGNAL LOST";
            monitorFeedText.text = feed + "\n\n" + CameraSwitchLabel + ": Change camera\n" + MonitorLabel + ": Lower monitor";
        }

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

        if (open && officeLight != null)
        {
            officeLight.enabled = false;
            UpdateLightControlVisual();
        }

        monitorOpen = open;
        SetPanelActive(monitorPanel, monitorOpen);
        UpdateHud();
    }

    private void ToggleOfficeLight()
    {
        if (officeLight == null || powerOut)
            return;

        officeLight.enabled = !officeLight.enabled;
        UpdateLightControlVisual();
    }

    private void ResetNightObjects()
    {
        CancelInvoke();
        RenderSettings.ambientLight = new Color(0.05f, 0.048f, 0.055f);
        ClearDanger();
        selectedCameraIndex = 0;
        SetMonitorOpen(false);

        if (officeDoor != null)
        {
            officeDoor.SetPowered(true);
            officeDoor.SetClosed(false);
        }

        if (officeLight != null)
            officeLight.enabled = false;

        if (officeFillLight != null)
            officeFillLight.enabled = true;

        UpdateLightControlVisual();

        if (hallwayLight != null)
        {
            hallwayLight.enabled = true;
            hallwayLight.color = new Color(0.7f, 0.04f, 0.03f);
            hallwayLight.intensity = 0.65f;
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

    private string CameraSwitchLabel => IsDesktopFallback ? "Q/E or arrows" : "Right stick";

    private string PauseLabel => IsDesktopFallback ? "Esc" : "Menu";

    private int GetCurrentHourIndex()
    {
        if (state == NightState.Won)
            return 6;

        float hourLength = Mathf.Max(1f, nightLengthSeconds) / 6f;
        return Mathf.Clamp(Mathf.FloorToInt(elapsedNightTime / hourLength), 0, 5);
    }

    private string GetClockLabel()
    {
        int hour = GetCurrentHourIndex();
        return hour == 0 ? "12 AM" : hour + " AM";
    }

    private int GetUsageLevel()
    {
        if (powerOut)
            return 0;

        int usage = 1;
        if (IsDoorClosed)
            usage++;
        if (monitorOpen)
            usage++;
        if (officeLight != null && officeLight.enabled)
            usage++;
        return usage;
    }

    private string GetUsageMeter()
    {
        int usage = GetUsageLevel();
        return "[" + new string('|', usage) + new string('.', 4 - usage) + "]";
    }

    private static string GetCameraName(int cameraIndex)
    {
        if (cameraIndex < 0 || cameraIndex >= CameraNames.Length)
            return "CAM -- / OFFLINE";

        return CameraNames[cameraIndex];
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
