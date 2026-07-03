using System.Collections.Generic;
using UnityEngine;
using HDY.Mem;

namespace HDY.Capture
{
    /// <summary>
    /// 포획에 성공한 머 데이터를 메모리에 보관하는 매니저.
    /// 파일 저장 등 영속화 로직은 다음 단계에서 추가 예정.
    /// </summary>
    public class MemCaptureManager : MonoBehaviour
    {
        [Header("포획된 머 목록 (런타임 메모리 보관)")]
        [SerializeField] private List<MemCaptureData> capturedMems = new List<MemCaptureData>();

        public IReadOnlyList<MemCaptureData> CapturedMems => capturedMems;

        public void AddCapture(MemCaptureData data)
        {
            capturedMems.Add(data);
        }
    }
}
