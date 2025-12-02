using UnityEngine;
using UnityEngine.UI; // Toggle UI 제어를 위해 필요

public class CanvasToggle : MonoBehaviour
{
    [Header("타겟 스크립트 및 UI")]
    public BellComm bellComm;      // BellComm 스크립트를 인스펙터에서 연결하세요
    public Toggle contactToggle;   // 캔버스 안에 있는 UI Toggle 컴포넌트를 연결하세요

    [Header("토글할 캔버스")]
    public Canvas canvas1;
    public Canvas canvas2;

    private bool _visible = true;

    void Start()
    {
        // 시작 시 캔버스 가시성 상태 동기화
        if (canvas1 != null) _visible = canvas1.enabled;

        // UI Toggle에 이벤트 리스너 연결
        // 토글 값이 바뀌면(터치/코드) 자동으로 BellComm의 isContacted 값을 갱신함
        if (contactToggle != null && bellComm != null)
        {
            // 초기 상태 맞추기 (Toggle UI 기준)
            bellComm.isContacted = contactToggle.isOn;

            // 값이 변할 때마다 호출될 함수 등록
            contactToggle.onValueChanged.AddListener(OnContactToggleChanged);
        }
    }

    // UI Toggle 값이 변경될 때 실행되는 함수
    void OnContactToggleChanged(bool isOn)
    {
        if (bellComm != null)
        {
            bellComm.isContacted = isOn;
            Debug.Log($"[Sync] Contact State Changed to: {isOn}");
        }
    }

    void Update()
    {
        // B 버튼: 캔버스 켜기/끄기 (기존 기능)
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            _visible = !_visible;

            if (canvas1 != null) canvas1.enabled = _visible;
            if (canvas2 != null) canvas2.enabled = _visible;
        }

        // A 버튼: 접촉 상태 토글 (캔버스가 보일 때만 작동)
        if (_visible && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            if (contactToggle != null)
            {
                contactToggle.isOn = !contactToggle.isOn;
            }
        }
    }
}