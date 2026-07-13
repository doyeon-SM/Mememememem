using UnityEngine;

namespace GH.Loading
{
    /// <summary>
    /// 로딩 설명과 정규화된 전체 진행률을 함께 전달하는 읽기 전용 값입니다.
    /// </summary>
    public readonly struct LoadingProgress
    {
        /// <summary>로딩 진행 정보 값을 생성합니다.</summary>
        /// <param name="description">현재 작업 설명입니다.</param>
        /// <param name="normalizedProgress">0~1 범위로 정규화할 전체 진행률입니다.</param>
        public LoadingProgress(string description, float normalizedProgress)
        {
            Description = description;
            NormalizedProgress = Mathf.Clamp01(normalizedProgress);
        }

        /// <summary>현재 표시 중인 로딩 작업 설명입니다.</summary>
        public string Description { get; }

        /// <summary>0~1 범위의 전체 로딩 진행률입니다.</summary>
        public float NormalizedProgress { get; }

        /// <summary>UI 표시에 사용할 0~100 정수 퍼센트입니다.</summary>
        public int Percent => Mathf.RoundToInt(NormalizedProgress * 100f);
    }
}
