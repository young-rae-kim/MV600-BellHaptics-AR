using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

[Serializable]
public class PressPayload
{
    public bool pressed;
    public int codeX;
    public int codeY;
}

[Serializable]
public class TransformData
{
    public Vector3 pos;
    public Quaternion rot;

    public TransformData(Transform t)
    {
        pos = t.position;
        rot = t.rotation;
    }
}

[Serializable]
public class ClientPacket
{
    public string user_id;       // 유저 식별자
    public bool is_contact;      // 접촉 상태인지
    public TransformData hmd;    // HMD 정보
    
    // 아래 리스트는 is_contact가 true일 때만 데이터가 채워짐
    public List<TransformData> left_hand = new List<TransformData>();
    public List<TransformData> right_hand = new List<TransformData>();
}

public class BellComm : MonoBehaviour
{
    // 유저 식별용 ID
    public string userId = "00000001";

    // 접촉 상태 플래그 
    [Header("Interaction State")]
    public bool isContacted = false;

    // 손 관절 트랜스폼
    [Header("Tracking Joints")]
    public Transform leftHandTouchOrigin;
    public Transform rightHandTouchOrigin;

    [SerializeField]
    private Animator flowerAnimator;

    [SerializeField]
    private GameObject particleObject;
    
    [SerializeField]
    private RockManager rockManager;

    [Header("Visual Cue (Network Toggle)")]
    public GameObject visualCueObject;
    public bool cueStartOn = false;

    public string serverURL = "ws://192.168.0.22:5000/ws";

    public TMP_Text statusText;
    public TMP_Text messageText;
    public Transform hmd;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;

    // 상태 플래그
    private bool pressedPending = false;
    private bool cueTogglePending = false;
    private int pendingCodeX = 0;
    private int pendingCodeY = 0;

    // Pressed 표시 시간
    public float pressedDisplayDuration = 0.1f;  // Pressed 를 표시할 시간(초)
    private float pressedTimer = 0f;             // 남은 시간
    private string defaultMessage = "";          // 기본 텍스트(처음 상태 저장)

    // 꽃 표시 관련
    public float flowerVisibleDuration = 10f;    // 꽃이 보이는 시간(초)
    private float flowerTimer = 0f;              // 남은 시간
    private bool flowerPending = false;          // 꽃 타이머 활성 상태

    void Start()
    {
        Debug.Log("My User ID: " + userId);

        if (hmd == null)
            hmd = Camera.main.transform;

        if (messageText != null)
            defaultMessage = messageText.text;

        if (flowerAnimator != null)
            flowerAnimator.gameObject.SetActive(false);
        
        if (particleObject != null)
            particleObject.SetActive(false);
        
        if (visualCueObject != null)
            visualCueObject.SetActive(cueStartOn);

        ConnectWebSocket();
        InvokeRepeating(nameof(SendTelemetry), 0.1f, 0.05f);
    }

    async void ConnectWebSocket()
    {
        try
        {
            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();

            if (statusText != null)
            {
                statusText.text = "Connecting...";
                statusText.color = Color.yellow;
            }

            await ws.ConnectAsync(new Uri(serverURL), cts.Token);

            if (statusText != null)
            {
                statusText.text = "Connected";
                statusText.color = Color.green;
            }

            _ = Task.Run(ReceiveLoop);
        }
        catch (Exception e)
        {
            if (statusText != null)
            {
                statusText.text = "Connection Failed";
                statusText.color = Color.red;
            }
            Debug.LogError("WebSocket Error: " + e.Message);
        }
    }

    async Task ReceiveLoop()
    {
        byte[] buffer = new byte[1024];

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError("ReceiveLoop error: " + e.Message);
                break;
            }

