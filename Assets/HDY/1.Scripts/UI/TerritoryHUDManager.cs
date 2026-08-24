using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using HDY.Territory;
using HDY.Item;
using HDY.Inventory;
using KMS;
using KMS.InventoryDuped;

namespace HDY.UI
{
    /// <summary>
    /// 영지 골드/레벨을 HUD에 텍스트로 표시하는 컴포넌트.
    /// 골드는 "000" 형식, 레벨은 평소 "Lv. N"으로 표시하며, 레벨 버튼에 마우스를 올리면
    /// "현재경험치/필요경험치" 형식으로 바뀌고 글자 크기도 줄어든다(HandleLevelButtonHoverEnter/Exit).
    /// 값이 실제로 바뀐 프레임에만 텍스트를 다시 대입한다(KMS PlayerHUD.SetGoldText와 동일한 방식).
    /// TerritoryData에는 골드/경험치 변경 이벤트가 없어서 Update()에서 매 프레임 값을 직접 확인한다.
    ///
    /// [호버 시 경험치 표시 - HDY 요청] levelButton에 마우스를 올리면 레벨 텍스트가 경험치 표시로,
    /// 폰트 크기가 20(원래 값, Awake 시점에 그대로 캡처)에서 10으로 바뀐다. 마우스가 나가면 원래
    /// 레벨/폰트 크기로 되돌아온다. Button은 자체적으로 마우스 진입/이탈 이벤트를 주지 않으므로,
    /// UIManager.ManagedPanelCloseWatcher와 동일한 패턴으로 LevelButtonHoverRelay를 버튼 오브젝트에
    /// 자동으로 붙여서(Awake, AddComponent) 중계한다 - Inspector에서 EventTrigger를 따로 구성할
    /// 필요가 없다. 경험치 표시는 0 패딩 없이 그대로 숫자만 찍는다(요청 확인 완료).
    /// </summary>
    public class TerritoryHUDManager : MonoBehaviour
    {
        /// <summary>levelButton에 자동으로 붙어서 마우스 진입/이탈을 TerritoryHUDManager로 중계하는 컴포넌트.</summary>
        private class LevelButtonHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public TerritoryHUDManager Owner;

            public void OnPointerEnter(PointerEventData eventData) => Owner?.HandleLevelButtonHoverEnter();
            public void OnPointerExit(PointerEventData eventData) => Owner?.HandleLevelButtonHoverExit();
        }

