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
    /// [저장/불러오기 대응] 저장 및 불러오기 시스템이 이 데이터의 생명주기(씬 로드 시 재생성/파일에서 값 복원)를
    /// 직접 관리할 예정이라, 더 이상 DontDestroyOnLoad 파괴불가 싱글톤을 쓰지 않는다(일반 컴포넌트).
    /// 다른 스크립트는 인스펙터에서 이 컴포넌트를 직접 참조해서 사용한다.
    /// [교통정리] HDY 폴더 소속. Pikachu 코드와 겹치는 기능 없음.
    /// </summary>
    public class TerritoryData : MonoBehaviour
    {
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
        // 시간 (인게임 시간, 누적 초 단위로 관리)
        // =================================================================
        [Header("인게임 시간")]
        [Tooltip("게임 시작 이후 누적된 인게임 시간(초)")]
        [SerializeField] private float elapsedTime = 0f;

        public float ElapsedTime => elapsedTime;

        /// <summary>인게임 시간을 seconds만큼 누적시킨다.</summary>
        public void AddTime(float seconds)
        {
            if (seconds <= 0) return;
            elapsedTime += seconds;
        }
    }
}
