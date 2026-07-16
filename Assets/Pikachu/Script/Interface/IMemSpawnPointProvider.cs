// ============================================================================
// IMemSpawnPointProvider.cs
// 월드 시스템이 멤 스폰 위치를 제공하기 위한 인터페이스
//
// [월드 담당자 필독]
// 이 인터페이스를 구현하여 MemSpawner에 주입하면,
// 멤이 월드의 지정된 위치에서 스폰됩니다.
//
// [사용법]
// 1. 이 인터페이스를 구현하는 클래스를 만듭니다.
// 2. MemSpawner.SetSpawnPointProvider(myProvider) 로 주입합니다.
// 3. 주입 전까지는 에디터에서 수동 배치한 Transform을 fallback으로 사용합니다.
// ============================================================================
using UnityEngine;

namespace MemSystem.Interface
{
    /// <summary>
    /// 월드 시스템이 구현할 스폰 포인트 제공 인터페이스.
    /// 
    /// 멤 스폰 시스템은 월드를 직접 참조하지 않고,
    /// 이 인터페이스를 통해 스폰 위치 정보를 받습니다.
    /// 
    /// [프로토타입 동작]
    /// Provider가 주입되지 않으면 MemSpawner의 Inspector에서
    /// 수동 배치한 Waypoint Transform 배열을 사용합니다.
    /// </summary>
    public interface IMemSpawnPointProvider
    {
        /// <summary>
        /// 지정된 구역의 스폰 가능 위치 목록을 반환합니다.
        /// 멤은 이 위치들 중 하나의 반경 200m 내에서 랜덤 스폰됩니다.
        /// </summary>
        /// <param name="zoneId">구역 식별자 (월드 시스템에서 정의)</param>
        /// <returns>스폰 가능 위치 배열 (Waypoint 좌표)</returns>
        Vector3[] GetSpawnPoints(string zoneId);

        /// <summary>
        /// 플레이어가 해당 구역 내에 있는지 확인합니다.
        /// 스폰 트리거 조건 판정에 사용됩니다.
        /// </summary>
        /// <param name="zoneId">구역 식별자</param>
        /// <returns>플레이어가 구역 내에 있으면 true</returns>
        bool IsPlayerInZone(string zoneId);

        /// <summary>
        /// 플레이어가 해당 구역에 체류한 시간(초)을 반환합니다.
        /// 기획서 기준 10~30초 체류 시 스폰 트리거가 발동됩니다.
        /// </summary>
        /// <param name="zoneId">구역 식별자</param>
        /// <returns>체류 시간 (초)</returns>
        float GetPlayerStayDuration(string zoneId);
    }
}
