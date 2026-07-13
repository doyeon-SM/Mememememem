using System;
using System.Collections;

namespace GH.Loading
{
    /// <summary>
    /// <see cref="LoadingManager"/>가 순서대로 실행하고 진행률을 통합할 수 있는 로딩 작업 규약입니다.
    /// 구현체는 작업 내부 진행률을 0~1 범위로 보고해야 합니다.
    /// </summary>
    public interface ILoadingTask
    {
        /// <summary>로딩 UI에 표시할 현재 작업 설명입니다.</summary>
        string Description { get; }

        /// <summary>전체 실제 로딩 구간에서 이 작업이 차지하는 상대 가중치입니다.</summary>
        float Weight { get; }

        /// <summary>
        /// 작업을 실행합니다.
        /// </summary>
        /// <param name="reportProgress">0~1 범위의 작업 진행률을 전달할 콜백입니다.</param>
        IEnumerator Run(Action<float> reportProgress);
    }
}
