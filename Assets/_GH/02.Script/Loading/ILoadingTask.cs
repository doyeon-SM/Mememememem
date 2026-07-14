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
        /// <param name="context">현재 로딩 요청의 대상 씬과 목적지 정보입니다.</param>
        /// <param name="reportProgress">0~1 범위의 작업 진행률을 전달할 콜백입니다.</param>
        IEnumerator Run(LoadingContext context, Action<float> reportProgress);
    }

    /// <summary>
    /// 한 번의 로딩 파이프라인에서 모든 <see cref="ILoadingTask"/>가 공유하는 요청 정보입니다.
    /// </summary>
    public readonly struct LoadingContext
    {
        /// <summary>동적인 씬 이동 요청이 없는 기본 컨텍스트입니다.</summary>
        public static LoadingContext Empty => new LoadingContext(string.Empty, string.Empty);

        /// <summary>Build Settings에 등록된 대상 씬 이름입니다.</summary>
        public string SceneName { get; }

        /// <summary>새 씬에서 사용할 목적지 또는 진입 지점 ID입니다.</summary>
        public string DestinationId { get; }

        /// <summary>동적으로 지정된 대상 씬이 있는지 나타냅니다.</summary>
        public bool HasSceneRequest => !string.IsNullOrWhiteSpace(SceneName);

        public LoadingContext(string sceneName, string destinationId)
        {
            SceneName = sceneName ?? string.Empty;
            DestinationId = destinationId ?? string.Empty;
        }
    }

    /// <summary>
    /// 씬 이동 요청을 처리할 수 있는 로딩 작업임을 표시하는 규약입니다.
    /// <see cref="LoadingManager"/>는 구체적인 씬 로더 타입 대신 이 규약만 확인합니다.
    /// </summary>
    public interface ISceneLoadingTask : ILoadingTask
    {
    }

    /// <summary>
    /// 기본 작업을 끝낸 뒤 로딩 연출 완료 시점에 마무리해야 하는 작업 규약입니다.
    /// 씬 활성화처럼 연출 뒤로 미뤄야 하는 처리를 구체 타입 참조 없이 실행할 때 사용합니다.
    /// </summary>
    public interface IDeferredCompletionTask
    {
        /// <summary>현재 연기된 마무리 작업이 있는지 나타냅니다.</summary>
        bool HasDeferredCompletion { get; }

        /// <summary>연기된 작업을 마무리하고 완료될 때까지 기다립니다.</summary>
        IEnumerator CompleteDeferredWork();
    }
}
