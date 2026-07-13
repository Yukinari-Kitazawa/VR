using UnityEngine;

public sealed class NightShiftEnemyStalker : MonoBehaviour
{
    private static readonly string[] CameraLocationNames =
    {
        "倉庫",
        "裏通路",
        "中央廊下",
        "分岐通路"
    };

    [SerializeField] private NightShiftGameController gameController;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Transform leftDoorWaypoint;
    [SerializeField] private Transform rightDoorWaypoint;
    [SerializeField] private Vector2 moveCheckIntervalSeconds = new Vector2(5f, 8f);
    [SerializeField, Range(0f, 1f)] private float baseAdvanceChance = 0.24f;
    [SerializeField, Range(0f, 0.25f)] private float hourlyAdvanceChanceBonus = 0.07f;
    [SerializeField, Range(0f, 1f)] private float observedChanceMultiplier = 0.18f;
    [SerializeField, Min(1)] private int maximumFailedMoveChecks = 3;
    [SerializeField] private float doorAttackDelay = 4.5f;
    [SerializeField] private Vector2 powerOutRushDelaySeconds = new Vector2(6f, 11f);
    [SerializeField] private float visualMoveSpeed = 2.2f;

    private int stageIndex;
    private int failedMoveChecks;
    private float moveCheckTimer;
    private float attackTimer;
    private float powerOutRushTimer;
    private bool isAttackingDoor;
    private bool powerOutRush;
    private NightShiftAttackSide attackSide;

    public int StageIndex => stageIndex;
    public int CameraCount => waypoints != null ? waypoints.Length : 0;
    public bool IsAtDoor => isAttackingDoor;
    public NightShiftAttackSide AttackSide => attackSide;

    private void Awake()
    {
        NormalizeTuning();
        if (gameController == null)
            gameController = GetComponentInParent<NightShiftGameController>();
    }

    public void Configure(NightShiftGameController controller, Transform[] route, Transform leftAttackPoint, Transform rightAttackPoint)
    {
        gameController = controller;
        waypoints = route;
        leftDoorWaypoint = leftAttackPoint;
        rightDoorWaypoint = rightAttackPoint;
    }

    private void Start()
    {
        ResetForNight();
    }

    private void Update()
    {
        if (gameController == null || !gameController.IsPlaying)
            return;

        UpdateVisualPosition();

        if (powerOutRush)
        {
            UpdatePowerOutRush();
            return;
        }

        if (isAttackingDoor)
        {
            UpdateDoorAttack();
            return;
        }

        moveCheckTimer -= Time.deltaTime;
        if (moveCheckTimer > 0f)
            return;

        EvaluateMoveOpportunity();
        ScheduleNextMoveCheck();
    }

    public void ResetForNight()
    {
        stageIndex = 0;
        isAttackingDoor = false;
        powerOutRush = false;
        attackSide = NightShiftAttackSide.Left;
        attackTimer = 0f;
        powerOutRushTimer = 0f;
        failedMoveChecks = 0;
        ScheduleNextMoveCheck();

        if (CameraCount > 0 && waypoints[0] != null)
            transform.position = waypoints[0].position;
    }

    public void BeginPowerOutRush()
    {
        powerOutRush = true;
        isAttackingDoor = false;
        ChooseAttackSide();
        float minimum = Mathf.Min(powerOutRushDelaySeconds.x, powerOutRushDelaySeconds.y);
        float maximum = Mathf.Max(powerOutRushDelaySeconds.x, powerOutRushDelaySeconds.y);
        powerOutRushTimer = Random.Range(minimum, maximum);
    }

    public string GetCameraFeedText(int cameraIndex)
    {
        if (CameraCount == 0)
            return "CAM -- / オフライン\n映像信号なし";

        cameraIndex = Mathf.Clamp(cameraIndex, 0, CameraCount - 1);
        bool subjectVisible = !powerOutRush && stageIndex == cameraIndex;
        string subjectStatus;

        if (subjectVisible && isAttackingDoor)
            subjectStatus = attackSide == NightShiftAttackSide.Left ? "対象: 左通路へ移動" : "対象: 右通路へ移動";
        else if (subjectVisible)
            subjectStatus = "対象を検知";
        else
            subjectStatus = "動体反応なし";

        return GetCameraName(cameraIndex) + "\n" + subjectStatus + "\n" + BuildCameraMap(cameraIndex);
    }

