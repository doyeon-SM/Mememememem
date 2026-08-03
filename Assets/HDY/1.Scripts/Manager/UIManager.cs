using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using HDY.Shop;
using HDY.Upgrade;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// HUD 버튼(상점/도감/창고/여신상/멤창고/대장간/탐험 등)으로 여는 최상위 UI들을 통합 관리하는 매니저.
    ///
    /// [버그 수정 - 패널 사전 생성/재사용] 예전에는 [열기] 버튼을 누를 때마다 그 버튼에 연결된 프리팹을
    /// uiRoot(P_UIRoot) 밑에 매번 새로 Instantiate하고, 닫을 때 Destroy했다. 이제는 Awake 시점에
    /// hudEntries의 프리팹을 전부 미리 uiRoot 밑에 Instantiate해서 로컬 좌표를 (0,0,0)으로 맞춰두고
    /// 즉시 SetActive(false)해둔다(CreateManagedPanels 참고). 이후로는 [열기]에서 SetActive(true),
    /// [닫기]에서 SetActive(false)만 하고 다시는 Instantiate/Destroy하지 않는다. hudEntries에 등록되지
    /// 않은 프리팹으로 예외적으로 호출되는 경우(외부 호출 등)에는 그 자리에서 즉석 생성하되, 이후에는 그
    /// 인스턴스도 동일하게 캐시해서 재사용한다(ResolvePanelInstance 참고).
    ///
    /// [한 번에 하나만 - 다른 버튼을 누르면 기존 것을 닫고 새로 연다] 최상위 UI는 한 번에 하나만 열려
    /// 있을 수 있다. 이미 어떤 UI가 열려 있는 상태에서 "다른" 버튼을 누르면 기존 UI를 먼저 닫고(SetActive
    /// (false)) 새 UI를 연다. 반대로 "이미 열려 있는 UI와 같은" 버튼을 다시 누르면, 그 패널이 여전히
    /// 활성 상태인 한 아무 동작도 하지 않는다(기획 확정 사항 - 토글로 닫히지 않음). 다만 SceneUIManager가
    /// ESC로 패널을 비활성화해둔 뒤라면(아래 참고) "닫혀 있다"고 보고 다시 연다.
    ///
    /// [ESC로 닫기 - _GH SceneUIManager에 위임] 이 매니저는 더 이상 ESC 키를 직접 감지하지 않는다.
    /// 대신 HUD 패널을 미리 생성할 때(Awake) 그 인스턴스를 _GH의 SceneUIManager가 갖고 있는
    /// managedUIObjects 리스트(비공개 필드)에 리플렉션으로 한 번만 등록해두고, 그 이후로는 다시
    /// 등록/해제하지 않는다(인스턴스가 파괴되지 않으므로 등록도 영구적으로 유지된다). 실제 ESC 입력
    /// 처리와 패널 SetActive(false)는 SceneUIManager가 대신 담당하도록 넘긴다(SceneUIManager.cs는
    /// 크로스팀 코드라 수정하지 않음).
    ///
    /// SceneUIManager는 패널을 닫을 때도 Destroy가 아니라 SetActive(false)만 호출하므로, 그 순간에도
    /// 이 매니저의 openStack/currentPrefabKey 상태가 실제 활성 여부와 어긋나지 않도록, 패널마다 붙여둔
    /// ManagedPanelCloseWatcher가 OnDisable 시점에 즉시 동기화(스택 pop + currentPrefabKey 초기화)한다.
    /// 이 동기화가 중요한 이유는, KMS 쪽 코드(KMSMemDexLauncher 등)가 HasActivePanel()을 매 프레임
    /// 확인해서 자기 자신(플레이어 이동/커서 잠금)을 풀어주기 때문이다ㅡ상태가 어긋나면 ESC로 패널을
    /// 닫아도 플레이어가 계속 "메뉴 모드"에 갇히게 된다.
    ///
    /// 팀 협의 결과, _GH의 SceneUIManager가 아직 배치되지 않은 씬(예: HDY_TestScene 단독 테스트)에서는
    /// ESC로 패널을 닫는 기능 자체가 동작하지 않는다. 그런 씬에서는 프리팹 내부의 자체 닫기(X) 버튼이나
    /// CloseCurrent() 직접 호출로만 닫을 수 있다.
    ///
    /// [상점(ShopUI) 특이사항] 상점은 열려있는 동안 내부적으로 다른 상점(마트/식당/철물점)이나 구매/판매
    /// 탭으로 이동할 수 있는데, 그건 이 매니저가 아니라 ShopUI 자신이 처리한다(ShopUI.Open(shopData)
    /// 호출은 이 매니저를 거치지 않는 내부 전환). 이 매니저는 "상점 버튼을 눌러서 상점 창을 여는 순간"에만
    /// 관여하며, 그때 defaultShop으로 Open을 호출해 내용을 채워준다(패널을 재사용하게 되면서, 두 번째로
    /// 여는 순간에도 이 호출이 다시 일어나 항상 기본 상점으로 리셋된다 - 예전부터 있던 동작 그대로 유지).
    ///
    /// [업그레이드 팝업 정리] UpgradePopupUI는 이 스택과 별개로 씬에 상시 배치된 싱글톤이라(P_UIRoot의
    /// 원래부터 있던 자식), 상위 UI(상점/여신상/창고 등)를 닫아도 자동으로 같이 닫히지 않는다. 그래서
    /// CloseCurrent()에서 상위 UI를 닫기 직전에 UpgradePopupUI.Instance?.Hide()를 먼저 호출해서, 다른
    /// UI로 넘어가거나 닫을 때 팝업만 화면에 덩그러니 남지 않도록 한다.
    ///
    /// [프리팹 내부의 자체 닫기(X) 버튼 주의] 개별 UI 프리팹 안에 자체 닫기 버튼이 있다면, 그 버튼은
    /// 반드시 UIManager.Instance.CloseCurrent()를 호출해야 한다 - 그래야 스택 상태와 실제로 열려있는
    /// 오브젝트가 어긋나지 않는다. ShopUI.Close()처럼 내부적으로 SetActive(false)만 하는 메서드를
    /// 그대로 연결하면, 스택은 여전히 "열려있다"고 착각해 같은 버튼을 다시 눌러도 아무 반응이 없는
    /// 상태가 될 수 있다.
    ///
    /// [영지 레벨 연동 - HUD 버튼 잠금 해제] hudEntries의 각 항목에 RequiredLevel(기본 0 = 항상 활성화)을
    /// 지정할 수 있다. TerritoryData.Level이 그 값 미만이면 버튼을 비활성화(interactable=false)해두고,
    /// TerritoryData.OnLevelChanged 이벤트를 구독해서 레벨이 오를 때마다 전체를 다시 계산한다. 매칭되는
    /// 레벨/버튼이 나중에 바뀌거나 새 버튼이 추가돼도, 코드 수정 없이 인스펙터에서 hudEntries 항목의
    /// RequiredLevel 값만 조정하면 된다(예: 대장간=3, 탐험=5).
    ///
    /// [재진입 시 잠금 상태 갱신 - 버그 수정] 예전에는 ApplyLevelGates()를 Awake와 OnLevelChanged
    /// 시점에만 호출했다. TerritoryData는 DontDestroyOnLoad 싱글톤이라 레벨 자체는 씬을 나갔다 들어와도
    /// 정상적으로 유지되지만, 이 UIManager가 달린 HUD 오브젝트가 (완전히 Destroy/재생성되는 게 아니라)
    /// SetActive(false) -> SetActive(true)로 껐다 켜지는 방식으로 영지에 재진입하는 경우 Awake는 최초
    /// 1회만 실행되고 이후에는 OnEnable만 실행된다. 그 사이에 레벨이 올라가 있어도(그리고 그 시점 이후로
    /// 레벨이 다시 바뀌지 않으면 OnLevelChanged도 재발행되지 않으므로) 버튼은 최초 Awake 때의 오래된
    /// 잠금 상태에 그대로 머물러 있었다. OnEnable에서도 ApplyLevelGates()를 호출하도록 추가해서, 재진입
    /// (재활성화)할 때마다 항상 최신 레벨 기준으로 다시 계산하도록 고쳤다.
    ///
    /// [시간 데이터 연결 - GameTimeManager] 리얼타임(KST)/인게임 시간(20분=하루) 표시를 위한
    /// GameTimeManager 참조를 들고 있다. 이 매니저는 시간 데이터 계산만 담당하고 Text 갱신은 직접 하지
    /// 않으므로, 시간 표시 Text를 실제로 붙이는 작업은 GameTime 프로퍼티로 GameTimeManager에 접근해서
    /// (GetRealTimeText()/GetInGameTimeText() 조회 또는 OnRealTimeTextChanged/OnInGameTimeTextChanged
    /// 이벤트 구독) 별도로 진행하면 된다.
    ///
    /// [패널이 완전히 닫힐 때 PanelManager 상태 복구 - 버그 수정] _Kyusoo의 PanelManager는 UIManager로
    /// HUD 패널이 열릴 때마다 NotifyHUDPanelOpened()를 통해 placeButtonGroup(P_TerritoryObjectButton)을
    /// 꺼서 숨긴다. 그런데 이걸 다시 켜주는 PanelManager.CloseAllPanels()는 PanelManager 자신의 Open***
    /// 계열 함수 안에서만 호출되고, UIManager 쪽에서 패널을 닫는 경로(CloseCurrent, 혹은 SceneUIManager가
    /// ESC로 SetActive(false)한 뒤 ManagedPanelCloseWatcher가 감지하는 경로)에는 CloseAllPanels() 호출이
    /// 전혀 없었다. 그 결과 UIManager로 HUD 패널을 한 번이라도 열면, 그 패널을 어떤 방법으로 닫든
    /// P_TerritoryObjectButton이 다시 켜지지 않는 문제가 있었다. PanelManager.cs는 크로스팀 코드라 직접
    /// 수정하지 않고, 대신 패널이 완전히 닫히는 두 지점(CloseCurrent, HandleManagedPanelDisabled)에서
    /// PanelManager.CloseAllPanels()를 호출해주는 방식으로 우회한다. PanelManager.CloseAllPanels()가
    /// 내부에서 다시 UIManager.Instance.CloseCurrent()를 호출하지만, 그 시점엔 이미 openStack이 비어있어
    /// 곧바로 return되므로 무한 재귀로 이어지지 않는다.
    ///
    /// [HDY 요청 - 튜토리얼 연동용 패널 오픈 이벤트] HUD 버튼으로 패널이 열릴 때마다 OnPanelOpened를
    /// 발행한다(패널이 실제로 SetActive(true)되는 시점, HandleHudButtonClicked 안). 이 이벤트는 튜토리얼
    /// 시스템(Assets/HDY/1.Scripts/Tutorial)의 TutorialUIPanelWatcher가 구독해서 "특정 UI를 처음 열었을
    /// 때" 안내를 띄우는 데 쓰인다 - 이 매니저 자체는 튜토리얼을 전혀 몰라도 되도록 순수 이벤트 발행만
    /// 담당한다(느슨한 결합). HudEntry.PanelKey를 채워두면 그 문자열이, 비워두면 prefab.name이 그대로
    /// 이벤트 인자로 전달된다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        /// <summary>HUD 버튼 하나와 그 버튼이 여는 프리팹을 짝짓는 항목.</summary>
        [Serializable]
        private class HudEntry
        {
            public Button button;
            public GameObject prefab;

            [Tooltip("이 버튼이 활성화되는 데 필요한 영지 레벨. 0이면 레벨 제한 없이 항상 활성화(기존 5개 버튼은 0으로 둔다).")]
            public int RequiredLevel = 0;

            [Tooltip("OnPanelOpened 이벤트에 실려나갈 식별 키(튜토리얼 등에서 사용). 비워두면 prefab.name을 그대로 쓴다.")]
            public string PanelKey;
        }

        /// <summary>
        /// Instantiate된 HUD 패널에 붙어서, _GH SceneUIManager가 ESC로 SetActive(false)만 했을 때도
        /// OnDisable 시점에 UIManager의 openStack/currentPrefabKey를 즉시 동기화해주는 감시자.
        /// CloseCurrent()가 먼저 스택에서 빼낸 뒤 SetActive(false)하는 경우에는 이미 Peek이 일치하지
        /// 않으므로 아무 동작도 하지 않는다.
        /// </summary>
        private class ManagedPanelCloseWatcher : MonoBehaviour
        {
            public UIManager Owner;

            private void OnDisable()
            {
                if (Owner != null) Owner.HandleManagedPanelDisabled(gameObject);
            }
        }

        public static UIManager Instance { get; private set; }

        [Tooltip("UI 프리팹이 배치될 부모(P_UIRoot). 여기 밑에 로컬 좌표 (0,0,0)으로 미리 Instantiate된다.")]
        [SerializeField] private Transform uiRoot;

        [Header("HUD 버튼 <-> 프리팹 연결 (RequiredLevel로 영지 레벨 잠금 설정)")]
        [SerializeField] private List<HudEntry> hudEntries = new List<HudEntry>();

        [Header("영지 레벨 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private TerritoryData territoryData;

        [Header("상점 전용 - 상점 창을 처음 열 때 기본으로 보여줄 상점")]
        [SerializeField] private ShopData defaultShop;

        [Header("시간 데이터 참조 (리얼타임/인게임 시간, 비어있으면 자동 탐색 - Text 연결은 별도 진행)")]
        [SerializeField] private GameTimeManager gameTimeManager;

        private readonly Stack<GameObject> openStack = new Stack<GameObject>();

        /// <summary>지금 열려있는 UI가 어떤 프리팹에서 나온 건지 식별하는 키. 같은 버튼 재클릭 판별에 사용.</summary>
        private GameObject currentPrefabKey;

        /// <summary>
        /// [버그 수정 - 패널 사전 생성/재사용] 프리팹 원본 -> 미리 만들어둔(혹은 예외적으로 즉석 생성한) 인스턴스.
        /// CreateManagedPanels()에서 hudEntries 전체를 채워두고, 이후로는 Instantiate/Destroy 없이 이 인스턴스를
        /// SetActive로만 껐다 켠다.
        /// </summary>
        private readonly Dictionary<GameObject, GameObject> prefabToInstance = new Dictionary<GameObject, GameObject>();

        /// <summary>_GH SceneUIManager의 private managedUIObjects 필드에 접근하기 위한 캐시된 FieldInfo.</summary>
        private static FieldInfo sceneUIManagerManagedObjectsField;

        /// <summary>리얼타임(KST)/인게임 시간 데이터. 시간 표시 Text 연결은 이 프로퍼티로 GameTimeManager에 접근해서 진행하면 된다.</summary>
        public GameTimeManager GameTime => gameTimeManager;

        /// <summary>UI 프리팹이 배치될 부모(P_UIRoot). TutorialManager가 튜토리얼 패널 프리팹을 심을 때 사용.</summary>
        public Transform UIRoot => uiRoot;

        /// <summary>
        /// HUD 버튼으로 패널이 열릴 때마다 발행(패널 식별 키 전달). 튜토리얼 시스템의
        /// TutorialUIPanelWatcher가 구독해서 "특정 UI를 처음 열었을 때" 트리거로 중계한다.
        /// </summary>
        public event Action<string> OnPanelOpened;

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

            territoryData = TerritoryData.Resolve(territoryData);
            if (territoryData != null)
            {
                territoryData.OnLevelChanged += HandleTerritoryLevelChanged;
            }

            foreach (var entry in hudEntries)
            {
                if (entry == null || entry.button == null || entry.prefab == null) continue;

                var prefab = entry.prefab; // 람다 클로저 캡처용 로컬 변수
                entry.button.onClick.AddListener(() => HandleHudButtonClicked(prefab));
            }

            CreateManagedPanels();

            ApplyLevelGates();
        }

        /// <summary>
        /// [버그 수정] HUD 오브젝트가 Destroy/재생성 없이 SetActive(false)->(true)로만 재진입하는 경우
        /// Awake는 다시 실행되지 않으므로, 여기서도 최신 레벨 기준으로 다시 계산해준다.
        /// </summary>
        private void OnEnable()
        {
            ApplyLevelGates();
        }

        private void OnDestroy()
        {
            if (territoryData != null)
            {
                territoryData.OnLevelChanged -= HandleTerritoryLevelChanged;
            }
        }

        private void HandleTerritoryLevelChanged(int newLevel)
        {
            ApplyLevelGates();
        }

        /// <summary>
        /// hudEntries를 훑어서 RequiredLevel을 만족하지 못하는 버튼은 비활성화(interactable=false),
        /// 만족하는 버튼은 활성화한다. RequiredLevel이 0 이하인 항목(기존 버튼들)은 항상 활성화된다.
        /// territoryData가 아직 비어있으면(Awake보다 먼저 OnEnable이 불릴 일은 없지만 방어적으로) 한 번 더
        /// 재탐색을 시도한다.
        /// </summary>
        private void ApplyLevelGates()
        {
            if (territoryData == null)
            {
                territoryData = TerritoryData.Resolve(territoryData);
            }

            int currentLevel = territoryData != null ? territoryData.Level : int.MaxValue;

            foreach (var entry in hudEntries)
            {
                if (entry?.button == null) continue;

                bool unlocked = entry.RequiredLevel <= 0 || currentLevel >= entry.RequiredLevel;
                entry.button.interactable = unlocked;
            }
        }

        /// <summary>
        /// [버그 수정 - 패널 사전 생성/재사용] hudEntries에 등록된 프리팹들을 uiRoot 아래 전부 미리
        /// Instantiate해서 prefabToInstance에 캐시해두고, 즉시 SetActive(false)로 숨겨둔다. 이후로는
        /// HandleHudButtonClicked/CloseCurrent가 이 인스턴스를 SetActive로만 껐다 켠다 - 다시는
        /// Instantiate/Destroy하지 않는다. 같은 프리팹이 hudEntries에 중복으로 등록돼 있어도 한 번만
        /// 생성한다.
        /// </summary>
        private void CreateManagedPanels()
        {
            if (uiRoot == null) return;

            foreach (var entry in hudEntries)
            {
                if (entry == null || entry.prefab == null) continue;
                if (prefabToInstance.ContainsKey(entry.prefab)) continue;

                prefabToInstance[entry.prefab] = CreatePanelInstance(entry.prefab);
            }
        }

        /// <summary>
        /// prefab을 uiRoot 아래 Instantiate하고 로컬 좌표를 (0,0,0)으로 초기화한 뒤, ManagedPanelCloseWatcher를
        /// 붙이고 SceneUIManager의 ESC 관리 대상에 등록하고 나서 SetActive(false)로 숨긴 인스턴스를 만든다.
        /// CreateManagedPanels()의 사전 생성과, hudEntries에 없는 프리팹이 예외적으로 들어왔을 때의 즉석 생성
        /// (ResolvePanelInstance) 양쪽에서 공용으로 쓴다.
        /// </summary>
        private GameObject CreatePanelInstance(GameObject prefab)
        {
            var instance = Instantiate(prefab, uiRoot);
            var instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            instance.AddComponent<ManagedPanelCloseWatcher>().Owner = this;
            RegisterWithSceneUIManager(instance);
            instance.SetActive(false);

            return instance;
        }

        public void HandleHudButtonClicked(GameObject prefab)
        {
            if (uiRoot == null || prefab == null) return;

            // 이미 열려있는 UI와 같은 버튼이면 아무 동작도 하지 않는다(기획 확정 사항).
            // 단, SceneUIManager가 ESC로 닫아서 비활성화된 상태라면 "닫혀 있다"고 보고 다시 연다.
            if (currentPrefabKey == prefab)
            {
                var currentTop = openStack.Count > 0 ? openStack.Peek() : null;
                if (currentTop != null && currentTop.activeSelf) return;
            }

            CloseCurrent();

            if (PanelManager.Instance != null)
            {
                PanelManager.Instance.NotifyHUDPanelOpened();
            }

            GameObject instance = ResolvePanelInstance(prefab);

            // [버그 수정 - z-order 유지] 예전에는 열 때마다 새로 Instantiate돼서 항상 uiRoot의 맨 마지막
            // 자식이 되었다(= 항상 다른 UI보다 위에 그려짐). 이제는 인스턴스가 고정되어 있으므로, 열 때마다
            // 맨 뒤로 옮겨서 예전과 동일하게 "열리는 UI가 항상 맨 위"가 되도록 한다.
            instance.transform.SetAsLastSibling();
            instance.SetActive(true);

            openStack.Push(instance);
            currentPrefabKey = prefab;

            // [HDY 요청 - 튜토리얼 연동용] 패널이 실제로 열린 시점에 OnPanelOpened를 발행한다.
            // hudEntries에서 이 prefab과 짝지어진 항목의 PanelKey를 찾아 쓰고, 없으면 prefab.name을 쓴다.
            OnPanelOpened?.Invoke(ResolvePanelKey(prefab));

            // 상점은 열리자마자 어떤 상점을 보여줄지 정해줘야 한다(이후 상점 내부 이동은 ShopUI 자신이 처리).
            // 패널을 재사용하게 되어도 이 호출은 열 때마다 다시 일어나므로 항상 기본 상점으로 리셋된다.
            var shopUI = instance.GetComponent<ShopUI>();
            if (shopUI != null && defaultShop != null) shopUI.Open(defaultShop);
        }

        /// <summary>
        /// prefab과 짝지어진 HudEntry.PanelKey를 찾는다. hudEntries에 없거나 PanelKey가 비어있으면
        /// prefab.name을 그대로 반환한다(OnPanelOpened 이벤트 인자로 쓰기 위함).
        /// </summary>
        private string ResolvePanelKey(GameObject prefab)
        {
            foreach (var entry in hudEntries)
            {
                if (entry != null && entry.prefab == prefab)
                {
                    return string.IsNullOrEmpty(entry.PanelKey) ? prefab.name : entry.PanelKey;
                }
            }
            return prefab.name;
        }

        /// <summary>
        /// prefabToInstance에서 미리 만들어둔 인스턴스를 찾는다. hudEntries에 등록되지 않은 프리팹으로
        /// 예외적으로 호출되는 경우(외부 호출 등)에 대비한 안전장치로, 캐시에 없으면 그 자리에서 즉석
        /// 생성해서 캐시에 추가한다 - 이후에는 그 인스턴스도 동일하게 재사용된다.
        /// </summary>
        private GameObject ResolvePanelInstance(GameObject prefab)
        {
            if (prefabToInstance.TryGetValue(prefab, out GameObject existing) && existing != null)
            {
                return existing;
            }

            Debug.LogWarning(
                $"[UIManager] '{prefab.name}'이 hudEntries에 등록되지 않아 즉석으로 미리 생성합니다(이후에는 재사용됩니다).",
                this);

            var instance = CreatePanelInstance(prefab);
            prefabToInstance[prefab] = instance;
            return instance;
        }

        /// <summary>
        /// 지금 열려있는 UI(스택 맨 위)를 닫는다. 프리팹 내부 닫기(X) 버튼이 이 메서드를 호출해야 한다.
        /// 업그레이드 팝업이 열려있으면 상위 UI보다 먼저 닫는다(팝업은 상위 UI의 자식이 아니라 별개의 씬
        /// 상시 배치 싱글톤이라, 상위 UI를 닫아도 자동으로 같이 닫히지 않기 때문).
        /// [버그 수정 - 패널 재사용] 예전에는 여기서 Destroy했지만, 이제는 SetActive(false)만 한다 -
        /// 인스턴스는 prefabToInstance에 계속 남아 다음에 열릴 때 재사용된다.
        /// </summary>
        public void CloseCurrent()
        {
            UpgradePopupUI.Instance?.Hide();

            if (openStack.Count == 0) return;

            var top = openStack.Pop();
            if (top != null)
            {
                top.SetActive(false);
            }

            currentPrefabKey = null;

            NotifyPanelFullyClosed();
        }

        /// <summary>
        /// [버그 수정] HUD 패널이 완전히 닫혔을 때 _Kyusoo PanelManager의 공통 HUD 상태(닫기/배치 버튼
        /// 그룹, 카메라 컨트롤러 등)를 원래대로 되돌린다. PanelManager.cs는 크로스팀 코드라 직접 수정하지
        /// 않고, PanelManager가 이미 가지고 있는 CloseAllPanels()를 그대로 호출해서 우회한다.
        /// PanelManager.CloseAllPanels()는 내부에서 다시 UIManager.Instance.CloseCurrent()를 호출하지만,
        /// 이 시점엔 이미 openStack이 비어있는 상태라 그 재호출은 곧바로 return되어 무한 재귀로 이어지지
        /// 않는다. PanelManager가 없는 씬에서는 Instance가 null이라 아무 동작도 하지 않는다.
        /// </summary>
        private void NotifyPanelFullyClosed()
        {
            PanelManager.Instance?.CloseAllPanels();
        }

        /// <summary>
        /// ManagedPanelCloseWatcher가 OnDisable에서 호출한다. SceneUIManager가 ESC로 패널을
        /// SetActive(false)만 해서 스택 상태와 실제 활성 여부가 어긋난 경우에만 동기화를 수행한다.
        /// 이미 CloseCurrent()가 스택에서 빼낸 상태라면 Peek이 일치하지 않으므로 무시한다.
        /// [버그 수정 - 패널 재사용] 예전에는 여기서도 Destroy했지만, 이제는 스택/키 동기화만 하고
        /// 인스턴스는 그대로 둔다(SetActive(false) 상태로 재사용 대기).
        /// </summary>
        private void HandleManagedPanelDisabled(GameObject panelInstance)
        {
            if (openStack.Count == 0 || openStack.Peek() != panelInstance) return;

            openStack.Pop();
            currentPrefabKey = null;

            NotifyPanelFullyClosed();
        }

        /// <summary>
        /// 현재 UIManager에 가동중이 프리팹 패널이 존재하는지 파악
        /// </summary>
        public bool HasActivePanel()
        {
            Debug.Log($"패널 열림확인 { openStack.Count > 0 && currentPrefabKey != null}");
            return openStack.Count > 0 && currentPrefabKey != null;
        }

        /// <summary>_GH SceneUIManager의 private managedUIObjects(List&lt;GameObject&gt;) 필드를 리플렉션으로 가져온다.</summary>
        private static List<GameObject> ResolveSceneUIManagerManagedList()
        {
            var manager = SceneUIManager.Instance;
            if (manager == null) return null;

            if (sceneUIManagerManagedObjectsField == null)
            {
                sceneUIManagerManagedObjectsField = typeof(SceneUIManager).GetField(
                    "managedUIObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return sceneUIManagerManagedObjectsField?.GetValue(manager) as List<GameObject>;
        }

        /// <summary>
        /// HUD 패널 인스턴스를 SceneUIManager의 ESC 관리 대상에 등록한다(SceneUIManager가 없으면 아무
        /// 동작 안 함). [버그 수정 - 패널 재사용] 패널이 더 이상 Destroy되지 않으므로, 이 등록은 인스턴스
        /// 생성 시점(CreatePanelInstance)에 한 번만 호출되고 이후로는 다시 호출되지 않는다 - 등록 해제도
        /// 더 이상 필요 없다.
        /// </summary>
        private static void RegisterWithSceneUIManager(GameObject uiInstance)
        {
            var list = ResolveSceneUIManagerManagedList();
            if (list != null && uiInstance != null && !list.Contains(uiInstance))
            {
                list.Add(uiInstance);
            }
        }
    }
}