            if (result.Count > 0)
            {
                string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Debug.Log("[WS RECV] " + msg);

                string trimmed = msg.TrimStart();
                if (trimmed.StartsWith("{"))
                {
                    if (msg.Contains("\"cue_toggle\"") && msg.Contains("true"))
                    {
                        cueTogglePending = true;
                    }

                    try
                    {
                        PressPayload payload = JsonUtility.FromJson<PressPayload>(msg);
                        if (payload != null && payload.pressed)
                        {
                            // 코드 값만 저장하고,
                            pendingCodeX = payload.codeX;
                            pendingCodeY = payload.codeY;

                            // "누름 이벤트 들어왔다" 플래그만 켜줌
                            pressedPending = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("PressPayload parse error: " + ex.Message);
                    }
                }
                else
                {
                    // 혹시 예전처럼 "Pressed" 문자열만 오는 경우 대비
                    if (msg.Contains("Pressed"))
                    {
                        pressedPending = true;

                        // 코드 정보가 없으니, RockManager에 세팅된 현재 코드 그대로 사용
                        if (rockManager != null)
                        {
                            pendingCodeX = rockManager.currentCodeX;
                            pendingCodeY = rockManager.currentCodeY;
                        }
                    }
                }
            }
        }
    }

    async void SendTelemetry()
    {
        if (ws == null || ws.State != WebSocketState.Open)
            return;

        // 1. 패킷 생성 및 기본 정보 채우기
        ClientPacket packet = new ClientPacket();
        packet.user_id = userId;
        packet.is_contact = isContacted;
        
        if (hmd != null)
            packet.hmd = new TransformData(hmd);

        // 2. 접촉 상태일 때만 손 데이터 추가 (데이터 부하 관리)
        if (isContacted)
        {
            if (leftHandTouchOrigin != null)
                packet.left_hand.Add(new TransformData(leftHandTouchOrigin));
            
            if (rightHandTouchOrigin != null)
                packet.right_hand.Add(new TransformData(rightHandTouchOrigin));
        }

        // 3. JSON 변환 및 전송
        string json = JsonUtility.ToJson(packet);

        try
        {
            await ws.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                WebSocketMessageType.Text,
                true,
                cts.Token
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Send Error: " + e.Message);
        }
    }

    /// <summary>
    /// Flask에서 받은 codeX, codeY를 RockManager에 넘겨서 돌 스폰
    /// </summary>
    void OnPressedReceived(int codeX, int codeY)
    {
        if (rockManager != null)
        {
            rockManager.SetCode(codeX, codeY);
            rockManager.TriggerSpawn();
        }
        else
        {
            Debug.LogWarning("[BellComm] rockManager가 설정되어 있지 않습니다.");
        }
    }

    void Update()
    {
        // 새로운 Pressed 이벤트가 들어왔을 때
        if (pressedPending)
        {
            pressedPending = false;              // 한 번만 처리
            OnPressedReceived(pendingCodeX, pendingCodeY);
            pressedTimer = pressedDisplayDuration;  // 타이머 리셋

            if (messageText != null)
                messageText.text = "Pressed!";

            if (flowerAnimator != null)
            {
                flowerAnimator.gameObject.SetActive(true);
                particleObject.SetActive(true);
                flowerAnimator.SetTrigger("TrOpen");

                flowerTimer = flowerVisibleDuration;
                flowerPending = true;
            }
        }

        // 서버에서 cue_toggle 들어온 경우
        if (cueTogglePending)
        {
            cueTogglePending = false;

            if (visualCueObject != null)
            {
                bool next = !visualCueObject.activeSelf;
                visualCueObject.SetActive(next);
                Debug.Log("[BellComm] Visual cue toggled to " + (next ? "ON" : "OFF"));
            }
            else
            {
                Debug.LogWarning("[BellComm] visualCueObject not assigned for cue toggle.");
            }
        }

        // Pressed 표시 중이면 타이머 감소시키다가 끝나면 원래 텍스트로 복귀
        if (pressedTimer > 0f)
        {
            pressedTimer -= Time.deltaTime;

            if (pressedTimer <= 0f)
            {
                pressedTimer = 0f;
                if (messageText != null)
                    messageText.text = defaultMessage;  // 다시 원래 상태로
            }
        }

        // 꽃 표시 타이머
        if (flowerPending && flowerTimer > 0f)
        {
            flowerTimer -= Time.deltaTime;

            if (flowerTimer <= 0f)
            {
                flowerTimer = 0f;
                flowerPending = false;

                if (flowerAnimator != null)
                    flowerAnimator.gameObject.SetActive(false);

                if (particleObject != null)
                    particleObject.SetActive(false);
            }
        }
    }
}
