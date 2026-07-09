using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GH.Loading
{
    public class SceneLoadTask : MonoBehaviour, ILoadingTask
    {
        [SerializeField] private string sceneName = "Main_World";
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
        [SerializeField] private float weight = 0.7f;
        [SerializeField] private string description = "월드 씬 불러오는 중";
        [SerializeField] private bool activateSceneWhenLoaded = true;

        public string Description => description;
        public float Weight => weight;

        public IEnumerator Run(Action<float> reportProgress)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoadTask] 로드할 씬 이름이 비어 있습니다.", this);
                reportProgress?.Invoke(1f);
                yield break;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoadTask] 씬 로드를 시작하지 못했습니다: {sceneName}", this);
                reportProgress?.Invoke(1f);
                yield break;
            }

            operation.allowSceneActivation = activateSceneWhenLoaded;

            while (!operation.isDone)
            {
                float normalizedProgress = Mathf.Clamp01(operation.progress / 0.9f);
                reportProgress?.Invoke(normalizedProgress);

                if (!activateSceneWhenLoaded && operation.progress >= 0.9f)
                {
                    reportProgress?.Invoke(1f);
                    yield break;
                }

                yield return null;
            }

            reportProgress?.Invoke(1f);
        }
    }
}
