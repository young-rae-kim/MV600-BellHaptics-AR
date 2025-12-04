using UnityEngine;
using UnityEngine.InputSystem; 

public class CueToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("켜고 끌 비주얼 큐 (씬에 있는 인스턴스)")]
    public GameObject visualCueObject;

    [Tooltip("시작할 때 ON으로 할지 여부")]
    public bool startOn = false;

    // Input System action (오른손 Grip)
    private InputAction rightGripAction;

    private void Awake()
    {
        if (visualCueObject != null)
        {
            visualCueObject.SetActive(startOn);
        }
    }

    private void OnEnable()
    {
        // 이미 만들어져 있지 않으면 생성
        if (rightGripAction == null)
        {
            // Quest 3 오른손 컨트롤러 Grip 버튼
            // 필요하면 "<XRController>{LeftHand}/gripPressed" 로 바꿔도 됨
            rightGripAction = new InputAction(
                name: "RightGripToggle",
                type: InputActionType.Button,
                binding: "<XRController>{RightHand}/gripPressed"
            );
        }

        rightGripAction.performed += OnRightGripPerformed;
        rightGripAction.Enable();
    }

    private void OnDisable()
    {
        if (rightGripAction != null)
        {
            rightGripAction.performed -= OnRightGripPerformed;
            rightGripAction.Disable();
        }
    }

    private void OnRightGripPerformed(InputAction.CallbackContext ctx)
    {
        // 버튼이 눌렸을 때만 토글 (떼는 시점은 무시)
        if (!ctx.ReadValueAsButton())
            return;

        ToggleVisualCue();
    }

    private void ToggleVisualCue()
    {
        if (visualCueObject == null)
        {
            Debug.LogWarning("[VisualCueToggle] visualCueObject not assigned.");
            return;
        }

        bool next = !visualCueObject.activeSelf;
        visualCueObject.SetActive(next);

        Debug.Log($"[VisualCueToggle] Visual cue set to {(next ? "ON" : "OFF")}");
    }
}
