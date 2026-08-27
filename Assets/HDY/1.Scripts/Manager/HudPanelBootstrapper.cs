using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.UI
{
    /// <summary>
    /// (멤) 영지 HUD 패널 프리팹들을 씬 시작 시 미리 Instantiate해서 캐싱하고, SceneUIManager의
    /// 관리 대상(Managed UI)으로 정식 등록하는 부트스트래퍼입니다.
    ///
    /// [예전 UIManager와의 차이] 예전 HDY.UI.UIManager는 "프리팹 사전 생성/캐싱" + "한 번에 하나만 열림
    /// (스택)" + "레벨 게이팅" + "상점 기본값" + "튜토리얼 이벤트"를 전부 한 클래스가 떠안고, 심지어
    /// SceneUIManager의 private 필드에 리플렉션으로 직접 등록하는 방식이었습니다. 이 클래스는 그중
    /// "프리팹 사전 생성/캐싱 + 정식 등록"만 담당합니다. 나머지는 전부 SceneUIManager(열기/닫기, 배타
    /// 처리, ESC)와 각 패널 스크립트 자신(IManagedUIPanel 구현 - 예: 상점의 기본값 리셋)의 몫입니다.
    ///
    /// SceneUIManager는 [DefaultExecutionOrder(-1000)]이라 이 컴포넌트의 Awake보다 먼저 Instance가
    /// 준비되므로, 여기서 곧바로 SceneUIManager.TryRegisterManagedUI를 호출해도 안전합니다.
    /// </summary>
    public class HudPanelBootstrapper : MonoBehaviour
    {
        /// <summary>HUD 패널 하나(등록할 Managed UI ID와 그 패널 프리팹)를 짝짓는 항목.</summary>
        [Serializable]
        private class HudEntry
        {
            [Tooltip("SceneUIManager에 등록할 Managed UI ID (예: Shop, MemDex, Storage, Forge, Goddess 등).")]
            public string id;

            public GameObject prefab;
        }

        [Tooltip("HUD 패널 프리팹이 배치될 부모(P_UIRoot). 이 밑에 로컬 좌표 (0,0,0)으로 미리 Instantiate됩니다.")]
        [SerializeField] private Transform uiRoot;

        [Header("Managed UI ID <-> 프리팹 연결")]
        [SerializeField] private List<HudEntry> hudEntries = new List<HudEntry>();

        private readonly Dictionary<string, GameObject> idToInstance =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        /// <summary>UI 프리팹이 배치되는 부모(P_UIRoot). 튜토리얼 등 다른 시스템이 패널을 심을 때 사용.</summary>
        public Transform UIRoot => uiRoot;

        /// <summary>(멤) 다른 시스템(튜토리얼 등)이 UIRoot에 접근할 수 있도록 하는 싱글턴 참조.</summary>
        public static HudPanelBootstrapper Instance { get; private set; }


        private void Awake()
        {
            Instance = this;
            if (uiRoot == null)
            {
                Debug.LogWarning("[HudPanelBootstrapper] uiRoot가 비어있습니다. UI를 어디에 배치할지 알 수 없습니다.", this);
                return;
            }

            foreach (var entry in hudEntries)
            {
                if (entry == null || entry.prefab == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                if (idToInstance.ContainsKey(entry.id)) continue;

                GameObject instance = CreatePanelInstance(entry.prefab);
                idToInstance[entry.id] = instance;

                if (!SceneUIManager.TryRegisterManagedUI(entry.id, instance))
                {
                    Debug.LogWarning(
                        $"[HudPanelBootstrapper] '{entry.id}' 등록 실패 - 씬에 SceneUIManager가 없습니다.",
                        instance);
                }
            }
        }

        /// <summary>prefab을 uiRoot 아래 Instantiate하고 로컬 좌표를 (0,0,0)으로 초기화한 뒤 SetActive(false)로 숨긴다.</summary>
        private GameObject CreatePanelInstance(GameObject prefab)
        {
            GameObject instance = Instantiate(prefab, uiRoot);
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            instance.SetActive(false);
            return instance;
        }

        /// <summary>ID로 이미 생성해둔 패널 인스턴스를 찾는다(등록되지 않은 ID면 null).</summary>
        public GameObject GetInstance(string id)
        {
            return idToInstance.TryGetValue(id, out GameObject instance) ? instance : null;
        }
    }
}
