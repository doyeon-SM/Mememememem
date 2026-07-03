using System;

namespace Mem.Mem
{
    /// <summary>
    /// 포획된 머를 저장하기 위한 데이터. 세이브 시스템에서 직렬화하여 사용할 예정.
    /// MemCapture_ID는 GUID 문자열이며, 방출(삭제)되어도 재사용하지 않는다.
    /// 참고: 실제 파일 저장 시에는 MemSO를 직접 직렬화할 수 없으므로,
    /// 추후 저장 로직 단계에서 Mem_ID(문자열)로 대체해 MemCatalogManager를 통해 복원하는 방식을 권장.
    /// 지금은 기획 스펙에 맞춰 MemSO 참조 필드를 그대로 둔다.
    /// </summary>
    [Serializable]
    public class MemCaptureData
    {
        public string MemCapture_ID;

        public MemData MemSO;

        public MemStat MemStat;
    }
}
