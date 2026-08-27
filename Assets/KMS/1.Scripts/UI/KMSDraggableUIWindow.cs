using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace KMS
{
    /// <summary>
    /// [멤] 배경을 마우스로 드래그하면 이 UI 창(패널)을 이동시키고, 이동한 위치를 PlayerPrefs에 저장해서
    /// 씬 전환/게임 재시작 후에도 마지막 위치가 유지되도록 하는 범용 드래그 컴포넌트.
    ///
    /// [자동으로 스크롤뷰/슬롯이 제외되는 이유] 이 컴포넌트는 패널의 배경 Image(레이캐스트 타겟)에 붙인다.
    /// 스크롤뷰/슬롯/버튼 등 자식 UI들은 배경보다 위에 그려지고 각자 자신의 Graphic이 레이캐스트를
    /// 가로채므로, EventSystem은 그 위를 클릭했을 때 이 배경이 아니라 자식 쪽으로 드래그 이벤트를
    /// 보낸다. 즉 "빈 여백(배경이 직접 맞는 지점)을 클릭했을 때만" 이 컴포넌트의 OnBeginDrag가 호출되고,
    /// 스크롤뷰/슬롯/버튼 위에서는 각자의 클릭·스크롤 동작이 그대로 우선한다 - 별도의 제외 목록 없이
    /// 자연스럽게 요구사항이 만족된다.
    ///
    /// [알트(커서 표시) 요구사항] requireCursorReleased를 켜두면 KMS.PlayerInput.IsCursorReleased가
    /// true일 때만(=알트를 눌러 커서가 보이는 상태) 드래그를 시작할 수 있다. 인벤토리처럼 패널이 열려있는
    /// 동안 항상 커서가 풀려있는(SetCursorReleased) 창은 이 체크를 꺼둬도 된다 - 패널이 열려있다는 것
    /// 자체가 이미 커서가 자유로운 상태이기 때문.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class KMSDraggableUIWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("PlayerPrefs에 저장될 때 쓰이는 고유 키. 서로 다른 창(스킬HUD/탐험 인벤토리/영지 인벤토리 등)은 " +
                 "반드시 서로 다른 키를 써야 한다 - 같은 키를 쓰면 서로 다른 레이아웃의 창이 같은 위치를 공유하게 된다.")]
        [SerializeField] private string saveKey;

        [Tooltip("이동시킬 대상 RectTransform. 비워두면 이 컴포넌트가 붙은 오브젝트 자신을 이동시킨다.")]
        [SerializeField] private RectTransform targetRect;

        [Tooltip("체크하면 KMS.PlayerInput.IsCursorReleased(알트로 커서가 보이는 상태)일 때만 드래그를 시작할 수 있다. " +
                 "패널이 열려있는 동안 항상 커서가 풀려있는 창(예: 인벤토리)은 체크를 꺼도 된다.")]
        [SerializeField] private bool requireCursorReleased = false;

        [Tooltip("체크하면 현재 씬 이름에 \"territory\"가 포함되어 있을 때(영지 씬) 이 컴포넌트 자체를 비활성화해 드래그/위치복원을 끄는다. " +
                 "같은 프리팹을 영지와 탐험 양쪽에서 공유하는데 탐험에서만 드래그가 되어야 하는 창(예: 캐릭터 스탯 패널)에 쓴다.")]
        [SerializeField] private bool disableInTerritoryScene = false;

        private RectTransform selfRect;
        private Canvas parentCanvas;
        private KMS.PlayerInput playerInput;
        private bool isDragging;

        private void Awake()
        {
            selfRect = transform as RectTransform;
            if (targetRect == null) targetRect = selfRect;
            parentCanvas = GetComponentInParent<Canvas>();

            if (disableInTerritoryScene && IsTerritoryScene())
            {
                // [멤] 영지 씬이면 이 컴포넌트 자체를 비활성화한다 - OnEnable(위치복원)도 호출되지 않아 완전히 고정 위치로 남는다.
                enabled = false;
            }
        }

        private static bool IsTerritoryScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(sceneName) && sceneName.ToLowerInvariant().Contains("territory");
        }

        private void OnEnable()
        {
            // [멤] 패널이 다시 활성화될 때마다(씬 재진입 포함) 마지막으로 저장된 위치를 다시 적용한다.
            ApplySavedPosition();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = false;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (targetRect == null) return;
            if (!IsDragAllowed()) return;

            isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || targetRect == null) return;

            if (!IsDragAllowed())
            {
                isDragging = false;
                return;
            }

            float scale = GetCanvasScaleFactor();
            targetRect.anchoredPosition += eventData.delta / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            bool wasDragging = isDragging;
            isDragging = false;
            if (!wasDragging || targetRect == null) return;

            SavePosition(targetRect.anchoredPosition);
        }

        private bool IsDragAllowed()
        {
            if (!requireCursorReleased) return true;

            if (playerInput == null) playerInput = FindFirstObjectByType<KMS.PlayerInput>();
            return playerInput != null && playerInput.IsCursorReleased;
        }

        private float GetCanvasScaleFactor()
        {
            return parentCanvas != null && parentCanvas.scaleFactor > 0f ? parentCanvas.scaleFactor : 1f;
        }

        private void SavePosition(Vector2 anchoredPosition)
        {
            if (string.IsNullOrEmpty(saveKey)) return;

            PlayerPrefs.SetFloat(saveKey + "_X", anchoredPosition.x);
            PlayerPrefs.SetFloat(saveKey + "_Y", anchoredPosition.y);
            PlayerPrefs.SetInt(saveKey + "_Set", 1);
            PlayerPrefs.Save();
        }

        private void ApplySavedPosition()
        {
            if (string.IsNullOrEmpty(saveKey) || targetRect == null) return;
            if (PlayerPrefs.GetInt(saveKey + "_Set", 0) != 1) return;

            float x = PlayerPrefs.GetFloat(saveKey + "_X", targetRect.anchoredPosition.x);
            float y = PlayerPrefs.GetFloat(saveKey + "_Y", targetRect.anchoredPosition.y);
            targetRect.anchoredPosition = new Vector2(x, y);
        }
    }
}
