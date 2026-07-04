using UnityEngine;
using HDY.Item;
using MemSystem.Interface;

namespace HDY.Capture
{
    /// <summary>
    /// 포켓볼 역할을 하는 캡슐. 멤과 트리거 충돌하면 포획을 시도한다.
    /// [교통정리] 포획 확률 계산과 성공/실패 처리는 Pikachu 팀의 ICapturable(MemSystem.Interface)에 위임한다.
    /// 기존 HDY 자체 확률 공식(CaptureRateTable/HP브래킷 등)은 폐기됨.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Capsule : MonoBehaviour
    {
        [Header("캡슐 아이템 데이터 (ItemClass = 캡슐 등급)")]
        public ItemData CapsuleItemData;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<ICapturable>(out var capturable)) return;

            TryCapture(capturable);
        }

        private void TryCapture(ICapturable capturable)
        {
            int capsuleTier = CapsuleItemData != null ? (int)CapsuleItemData.ItemClass : 0;

            float rate = capturable.GetCaptureRate(capsuleTier);
            bool success = Random.value <= rate;

            if (success)
            {
                capturable.OnCaptureSuccess();
                Debug.Log($"[Capsule] 포획 성공 / 확률: {rate:P1}");
            }
            else
            {
                // TODO: 도주 여부 판정은 임시로 50% 확률 (추후 기획에 맞춰 조정)
                bool shouldFlee = Random.value > 0.5f;
                capturable.OnCaptureFail(shouldFlee);
                Debug.Log($"[Capsule] 포획 실패 / 확률: {rate:P1} / 도주: {shouldFlee}");
            }
        }
    }
}
