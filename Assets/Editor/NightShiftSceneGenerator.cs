using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NightShiftSceneGenerator
{
    private const string TitleScenePath = "Assets/Script/NightShiftPrototype/Scenes/NightShiftTitle.unity";
    private const string ScenePath = "Assets/Scenes/NightShiftPrototype.unity";
    private const string ResultScenePath = "Assets/Script/NightShiftPrototype/Scenes/NightShiftResult.unity";
    private const string MaterialFolder = "Assets/Script/NightShiftPrototype/GeneratedMaterials";
    private const string JapaneseFontPath = "Assets/Script/NightShiftPrototype/NotoSansJP-VF.ttf";
    private const string JapaneseFontAssetPath = "Assets/Script/NightShiftPrototype/Resources/NSP_Japanese SDF.asset";

    private static TMP_FontAsset japaneseFontAsset;

    [MenuItem("Tools/Night Shift Prototype/Generate Scene")]
    public static void GenerateNightShiftPrototype()
    {
        EnsureFolder("Assets/Script/NightShiftPrototype/GeneratedMaterials");
        EnsureFolder("Assets/Script/NightShiftPrototype/Resources");
        japaneseFontAsset = EnsureJapaneseFontAsset();

        Material wallMaterial = CreateMaterial("NSP_Wall", new Color(0.18f, 0.19f, 0.21f));
        Material floorMaterial = CreateMaterial("NSP_Floor", new Color(0.1f, 0.105f, 0.115f));
        Material metalMaterial = CreateMaterial("NSP_DarkMetal", new Color(0.26f, 0.28f, 0.32f), 0.45f, 0.3f);
        Material deskMaterial = CreateMaterial("NSP_Desk", new Color(0.2f, 0.16f, 0.11f));
        Material monitorMaterial = CreateMaterial("NSP_MonitorGlow", new Color(0.02f, 0.18f, 0.12f), 0f, 0.3f, new Color(0.01f, 0.18f, 0.09f));
        Material redMaterial = CreateMaterial("NSP_RedIndicator", new Color(0.65f, 0.04f, 0.025f), 0f, 0.25f, new Color(0.36f, 0.02f, 0.01f));
        Material eyeMaterial = CreateMaterial("NSP_Eyes", new Color(1f, 0.03f, 0.02f), 0f, 0.6f, new Color(1.4f, 0f, 0f));
        Material enemyMaterial = CreateMaterial("NSP_Enemy", new Color(0.025f, 0.025f, 0.028f));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.048f, 0.055f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogDensity = 0.02f;

        GameObject root = new GameObject("Night Shift Prototype");
        NightShiftGameController gameController = root.AddComponent<NightShiftGameController>();
        NightShiftAudioController audioController = root.AddComponent<NightShiftAudioController>();
        NightShiftSecurityCameraSystem securityCameraSystem = root.AddComponent<NightShiftSecurityCameraSystem>();

        Transform environment = new GameObject("Environment").transform;
        environment.SetParent(root.transform);
        BuildEnvironment(environment, wallMaterial, floorMaterial, metalMaterial, deskMaterial, monitorMaterial, redMaterial, gameController, out NightShiftOfficeDoor leftDoor, out NightShiftOfficeDoor rightDoor, out Light leftHallLight, out Light rightHallLight, out Transform monitorUiAnchor);

        BuildPlayer(root.transform, gameController, out Camera playerCamera, out NightShiftVRRigController vrRig, out Transform interactionRayOrigin);
        BuildEnemy(root.transform, gameController, enemyMaterial, eyeMaterial, out NightShiftEnemyStalker enemyStalker);
        BuildUi(monitorUiAnchor, playerCamera, out TextMeshProUGUI timerText, out TextMeshProUGUI powerText, out TextMeshProUGUI statusText, out TextMeshProUGUI promptText, out TextMeshProUGUI dangerText, out TextMeshProUGUI monitorFeedText, out RawImage monitorImage, out GameObject pausePanel, out GameObject monitorPanel, out GameObject jumpscarePanel, out Image staticOverlay);
        BuildSecurityCameras(root.transform, monitorImage, securityCameraSystem);

        SerializedObject serializedController = new SerializedObject(gameController);
        SetObject(serializedController, "playerCamera", playerCamera);
        SetObject(serializedController, "vrRig", vrRig);
        SetObject(serializedController, "interactionRayOrigin", interactionRayOrigin);
        SetObject(serializedController, "leftDoor", leftDoor);
        SetObject(serializedController, "rightDoor", rightDoor);
        SetObject(serializedController, "enemyStalker", enemyStalker);
        SetObject(serializedController, "leftHallLight", leftHallLight);
        SetObject(serializedController, "rightHallLight", rightHallLight);
        SetObject(serializedController, "securityCameraSystem", securityCameraSystem);
        SetObject(serializedController, "audioController", audioController);
        SetObject(serializedController, "timerText", timerText);
        SetObject(serializedController, "powerText", powerText);
        SetObject(serializedController, "statusText", statusText);
        SetObject(serializedController, "promptText", promptText);
        SetObject(serializedController, "dangerText", dangerText);
        SetObject(serializedController, "monitorFeedText", monitorFeedText);
        SetObject(serializedController, "pausePanel", pausePanel);
        SetObject(serializedController, "monitorPanel", monitorPanel);
        SetObject(serializedController, "jumpscarePanel", jumpscarePanel);
        SetObject(serializedController, "staticOverlay", staticOverlay);
        SetObject(serializedController, "monitorUiAnchor", monitorUiAnchor);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateBuildSettings();

        Debug.Log("Generated " + ScenePath);
    }

    [MenuItem("Tools/Night Shift Prototype/Validate Generated Scene")]
    public static void ValidateGeneratedScene()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        NightShiftGameController controller = Object.FindFirstObjectByType<NightShiftGameController>();
        if (controller == null)
            throw new System.InvalidOperationException("NightShiftGameController is missing.");

        SerializedObject serializedController = new SerializedObject(controller);
        string[] requiredControllerReferences =
        {
            "playerCamera",
            "vrRig",
            "interactionRayOrigin",
            "leftDoor",
            "rightDoor",
            "enemyStalker",
            "leftHallLight",
            "rightHallLight",
            "securityCameraSystem",
            "audioController",
            "monitorPanel",
            "monitorFeedText"
        };
        foreach (string propertyName in requiredControllerReferences)
            ValidateObjectReference(serializedController, propertyName);

        string[] requiredObjects =
        {
            "Left Office Door",
            "Right Office Door",
            "Left Door Control",
            "Right Door Control",
            "Left Light Control",
            "Right Light Control",
            "CAM 01 Storage",
            "CAM 02 Back Hall",
            "CAM 03 Main Hall",
            "CAM 04 Junction",
            "Live Camera Image"
        };
        foreach (string objectName in requiredObjects)
        {
            if (FindSceneObject(objectName) == null)
                throw new System.InvalidOperationException(objectName + " is missing.");
        }

        NightShiftSecurityCameraSystem cameraSystem = Object.FindFirstObjectByType<NightShiftSecurityCameraSystem>();
        SerializedProperty cameras = new SerializedObject(cameraSystem).FindProperty("feedCameras");
        if (cameras == null || !cameras.isArray || cameras.arraySize != 4)
            throw new System.InvalidOperationException("Exactly four security cameras are required.");
        for (int i = 0; i < cameras.arraySize; i++)
        {
            if (cameras.GetArrayElementAtIndex(i).objectReferenceValue == null)
                throw new System.InvalidOperationException("Security camera " + (i + 1) + " is not assigned.");
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontAssetPath) == null)
            throw new System.InvalidOperationException("Japanese TMP font asset is missing.");

        string[] requiredScenePaths = { TitleScenePath, ScenePath, ResultScenePath };
        for (int i = 0; i < requiredScenePaths.Length; i++)
        {
            if (EditorBuildSettings.scenes.Length <= i || EditorBuildSettings.scenes[i].path != requiredScenePaths[i] || !EditorBuildSettings.scenes[i].enabled)
                throw new System.InvalidOperationException("Build Settings scene order is invalid.");
        }

        Debug.Log("Night Shift validation passed: two doors, four controls, four live cameras, Japanese UI, and three-scene loop.");
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child.gameObject;
            }
        }

        return null;
    }

    private static void ValidateObjectReference(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            throw new System.InvalidOperationException(propertyName + " is not assigned.");
    }

    private static void BuildEnvironment(Transform parent, Material wallMaterial, Material floorMaterial, Material metalMaterial, Material deskMaterial, Material monitorMaterial, Material redMaterial, NightShiftGameController controller, out NightShiftOfficeDoor leftDoor, out NightShiftOfficeDoor rightDoor, out Light leftHallLight, out Light rightHallLight, out Transform monitorUiAnchor)
    {
        CreateCube("Office Floor", parent, new Vector3(0f, -0.1f, -0.1f), new Vector3(8.5f, 0.2f, 6.8f), floorMaterial);
        CreateCube("Main Hall Floor", parent, new Vector3(0f, -0.1f, -12f), new Vector3(5.3f, 0.2f, 17.5f), floorMaterial);
        CreateCube("Cross Hall Floor", parent, new Vector3(0f, -0.1f, -4.25f), new Vector3(13.2f, 0.2f, 2.2f), floorMaterial);
        CreateCube("Left Side Hall Floor", parent, new Vector3(5.35f, -0.1f, -1.55f), new Vector3(2.5f, 0.2f, 5.6f), floorMaterial);
        CreateCube("Right Side Hall Floor", parent, new Vector3(-5.35f, -0.1f, -1.55f), new Vector3(2.5f, 0.2f, 5.6f), floorMaterial);

        CreateCube("Office Ceiling", parent, new Vector3(0f, 3.15f, -0.1f), new Vector3(8.5f, 0.18f, 6.8f), wallMaterial);
        CreateCube("Main Hall Ceiling", parent, new Vector3(0f, 3.1f, -12f), new Vector3(5.3f, 0.18f, 17.5f), wallMaterial);
        CreateCube("Cross Hall Ceiling", parent, new Vector3(0f, 3.1f, -4.25f), new Vector3(13.2f, 0.18f, 2.2f), wallMaterial);
        CreateCube("Left Side Hall Ceiling", parent, new Vector3(5.35f, 3.1f, -1.55f), new Vector3(2.5f, 0.18f, 5.6f), wallMaterial);
        CreateCube("Right Side Hall Ceiling", parent, new Vector3(-5.35f, 3.1f, -1.55f), new Vector3(2.5f, 0.18f, 5.6f), wallMaterial);

        CreateCube("Office Back Wall", parent, new Vector3(0f, 1.5f, 3.15f), new Vector3(8.5f, 3f, 0.25f), wallMaterial);
        CreateCube("Office Front Wall", parent, new Vector3(0f, 1.5f, -3.35f), new Vector3(8.5f, 3f, 0.25f), wallMaterial);
        CreateSideOfficeWall(parent, "Left", 4.15f, wallMaterial);
        CreateSideOfficeWall(parent, "Right", -4.15f, wallMaterial);

        CreateCube("Main Hall Left Wall", parent, new Vector3(2.65f, 1.5f, -12f), new Vector3(0.25f, 3f, 15.4f), wallMaterial);
        CreateCube("Main Hall Right Wall", parent, new Vector3(-2.65f, 1.5f, -12f), new Vector3(0.25f, 3f, 15.4f), wallMaterial);
        CreateCube("Main Hall End Wall", parent, new Vector3(0f, 1.5f, -20.75f), new Vector3(5.3f, 3f, 0.25f), wallMaterial);
        CreateCube("Left Outer Wall", parent, new Vector3(6.6f, 1.5f, -1.55f), new Vector3(0.25f, 3f, 5.6f), wallMaterial);
        CreateCube("Right Outer Wall", parent, new Vector3(-6.6f, 1.5f, -1.55f), new Vector3(0.25f, 3f, 5.6f), wallMaterial);

        CreateCube("Desk", parent, new Vector3(0f, 0.55f, 1.05f), new Vector3(3.6f, 1.1f, 1f), deskMaterial);
        CreateCube("Desk Top", parent, new Vector3(0f, 1.15f, 1.05f), new Vector3(3.9f, 0.18f, 1.25f), deskMaterial);
        GameObject monitor = CreateCube("Security Monitor", parent, new Vector3(0f, 1.65f, 0.72f), new Vector3(1.5f, 0.86f, 0.18f), monitorMaterial);
        monitor.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);
        monitor.AddComponent<NightShiftInteractable>().Configure(controller, NightShiftInteractionAction.ToggleMonitor, "監視モニターを開く");

        GameObject monitorAnchor = new GameObject("Monitor UI Anchor");
        monitorUiAnchor = monitorAnchor.transform;
        monitorUiAnchor.SetParent(parent, false);
        monitorUiAnchor.position = monitor.transform.position + monitor.transform.forward * 0.096f;
        monitorUiAnchor.rotation = monitor.transform.rotation;

        GameObject leftDoorControl = CreateControl("Left Door Control", parent, new Vector3(1f, 1.45f, 1.35f), redMaterial, controller, NightShiftInteractionAction.ToggleLeftDoor, "左ドアを開閉");
        GameObject rightDoorControl = CreateControl("Right Door Control", parent, new Vector3(-1f, 1.45f, 1.35f), redMaterial, controller, NightShiftInteractionAction.ToggleRightDoor, "右ドアを開閉");
        CreateControl("Left Light Control", parent, new Vector3(1f, 1.02f, 1.35f), redMaterial, controller, NightShiftInteractionAction.ToggleLeftLight, "左通路ライトを切替");
        CreateControl("Right Light Control", parent, new Vector3(-1f, 1.02f, 1.35f), redMaterial, controller, NightShiftInteractionAction.ToggleRightLight, "右通路ライトを切替");

        leftDoor = CreateSideDoor("Left Office Door", parent, new Vector3(4.02f, 0f, 0.35f), Quaternion.Euler(0f, 90f, 0f), metalMaterial, leftDoorControl);
        rightDoor = CreateSideDoor("Right Office Door", parent, new Vector3(-4.02f, 0f, 0.35f), Quaternion.Euler(0f, 90f, 0f), metalMaterial, rightDoorControl);

        GameObject fillLightObject = new GameObject("Office Fill Light");
        fillLightObject.transform.SetParent(parent);
        fillLightObject.transform.position = new Vector3(0f, 2.45f, 0.9f);
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.range = 8f;
        fillLight.intensity = 0.95f;
        fillLight.color = new Color(0.82f, 0.86f, 0.92f);
        fillLight.shadows = LightShadows.None;

        leftHallLight = CreateHallLight("Left Hall Light", parent, new Vector3(3.75f, 2.35f, 0.35f), Vector3.right);
        rightHallLight = CreateHallLight("Right Hall Light", parent, new Vector3(-3.75f, 2.35f, 0.35f), Vector3.left);
    }

    private static void CreateSideOfficeWall(Transform parent, string sideName, float x, Material wallMaterial)
    {
        CreateCube("Office " + sideName + " Wall Front", parent, new Vector3(x, 1.5f, -1.95f), new Vector3(0.25f, 3f, 2.8f), wallMaterial);
        CreateCube("Office " + sideName + " Wall Back", parent, new Vector3(x, 1.5f, 2.2f), new Vector3(0.25f, 3f, 1.9f), wallMaterial);
        CreateCube("Office " + sideName + " Door Header", parent, new Vector3(x, 2.8f, 0.35f), new Vector3(0.25f, 0.4f, 1.8f), wallMaterial);
    }

    private static GameObject CreateControl(string name, Transform parent, Vector3 position, Material material, NightShiftGameController controller, NightShiftInteractionAction action, string prompt)
    {
        GameObject control = CreateCube(name, parent, position, new Vector3(0.36f, 0.34f, 0.15f), material);
        control.AddComponent<NightShiftInteractable>().Configure(controller, action, prompt);
        return control;
    }

    private static NightShiftOfficeDoor CreateSideDoor(string name, Transform parent, Vector3 position, Quaternion rotation, Material material, GameObject doorControl)
    {
        GameObject doorRoot = new GameObject(name);
        doorRoot.transform.SetParent(parent);
        doorRoot.transform.SetPositionAndRotation(position, rotation);
        NightShiftOfficeDoor door = doorRoot.AddComponent<NightShiftOfficeDoor>();
        Transform slab = CreateCube("Door Slab", doorRoot.transform, new Vector3(0f, 3.35f, 0f), new Vector3(1.85f, 2.6f, 0.24f), material).transform;

        GameObject lampObject = new GameObject(name + " Indicator");
        lampObject.transform.SetParent(doorControl.transform, false);
        lampObject.transform.localPosition = new Vector3(0f, 0f, -0.12f);
        Light indicator = lampObject.AddComponent<Light>();
        indicator.type = LightType.Point;
        indicator.range = 1.4f;
        indicator.intensity = 0.5f;
        indicator.color = Color.green;
        indicator.shadows = LightShadows.None;
        door.Configure(slab, doorControl.GetComponent<Renderer>(), indicator);
        return door;
    }

    private static Light CreateHallLight(string name, Transform parent, Vector3 position, Vector3 direction)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = position;
        lightObject.transform.rotation = Quaternion.LookRotation((direction + Vector3.down * 0.12f).normalized, Vector3.up);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = 8f;
        light.intensity = 0f;
        light.spotAngle = 72f;
        light.color = new Color(1f, 0.82f, 0.58f);
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    private static void BuildPlayer(Transform parent, NightShiftGameController controller, out Camera playerCamera, out NightShiftVRRigController vrRig, out Transform interactionRayOrigin)
    {
        GameObject player = new GameObject("XR Player Rig");
        player.transform.SetParent(parent);
        player.transform.position = new Vector3(0f, 0f, 1.65f);
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.78f;
        characterController.radius = 0.28f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.slopeLimit = 45f;

        vrRig = player.AddComponent<NightShiftVRRigController>();

        GameObject trackingSpace = new GameObject("Tracking Space");
        trackingSpace.transform.SetParent(player.transform);
        trackingSpace.transform.localPosition = Vector3.zero;
        trackingSpace.transform.localRotation = Quaternion.identity;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(trackingSpace.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.fieldOfView = 70f;
        playerCamera.nearClipPlane = 0.03f;
        playerCamera.farClipPlane = 60f;
        playerCamera.backgroundColor = Color.black;
        cameraObject.AddComponent<AudioListener>();

        GameObject leftHand = new GameObject("Left Controller Anchor");
        leftHand.transform.SetParent(trackingSpace.transform);
        leftHand.transform.localPosition = new Vector3(-0.25f, 1.25f, 0.35f);
        leftHand.transform.localRotation = Quaternion.identity;

        GameObject rightHand = new GameObject("Right Controller Anchor");
        rightHand.transform.SetParent(trackingSpace.transform);
        rightHand.transform.localPosition = new Vector3(0.25f, 1.25f, 0.35f);
        rightHand.transform.localRotation = Quaternion.identity;
        LineRenderer pointerLine = rightHand.AddComponent<LineRenderer>();
        pointerLine.useWorldSpace = true;
        pointerLine.widthMultiplier = 0.003f;
        pointerLine.positionCount = 2;
        pointerLine.startColor = new Color(0.65f, 0.9f, 1f, 0.7f);
        pointerLine.endColor = new Color(0.35f, 0.7f, 1f, 0.18f);
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null)
            lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null)
            lineShader = Shader.Find("Standard");
        pointerLine.sharedMaterial = new Material(lineShader);

        interactionRayOrigin = rightHand.transform;
        vrRig.Configure(controller, cameraObject.transform, leftHand.transform, rightHand.transform, pointerLine);
    }

    private static void BuildEnemy(Transform parent, NightShiftGameController controller, Material enemyMaterial, Material eyeMaterial, out NightShiftEnemyStalker enemyStalker)
    {
        Transform routeRoot = new GameObject("Enemy Route").transform;
        routeRoot.SetParent(parent);

        Transform[] route =
        {
            CreateWaypoint(routeRoot, "Storage", new Vector3(0f, 0f, -19f)),
            CreateWaypoint(routeRoot, "Back Hall", new Vector3(-1.2f, 0f, -14.2f)),
            CreateWaypoint(routeRoot, "Main Hall", new Vector3(1.1f, 0f, -9.2f)),
            CreateWaypoint(routeRoot, "Junction", new Vector3(0f, 0f, -4.25f))
        };
        Transform leftAttackPoint = CreateWaypoint(routeRoot, "Left Door Attack", new Vector3(4.9f, 0f, 0.35f));
        Transform rightAttackPoint = CreateWaypoint(routeRoot, "Right Door Attack", new Vector3(-4.9f, 0f, 0.35f));

        GameObject enemy = new GameObject("Stalker");
        enemy.transform.SetParent(parent);
        enemyStalker = enemy.AddComponent<NightShiftEnemyStalker>();
        enemyStalker.Configure(controller, route, leftAttackPoint, rightAttackPoint);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(enemy.transform);
        body.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.78f, 0.4f);
        body.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(enemy.transform);
        head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        head.transform.localScale = new Vector3(0.7f, 0.58f, 0.58f);
        head.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        GameObject leftEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftEye.name = "Left Eye";
        leftEye.transform.SetParent(enemy.transform);
        leftEye.transform.localPosition = new Vector3(-0.18f, 2f, -0.3f);
        leftEye.transform.localScale = Vector3.one * 0.09f;
        leftEye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;
        Object.DestroyImmediate(leftEye.GetComponent<Collider>());

        GameObject rightEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightEye.name = "Right Eye";
        rightEye.transform.SetParent(enemy.transform);
        rightEye.transform.localPosition = new Vector3(0.18f, 2f, -0.3f);
        rightEye.transform.localScale = Vector3.one * 0.09f;
        rightEye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;
        Object.DestroyImmediate(rightEye.GetComponent<Collider>());

        GameObject glow = new GameObject("Eye Glow");
        glow.transform.SetParent(enemy.transform);
        glow.transform.localPosition = new Vector3(0f, 2f, -0.35f);
        Light glowLight = glow.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = Color.red;
        glowLight.range = 3.5f;
        glowLight.intensity = 1.8f;
    }

    private static void BuildUi(Transform monitorUiAnchor, Camera playerCamera, out TextMeshProUGUI timerText, out TextMeshProUGUI powerText, out TextMeshProUGUI statusText, out TextMeshProUGUI promptText, out TextMeshProUGUI dangerText, out TextMeshProUGUI monitorFeedText, out RawImage monitorImage, out GameObject pausePanel, out GameObject monitorPanel, out GameObject jumpscarePanel, out Image staticOverlay)
    {
        GameObject menuCanvasObject = CreateCameraCanvas("Menu UI Canvas", playerCamera.transform.root, playerCamera, 1.5f, new Vector2(1600f, 900f));
        GameObject monitorCanvasObject = CreateWorldCanvas("Monitor Camera Canvas", monitorUiAnchor, playerCamera, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.00112f, new Vector2(1200f, 680f));
        Transform menuRoot = menuCanvasObject.transform;
        Transform monitorRoot = monitorCanvasObject.transform;

        timerText = CreateText("Timer", menuRoot, "12 AM", 58f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -85f), new Vector2(280f, 110f), Color.white);
        powerText = CreateText("Power", menuRoot, "電力: 100%\n使用量 [|.....]", 44f, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-120f, -85f), new Vector2(400f, 150f), Color.white);
        statusText = CreateText("Status", menuRoot, "", 32f, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(120f, 100f), new Vector2(680f, 150f), new Color(0.82f, 0.95f, 0.86f));
        promptText = CreateText("Interaction Prompt", menuRoot, "", 34f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 165f), new Vector2(900f, 80f), Color.white);
        dangerText = CreateText("Danger", menuRoot, "", 44f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(1000f, 100f), new Color(1f, 0.15f, 0.12f));
        dangerText.gameObject.SetActive(false);
        CreateText("Reticle", menuRoot, "+", 30f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), new Color(1f, 1f, 1f, 0.55f));

        pausePanel = CreatePanel("Pause Panel", menuRoot, new Color(0f, 0f, 0f, 0.78f));
        CreateText("Pause Title", pausePanel.transform, "一時停止", 72f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(680f, 110f), Color.white);
        CreateText("Pause Body", pausePanel.transform, "メニューボタンまたは右トリガーで再開", 30f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(900f, 80f), Color.white);
        pausePanel.SetActive(false);

        monitorPanel = CreatePanel("Monitor Panel", monitorRoot, new Color(0.005f, 0.012f, 0.009f, 1f));
        GameObject imageObject = new GameObject("Live Camera Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(monitorPanel.transform, false);
        monitorImage = imageObject.GetComponent<RawImage>();
        monitorImage.color = Color.white;
        monitorImage.raycastTarget = false;
        RectTransform imageRect = monitorImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.025f, 0.08f);
        imageRect.anchorMax = new Vector2(0.975f, 0.92f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        GameObject staticObject = new GameObject("Static", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        staticObject.transform.SetParent(monitorPanel.transform, false);
        staticOverlay = staticObject.GetComponent<Image>();
        staticOverlay.raycastTarget = false;
        StretchFullScreen(staticOverlay.rectTransform);

        monitorFeedText = CreateText("Monitor Feed", monitorPanel.transform, "", 38f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-70f, 180f), new Color(0.68f, 1f, 0.74f));
        TextMeshProUGUI monitorHelp = CreateText("Monitor Help", monitorPanel.transform, "右スティック左右: カメラ切替   左コントローラー A/X: モニターを下げる", 28f, TextAlignmentOptions.Bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-60f, 70f), Color.white);
        monitorHelp.textWrappingMode = TextWrappingModes.NoWrap;
        monitorPanel.SetActive(false);

        jumpscarePanel = CreatePanel("Jumpscare Panel", menuRoot, new Color(0.42f, 0f, 0f, 0.92f));
        CreateText("Jumpscare Text", jumpscarePanel.transform, "侵入されました", 96f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 160f), Color.white);
        jumpscarePanel.SetActive(false);
    }

    private static void BuildSecurityCameras(Transform parent, RawImage monitorImage, NightShiftSecurityCameraSystem cameraSystem)
    {
        Transform cameraRoot = new GameObject("Security Cameras").transform;
        cameraRoot.SetParent(parent);

        Camera[] cameras =
        {
            CreateSecurityCamera(cameraRoot, "CAM 01 Storage", new Vector3(2.1f, 2.35f, -17.2f), new Vector3(0f, 1.1f, -19f)),
            CreateSecurityCamera(cameraRoot, "CAM 02 Back Hall", new Vector3(-2.15f, 2.35f, -12.7f), new Vector3(-1.2f, 1.1f, -14.2f)),
            CreateSecurityCamera(cameraRoot, "CAM 03 Main Hall", new Vector3(2.15f, 2.35f, -7.6f), new Vector3(1.1f, 1.1f, -9.2f)),
            CreateSecurityCamera(cameraRoot, "CAM 04 Junction", new Vector3(0f, 2.5f, -2.9f), new Vector3(0f, 1.1f, -4.25f))
        };

        cameraSystem.Configure(cameras, monitorImage);
    }

    private static Camera CreateSecurityCamera(Transform parent, string name, Vector3 position, Vector3 lookTarget)
    {
        GameObject cameraObject = new GameObject(name);
        cameraObject.transform.SetParent(parent);
        cameraObject.transform.position = position;
        cameraObject.transform.rotation = Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 68f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 22f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.012f, 0.009f);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.stereoTargetEye = StereoTargetEyeMask.None;

        GameObject lightObject = new GameObject(name + " Feed Light");
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = lookTarget + Vector3.up * 1.35f;
        Light feedLight = lightObject.AddComponent<Light>();
        feedLight.type = LightType.Point;
        feedLight.range = 5.5f;
        feedLight.intensity = 1.25f;
        feedLight.color = new Color(0.58f, 0.78f, 0.64f);
        feedLight.shadows = LightShadows.None;
        return camera;
    }

    private static GameObject CreateWorldCanvas(string name, Transform parent, Camera worldCamera, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Vector2 size)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = localPosition;
        canvasObject.transform.localRotation = localRotation;
        canvasObject.transform.localScale = localScale;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = worldCamera;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        return canvasObject;
    }

    private static GameObject CreateCameraCanvas(string name, Transform parent, Camera worldCamera, float planeDistance, Vector2 referenceResolution)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = worldCamera;
        canvas.planeDistance = planeDistance;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        return cube;
    }

    private static Transform CreateWaypoint(Transform parent, string name, Vector3 position)
    {
        GameObject waypoint = new GameObject(name);
        waypoint.transform.SetParent(parent);
        waypoint.transform.position = position;
        return waypoint.transform;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        if (japaneseFontAsset != null)
            label.font = japaneseFontAsset;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        StretchFullScreen(panel.GetComponent<RectTransform>());
        return panel;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Material CreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0f, Color? emission = null)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, color);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);
        SetMaterialFloat(material, "_Glossiness", smoothness);

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            SetMaterialColor(material, emission.Value, "_EmissionColor");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color, string property = "_BaseColor")
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
            return;
        }

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static TMP_FontAsset EnsureJapaneseFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontAssetPath);
        if (existing != null)
        {
            existing.normalStyle = 0.25f;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.ImportAsset(JapaneseFontPath, ImportAssetOptions.ForceSynchronousImport);
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(JapaneseFontPath);
        if (sourceFont == null)
        {
            Debug.LogWarning("Japanese font was not found at " + JapaneseFontPath);
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        fontAsset.name = "NSP_Japanese SDF";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.normalStyle = 0.25f;
        AssetDatabase.CreateAsset(fontAsset, JapaneseFontAssetPath);

        if (fontAsset.material != null)
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
        {
            if (atlasTexture != null)
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != TitleScenePath && scene.path != ScenePath && scene.path != ResultScenePath)
            .ToList();

        scenes.InsertRange(0, new[]
        {
            new EditorBuildSettingsScene(TitleScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(ResultScenePath, true)
        });
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