        private sealed class ActiveItemObtainedToast
        {
            public KMSItemObtainedToastView view;
            public Coroutine lifetimeRoutine;
        }

        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("골드 텍스트 연결")]
        [Tooltip("\"Gold: 000 \" 형식으로 표시할 TMP_Text.")]
        [SerializeField] private TMP_Text goldText;

        [Header("영지 레벨 텍스트 연결")]
        [Tooltip("\"Lv. 00 \" 형식으로 표시할 TMP_Text.")]
        [SerializeField] private TMP_Text levelText;

        [Header("영지 레벨 버튼 (호버 시 경험치 표시로 전환)")]
        [Tooltip("이 버튼에 마우스를 올리면 levelText가 \"현재경험치/필요경험치\"로 바뀌고 폰트 크기가 10으로 줄어든다.")]
        [SerializeField] private Button levelButton;

        [Header("아이템 획득 토스트 (탐험 캐릭터 HUD의 KMSPlayerHudView.ShowItemObtained 로직을 그대로 포팅 - HDY 요청)")]
        [Tooltip("토스트가 배치될 컨테이너. P_TerritoryHUD 안에 복제해둔 ItemObtainedToastContainer를 연결한다.")]
        [SerializeField] private RectTransform itemObtainedToastContainer;
        [Tooltip("토스트 1개의 템플릿(항상 비활성 상태로 보관). 복제해둔 ItemObtainedToastTemplate을 연결한다.")]
        [SerializeField] private KMSItemObtainedToastView itemObtainedToastTemplate;
        [SerializeField, Min(0f)] private float itemObtainedToastDuration = 2.5f;
        [SerializeField, Min(0f)] private float itemObtainedToastFadeDuration = 0.3f;
        [SerializeField, Min(1)] private int maxVisibleItemObtainedToasts = 4;

        [Header("아이템 획득 이벤트 참조 (비어있으면 자동 탐색)")]
        [Tooltip("플레이어 인벤토리로 직접 들어간 아이템도 토스트로 띄우기 위해 구독한다.")]
        [SerializeField] private PlayerInventory playerInventory;
        [Tooltip("영지에서는 대부분 창고로 먼저 들어가므로 창고 이벤트도 함께 구독한다.")]
        [SerializeField] private WarehouseInventory warehouseInventory;

        private const float HoverLevelFontSize = 13f;

        private int lastDisplayedGold = int.MinValue;
        private int lastDisplayedLevel = int.MinValue;
        private int lastDisplayedExp = int.MinValue;
        private int lastDisplayedRequiredExp = int.MinValue;
        private bool hasDisplayedGold;
        private bool hasDisplayedLevel;
        private bool hasDisplayedExp;

        private bool isHoveringLevelButton;
        private float normalLevelFontSize;

        private readonly List<ActiveItemObtainedToast> itemObtainedToasts = new List<ActiveItemObtainedToast>();

        private void Awake()
        {
            territoryData = TerritoryData.Resolve(territoryData);

            if (territoryData == null) Debug.LogWarning("[TerritoryHUDManager] TerritoryData를 찾을 수 없습니다. 골드가 표시되지 않습니다.", this);
            if (goldText == null) Debug.LogWarning("[TerritoryHUDManager] goldText가 비어있습니다.", this);
            if (levelText == null) Debug.LogWarning("[TerritoryHUDManager] levelText가 비어있습니다.", this);

            if (levelText != null) normalLevelFontSize = levelText.fontSize;

            if (levelButton != null)
            {
                var relay = levelButton.gameObject.AddComponent<LevelButtonHoverRelay>();
                relay.Owner = this;
            }
            else
            {
                Debug.LogWarning("[TerritoryHUDManager] levelButton이 비어있습니다. 호버 시 경험치 표시가 동작하지 않습니다.", this);
            }

            if (itemObtainedToastTemplate != null) itemObtainedToastTemplate.gameObject.SetActive(false);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (playerInventory == null && warehouseInventory == null)
            {
                Debug.LogWarning("[TerritoryHUDManager] playerInventory/warehouseInventory를 모두 찾을 수 없습니다. 아이템 획득 토스트가 표시되지 않습니다.", this);
            }
        }

