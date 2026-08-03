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

        private TerritoryData subscribedTerritoryData;
        private bool loggedMissingTable;

        public int TerritoryLevel => territoryData != null ? territoryData.Level : 1;

        private void Reset()
        {
            ResolveStats();
        }

        private void Awake()
        {
            ResolveStats();
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
            stats.SetMaxHealth(healthTable.GetMaxHealth(level));
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
