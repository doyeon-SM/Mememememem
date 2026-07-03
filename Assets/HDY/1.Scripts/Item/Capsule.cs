using UnityEngine;
using Mem.Item;
using Mem.Mem;

namespace Mem.Capture
{
    /// <summary>
    /// 포켓볼 역할을 하는 캡슐. 머와 트리거 충돌하면 포획을 시도한다.
    /// 최종 확률 = 등급별 HP브래킷 보정 + 머 데이터 기본 포획확률 + 캡슐 등급 어드밴티지 (0~100% 클램프).
    /// 캡슐 등급 어드밴티지: 캡슐 등급이 머 등급보다 높은 단계당 +20%, 낮은 단계당 -20%.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Capsule : MonoBehaviour
    {
        [Header("캡슐 아이템 데이터 (ItemClass = 캡슐 등급)")]
        public ItemData CapsuleItemData;

        [Header("포획 확률 테이블")]
        public CaptureRateTable CaptureRateTable;

        [Header("포획 결과 저장 매니저")]
        public MemCaptureManager CaptureManager;

        private void OnTriggerEnter(Collider other)
        {
            MemSpawnRuntime mem = other.GetComponent<MemSpawnRuntime>();
            if (mem == null || mem.MemSO == null) return;

            TryCapture(mem);
        }

        private void TryCapture(MemSpawnRuntime mem)
        {
            float finalRate = CalculateCaptureRate(mem);
            bool success = Random.value <= finalRate;

            if (success)
            {
                MemStat capturedStat = mem.MemSO.MemStat;
                capturedStat.Exploration = mem.Exploration;

                MemCaptureData captureData = new MemCaptureData
                {
                    MemCapture_ID = System.Guid.NewGuid().ToString(),
                    MemSO = mem.MemSO,
                    MemStat = capturedStat
                };

                if (CaptureManager != null)
                {
                    CaptureManager.AddCapture(captureData);
                }

                Debug.Log($"[Capsule] 포획 성공: {mem.MemSO.MemName} / ID: {captureData.MemCapture_ID} / 확률: {finalRate:P1}");
            }
            else
            {
                Debug.Log($"[Capsule] 포획 실패: {mem.MemSO.MemName} / 확률: {finalRate:P1}");
            }

            // TODO: 이번 단계에서는 Mem/캡슐 오브젝트 처리(풀 반환 등)는 하지 않음
        }

        private float CalculateCaptureRate(MemSpawnRuntime mem)
        {
            MemData memData = mem.MemSO;

            // TODO: MemSpawnRuntime에 현재 HP 필드가 아직 없어 임시로 100%로 가정.
            // 추후 CurrentHP 필드가 추가되면 (CurrentHP / MemHP)로 교체할 것.
            float hpRatio = 1f;

            float hpBracketValue = CaptureRateTable != null
                ? CaptureRateTable.GetRate(memData.MemClass, hpRatio)
                : 0f;

            float baseRate = memData.BaseCaptureRate;

            float capsuleAdvantage = 0f;
            if (CapsuleItemData != null)
            {
                int capsuleGradeIndex = (int)CapsuleItemData.ItemClass;
                int memGradeIndex = (int)memData.MemClass;
                capsuleAdvantage = (capsuleGradeIndex - memGradeIndex) * 0.20f;
            }

            float finalRate = hpBracketValue + baseRate + capsuleAdvantage;
            return Mathf.Clamp01(finalRate);
        }
    }
}