private void OnEnable()
        {
            if (playerInventory != null) playerInventory.OnItemObtained += HandleItemObtained;
            if (warehouseInventory != null) warehouseInventory.OnItemAdded += HandleItemObtained;
        }

        private void OnDisable()
        {
            if (playerInventory != null) playerInventory.OnItemObtained -= HandleItemObtained;
            if (warehouseInventory != null) warehouseInventory.OnItemAdded -= HandleItemObtained;

            ClearItemObtainedToasts();
        }


        private void Update()
        {
            if (territoryData == null)
            {
                territoryData = TerritoryData.Resolve(territoryData);
                if (territoryData == null) return;
            }

            RefreshGoldText(territoryData.Gold);

            if (isHoveringLevelButton)
            {
                RefreshExpText(territoryData.CurrentExp, territoryData.RequiredExp);
            }
            else
            {
                RefreshLevelText(territoryData.Level);
            }
        }

        /// <summary>골드 값이 실제로 바뀐 경우에만 텍스트를 다시 대입한다.</summary>
        private void RefreshGoldText(int gold)
        {
            if (goldText == null || (hasDisplayedGold && gold == lastDisplayedGold)) return;

            lastDisplayedGold = gold;
            hasDisplayedGold = true;
            goldText.text = $"{gold}";
        }

        private void RefreshLevelText(int level)
        {
            // [버그 수정] hasDisplayedGold를 검사하던 오타를 hasDisplayedLevel로 수정.
            if (levelText == null || (hasDisplayedLevel && level == lastDisplayedLevel)) return;
            lastDisplayedLevel = level;
            hasDisplayedLevel = true;
            levelText.text = $"Lv. {level}";
        }

        /// <summary>경험치 값이 실제로 바뀐 경우에만 텍스트를 다시 대입한다(호버 중일 때만 호출됨). 0 패딩 없음.</summary>
        private void RefreshExpText(int currentExp, int requiredExp)
        {
            if (levelText == null || (hasDisplayedExp && currentExp == lastDisplayedExp && requiredExp == lastDisplayedRequiredExp)) return;

            lastDisplayedExp = currentExp;
            lastDisplayedRequiredExp = requiredExp;
            hasDisplayedExp = true;
            levelText.text = $"{currentExp}/{requiredExp}";
        }

        private void HandleLevelButtonHoverEnter()
        {
            isHoveringLevelButton = true;
            if (levelText != null) levelText.fontSize = HoverLevelFontSize;

            hasDisplayedExp = false; // 다음 Update에서 즉시 새로 대입되도록
        }

        private void HandleLevelButtonHoverExit()
        {
            isHoveringLevelButton = false;
            if (levelText != null) levelText.fontSize = normalLevelFontSize;

            hasDisplayedLevel = false; // 다음 Update에서 즉시 새로 대입되도록
        }

        private void HandleItemObtained(ItemData item, int amount)
        {
            ShowItemObtained(item, amount, itemObtainedToastDuration, itemObtainedToastFadeDuration, maxVisibleItemObtainedToasts);
        }

        private void ShowItemObtained(
            ItemData item,
            int amount,
            float visibleDuration,
            float fadeDuration,
            int maxVisible)
        {
            if (item == null || amount <= 0 || itemObtainedToastContainer == null || itemObtainedToastTemplate == null)
                return;

            PruneItemObtainedToasts();
            while (itemObtainedToasts.Count >= Mathf.Max(1, maxVisible))
            {
                RemoveItemObtainedToast(itemObtainedToasts[0]);
            }

            KMSItemObtainedToastView instance = Instantiate(itemObtainedToastTemplate, itemObtainedToastContainer);
            instance.name = $"ItemObtained_{item.Item_ID}";
            instance.SetData(item, amount);
            instance.gameObject.SetActive(true);

            ActiveItemObtainedToast toast = new ActiveItemObtainedToast { view = instance };
            itemObtainedToasts.Add(toast);
            toast.lifetimeRoutine = StartCoroutine(
                RunItemObtainedToastLifetime(toast, visibleDuration, fadeDuration));
        }

        private IEnumerator RunItemObtainedToastLifetime(
            ActiveItemObtainedToast toast,
            float visibleDuration,
            float fadeDuration)
        {
            if (toast.view == null) yield break;
            CanvasGroup canvasGroup = toast.view.CanvasGroup;
            if (canvasGroup == null)
            {
                RemoveItemObtainedToast(toast, false);
                yield break;
            }

            float fadeInElapsed = 0f;
            float fadeInDuration = Mathf.Min(0.16f, Mathf.Max(0f, fadeDuration));
            canvasGroup.alpha = fadeInDuration > 0f ? 0f : 1f;
            while (fadeInElapsed < fadeInDuration && toast.view != null)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(fadeInElapsed / fadeInDuration);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, visibleDuration));
            if (toast.view == null) yield break;

            yield return FadeOut(canvasGroup, Mathf.Max(0f, fadeDuration));
            RemoveItemObtainedToast(toast, false);
        }

        private void PruneItemObtainedToasts()
        {
            itemObtainedToasts.RemoveAll(toast => toast == null || toast.view == null);
        }

        private void RemoveItemObtainedToast(ActiveItemObtainedToast toast, bool stopRoutine = true)
        {
            if (toast == null) return;
            if (stopRoutine && toast.lifetimeRoutine != null) StopCoroutine(toast.lifetimeRoutine);
            itemObtainedToasts.Remove(toast);
            if (toast.view != null) Destroy(toast.view.gameObject);
        }

        private void ClearItemObtainedToasts()
        {
            for (int i = itemObtainedToasts.Count - 1; i >= 0; i--)
            {
                RemoveItemObtainedToast(itemObtainedToasts[i]);
            }
        }

        private static IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
        }
    }
}
