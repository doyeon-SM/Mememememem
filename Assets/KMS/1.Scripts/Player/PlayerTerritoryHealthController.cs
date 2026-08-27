using HDY.Territory;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS
{
    // PlayerHUD (default order) ensures the shared TerritoryData exists first,
    // while this still runs before sceneLoaded save restoration callbacks.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerTerritoryHealthController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private TerritoryData territoryData;
        [SerializeField] private KMSTerritoryHealthTable healthTable;
        [SerializeField] private PlayerCombatStats combatStats; // [멤] 힘 스탯 기반 체력 배율 적용을 위한 참조

        private TerritoryData subscribedTerritoryData;
        private bool loggedMissingTable;

        public int TerritoryLevel => territoryData != null ? territoryData.Level : 1;

        private void Reset()
        {
            ResolveStats();
            ResolveCombatStats();
        }

        private void Awake()
        {
            ResolveStats();
            ResolveCombatStats();
            ResolveTerritoryData();
            ApplyCurrentTerritoryLevel();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RebindTerritoryData();
        }

        private void Start()
        {
            // Retry after every component has completed Awake. PlayerHUD may create
            // the shared TerritoryData object while the scene is initializing.
            RebindTerritoryData();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeTerritoryData();
        }

        public void ApplyTerritoryLevel(int level)
        {
            ResolveStats();
            if (stats == null) return;

            if (healthTable == null)
            {
                if (!loggedMissingTable)
                {
                    Debug.LogWarning(
                        "[PlayerTerritoryHealthController] Territory health table is not assigned.",
                        this);
                    loggedMissingTable = true;
                }

                return;
            }

            loggedMissingTable = false;

            // [멤] 힘 스탯 배율(CharacterStatFormulas.HealthMultiplier)을 영지레벨 기반 체력에 곱연산으로 적용한다.
            float baseHealth = healthTable.GetMaxHealth(level);
            float multiplier = combatStats != null ? combatStats.GetHealthMultiplier() : 1f;
            stats.SetMaxHealth(baseHealth * multiplier);
        }

        private void HandleTerritoryLevelChanged(int level)
        {
            ApplyTerritoryLevel(level);
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            RebindTerritoryData();
        }

        private void RebindTerritoryData()
        {
            ResolveTerritoryData();

            if (subscribedTerritoryData != territoryData)
            {
                UnsubscribeTerritoryData();
                subscribedTerritoryData = territoryData;

                if (subscribedTerritoryData != null)
                {
                    subscribedTerritoryData.OnLevelChanged += HandleTerritoryLevelChanged;
                }
            }

            ApplyCurrentTerritoryLevel();
        }

        private void ApplyCurrentTerritoryLevel()
        {
            if (territoryData != null)
            {
                ApplyTerritoryLevel(territoryData.Level);
            }
        }

        private void ResolveStats()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
        }

        private void ResolveCombatStats()
        {
            if (combatStats == null) combatStats = GetComponent<PlayerCombatStats>();
        }

        // [멤] PlayerCombatStats에서 스탯이 변경되었을 때(레벨업, 포인트 투자, 리스펙) 호출되어 체력을 재계산한다.
        public void RefreshFromExternalStatChange()
        {
            ApplyCurrentTerritoryLevel();
        }

        private void ResolveTerritoryData()
        {
            territoryData = TerritoryData.Resolve(territoryData);
        }

        private void UnsubscribeTerritoryData()
        {
            if (subscribedTerritoryData != null)
            {
                subscribedTerritoryData.OnLevelChanged -= HandleTerritoryLevelChanged;
            }

            subscribedTerritoryData = null;
        }
    }
}
