using UnityEngine;

namespace KMS
{
    /// <summary>
    /// 어느 입력 버튼 계열(클릭/우클릭)을 점유하는지를 나타낸다. 같은 슬롯은 동시에 한
    /// 오너만 점유할 수 있고, 두 슬롯도 서로 배타적이라 한쪽이 점유 중이면 다른 쪽은
    /// 시작할 수 없다(한 번에 전체 행동 중 하나만 진행).
    /// </summary>
    public enum ActionInputSlot
    {
        Primary,
        Secondary
    }

    /// <summary>이동속도 감속 등급. 실제 배율(퍼센트) 값은 PlayerActionSlotCoordinator 한 곳에서만 관리한다.</summary>
    public enum ActionSpeedTier
    {
        Light,
        Heavy
    }

    /// <summary>
    /// [멤] 공격/채집, 캡슐 던지기, 음식 섭취(그리고 추후 스킬 사용/스킬 장전)처럼 "행동 중
    /// 이동속도가 감속되고, 서로 동시에 진행되면 안 되는" 행동들을 한 곳에서 관리하기 위한
    /// 코디네이터다.
    ///
    /// 예전에는 각 컨트롤러가 PlayerMovement.SetMovementBlocked()를 직접 호출해서 이동을
    /// 완전히 잠갔는데, 이제는 완전 잠금 대신 "감속 등급(Light=30%↓, Heavy=60%↓)"을 이
    /// 컴포넌트가 대신 계산해서 PlayerMovement에 적용한다. 새 행동(스킬 사용/장전 등)을
    /// 추가할 때도 각 컨트롤러는 TryBeginAction/EndAction만 호출하면 되고, 퍼센트 값이나
    /// "다른 행동과 동시에 진행되면 안 된다"는 규칙은 이 클래스 한 곳만 고치면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerActionSlotCoordinator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMovement movement;

        [Header("Speed Tiers")]
        [Tooltip("가벼운 행동(공격/채집, 추후 스킬 사용 등) 중 이동속도 배율입니다. 0.7 = 30% 감속.")]
        [SerializeField, Range(0f, 1f)] private float lightSpeedMultiplier = 0.7f;
        [Tooltip("무거운 행동(캡슐 던지기, 음식 섭취, 추후 스킬 장전 등) 중 이동속도 배율입니다. 0.4 = 60% 감속.")]
        [SerializeField, Range(0f, 1f)] private float heavySpeedMultiplier = 0.4f;

        private object primaryOwner;
        private object secondaryOwner;

        public bool IsPrimarySlotActive => primaryOwner != null;
        public bool IsSecondarySlotActive => secondaryOwner != null;

        private void Reset()
        {
            movement = GetComponent<PlayerMovement>();
        }

        private void Awake()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
        }

        private void OnDisable()
        {
            // 극단적인 상황(씬 전환 등)에서도 이동속도 오버라이드가 고착되지 않도록,
            // 비활성화될 때 남아있는 점유를 전부 해제한다.
            if (movement != null)
            {
                if (primaryOwner != null) movement.SetMoveSpeedOverride(primaryOwner, null);
                if (secondaryOwner != null) movement.SetMoveSpeedOverride(secondaryOwner, null);
            }

            primaryOwner = null;
            secondaryOwner = null;
        }

        /// <summary>
        /// owner가 지금 slot을 새로 점유할 수 있는지(또는 이미 점유하고 있는지)를 상태 변경 없이
        /// 확인한다. 애니메이션/사운드를 재생하기 전에 미리 검사할 때 사용한다.
        /// </summary>
        public bool CanBeginAction(object owner, ActionInputSlot slot)
        {
            if (owner == null) return false;
            if (GetOwner(slot) == owner) return true;

            return !IsSlotOccupiedByOther(slot, owner) && !IsOtherSlotOccupied(slot);
        }

        /// <summary>
        /// 지정한 입력 슬롯을 점유하며 tier에 해당하는 이동속도 감속을 적용한다. 이미 다른
        /// 오너가 같은 슬롯을 점유 중이거나 반대쪽 슬롯이 점유 중이면 실패하고 false를 반환한다
        /// (이 경우 이동속도에는 아무 영향도 주지 않는다).
        /// </summary>
        public bool TryBeginAction(object owner, ActionInputSlot slot, ActionSpeedTier tier)
        {
            if (!CanBeginAction(owner, slot)) return false;

            SetOwner(slot, owner);
            ApplySpeedOverride(owner, tier);
            return true;
        }

        /// <summary>owner가 점유 중인 슬롯을 전부 해제하고 이동속도 감속을 되돌린다.</summary>
        public void EndAction(object owner)
        {
            if (owner == null) return;

            bool released = false;
            if (primaryOwner == owner)
            {
                primaryOwner = null;
                released = true;
            }

            if (secondaryOwner == owner)
            {
                secondaryOwner = null;
                released = true;
            }

            if (released && movement != null)
            {
                movement.SetMoveSpeedOverride(owner, null);
            }
        }

        public bool IsSlotActive(ActionInputSlot slot) => GetOwner(slot) != null;

        private void ApplySpeedOverride(object owner, ActionSpeedTier tier)
        {
            if (movement == null) return;

            float multiplier = tier == ActionSpeedTier.Heavy ? heavySpeedMultiplier : lightSpeedMultiplier;
            movement.SetMoveSpeedOverride(owner, multiplier);
        }

        private object GetOwner(ActionInputSlot slot)
        {
            return slot == ActionInputSlot.Primary ? primaryOwner : secondaryOwner;
        }

        private void SetOwner(ActionInputSlot slot, object owner)
        {
            if (slot == ActionInputSlot.Primary) primaryOwner = owner;
            else secondaryOwner = owner;
        }

        private bool IsSlotOccupiedByOther(ActionInputSlot slot, object owner)
        {
            object current = GetOwner(slot);
            return current != null && current != owner;
        }

        private bool IsOtherSlotOccupied(ActionInputSlot slot)
        {
            ActionInputSlot other = slot == ActionInputSlot.Primary
                ? ActionInputSlot.Secondary
                : ActionInputSlot.Primary;
            return GetOwner(other) != null;
        }

        private void OnValidate()
        {
            lightSpeedMultiplier = Mathf.Clamp01(lightSpeedMultiplier);
            heavySpeedMultiplier = Mathf.Clamp01(heavySpeedMultiplier);
        }
    }
}
