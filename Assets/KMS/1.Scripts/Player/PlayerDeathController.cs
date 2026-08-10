using System;
using System.Collections;
using GH.Loading;
using KMS.InventoryDuped;
using KMS.Harvesting;
using KMS.Audio;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// PlayerStats의 사망 이벤트를 실제 게임플레이 사망/리스폰 흐름으로 연결한다.
    /// 현재는 사망 위치에서 부활하며, 이후 체크포인트 정책을 추가할 수 있도록 위치 결정을 분리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(PlayerMovement), typeof(PlayerInventory))]
    public sealed class PlayerDeathController : MonoBehaviour
    {
        private enum RespawnLocationMode
        {
            NearestActiveWayPoint
            // TODO: 기획 확정 후 Checkpoint, SceneSpawnPoint 등의 정책을 추가한다.
        }

        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerHUD hud;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private PlayerCapsuleThrowController capsuleThrowController;
        [SerializeField] private PlayerConsumableController consumableController;
        [SerializeField] private PlayerHarvestController harvestController;
        [SerializeField] private PlayerToolAnimationController toolAnimationController;
        [SerializeField] private KMSMemDexLauncher memDexLauncher;
        [SerializeField] private Animator animator;

        [Header("Respawn")]
        [SerializeField] private RespawnLocationMode respawnLocationMode = RespawnLocationMode.NearestActiveWayPoint;
        [SerializeField, Range(0.01f, 1f)] private float respawnHealthPercent = 1f;
        [SerializeField, Min(0f)] private float respawnInvulnerabilityDuration = 2f;

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float musicDuckFadeDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float deathMusicMultiplier = 1f / 3f;

        public bool IsDead { get; private set; }
        public event Action Respawned;

        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int ReviveHash = Animator.StringToHash("Revive");

        private Vector3 deathPosition;
        private Vector3 respawnPosition;
        private Vector3 sceneSpawnPosition;
        private Coroutine invulnerabilityCoroutine;
        private LoadingManager subscribedLoadingManager;
        private bool hasActiveRespawnWayPoint;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            sceneSpawnPosition = transform.position;
        }

        private void OnEnable()
        {
            if (stats != null) stats.Died += HandleDied;
            if (hud != null) hud.RespawnRequested += Respawn;
            BindLoadingManager();
        }

        private void Start()
        {
            // Scene objects have their authored spawn position by Start. If the loading
            // flow moves the player afterward, HandleLoadingCompleted records that actual
            // scene-entry position instead.
            sceneSpawnPosition = transform.position;
            BindLoadingManager();

            // 저장 데이터 복원 등으로 이미 HP가 0인 상태에서 시작한 경우도 동일한 흐름으로 처리한다.
            if (stats != null && !stats.IsAlive) HandleDied();
        }

        private void OnDisable()
        {
            if (stats != null) stats.Died -= HandleDied;
            if (hud != null) hud.RespawnRequested -= Respawn;
            UnbindLoadingManager();

            if (invulnerabilityCoroutine != null)
            {
                StopCoroutine(invulnerabilityCoroutine);
                invulnerabilityCoroutine = null;
            }

            if (stats != null) stats.SetInvulnerable(false);
            if (IsDead)
            {
                KMSAudioService.SetTemporaryMusicDuck(1f, musicDuckFadeDuration);
            }
        }

        private void BindLoadingManager()
        {
            LoadingManager manager = LoadingManager.Instance;
            if (subscribedLoadingManager == manager) return;

            UnbindLoadingManager();
            subscribedLoadingManager = manager;
            if (subscribedLoadingManager != null)
            {
                subscribedLoadingManager.LoadingCompleted += HandleLoadingCompleted;
            }
        }

        private void UnbindLoadingManager()
        {
            if (subscribedLoadingManager != null)
            {
                subscribedLoadingManager.LoadingCompleted -= HandleLoadingCompleted;
                subscribedLoadingManager = null;
            }
        }

        private void HandleLoadingCompleted()
        {
            // Raised after LoadingManager places the player at the destination waypoint.
            sceneSpawnPosition = transform.position;
        }

        private void ResolveReferences()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (input == null) input = GetComponent<PlayerInput>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (hud == null) hud = GetComponent<PlayerHUD>();
            if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
            if (capsuleThrowController == null) capsuleThrowController = GetComponent<PlayerCapsuleThrowController>();
            if (consumableController == null) consumableController = GetComponent<PlayerConsumableController>();
            if (harvestController == null) harvestController = GetComponent<PlayerHarvestController>();
            if (toolAnimationController == null) toolAnimationController = GetComponent<PlayerToolAnimationController>();
            if (memDexLauncher == null) memDexLauncher = GetComponent<KMSMemDexLauncher>();
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            else if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void HandleDied()
        {
            if (IsDead) return;
            IsDead = true;
            deathPosition = transform.position;
            respawnPosition = ResolveNearestActiveWayPointPosition(
                deathPosition,
                out hasActiveRespawnWayPoint);

            // Closing the map clears both its input request and the source-stone
            // authorization used by territory travel before death takes ownership.
            WayPointManager.Instance?.CloseMap();
            memDexLauncher?.Close();

            // 커서에 들고 있던 아이템을 먼저 인벤토리로 반환해야 사망 손실 판정에서 빠지지 않는다.
            InventoryUI inventoryUi = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (inventoryUi != null && inventoryUi.playerInventory == inventory) inventoryUi.Close();

            // 투척 예약을 취소하고 아이템을 돌려놓은 뒤 전체 손실을 적용한다.
            capsuleThrowController?.CancelActiveThrow();
            consumableController?.CancelPendingConsume();
            harvestController?.CancelActiveToolUse();
            toolAnimationController?.CancelToolAction();

            if (movement != null)
            {
                movement.SetMovementBlocked(this, true);
                movement.SetDead(true);
            }

            if (input != null)
            {
                input.SetDeathInputBlocked(true);
                input.SetDeathCursorReleased(true);
            }

            if (cameraController != null)
            {
                cameraController.SetAimZoom(false);
                cameraController.SetCursorLocked(false);
            }

            int lostAmount = inventory != null ? inventory.ApplyDeathPenalty() : 0;
            Debug.Log($"[PlayerDeath] 사망 처리 완료. 손실 수량={lostAmount}, 위치={deathPosition}", this);

            KMSAudioService.SetTemporaryMusicDuck(
                deathMusicMultiplier,
                musicDuckFadeDuration);
            hud?.ShowDeathPresentation(hasActiveRespawnWayPoint);

            if (animator != null)
            {
                animator.ResetTrigger(ReviveHash);
                animator.SetTrigger(DeathHash);
            }
        }

        public void Respawn()
        {
            if (!IsDead || stats == null) return;

            Vector3 respawnPosition = ResolveRespawnPosition();
            if (movement != null)
            {
                movement.SetPosition(respawnPosition);
                movement.SetDead(false);
                movement.ResetMovementForces();
                movement.SetMovementBlocked(this, false);
            }
            else
            {
                transform.position = respawnPosition;
            }

            if (animator != null)
            {
                animator.ResetTrigger(DeathHash);
                animator.SetTrigger(ReviveHash);
            }

            IsDead = false;
            stats.Revive(respawnHealthPercent);
            KMSAudioService.SetTemporaryMusicDuck(1f, musicDuckFadeDuration);

            if (input != null)
            {
                input.SetDeathCursorReleased(false);
                input.SetDeathInputBlocked(false);
            }

            if (invulnerabilityCoroutine != null) StopCoroutine(invulnerabilityCoroutine);
            invulnerabilityCoroutine = StartCoroutine(ApplyRespawnInvulnerability());
            Respawned?.Invoke();
        }

        private Vector3 ResolveRespawnPosition()
        {
            switch (respawnLocationMode)
            {
                case RespawnLocationMode.NearestActiveWayPoint:
                    return respawnPosition;
                default:
                    return deathPosition;
            }
        }

        private Vector3 ResolveNearestActiveWayPointPosition(
            Vector3 origin,
            out bool foundActiveWayPoint)
        {
            WayPointManager manager = WayPointManager.Instance;
            foundActiveWayPoint = false;
            if (manager == null) return sceneSpawnPosition;

            bool found = false;
            float nearestSqrDistance = float.PositiveInfinity;
            Vector3 nearestPosition = sceneSpawnPosition;

            foreach (WayPointRunTime state in manager.GetAllStates())
            {
                if (state == null || !state.IsActive || state.Stone == null) continue;
                if (state.Stone.gameObject.scene != gameObject.scene) continue;

                Vector3 candidate = state.Stone.SpawnPosition;
                float sqrDistance = (candidate - origin).sqrMagnitude;
                if (found && sqrDistance >= nearestSqrDistance) continue;

                found = true;
                nearestSqrDistance = sqrDistance;
                nearestPosition = candidate;
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[PlayerDeath] No active waypoint stone exists in the current scene. Falling back to the scene spawn position.",
                    this);
            }

            foundActiveWayPoint = found;
            return nearestPosition;
        }

        private IEnumerator ApplyRespawnInvulnerability()
        {
            stats.SetInvulnerable(true);
            yield return new WaitForSeconds(respawnInvulnerabilityDuration);
            stats.SetInvulnerable(false);
            invulnerabilityCoroutine = null;
        }
    }
}