    private void EvaluateMoveOpportunity()
    {
        if (CameraCount == 0)
            return;

        if (stageIndex >= CameraCount - 1)
        {
            BeginDoorAttack();
            return;
        }

        float advanceChance = baseAdvanceChance + gameController.CurrentHourIndex * hourlyAdvanceChanceBonus;
        if (gameController.IsCameraWatching(stageIndex))
            advanceChance *= observedChanceMultiplier;

        failedMoveChecks++;
        bool forceAdvance = failedMoveChecks >= maximumFailedMoveChecks;
        if (!forceAdvance && Random.value > Mathf.Clamp01(advanceChance))
            return;

        failedMoveChecks = 0;
        stageIndex = Mathf.Min(stageIndex + 1, CameraCount - 1);
        gameController.NotifyEnemyMoved(stageIndex);

        if (stageIndex >= CameraCount - 1)
            BeginDoorAttack();
    }

    private void BeginDoorAttack()
    {
        ChooseAttackSide();
        isAttackingDoor = true;
        attackTimer = doorAttackDelay;
        string sideLabel = attackSide == NightShiftAttackSide.Left ? "左" : "右";
        gameController.SetDanger(sideLabel + "通路で接近反応");
    }

    private void ChooseAttackSide()
    {
        attackSide = Random.value < 0.5f ? NightShiftAttackSide.Left : NightShiftAttackSide.Right;
    }

    private void UpdateDoorAttack()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        if (gameController.IsDoorClosed(attackSide))
        {
            stageIndex = Mathf.Max(1, CameraCount - 3);
            isAttackingDoor = false;
            failedMoveChecks = 0;
            ScheduleNextMoveCheck();
            gameController.BlockEnemyAtDoor(attackSide);
            return;
        }

        gameController.LoseNight("侵入者がオフィスに入りました。");
    }

    private void UpdatePowerOutRush()
    {
        powerOutRushTimer -= Time.deltaTime;
        if (powerOutRushTimer > 0f)
            return;

        powerOutRush = false;
        stageIndex = Mathf.Max(0, CameraCount - 1);
        isAttackingDoor = true;
        attackTimer = Random.Range(2f, 4f);
        gameController.SetDanger("停電中: 接近反応");
    }

    private void ScheduleNextMoveCheck()
    {
        float minimum = Mathf.Max(1f, Mathf.Min(moveCheckIntervalSeconds.x, moveCheckIntervalSeconds.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(moveCheckIntervalSeconds.x, moveCheckIntervalSeconds.y));
        moveCheckTimer = Random.Range(minimum, maximum);
    }

    private void NormalizeTuning()
    {
        if (moveCheckIntervalSeconds.x <= 0f || moveCheckIntervalSeconds.y <= 0f)
            moveCheckIntervalSeconds = new Vector2(5f, 8f);
        if (baseAdvanceChance <= 0f)
            baseAdvanceChance = 0.24f;
        if (hourlyAdvanceChanceBonus <= 0f)
            hourlyAdvanceChanceBonus = 0.07f;
        if (observedChanceMultiplier <= 0f)
            observedChanceMultiplier = 0.18f;
        if (maximumFailedMoveChecks <= 0)
            maximumFailedMoveChecks = 3;
        if (powerOutRushDelaySeconds.x <= 0f || powerOutRushDelaySeconds.y <= 0f)
            powerOutRushDelaySeconds = new Vector2(6f, 11f);
    }

    private string GetCameraName(int cameraIndex)
    {
        string location = cameraIndex < CameraLocationNames.Length
            ? CameraLocationNames[cameraIndex]
            : "不明";
        return "CAM " + (cameraIndex + 1).ToString("00") + " / " + location;
    }

    private string BuildCameraMap(int selectedCamera)
    {
        string map = "";
        for (int i = 0; i < CameraCount; i++)
        {
            if (i > 0)
                map += "-";

            string marker = (i + 1).ToString("00");
            map += i == selectedCamera ? "[>" + marker + "<]" : "[" + marker + "]";
        }

        return map + ">左右ドア";
    }

    private void UpdateVisualPosition()
    {
        if (CameraCount == 0)
            return;

        Transform targetTransform = waypoints[Mathf.Clamp(stageIndex, 0, CameraCount - 1)];
        if (isAttackingDoor)
        {
            Transform attackTarget = attackSide == NightShiftAttackSide.Left ? leftDoorWaypoint : rightDoorWaypoint;
            if (attackTarget != null)
                targetTransform = attackTarget;
        }

        if (targetTransform != null)
            transform.position = Vector3.Lerp(transform.position, targetTransform.position, Time.deltaTime * visualMoveSpeed);
    }
}
