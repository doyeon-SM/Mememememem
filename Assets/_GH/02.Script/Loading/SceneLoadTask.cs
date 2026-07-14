using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace GH.Loading
{
    /// <summary>
    /// Unity 비동기 씬 로드를 <see cref="ILoadingTask"/>로 제공하는 작업입니다.
    /// 웨이포인트 이동에서는 실제 로딩이 90%에 도달한 뒤 씬 활성화를 보류하고,
    /// <see cref="LoadingManager"/>의 연출 구간이 끝나면 활성화합니다.
    /// </summary>
    public class SceneLoadTask : MonoBehaviour, ISceneLoadingTask, IDeferredCompletionTask
    {
        [FormerlySerializedAs("sceneName")]
        [Tooltip("LoadingManager.StartLoading()을 직접 호출할 때만 사용하는 기본 씬입니다. LoadScene() 요청은 이 값을 사용하지 않습니다.")]
        [SerializeField] private string fallbackSceneName = "Main_World";
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
        [SerializeField] private float weight = 0.7f;
        [SerializeField] private string description = "월드 씬 불러오는 중";

        /// <inheritdoc />
        public string Description => description;

        /// <inheritdoc />
        public float Weight => weight;

        /// <summary>현재 실행 중이거나 마지막으로 실행한 대상 씬 이름입니다.</summary>
        public string SceneName { get; private set; }

        /// <inheritdoc />
        public bool HasDeferredCompletion => pendingOperation != null && !pendingOperation.isDone;

        private AsyncOperation pendingOperation;

        /// <inheritdoc />
        public IEnumerator Run(LoadingContext context, Action<float> reportProgress)
        {
            SceneName = context.HasSceneRequest ? context.SceneName : fallbackSceneName;

            if (string.IsNullOrWhiteSpace(SceneName))
            {
                Debug.LogError("[SceneLoadTask] 로드할 씬 이름이 비어 있습니다.", this);
                reportProgress?.Invoke(1f);
                yield break;
            }

            pendingOperation = SceneManager.LoadSceneAsync(SceneName, loadSceneMode);
            if (pendingOperation == null)
            {
                Debug.LogError($"[SceneLoadTask] 씬 로드를 시작하지 못했습니다: {SceneName}", this);
                reportProgress?.Invoke(1f);
                yield break;
            }

            pendingOperation.allowSceneActivation = false;

            while (!pendingOperation.isDone)
            {
                float normalizedProgress = Mathf.Clamp01(pendingOperation.progress / 0.9f);
                reportProgress?.Invoke(normalizedProgress);

                if (pendingOperation.progress >= 0.9f)
                {
                    reportProgress?.Invoke(1f);
                    yield break;
                }

                yield return null;
            }

            reportProgress?.Invoke(1f);
            pendingOperation = null;
        }

        /// <summary>
        /// 실제 로딩을 마치고 대기 중인 씬을 활성화한 뒤 완료될 때까지 기다립니다.
        /// </summary>
        public IEnumerator CompleteDeferredWork()
        {
            if (pendingOperation == null)
            {
                yield break;
            }

            pendingOperation.allowSceneActivation = true;
            while (!pendingOperation.isDone)
            {
                yield return null;
            }

            pendingOperation = null;
        }
    }
}
