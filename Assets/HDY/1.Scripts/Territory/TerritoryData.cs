using System;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Territory
{
    /// <summary>
    /// 밥통에 아이템 단위로 보관되는 음식 하나.
    /// 소비도 아이템 단위로 이루어진다.
    /// </summary>
    [Serializable]
    public class FoodStorageEntry
    {
        public string Item_ID;
        public int Quantity;
    }

    /// <summary>
    /// 영지에 필요한 기초 데이터를 보관하는 컴포넌트.
    /// [임시 조치 - 싱글톤 + DontDestroyOnLoad] 저장/불러오기 시스템이 아직 없어서, 그 시스템이 붙기 전까지
    /// GameTimeManager와 동일한 패턴(Instance 싱글톤 + DontDestroyOnLoad + Resolve(existing) 폴백)을 임시로
    /// 사용한다. TODO: 추후 저장/불러오기 시스템이 추가되면 이 생명주기 관리 방식(싱글톤 유지 여부, 씬 로드
    /// 시 값 복원 시점 등)을 다시 검토해야 한다.
    /// 다른 스크립트는 인스펙터 참조가 비어있으면 Resolve(existing)로 이 컴포넌트를 찾아 쓸 수 있다.
    /// [교통정리] HDY 폴더 소속. Pikachu 코드와 겹치는 기능 없음.
    /// </summary>
    public class TerritoryData : MonoBehaviour
    {
        public static TerritoryData Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TerritoryData] 씬에 TerritoryData가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 TerritoryData 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// 1) 이미 참조가 있으면 그대로 반환, 2) 없으면 싱글톤(Instance), 3) 그래도 없으면 씬 전체에서 검색.
        /// </summary>
        public static TerritoryData Resolve(TerritoryData existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<TerritoryData>();
            if (found == null)
            {
                Debug.LogWarning("[TerritoryData] 씬에서 TerritoryData를 찾을 수 없습니다.");
            }

            return found;
        }

        // =================================================================
        // 만족도 (= 영지 포인트)
        // =================================================================
        [Header("만족도 (영지 포인트)")]
        [Tooltip("영지 만족도. 최소값 0 이하로는 내려가지 않음")]
        [SerializeField] private int satisfaction = 0;
        [SerializeField] private int minSatisfaction = 0;

        public int Satisfaction => satisfaction;

        /// <summary>만족도를 amount만큼 더하거나 뺀다. 0 미만으로는 내려가지 않음.</summary>
        public void AddSatisfaction(int amount)
        {
            satisfaction = Mathf.Max(satisfaction + amount, minSatisfaction);
        }

        // =================================================================
        // 영지 경험치 및 레벨
        // =================================================================
        [Header("영지 경험치 및 레벨")]
        [SerializeField] private int level = 1;
        [SerializeField] private int currentExp = 0;
        [Tooltip("다음 레벨업에 필요한 경험치. 계산식은 임시값이며 기획 확정 후 교체 예정")]
        [SerializeField] private List<int> requiredExp;

        public int Level => level;
        public int CurrentExp => currentExp;
        public int RequiredExp => requiredExp[level-1];

        /// <summary>
        /// 영지 경험치를 획득한다. requiredExp를 넘기면 자동으로 레벨업 처리.
        /// TODO: 레벨업 시 requiredExp 증가 공식은 임시값(x1.2). 기획 확정 후 교체 필요.
        /// </summary>
        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            if (requiredExp[level - 1] <= 0) requiredExp[level - 1] = 10000;

            currentExp += amount;

            while (currentExp >= requiredExp[level-1])
            {                
                currentExp -= requiredExp[level-1];
                level++;
                //requiredExp = Mathf.RoundToInt(requiredExp * 1.2f);
                Debug.Log($"[TerritoryData] 영지 레벨업! 현재 레벨: {level}");
            }
        }

        // =================================================================
        // 밥통 (멤의 허기를 채워줄 음식 저장소)
        // 아이템(Item_ID) 단위로 보관하며, 전체 포만감은 보관된 아이템들의 포만감 수치를 합산해 계산한다.
        // 소비 역시 아이템 단위로 이루어진다.
        // 현재 ItemData에는 포만감 수치가 아직 없어 GetTotalSatiety()는 임시로 0을 반환한다.
        // TODO: ItemData에 포만감 필드가 추가되면 ItemCatalogManager로 Item_ID를 조회해 합산하도록 교체.
        // =================================================================
        [Header("밥통 (음식 저장소)")]
        [SerializeField] private List<FoodStorageEntry> foodStorage = new List<FoodStorageEntry>();

        public IReadOnlyList<FoodStorageEntry> FoodStorage => foodStorage;

        /// <summary>밥통에 음식 아이템을 추가한다. 이미 있는 Item_ID면 수량만 더한다.</summary>
        public void AddFood(string itemId, int quantity)
        {
            if (quantity <= 0) return;

            var entry = foodStorage.Find(e => e.Item_ID == itemId);
            if (entry != null)
            {
                entry.Quantity += quantity;
            }
            else
            {
                foodStorage.Add(new FoodStorageEntry { Item_ID = itemId, Quantity = quantity });
            }
        }

        /// <summary>밥통에서 음식 아이템을 소비한다. 수량이 부족하면 false를 반환하고 아무 것도 소비하지 않는다.</summary>
        public bool TryConsumeFood(string itemId, int quantity)
        {
            if (quantity <= 0) return false;

            var entry = foodStorage.Find(e => e.Item_ID == itemId);
            if (entry == null || entry.Quantity < quantity) return false;

            entry.Quantity -= quantity;
            if (entry.Quantity <= 0)
            {
                foodStorage.Remove(entry);
            }
            return true;
        }

        /// <summary>밥통에 보관된 아이템들의 포만감을 합산한다. (ItemData 포만감 필드 추가 전까지 임시 0 반환)</summary>
        public int GetTotalSatiety()
        {
            return 0;
        }

        // =================================================================
        // 보유 골드
        // =================================================================
        [Header("골드")]
        [SerializeField] private int gold = 0;
        public int Gold => gold;

        /// <summary>골드를 amount만큼 더한다 (음수 입력 시 감소, 0 미만으로는 내려가지 않음).</summary>
        public void AddGold(int amount)
        {
            gold = Mathf.Max(0, gold + amount);
        }

        /// <summary>골드가 충분하면 소비 처리하고 true를 반환한다.</summary>
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || gold < amount) return false;
            gold -= amount;
            return true;
        }

        // =================================================================
        // 시간 (인게임 시간, 누적 초 단위 - 저장 호환용 다리)
        // [타이머 이주] 실제 시간 누적(타이머) 로직은 GameTimeManager로 이주했다. 이 필드는 스스로
        // 누적하지 않으며, GameTimeManager가 매 프레임 SyncElapsedTimeFromGameTimeManager로 최신 값을
        // 밀어넣어주는 "저장 호환용 거울"일 뿐이다. Kyusoo팀 RecordManager.cs가 저장 시
        // TerritoryData.ElapsedTime을 읽고, 불러오기 시 reflection으로 이 필드("elapsedTime")에 직접
        // 대입하기 때문에(Kyusoo 파일은 건드리지 않기로 함) 필드/프로퍼티 이름과 타입을 그대로 유지한다.
        // =================================================================
        [Header("인게임 시간 (저장 호환용 - 실제 누적은 GameTimeManager가 전담)")]
        [Tooltip("GameTimeManager가 매 프레임 동기화해주는 값. 이 컴포넌트 스스로는 더 이상 누적하지 않는다.")]
        [SerializeField] private float elapsedTime = 0f;

        public float ElapsedTime => elapsedTime;

        /// <summary>
        /// [저장 호환용 다리] GameTimeManager가 매 프레임 자신이 누적한 값을 이 필드에 동기화하기 위해
        /// 호출한다. Kyusoo팀 RecordManager가 저장 시 여전히 TerritoryData.ElapsedTime을 읽으므로,
        /// 실제 누적 주체는 GameTimeManager이지만 이 값도 항상 최신 상태로 맞춰둔다.
        /// 이 메서드 외에는 elapsedTime을 수정하는 경로가 없다(GameTimeManager.Update에서만 호출됨).
        /// </summary>
        public void SyncElapsedTimeFromGameTimeManager(float elapsedSeconds)
        {
            elapsedTime = elapsedSeconds;
        }
    }
}
