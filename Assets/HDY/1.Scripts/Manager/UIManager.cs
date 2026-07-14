using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using HDY.Shop;
using HDY.Upgrade;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// HUD 버튼(상점/도감/창고/여신상/멤창고)으로 여는 최상위 UI들을 통합 관리하는 매니저.
    ///
    /// [열기] 버튼을 누르면 그 버튼에 연결된 프리팹을 uiRoot(P_UIRoot) 밑에 Instantiate하고,
    /// 로컬 좌표를 (0,0,0)으로 맞춰 중앙에 배치한다.
    ///
    /// [한 번에 하나만 - 다른 버튼을 누르면 기존 것을 닫고 새로 연다] 최상위 UI는 한 번에 하나만 열려
    /// 있을 수 있다. 이미 어떤 UI가 열려 있는 상태에서 "다른" 버튼을 누르면 기존 UI를 먼저 Destroy하고
    /// 새 UI를 연다. 반대로 "이미 열려 있는 UI와 같은" 버튼을 다시 누르면 아무 동작도 하지 않는다
    /// (기획 확정 사항 - 토글로 닫히지 않음).
    ///
    /// [ESC로 닫기 = 스택 pop] 연 순서를 Stack&lt;GameObject&gt;에 쌓아두고, ESC를 누르면 맨 위(가장
    /// 최근에 연 것)를 pop해서 Destroy한다. 지금 정책상 한 번에 하나만 열리므로 스택에는 사실상 0개
    /// 아니면 1개만 쌓이지만, 나중에 "여러 개 동시에" 정책으로 바뀌어도 Push/Pop 골격을 그대로 재사용할
    /// 수 있도록 스택 구조를 유지한다.
    ///
    /// [상점(ShopUI) 특이사항] 상점은 열려있는 동안 내부적으로 다른 상점(마트/식당/철물점)이나 구매/판매
    /// 탭으로 이동할 수 있는데, 그건 이 매니저가 아니라 ShopUI 자신이 처리한다(ShopUI.Open(shopData)
    /// 호출은 이 매니저를 거치지 않는 내부 전환). 이 매니저는 "상점 버튼을 처음 눌러서 상점 창 자체를
    /// 여는 순간"에만 관여하며, 그때 defaultShop으로 Open을 한 번 호출해 초기 내용을 채워준다.
    ///
    /// [업그레이드 팝업 정리] UpgradePopupUI는 이 스택과 별개로 씬에 상시 배치된 싱글톤이라(P_UIRoot의
    /// 원래부터 있던 자식), 상위 UI(상점/여신상/창고 등)를 Destroy해도 자동으로 같이 닫히지 않는다.
    /// 그래서 CloseCurrent()에서 상위 UI를 닫기 직전에 UpgradePopupUI.Instance?.Hide()를 먼저 호출해서,
    /// 다른 UI로 넘어가거나 ESC로 닫을 때 팝업만 화면에 덩그러니 남지 않도록 한다.
    ///
    /// [프리팹 내부의 자체 닫기(X) 버튼 주의] 개별 UI 프리팹 안에 자체 닫기 버튼이 있다면, 그 버튼은
    /// 반드시 UIManager.Instance.CloseCurrent()를 호출해야 한다 - 그래야 스택 상태와 실제로 열려있는
    /// 오브젝트가 어긋나지 않는다. ShopUI.Close()처럼 내부적으로 SetActive(false)만 하는 메서드를
    /// 그대로 연결하면, 스택은 여전히 "열려있다"고 착각해 같은 버튼을 다시 눌러도 아무 반응이 없는
    /// 상태가 될 수 있다.
    ///
    /// [시간 데이터 연결 - GameTimeManager] 리얼타임(KST)/인게임 시간(20분=하루) 표시를 위한
    /// GameTimeManager 참조를 들고 있다. 이 매니저는 시간 데이터 계산만 담당하고 Text 갱신은 직접 하지
    /// 않으므로, 시간 표시 Text를 실제로 붙이는 작업은 GameTime 프로퍼티로 GameTimeManager에 접근해서
    /// (GetRealTimeText()/GetInGameTimeText() 조회 또는 OnRealTimeTextChanged/OnInGameTimeTextChanged
    /// 이벤트 구독) 별도로 진행하면 된다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        /// <summary>HUD 버튼 하나와 그 버튼이 여는 프리팹을 짝짓는 항목.</summary>
        [Serializable]
        private class HudEntry
        {
            public Button button;
            public GameObject prefab;
        }

        public static UIManager Instance { get; private set; }

        [Tooltip("UI 프리팹이 배치될 부모(P_UIRoot). 여기 밑에 로컬 좌표 (0,0,0)으로 Instantiate된다.")]
        [SerializeField] private Transform uiRoot;

        [Header("HUD 버튼 <-> 프리팹 연결")]
        [SerializeField] private List<HudEntry> hudEntries = new List<HudEntry>();

        [Header("상점 전용 - 상점 창을 처음 열 때 기본으로 보여줄 상점")]
        [SerializeField] private ShopData defaultShop;

        [Header("시간 데이터 참조 (리얼타임/인게임 시간, 비어있으면 자동 탐색 - Text 연결은 별도 진행)")]
        [SerializeField] private GameTimeManager gameTimeManager;

        private readonly Stack<GameObject> openStack = new Stack<GameObject>();

        /// <summary>지금 열려있는 UI가 어떤 프리팹에서 나온 건지 식별하는 키. 같은 버튼 재클릭 판별에 사용.</summary>
        private GameObject currentPrefabKey;

        /// <summary>리얼타임(KST)/인게임 시간 데이터. 시간 표시 Text 연결은 이 프로퍼티로 GameTimeManager에 접근해서 진행하면 된다.</summary>
        public GameTimeManager GameTime => gameTimeManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UIManager] 씬에 UIManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (uiRoot == null) Debug.LogWarning("[UIManager] uiRoot가 비어있습니다. UI를 어디에 배치할지 알 수 없습니다.", this);

            gameTimeManager = GameTimeManager.Resolve(gameTimeManager);
            if (gameTimeManager == null) Debug.LogWarning("[UIManager] gameTimeManager를 찾을 수 없습니다. 시간 UI를 연결할 수 없습니다.", this);

            foreach (var entry in hudEntries)
            {
                if (entry == null || entry.button == null || entry.prefab == null) continue;

                var prefab = entry.prefab; // 람다 클로저 캡처용 로컬 변수
                entry.button.onClick.AddListener(() => HandleHudButtonClicked(prefab));
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (PanelManager.Instance != null)
                {
                    PanelManager.Instance.CloseAllPanels();
                }
                else
                {
                    CloseCurrent();
                }
            }
        }

        private void HandleHudButtonClicked(GameObject prefab)
        {
            if (uiRoot == null || prefab == null) return;

            // 이미 열려있는 UI와 같은 버튼이면 아무 동작도 하지 않는다(기획 확정 사항).
            if (currentPrefabKey == prefab) return;

            CloseCurrent();

            if (PanelManager.Instance != null)
            {
                PanelManager.Instance.NotifyHUDPanelOpened();
            }

            var instance = Instantiate(prefab, uiRoot);
            var instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            openStack.Push(instance);
            currentPrefabKey = prefab;

            // 상점은 열리자마자 어떤 상점을 보여줄지 정해줘야 한다(이후 상점 내부 이동은 ShopUI 자신이 처리).
            var shopUI = instance.GetComponent<ShopUI>();
            if (shopUI != null && defaultShop != null) shopUI.Open(defaultShop);
        }

        /// <summary>
        /// 지금 열려있는 UI(스택 맨 위)를 닫는다. ESC와 프리팹 내부 닫기(X) 버튼이 이 메서드를 호출해야 한다.
        /// 업그레이드 팝업이 열려있으면 상위 UI보다 먼저 닫는다(팝업은 상위 UI의 자식이 아니라 별개의 씬
        /// 상시 배치 싱글톤이라, 상위 UI를 Destroy해도 자동으로 같이 닫히지 않기 때문).
        /// </summary>
        public void CloseCurrent()
        {
            UpgradePopupUI.Instance?.Hide();

            if (openStack.Count == 0) return;

            var top = openStack.Pop();
            if (top != null) Destroy(top);

            currentPrefabKey = null;
        }

        /// <summary>
        /// 현재 UIManager에 가동중이 프리팹 패널이 존재하는지 파악
        /// </summary>
        public bool HasActivePanel()
        {
            return openStack.Count > 0 && currentPrefabKey != null;
        }
    }
}
