using System;
using System.Collections.Generic;
using HDY.Item;
using KMS.Audio;
using UnityEngine;

using KmsItemStack = KMS.InventoryDuped.ItemStack;
using KmsPlayerInventory = KMS.InventoryDuped.PlayerInventory;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 무기(Weapon 카테고리 아이템)를 들었을 때의 전투 컨트롤러.
    /// - 클릭(PrimaryAction): 장전 큐가 비어있으면 원거리 기본 공격, 큐에 스킬이 있으면 맨 앞
    ///   스킬을 소모해서 발동한다(선입선출, FIFO).
    /// - 우클릭 유지(SecondaryAction): 1초(stageDuration)에 한 단계씩 최대 4단계까지 장전을
    ///   평가한다. 등록되지 않았거나 쿨타임 중인 단계는 그 단계 자체가 저장되지는 않지만, 평가는
    ///   계속 다음 단계로 진행한다(그 뒤에 유효한 더 높은 단계가 있으면 그게 저장됨). 해제 시
    ///   마지막으로 유효했던 단계만 큐에 들어간다. 쿨타임 중인 스킬은 장전(큐 추가) 자체가
    ///   막힌다 - 미등록 취급과 동일하게 처리한다.
    /// - 이동속도 감속/행동 간 상호 배타 처리는 전부 PlayerActionSlotCoordinator에 위임한다
    ///   (공격 = Primary/Light(-30%), 장전 = Secondary/Heavy(-60%)). 두 슬롯은 서로 배타적이라
    ///   장전 중에는 클릭 공격이, 공격 중에는 장전이 자동으로 막힌다.
    ///
    /// [멤] 기본 공격(좌클릭)은 무기(WeaponItemData)마다 다른 ProjectilePrefab/ProjectileSpeed/ProjectileLifetime과
    /// 데미지(ProjectileDamage + PlayerStats.AttackPower)를 사용하고, 오브젝트 풀(ProjectilePool)로 재활용된다.
    /// 반면 큐에 있는 스킬을 발동할 때는 무기 것을 빌려쓰지 않고 SkillData 자신의 ProjectilePrefab/Speed/Lifetime과
    /// Damage/Cooldown을 사용한다 - "스킬은 어떤 무기를 쓰든 항상 동일한 효과"를 보장하기 위함이며, 발사 빈도가
    /// 낮아 풀링하지 않고 기존처럼 Instantiate/Destroy를 그대로 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerWeaponSkillController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KMS.PlayerInput input;
        [SerializeField] private KMS.PlayerStats stats;
        [SerializeField] private KMS.PlayerMovement movement;
        [SerializeField] private KMS.PlayerActionSlotCoordinator actionCoordinator;
        [SerializeField] private KmsPlayerInventory inventory;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Animator animator;
        [SerializeField] private ItemCatalogManager catalogManager;
        [SerializeField] private SkillCatalogManager skillCatalogManager;
        [SerializeField] private PlayerSkillLoadout skillLoadout;

        [Header("Ranged Attack")]
        [SerializeField, Min(0f)] private float attackOriginHeight = 1.2f;
        [Tooltip("공격 한 번마다 Primary 슬롯을 점유해 이동속도를 감속시키는 시간(초). 실제 재사용 대기시간(쿨타임)과는 별개의 값이다.")]
        [SerializeField, Min(0.05f)] private float attackActionDuration = 0.3f;
        [Tooltip("원거리 공격이 데미지를 줄 대상 레이어. 비워두면(0) Awake에서 \"Monster\" 레이어를 자동으로 찾아 채운다.")]
        [SerializeField] private LayerMask monsterLayer;

        [Header("Charge / Queue")]
        [Tooltip("장전 한 단계가 완료되는데 걸리는 시간(초). 4단계까지 전부 채우려면 이 값의 4배가 필요하다.")]
        [SerializeField, Min(0.1f)] private float stageDuration = 1f;

        private static readonly int RangedAttackHash = Animator.StringToHash("RangedAttack");

        private readonly Queue<string> skillQueue = new Queue<string>();
        private readonly Dictionary<string, float> skillCooldownTimers = new Dictionary<string, float>();
        private readonly List<string> cooldownKeysScratch = new List<string>();

        private bool isCharging;
        private float chargeTimer;
        private int nextStageToEvaluate;
        private int bankedStageIndex = -1;
        private float basicAttackCooldownTimer;
        private float attackLockTimer;

        private const float LightSpeedMultiplierFallback = 0.7f;
        private const float HeavySpeedMultiplierFallback = 0.4f;

        public bool IsCharging => isCharging;
        public int BankedStageIndex => bankedStageIndex;
        public int QueuedSkillCount => skillQueue.Count;
        /// <summary>[HUD 연동용] 장전 중 지금까지 평가를 마친 단계 수(0~4). 4면 더 이상 진행할 단계가 없다.</summary>
        public int NextStageToEvaluate => nextStageToEvaluate;
        /// <summary>[HUD 연동용] 지금 진행 중인 단계의 진행률(0~1). 장전 중이 아닐 때도 마지막 값이 남아있을 수 있으므로 IsCharging과 함께 사용한다.</summary>
        public float CurrentStageProgress01 => stageDuration > 0f ? Mathf.Clamp01(chargeTimer / stageDuration) : 0f;

        /// <summary>장전 시작/종료(해제·취소 포함) 시 발행된다(true = 시작).</summary>
        public event Action<bool> OnChargingStateChanged;
        /// <summary>장전 단계가 하나 평가될 때마다 발행된다(0~3, 그 단계가 유효했는지 여부).</summary>
        public event Action<int, bool> OnChargeStageEvaluated;
        /// <summary>장전 해제로 큐에 스킬이 추가되면 발행된다(Skill_ID).</summary>
        public event Action<string> OnSkillQueued;
        /// <summary>큐에서 스킬이 발동(소모)되면 발행된다(Skill_ID).</summary>
        public event Action<string> OnSkillFired;
        /// <summary>큐가 비어있어 기본 원거리 공격이 나갔을 때 발행된다.</summary>
        public event Action OnBasicAttackFired;

        private void Reset()
        {
            input = GetComponent<KMS.PlayerInput>();
            stats = GetComponent<KMS.PlayerStats>();
            movement = GetComponent<KMS.PlayerMovement>();
            actionCoordinator = GetComponent<KMS.PlayerActionSlotCoordinator>();
            inventory = GetComponent<KmsPlayerInventory>();
            skillLoadout = GetComponent<PlayerSkillLoadout>();

            if (Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Awake()
        {
            if (input == null) input = GetComponent<KMS.PlayerInput>();
            if (stats == null) stats = GetComponent<KMS.PlayerStats>();
            if (movement == null) movement = GetComponent<KMS.PlayerMovement>();
            if (actionCoordinator == null) actionCoordinator = GetComponent<KMS.PlayerActionSlotCoordinator>();
            if (inventory == null) inventory = GetComponent<KmsPlayerInventory>();
            if (skillLoadout == null) skillLoadout = GetComponent<PlayerSkillLoadout>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (movement != null && movement.Animator != null) animator = movement.Animator;
            else if (animator == null) animator = GetComponentInChildren<Animator>();

            if (projectileOrigin == null && animator != null && animator.isHuman)
            {
                projectileOrigin = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            skillCatalogManager = SkillCatalogManager.Resolve(skillCatalogManager);

            if (monsterLayer.value == 0)
            {
                int layerIndex = LayerMask.NameToLayer("Monster");
                if (layerIndex >= 0)
                {
                    monsterLayer = 1 << layerIndex;
                }
                else
                {
                    Debug.LogWarning("[PlayerWeaponSkillController] \"Monster\" 레이어를 찾을 수 없습니다. 원거리 공격이 아무것도 맞추지 못합니다.", this);
                }
            }
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed += HandlePrimaryPressed;
                input.SecondaryActionPressed += BeginCharge;
                input.SecondaryActionReleased += ReleaseCharge;
                input.ReloadPressed += HandleSpecialSkillPressed;
            }

            if (inventory != null)
            {
                inventory.OnQuickSlotSelectionRequested += HandleQuickSlotSelectionRequested;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PrimaryActionPressed -= HandlePrimaryPressed;
                input.SecondaryActionPressed -= BeginCharge;
                input.SecondaryActionReleased -= ReleaseCharge;
                input.ReloadPressed -= HandleSpecialSkillPressed;
            }

            if (inventory != null)
            {
                inventory.OnQuickSlotSelectionRequested -= HandleQuickSlotSelectionRequested;
            }

            CancelCharge();

            if (attackLockTimer > 0f)
            {
                attackLockTimer = 0f;
                EndAttackLock();
            }
        }

        private void Update()
        {
            TickTimers();
        }

        private void TickTimers()
        {
            if (basicAttackCooldownTimer > 0f)
            {
                basicAttackCooldownTimer -= Time.deltaTime;
            }

            if (attackLockTimer > 0f)
            {
                attackLockTimer -= Time.deltaTime;
                if (attackLockTimer <= 0f)
                {
                    EndAttackLock();
                }
            }

            if (skillCooldownTimers.Count > 0)
            {
                cooldownKeysScratch.Clear();
                cooldownKeysScratch.AddRange(skillCooldownTimers.Keys);
                for (int i = 0; i < cooldownKeysScratch.Count; i++)
                {
                    string key = cooldownKeysScratch[i];
                    float remaining = skillCooldownTimers[key] - Time.deltaTime;
                    if (remaining <= 0f) skillCooldownTimers.Remove(key);
                    else skillCooldownTimers[key] = remaining;
                }
            }

            if (isCharging)
            {
                chargeTimer += Time.deltaTime;
                while (isCharging && nextStageToEvaluate < PlayerSkillLoadout.SlotCount && chargeTimer >= stageDuration)
                {
                    chargeTimer -= stageDuration;
                    EvaluateNextChargeStage();
                }
            }
        }

        // ---- Primary: 원거리 기본 공격 / 큐 발동 ----

        private void HandlePrimaryPressed()
        {
            if (stats != null && !stats.IsAlive) return;
            if (cameraTransform == null) return;
            if (!TryGetSelectedWeapon(out WeaponItemData weapon)) return;

            if (skillQueue.Count > 0)
            {
                FireQueuedSkill(weapon);
            }
            else
            {
                FireBasicAttack(weapon);
            }
        }

private void FireBasicAttack(WeaponItemData weapon)
        {
            if (basicAttackCooldownTimer > 0f) return;
            if (!LockPrimary()) return;

            basicAttackCooldownTimer = Mathf.Max(0f, weapon.ProjectileAttackCooldown);

            if (animator != null) animator.SetTrigger(RangedAttackHash);

            Vector3 origin = GetProjectileOrigin();
            Vector3 direction = cameraTransform.forward;
            int attackPowerBonus = stats != null ? stats.AttackPower : 0;
            int damage = Mathf.Max(0, weapon.ProjectileDamage + attackPowerBonus);
            FireBasicAttackProjectile(origin, direction, damage, weapon);

            KMSAudioService.PlayAt(GameSfxId.WeaponRangedAttack, origin);
            OnBasicAttackFired?.Invoke();
        }

private void FireQueuedSkill(WeaponItemData weapon)
        {
            if (skillQueue.Count == 0) return;

            string skillId = skillQueue.Peek();
            SkillData skill = skillCatalogManager != null ? skillCatalogManager.FindSkillData(skillId) : null;
            if (skill == null)
            {
                Debug.LogWarning($"[PlayerWeaponSkillController] 큐에 있는 Skill_ID({skillId})를 카탈로그에서 찾을 수 없어 큐에서 제거합니다.");
                skillQueue.Dequeue();
                return;
            }

            if (!LockPrimary()) return;

            skillQueue.Dequeue();

            if (animator != null) animator.SetTrigger(RangedAttackHash);

            Vector3 origin = GetProjectileOrigin();
            Vector3 direction = cameraTransform.forward;
            FireSkillProjectile(origin, direction, Mathf.Max(0, skill.Damage), skill);

            StartSkillCooldown(skill.Skill_ID, skill.Cooldown);
            KMSAudioService.PlayAt(GameSfxId.SkillFire, origin);
            OnSkillFired?.Invoke(skill.Skill_ID);
        }

/// <summary>
        /// [멤] 5등급 특수 스킬 발동(R키/Reload 액션). 장전 큐/충전 시스템과 무관하게 즉시 발동되는
        /// 즉발형 스킬이지만, 발동 메카니즘(무기 투사체 재사용/Primary 슬롯 점유/쿨타임)은
        /// FireQueuedSkill과 동일하다 - "칸과 발동 로직(R키, 즉발형)만 다르고 스킬 관리/규칙은
        /// 동일하다"는 요구사항을 그대로 반영한다.
        /// </summary>
private void HandleSpecialSkillPressed()
        {
            if (stats != null && !stats.IsAlive) return;
            if (cameraTransform == null) return;
            if (!TryGetSelectedWeapon(out WeaponItemData weapon)) return;
            if (skillLoadout == null) return;

            SkillData skill = skillLoadout.GetSpecialSkill();
            if (skill == null) return;
            if (IsSkillOnCooldown(skill.Skill_ID)) return;

            if (!LockPrimary()) return;

            if (animator != null) animator.SetTrigger(RangedAttackHash);

            Vector3 origin = GetProjectileOrigin();
            Vector3 direction = cameraTransform.forward;
            FireSkillProjectile(origin, direction, Mathf.Max(0, skill.Damage), skill);

            StartSkillCooldown(skill.Skill_ID, skill.Cooldown);
            KMSAudioService.PlayAt(GameSfxId.SkillFire, origin);
            OnSkillFired?.Invoke(skill.Skill_ID);
        }


        private bool LockPrimary()
        {
            bool began = actionCoordinator == null || actionCoordinator.TryBeginAction(this, KMS.ActionInputSlot.Primary, KMS.ActionSpeedTier.Light);
            if (!began) return false;

            if (actionCoordinator == null && movement != null)
            {
                movement.SetMoveSpeedOverride(this, LightSpeedMultiplierFallback);
            }

            attackLockTimer = attackActionDuration;
            return true;
        }

        private void EndAttackLock()
        {
            if (actionCoordinator != null)
            {
                actionCoordinator.EndAction(this);
            }
            else if (movement != null)
            {
                movement.SetMoveSpeedOverride(this, null);
            }
        }

        // ---- Secondary: 장전(충전) / 큐 등록 ----

        private void BeginCharge()
        {
            if (stats != null && !stats.IsAlive) return;
            if (isCharging) return;
            if (!TryGetSelectedWeapon(out _)) return;

            bool began = actionCoordinator == null || actionCoordinator.TryBeginAction(this, KMS.ActionInputSlot.Secondary, KMS.ActionSpeedTier.Heavy);
            if (!began) return;

            if (actionCoordinator == null && movement != null)
            {
                movement.SetMoveSpeedOverride(this, HeavySpeedMultiplierFallback);
            }

            isCharging = true;
            chargeTimer = 0f;
            nextStageToEvaluate = 0;
            bankedStageIndex = -1;
            OnChargingStateChanged?.Invoke(true);
        }

        private void ReleaseCharge()
        {
            if (!isCharging) return;

            isCharging = false;
            UnlockSecondary();

            if (bankedStageIndex >= 0 && skillLoadout != null && skillQueue.Count < PlayerSkillLoadout.SlotCount)
            {
                SkillData skill = skillLoadout.GetEquippedSkill(bankedStageIndex);
                if (skill != null && !IsSkillOnCooldown(skill.Skill_ID))
                {
                    skillQueue.Enqueue(skill.Skill_ID);
                    KMSAudioService.PlayAt(GameSfxId.SkillQueued, transform.position);
                    OnSkillQueued?.Invoke(skill.Skill_ID);
                }
            }

            chargeTimer = 0f;
            nextStageToEvaluate = 0;
            bankedStageIndex = -1;
            OnChargingStateChanged?.Invoke(false);
        }

        private void CancelCharge()
        {
            if (!isCharging) return;

            isCharging = false;
            UnlockSecondary();
            chargeTimer = 0f;
            nextStageToEvaluate = 0;
            bankedStageIndex = -1;
            OnChargingStateChanged?.Invoke(false);
        }

        /// <summary>사망 등 외부 상태 전환에서 진행 중인 장전을 안전하게 취소한다(큐에 넣지 않음).</summary>
        public void CancelActiveCharge()
        {
            CancelCharge();
        }

        private void UnlockSecondary()
        {
            if (actionCoordinator != null)
            {
                actionCoordinator.EndAction(this);
            }
            else if (movement != null)
            {
                movement.SetMoveSpeedOverride(this, null);
            }
        }

        private void HandleQuickSlotSelectionRequested(int _)
        {
            CancelCharge();
        }

        private void EvaluateNextChargeStage()
        {
            int stageIndex = nextStageToEvaluate;
            SkillData skill = skillLoadout != null ? skillLoadout.GetEquippedSkill(stageIndex) : null;
            bool isValid = skill != null && !IsSkillOnCooldown(skill.Skill_ID);

            if (isValid)
            {
                bankedStageIndex = stageIndex;
            }

            nextStageToEvaluate++;

            KMSAudioService.PlayAt(GetChargeStageSfx(stageIndex), transform.position);
            OnChargeStageEvaluated?.Invoke(stageIndex, isValid);
        }

        private static GameSfxId GetChargeStageSfx(int stageIndex)
        {
            switch (stageIndex)
            {
                case 0: return GameSfxId.SkillChargeStage1;
                case 1: return GameSfxId.SkillChargeStage2;
                case 2: return GameSfxId.SkillChargeStage3;
                default: return GameSfxId.SkillChargeStage4;
            }
        }

        // ---- 스킬 쿨타임 ----

        public bool IsSkillOnCooldown(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && skillCooldownTimers.ContainsKey(skillId);
        }

        private void StartSkillCooldown(string skillId, float cooldown)
        {
            if (string.IsNullOrEmpty(skillId) || cooldown <= 0f) return;
            skillCooldownTimers[skillId] = cooldown;
        }

        /// <summary>[HUD 연동용] 이 스킬의 남은 쿨타임(초). 쿨타임 중이 아니면 0을 반환한다.</summary>
        public float GetSkillCooldownRemaining(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return 0f;
            return skillCooldownTimers.TryGetValue(skillId, out float remaining) ? remaining : 0f;
        }

/// <summary>[저장/불러오기 연동용] 지금 쿨타임 중인 스킬 전체(Skill_ID -> 남은 시간) 스냅샷.</summary>
        public IReadOnlyDictionary<string, float> GetSkillCooldownSnapshotForSave()
        {
            return skillCooldownTimers;
        }

        /// <summary>[저장/불러오기 연동용] 세이브 파일에서 불러온 쿨타임을 그대로 주입한다(0 이하 값은 무시).</summary>
        public void LoadSkillCooldowns(IEnumerable<KeyValuePair<string, float>> loadedCooldowns)
        {
            skillCooldownTimers.Clear();

            if (loadedCooldowns != null)
            {
                foreach (var pair in loadedCooldowns)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0f) continue;
                    skillCooldownTimers[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>[영지 진입/보스씬 초기화 구역 등] 모든 스킬 쿨타임을 즉시 초기화한다.</summary>
        public void ClearAllCooldowns()
        {
            skillCooldownTimers.Clear();
        }


        // ---- 보조 함수 ----

        /// <summary>[HUD 연동용] 지금 선택된 퀵슬롯 아이템이 Weapon 카테고리인지(=스킬 패널을 표시해야 하는지) 확인한다.</summary>
        public bool IsHoldingWeapon()
        {
            return TryGetSelectedWeapon(out _);
        }

        private bool TryGetSelectedWeapon(out WeaponItemData weapon)
        {
            weapon = null;

            if (inventory == null) return false;

            catalogManager = ItemCatalogManager.Resolve(catalogManager);
            if (catalogManager == null) return false;

            KmsItemStack selected = inventory.GetSelectedQuickSlot();
            if (selected == null || selected.IsEmpty) return false;

            ItemData itemData = catalogManager.FindItemData(selected.itemId);
            if (itemData == null || itemData.Category != ItemCategory.Weapon) return false;

            weapon = itemData as WeaponItemData;
            return weapon != null;
        }

        private Vector3 GetProjectileOrigin()
        {
            return projectileOrigin != null
                ? projectileOrigin.position
                : transform.position + transform.up * attackOriginHeight;
        }

private void FireBasicAttackProjectile(Vector3 origin, Vector3 direction, int damage, WeaponItemData weapon)
        {
            GameObject prefab = weapon.ProjectilePrefab;
            if (prefab == null)
            {
                Debug.LogError("[PlayerWeaponSkillController] WeaponItemData에 ProjectilePrefab이 연결되지 않았습니다.", this);
                return;
            }

            direction = NormalizeFireDirection(direction);
            GameObject projectileObject = KMS.Combat.ProjectilePool.Get(prefab, origin, Quaternion.LookRotation(direction));
            System.Action<GameObject> release = (obj) => KMS.Combat.ProjectilePool.Release(prefab, obj);
            ConfigureAndLaunch(projectileObject, direction, damage, weapon.ProjectileSpeed, weapon.ProjectileLifetime, release);
        }

        private void FireSkillProjectile(Vector3 origin, Vector3 direction, int damage, SkillData skill)
        {
            GameObject prefab = skill.ProjectilePrefab;
            if (prefab == null)
            {
                Debug.LogError($"[PlayerWeaponSkillController] SkillData({skill.Skill_ID})에 ProjectilePrefab이 연결되지 않았습니다.", this);
                return;
            }

            direction = NormalizeFireDirection(direction);
            GameObject projectileObject = Instantiate(prefab, origin, Quaternion.LookRotation(direction));
            ConfigureAndLaunch(projectileObject, direction, damage, skill.ProjectileSpeed, skill.ProjectileLifetime, null);
        }

        private Vector3 NormalizeFireDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();
            return direction;
        }

        private void ConfigureAndLaunch(GameObject projectileObject, Vector3 direction, int damage, float speed, float lifetime, System.Action<GameObject> releaseToPool)
        {
            if (projectileObject == null) return;

            Rigidbody body = projectileObject.GetComponent<Rigidbody>();

            if (body == null)
            {
                Debug.LogError("[PlayerWeaponSkillController] 투사체 Prefab에 Rigidbody가 없습니다.", projectileObject);
                if (releaseToPool != null) releaseToPool(projectileObject);
                else Destroy(projectileObject);
                return;
            }

            body.isKinematic = false;
            body.linearVelocity = direction * Mathf.Max(0f, speed);
            IgnorePlayerCollisions(projectileObject);

            KMS.InventoryDuped.ItemProjectile itemProjectile = projectileObject.GetComponent<KMS.InventoryDuped.ItemProjectile>();
            if (itemProjectile != null)
            {
                itemProjectile.Initialize(damage, monsterLayer, transform, lifetime, releaseToPool);
            }
            else
            {
                Debug.LogWarning("[PlayerWeaponSkillController] 투사체 Prefab에 ItemProjectile 컴포넌트가 없습니다.", projectileObject);
            }
        }




        private void IgnorePlayerCollisions(GameObject projectileObject)
        {
            Collider[] projectileColliders = projectileObject.GetComponentsInChildren<Collider>(true);
            Collider[] playerColliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < projectileColliders.Length; i++)
            {
                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Physics.IgnoreCollision(projectileColliders[i], playerColliders[j], true);
                }
            }
        }

        private void OnValidate()
        {
            stageDuration = Mathf.Max(0.1f, stageDuration);
            attackActionDuration = Mathf.Max(0.05f, attackActionDuration);
        }
    }
}
