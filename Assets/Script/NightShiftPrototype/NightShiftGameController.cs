using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class NightShiftGameController : MonoBehaviour
{
    private static readonly string[] CameraNames =
    {
        "CAM 01 / 倉庫",
        "CAM 02 / 裏通路",
        "CAM 03 / 中央廊下",
        "CAM 04 / 分岐通路"
    };

    private enum NightState
    {
        Initializing,
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
    [SerializeField, FormerlySerializedAs("officeDoor")] private NightShiftOfficeDoor leftDoor;
    [SerializeField] private NightShiftOfficeDoor rightDoor;
    [SerializeField] private NightShiftEnemyStalker enemyStalker;
    [SerializeField, FormerlySerializedAs("officeLight")] private Light leftHallLight;
    [SerializeField, FormerlySerializedAs("hallwayLight")] private Light rightHallLight;
    [SerializeField] private NightShiftSecurityCameraSystem securityCameraSystem;
    [SerializeField] private NightShiftAudioController audioController;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI dangerText;
    [SerializeField] private TextMeshProUGUI monitorFeedText;
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
    [SerializeField] private Vector3 monitorUiLocalScale = Vector3.one * 0.00112f;
    [SerializeField] private Vector2 monitorUiSize = new Vector2(1200f, 680f);

    [Header("Camera-space UI")]
    [SerializeField, Min(0.1f)] private float menuUiPlaneDistance = 1.5f;
    [SerializeField, FormerlySerializedAs("menuUiSize")] private Vector2 menuUiReferenceResolution = new Vector2(1600f, 900f);
    [SerializeField, Range(0f, 1f)] private float menuUiScreenMatch = 0.5f;

    [Header("HUD Positions (1600 x 900 Reference)")]
    [SerializeField, Tooltip("Offset from the top-left anchor. Positive Y moves upward.")]
    private Vector2 timerHudPosition = new Vector2(120f, -85f);
    [SerializeField, Tooltip("Offset from the top-right anchor. Negative X moves left.")]
    private Vector2 powerHudPosition = new Vector2(-120f, -85f);
    [SerializeField, Tooltip("Offset from the bottom-left anchor.")]
    private Vector2 statusHudPosition = new Vector2(120f, 100f);
    [SerializeField, Tooltip("Offset from the bottom-center anchor.")]
    private Vector2 promptHudPosition = new Vector2(0f, 165f);
    [SerializeField, Tooltip("Offset from the top-center anchor.")]
    private Vector2 dangerHudPosition = new Vector2(0f, -145f);

    private NightState state = NightState.Initializing;
    private float elapsedNightTime;
    private float power;
    private bool monitorOpen;
    private bool powerOut;
    private int selectedCameraIndex;
    private NightShiftInteractable focusedInteractable;
    private Light officeFillLight;
    private Renderer leftLightControlRenderer;
    private Renderer rightLightControlRenderer;
    private readonly List<Material> runtimeOverlayMaterials = new List<Material>();

    public bool IsPlaying => state == NightState.Playing;
    public bool IsMonitorOpen => monitorOpen;
    public bool PlayerHasControl => state == NightState.Playing && !monitorOpen;
    public int SelectedCameraIndex => selectedCameraIndex;
    public int CurrentHourIndex => GetCurrentHourIndex();
    public float NightProgress => Mathf.Clamp01(elapsedNightTime / Mathf.Max(1f, nightLengthSeconds));

    public bool IsDoorClosed(NightShiftAttackSide side)
    {
        NightShiftOfficeDoor door = side == NightShiftAttackSide.Left ? leftDoor : rightDoor;
        return door != null && door.IsClosed;
    }

    public bool IsCameraWatching(int cameraIndex)
    {
        return state == NightState.Playing && monitorOpen && selectedCameraIndex == cameraIndex;
    }

    private void Awake()
    {
        NormalizeRuleTuning();
        if (audioController == null)
            audioController = GetComponent<NightShiftAudioController>();
        if (securityCameraSystem == null)
            securityCameraSystem = GetComponent<NightShiftSecurityCameraSystem>();

        power = startingPower;
        SetPanelActive(titlePanel, false);
        SetPanelActive(pausePanel, false);
        SetPanelActive(resultPanel, false);
        SetPanelActive(monitorPanel, false);
        SetPanelActive(jumpscarePanel, false);
        NightShiftJapaneseFont.ApplyToChildren(transform);
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
        BeginNight();
    }

    private void ResetPlayerRigToOffice()
    {
        if (vrRig == null)
            return;

        vrRig.RecenterTrackingOrigin();
    }

    private void Update()
    {
        if (vrRig != null && vrRig.PausePressedThisFrame)
            HandlePauseInput();

        if (state == NightState.Won || state == NightState.Lost)
            return;

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

    private void OnValidate()
    {
        menuUiPlaneDistance = Mathf.Max(0.1f, menuUiPlaneDistance);
        menuUiScreenMatch = Mathf.Clamp01(menuUiScreenMatch);
        ConfigureVrHudLayout();
    }

    public void UseInteraction(NightShiftInteractionAction action)
    {
        if (state != NightState.Playing)
            return;

        switch (action)
        {
            case NightShiftInteractionAction.ToggleLeftDoor:
                ToggleDoor(leftDoor);
                break;
            case NightShiftInteractionAction.ToggleMonitor:
                SetMonitorOpen(!monitorOpen);
                break;
            case NightShiftInteractionAction.ToggleLeftLight:
                ToggleHallLight(leftHallLight);
                break;
            case NightShiftInteractionAction.ToggleRightDoor:
                ToggleDoor(rightDoor);
                break;
            case NightShiftInteractionAction.ToggleRightLight:
                ToggleHallLight(rightHallLight);
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

    public void NotifyEnemyMoved(int stageIndex)
    {
        if (audioController != null)
            audioController.PlayEnemyMove();

        if (monitorOpen && selectedCameraIndex == stageIndex)
            SetDanger("監視中のカメラで動きを検知");
    }

    public void BlockEnemyAtDoor(NightShiftAttackSide side)
    {
        Light hallLight = side == NightShiftAttackSide.Left ? leftHallLight : rightHallLight;
        if (hallLight != null)
            hallLight.intensity = 2.2f;

        if (audioController != null)
            audioController.PlayDoorImpact();

        SetDanger((side == NightShiftAttackSide.Left ? "左" : "右") + "ドアで侵入を防ぎました");
        Invoke(nameof(RestoreHallLights), 0.45f);
        Invoke(nameof(ClearDanger), 2.25f);
    }

    private void RestoreHallLights()
    {
        ConfigureHallLight(leftHallLight);
        ConfigureHallLight(rightHallLight);
    }

    public void LoseNight(string reason)
    {
        if (state == NightState.Lost || state == NightState.Won)
            return;

        state = NightState.Lost;
        SetMonitorOpen(false);
        SetPanelActive(jumpscarePanel, true);
        if (audioController != null)
            audioController.PlayJumpscare();
        NightShiftSceneFlow.SetResult(false, "ゲームオーバー", reason);
        StartCoroutine(LoadResultSceneAfterDelay());
    }

    private void BeginNight()
    {
        NightShiftSceneFlow.ClearResult();
        state = NightState.Playing;
        elapsedNightTime = 0f;
        power = startingPower;
        powerOut = false;
        selectedCameraIndex = 0;

        ResetNightObjects();
        if (audioController != null)
            audioController.StartNightAmbience();
        SetPanelActive(titlePanel, false);
        SetPanelActive(resultPanel, false);
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
            ApplyMenuOverlayMaterials(menuCanvas.transform);
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
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = playerCamera;
        float minimumDistance = playerCamera != null ? playerCamera.nearClipPlane + 0.05f : 0.1f;
        float maximumDistance = playerCamera != null
            ? Mathf.Max(minimumDistance, playerCamera.farClipPlane - 0.05f)
            : 100f;
        canvas.planeDistance = Mathf.Clamp(menuUiPlaneDistance, minimumDistance, maximumDistance);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        EnsureCanvasComponents(canvas);

        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Vector2 referenceResolution = menuUiReferenceResolution.x > 0f && menuUiReferenceResolution.y > 0f
            ? menuUiReferenceResolution
            : new Vector2(1600f, 900f);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = Mathf.Clamp01(menuUiScreenMatch);
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
        SetHudRect(timerText, new Vector2(0f, 1f), new Vector2(0f, 1f), timerHudPosition, new Vector2(280f, 110f));
        SetHudRect(powerText, new Vector2(1f, 1f), new Vector2(1f, 1f), powerHudPosition, new Vector2(400f, 150f));
        SetHudRect(statusText, new Vector2(0f, 0f), new Vector2(0f, 0f), statusHudPosition, new Vector2(680f, 150f));
        SetHudRect(promptText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), promptHudPosition, new Vector2(900f, 80f));
        SetHudRect(dangerText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), dangerHudPosition, new Vector2(1000f, 100f));

        SetHudFontSize(timerText, 58f);
        SetHudFontSize(powerText, 44f);
        SetHudFontSize(statusText, 32f);
        SetHudFontSize(promptText, 34f);
        SetHudFontSize(dangerText, 44f);
    }

    private static void SetHudRect(TMP_Text text, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        if (text == null)
            return;

        text.rectTransform.anchorMin = anchor;
        text.rectTransform.anchorMax = anchor;
        text.rectTransform.pivot = pivot;
        text.rectTransform.anchoredPosition = anchoredPosition;
        text.rectTransform.sizeDelta = size;
    }

    private static void SetHudFontSize(TMP_Text text, float fontSize)
    {
        if (text != null)
            text.fontSize = fontSize;
    }

    private void MoveMonitorUi(Transform parent)
    {
        MoveToCanvas(monitorPanel, parent);

        if (monitorFeedText != null)
        {
            monitorFeedText.fontSize = 38f;
            monitorFeedText.alignment = TextAlignmentOptions.TopLeft;
            monitorFeedText.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    private void UpdateMenuCopy(Transform menuRoot)
    {
        TextMeshProUGUI[] labels = menuRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label.name == "Title Body")
            {
                label.text = "12 AMから6 AMまで生き残ってください。\n"
                    + ConfirmLabel + ": 開始\n"
                    + MonitorLabel + ": モニター  /  " + CameraSwitchLabel + ": カメラ切替";
                label.fontSize = 30f;
                label.rectTransform.sizeDelta = new Vector2(1250f, 220f);
            }
            else if (label.name == "Pause Body")
            {
                label.text = PauseLabel + " または " + ConfirmLabel + ": 再開";
            }
        }
    }

    private void PlaceReachableControls()
    {
        Transform player = vrRig != null ? vrRig.transform : playerCamera != null ? playerCamera.transform : null;
        if (player == null)
            return;

        Vector3 left = -player.right;
        Vector3 right = player.right;
        Vector3 forward = player.forward;

        PlaceReachableControl("Left Door Control", player, left * 1.02f + forward * 0.28f, 1.45f);
        PlaceReachableControl("Left Light Control", player, left * 1.02f + forward * 0.28f, 1.02f);
        PlaceReachableControl("Right Door Control", player, right * 1.02f + forward * 0.28f, 1.45f);
        PlaceReachableControl("Right Light Control", player, right * 1.02f + forward * 0.28f, 1.02f);

        PlaceReachableControl("Door Control", player, left * 1.02f + forward * 0.28f, 1.45f);
        PlaceReachableControl("Light Control", player, left * 1.02f + forward * 0.28f, 1.02f);
    }

    private static void PlaceReachableControl(string objectName, Transform player, Vector3 offset, float height)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        if (sceneObject == null)
            return;

        Vector3 position = player.position + offset;
        position.y = height;
        Vector3 lookDirection = player.position - position;
        lookDirection.y = 0f;
        Quaternion rotation = lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : sceneObject.transform.rotation;
        PlaceSceneObject(objectName, position, rotation);
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

        Transform environment = leftDoor != null && leftDoor.transform.parent != null
            ? leftDoor.transform.parent
            : transform;

        Vector3 fillPosition = vrRig != null
            ? vrRig.transform.position + Vector3.up * 2.4f + vrRig.transform.forward * 0.5f
            : new Vector3(0f, 2.4f, 1f);
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

        GameObject leftLightControl = GameObject.Find("Left Light Control") ?? GameObject.Find("Light Control");
        GameObject rightLightControl = GameObject.Find("Right Light Control");
        leftLightControlRenderer = leftLightControl != null ? leftLightControl.GetComponent<Renderer>() : null;
        rightLightControlRenderer = rightLightControl != null ? rightLightControl.GetComponent<Renderer>() : null;

        ConfigureHallLight(leftHallLight);
        ConfigureHallLight(rightHallLight);
        UpdateLightControlVisuals();
    }

    private static void ConfigureHallLight(Light hallLight)
    {
        if (hallLight == null)
            return;

        hallLight.type = LightType.Spot;
        hallLight.range = 8f;
        hallLight.intensity = hallLight.enabled ? 2.1f : 0f;
        hallLight.spotAngle = 72f;
        hallLight.color = new Color(1f, 0.82f, 0.58f);
        hallLight.shadows = LightShadows.None;
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

    private void UpdateLightControlVisuals()
    {
        UpdateLightControlVisual(leftLightControlRenderer, leftHallLight);
        UpdateLightControlVisual(rightLightControlRenderer, rightHallLight);
    }

    private void UpdateLightControlVisual(Renderer controlRenderer, Light hallLight)
    {
        if (controlRenderer == null)
            return;

        bool lightOn = !powerOut && hallLight != null && hallLight.enabled;
        Color baseColor = lightOn ? new Color(1f, 0.72f, 0.12f) : new Color(0.45f, 0.18f, 0.025f);
        Color emission = lightOn ? new Color(1.1f, 0.55f, 0.06f) : new Color(0.28f, 0.08f, 0.01f);
        if (powerOut)
        {
            baseColor = new Color(0.08f, 0.08f, 0.08f);
            emission = Color.black;
        }

        SetRendererColorAndEmission(controlRenderer, baseColor, emission);
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

    private void ApplyMenuOverlayMaterials(Transform menuRoot)
    {
        Material uiTemplate = Resources.Load<Material>("NSP_UIAlwaysOnTop");
        if (uiTemplate != null)
        {
            Material uiOverlay = CreateRuntimeMaterial(uiTemplate, "NSP UI Always On Top (Runtime)");
            Graphic[] graphics = menuRoot.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in graphics)
            {
                if (!(graphic is TextMeshProUGUI))
                    graphic.material = uiOverlay;
            }
        }

        Material textTemplate = Resources.Load<Material>("NSP_TMPAlwaysOnTop");
        if (textTemplate == null || textTemplate.shader == null)
            return;

        TextMeshProUGUI[] labels = menuRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            Material source = label.fontSharedMaterial;
            if (source == null)
                continue;

            Material textOverlay = new Material(source)
            {
                name = label.name + " Overlay (Runtime)",
                shader = textTemplate.shader,
                renderQueue = 4000,
                hideFlags = HideFlags.DontSave
            };
            runtimeOverlayMaterials.Add(textOverlay);
            label.fontSharedMaterial = textOverlay;
            label.UpdateMeshPadding();
        }
    }

    private Material CreateRuntimeMaterial(Material template, string materialName)
    {
        Material material = new Material(template)
        {
            name = materialName,
            renderQueue = 4000,
            hideFlags = HideFlags.DontSave
        };
        runtimeOverlayMaterials.Add(material);
        return material;
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeOverlayMaterials)
        {
            if (material != null)
                Destroy(material);
        }
        runtimeOverlayMaterials.Clear();
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
        UpdateHud();
        NightShiftSceneFlow.SetResult(true, "6:00 AM", "夜勤を生き延びました。");
        StartCoroutine(LoadResultSceneAfterDelay());
    }

    private static IEnumerator LoadResultSceneAfterDelay()
    {
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.75f);
        SceneManager.LoadScene(NightShiftSceneFlow.ResultSceneName);
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
        int cameraCount = securityCameraSystem != null && securityCameraSystem.CameraCount > 0
            ? securityCameraSystem.CameraCount
            : enemyStalker != null ? enemyStalker.CameraCount : CameraNames.Length;
        if (cameraCount <= 0)
            return;

        selectedCameraIndex = (selectedCameraIndex + direction) % cameraCount;
        if (selectedCameraIndex < 0)
            selectedCameraIndex += cameraCount;

        if (securityCameraSystem != null)
            securityCameraSystem.SetFeed(monitorOpen, selectedCameraIndex);
        if (audioController != null)
            audioController.PlayCameraSwitch();
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
        if (leftDoor != null && leftDoor.IsClosed)
            drain += doorPowerDrainPerSecond;
        if (rightDoor != null && rightDoor.IsClosed)
            drain += doorPowerDrainPerSecond;
        if (monitorOpen)
            drain += monitorPowerDrainPerSecond;
        if (leftHallLight != null && leftHallLight.enabled)
            drain += officeLightPowerDrainPerSecond;
        if (rightHallLight != null && rightHallLight.enabled)
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

        PowerDownDoor(leftDoor);
        PowerDownDoor(rightDoor);

        SetHallLightEnabled(leftHallLight, false);
        SetHallLightEnabled(rightHallLight, false);

        if (officeFillLight != null)
            officeFillLight.enabled = false;

        UpdateLightControlVisuals();

        if (enemyStalker != null)
            enemyStalker.BeginPowerOutRush();

        if (audioController != null)
            audioController.PlayPowerOut();
        SetDanger("停電: ドアと照明が停止しました");
    }

    private static void PowerDownDoor(NightShiftOfficeDoor door)
    {
        if (door == null)
            return;

        door.SetPowered(false);
        door.ForceOpen();
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
            string powerLabel = powerOut ? "電力: 停止" : "電力: " + Mathf.CeilToInt(power) + "%";
            powerText.text = powerLabel + "\n使用量 " + GetUsageMeter();
        }

        if (statusText != null)
        {
            string leftDoorStatus = IsDoorClosed(NightShiftAttackSide.Left) ? "左ドア 閉" : "左ドア 開";
            string rightDoorStatus = IsDoorClosed(NightShiftAttackSide.Right) ? "右ドア 閉" : "右ドア 開";
            string leftLightStatus = IsLightEnabled(leftHallLight) ? "左ライト 点灯" : "左ライト 消灯";
            string rightLightStatus = IsLightEnabled(rightHallLight) ? "右ライト 点灯" : "右ライト 消灯";
            statusText.text = leftDoorStatus + " / " + rightDoorStatus + "\n"
                + leftLightStatus + " / " + rightLightStatus;
        }

        if (monitorFeedText != null)
        {
            string feed = enemyStalker != null
                ? enemyStalker.GetCameraFeedText(selectedCameraIndex)
                : GetCameraName(selectedCameraIndex) + "\n映像信号なし";
            monitorFeedText.text = feed;
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

        if (open)
        {
            SetHallLightEnabled(leftHallLight, false);
            SetHallLightEnabled(rightHallLight, false);
            UpdateLightControlVisuals();
        }

        bool changed = monitorOpen != open;
        monitorOpen = open;
        SetPanelActive(monitorPanel, monitorOpen);
        if (securityCameraSystem != null)
            securityCameraSystem.SetFeed(monitorOpen, selectedCameraIndex);
        if (changed && audioController != null)
            audioController.PlayMonitor();
        UpdateHud();
    }

    private void ToggleDoor(NightShiftOfficeDoor door)
    {
        if (door == null || powerOut)
            return;

        bool closed = !door.IsClosed;
        door.SetClosed(closed);
        if (audioController != null)
            audioController.PlayDoor(closed);
    }

    private void ToggleHallLight(Light hallLight)
    {
        if (hallLight == null || powerOut)
            return;

        SetHallLightEnabled(hallLight, !hallLight.enabled);
        UpdateLightControlVisuals();
        if (audioController != null)
            audioController.PlaySwitch();
    }

    private static void SetHallLightEnabled(Light hallLight, bool enabled)
    {
        if (hallLight == null)
            return;

        hallLight.enabled = enabled;
        hallLight.intensity = enabled ? 2.1f : 0f;
    }

    private static bool IsLightEnabled(Light hallLight)
    {
        return hallLight != null && hallLight.enabled;
    }

    private void ResetNightObjects()
    {
        CancelInvoke();
        RenderSettings.ambientLight = new Color(0.05f, 0.048f, 0.055f);
        ClearDanger();
        selectedCameraIndex = 0;
        SetMonitorOpen(false);

        ResetDoor(leftDoor);
        ResetDoor(rightDoor);

        SetHallLightEnabled(leftHallLight, false);
        SetHallLightEnabled(rightHallLight, false);

        if (officeFillLight != null)
            officeFillLight.enabled = true;

        UpdateLightControlVisuals();

        if (enemyStalker != null)
            enemyStalker.ResetForNight();
    }

    private static void ResetDoor(NightShiftOfficeDoor door)
    {
        if (door == null)
            return;

        door.SetPowered(true);
        door.SetClosed(false);
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

    private string InteractLabel => IsDesktopFallback ? "左クリック / Enter" : "右トリガー";

    private string ConfirmLabel => IsDesktopFallback ? "左クリック / Enter / Space" : "右トリガー";

    private string MonitorLabel => IsDesktopFallback ? "Tab / M" : "左コントローラー A/X";

    private string CameraSwitchLabel => IsDesktopFallback ? "Q/E または矢印" : "右スティック左右";

    private string PauseLabel => IsDesktopFallback ? "Esc" : "メニューボタン";

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
        if (IsDoorClosed(NightShiftAttackSide.Left))
            usage++;
        if (IsDoorClosed(NightShiftAttackSide.Right))
            usage++;
        if (monitorOpen)
            usage++;
        if (IsLightEnabled(leftHallLight))
            usage++;
        if (IsLightEnabled(rightHallLight))
            usage++;
        return usage;
    }

    private string GetUsageMeter()
    {
        int usage = GetUsageLevel();
        return "[" + new string('|', usage) + new string('.', Mathf.Max(0, 6 - usage)) + "]";
    }

    private static string GetCameraName(int cameraIndex)
    {
        if (cameraIndex < 0 || cameraIndex >= CameraNames.Length)
            return "CAM -- / オフライン";

        return CameraNames[cameraIndex];
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
