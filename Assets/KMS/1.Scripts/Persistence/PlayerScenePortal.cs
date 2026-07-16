using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.Persistence
{
    [RequireComponent(typeof(Collider))]
    public class PlayerScenePortal : MonoBehaviour
    {
        [SerializeField] private string targetSceneName;
        private bool isLoading;

        private void Reset()
        {
            Collider portalCollider = GetComponent<Collider>();
            portalCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isLoading) return;

            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null) return;

            PlayerStats stats = inventory.GetComponent<PlayerStats>();
            if (stats == null)
            {
                Debug.LogWarning("[PlayerScenePortal] 플레이어에 PlayerStats가 없어 씬을 전환하지 않습니다.", inventory);
                return;
            }

            if (string.IsNullOrWhiteSpace(targetSceneName) || !Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError($"[PlayerScenePortal] 빌드 설정에서 대상 씬을 찾을 수 없습니다: '{targetSceneName}'", this);
                return;
            }

            isLoading = true;
            PlayerPersistenceManager.EnsureInstance().Capture(inventory, stats);
            Debug.Log($"[PlayerScenePortal] 씬 전환: {SceneManager.GetActiveScene().name} -> {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
