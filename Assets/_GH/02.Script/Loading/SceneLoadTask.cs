using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GH.Loading
{
    /// <summary>
    /// Unity 비동기 씬 로드를 <see cref="ILoadingTask"/>로 제공하는 작업입니다.
    /// 웨이포인트 이동에서는 실제 로딩이 90%에 도달한 뒤 씬 활성화를 보류하고,
    /// <see cref="LoadingManager"/>의 연출 구간이 끝나면 활성화합니다.
    /// </summary>
    public class SceneLoadTask : MonoBehaviour, ILoadingTask
    {
        [SerializeField] private string sceneName = "Main_World";
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
        [SerializeField] private float weight = 0.7f;
        [SerializeField] private string description = "월드 씬 불러오는 중";
        [SerializeField] private bool activateSceneWhenLoaded = true;

        /// <inheritdoc />
        public string Description => description;

        /// <inheritdoc />
        public float Weight => weight;

        /// <summary>현재 설정된 대상 씬 이름입니다.</summary>
        public string SceneName => sceneName;

        /// <summary>로딩 완료 후 활성화를 기다리는 씬 작업이 있는지 나타냅니다.</summary>
        public bool HasPendingActivation => pendingOperation != null && !pendingOperation.isDone;

        private AsyncOperation pendingOperation;
        private bool holdActivationForPresentation;

        /// <summary>
        /// 다음 실행에서 로드할 씬과 활성화 보류 여부를 설정합니다.
        /// </summary>
        /// <param name="targetSceneName">Build Settings에 등록된 대상 씬 이름입니다.</param>
        /// <param name="holdActivation">참이면 실제 로딩 후 별도 호출 전까지 씬 활성화를 보류합니다.</param>
        public void Configure(string targetSceneName, bool holdActivation)
        {
            sceneName = targetSceneName;
            holdActivationForPresentation = holdActivation;
        }

        /// <inheritdoc />
        public IEnumerator Run(Action<float> reportProgress)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoadTask] 로드할 씬 이름이 비어 있습니다.", this);
                reportProgress?.Invoke(1f);
                yield break;
            }

            pendingOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (pendingOperation == null)
            {
                Debug.LogError($"[SceneLoadTask] 씬 로드를 시작하지 못했습니다: {sceneName}", this);
                reportProgress?.Invoke(1f);
                yield break;
            }

            bool holdActivation = holdActivationForPresentation || !activateSceneWhenLoaded;
            pendingOperation.allowSceneActivation = !holdActivation;

            while (!pendingOperation.isDone)
            {
                float normalizedProgress = Mathf.Clamp01(pendingOperation.progress / 0.9f);
                reportProgress?.Invoke(normalizedProgress);

                if (holdActivation && pendingOperation.progress >= 0.9f)
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
        public IEnumerator ActivatePendingScene()
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
            holdActivationForPresentation = false;
        }
    }
}
