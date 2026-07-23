using System;
using System.Collections;
using KMS.InventoryDuped;
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
            DeathPosition
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
        [SerializeField] private Animator animator;

        [Header("Respawn")]
        [SerializeField] private RespawnLocationMode respawnLocationMode = RespawnLocationMode.DeathPosition;
        [SerializeField, Range(0.01f, 1f)] private float respawnHealthPercent = 1f;
        [SerializeField, Min(0f)] private float respawnInvulnerabilityDuration = 2f;

        public bool IsDead { get; private set; }
        public event Action Respawned;

        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int ReviveHash = Animator.StringToHash("Revive");

        private Vector3 deathPosition;
        private bool previousMovementEnabled = true;
        private bool previousGameplayInputBlocked;
        private bool previousCursorReleased;
        private Coroutine invulnerabilityCoroutine;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (stats != null) stats.Died += HandleDied;
            if (hud != null) hud.RespawnRequested += Respawn;
        }

        private void Start()
        {
            // 저장 데이터 복원 등으로 이미 HP가 0인 상태에서 시작한 경우도 동일한 흐름으로 처리한다.
            if (stats != null && !stats.IsAlive) HandleDied();
        }

        private void OnDisable()
        {
            if (stats != null) stats.Died -= HandleDied;
            if (hud != null) hud.RespawnRequested -= Respawn;

            if (invulnerabilityCoroutine != null)
            {
                StopCoroutine(invulnerabilityCoroutine);
                invulnerabilityCoroutine = null;
            }

            if (stats != null) stats.SetInvulnerable(false);
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
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            else if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void HandleDied()
        {
            if (IsDead) return;
            IsDead = true;
            deathPosition = transform.position;

            // 커서에 들고 있던 아이템을 먼저 인벤토리로 반환해야 사망 손실 판정에서 빠지지 않는다.
            InventoryUI inventoryUi = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (inventoryUi != null && inventoryUi.playerInventory == inventory) inventoryUi.Close();

            // 투척 예약을 취소하고 아이템을 돌려놓은 뒤 전체 손실을 적용한다.
            capsuleThrowController?.CancelActiveThrow();

            previousMovementEnabled = movement == null || movement.IsMovementEnabled;
            previousGameplayInputBlocked = input != null && input.IsGameplayInputBlocked;
            previousCursorReleased = input != null && input.IsCursorReleased;

            if (movement != null)
            {
                movement.IsMovementEnabled = false;
                movement.SetDead(true);
            }

            if (input != null)
            {
                input.SetGameplayInputBlocked(true);
                input.SetCursorReleased(true);
            }

            if (cameraController != null)
            {
                cameraController.SetAimZoom(false);
                cameraController.SetCursorLocked(false);
            }

            int lostAmount = inventory != null ? inventory.ApplyDeathPenalty() : 0;
            Debug.Log($"[PlayerDeath] 사망 처리 완료. 손실 수량={lostAmount}, 위치={deathPosition}", this);

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
                movement.IsMovementEnabled = previousMovementEnabled;
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

            if (input != null)
            {
                input.SetCursorReleased(previousCursorReleased);
                input.SetGameplayInputBlocked(previousGameplayInputBlocked);
            }

            if (cameraController != null)
            {
                cameraController.SetCursorLocked(!previousCursorReleased);
            }

            if (invulnerabilityCoroutine != null) StopCoroutine(invulnerabilityCoroutine);
            invulnerabilityCoroutine = StartCoroutine(ApplyRespawnInvulnerability());
            Respawned?.Invoke();
        }

        private Vector3 ResolveRespawnPosition()
        {
            switch (respawnLocationMode)
            {
                case RespawnLocationMode.DeathPosition:
                default:
                    return deathPosition;
            }
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
