using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GH.Loading
{
    [DisallowMultipleComponent]
    public class LoadingManager : MonoBehaviour
    {
        public static LoadingManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text percentText;

        [Header("Tasks")]
        [Tooltip("ILoadingTask를 구현한 MonoBehaviour를 순서대로 등록합니다.")]
        [SerializeField] private List<MonoBehaviour> taskBehaviours = new List<MonoBehaviour>();
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onLoadingStarted;
        [SerializeField] private UnityEvent onLoadingCompleted;

        public event Action<LoadingProgress> ProgressChanged;
        public event Action LoadingCompleted;

        public bool IsLoading { get; private set; }
        public float CurrentProgress { get; private set; }
        public string CurrentDescription { get; private set; }

        private Coroutine loadingRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                StartLoading();
            }
        }

        public void StartLoading()
        {
            if (IsLoading)
            {
                return;
            }

            loadingRoutine = StartCoroutine(RunLoading());
        }

        public void StopLoading()
        {
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
                loadingRoutine = null;
            }

            IsLoading = false;
        }

        private IEnumerator RunLoading()
        {
            IsLoading = true;
            onLoadingStarted?.Invoke();

            List<ILoadingTask> tasks = BuildTaskList();
            float totalWeight = GetTotalWeight(tasks);
            float completedWeight = 0f;

            Report("로딩 준비 중", 0f);

            foreach (ILoadingTask task in tasks)
            {
                float taskWeight = Mathf.Max(0f, task.Weight);
                string description = string.IsNullOrWhiteSpace(task.Description) ? "로딩 중" : task.Description;

                yield return task.Run(localProgress =>
                {
                    float weightedProgress = completedWeight + Mathf.Clamp01(localProgress) * taskWeight;
                    float normalizedProgress = totalWeight > 0f ? weightedProgress / totalWeight : 1f;
                    Report(description, normalizedProgress);
                });

                completedWeight += taskWeight;
                Report(description, totalWeight > 0f ? completedWeight / totalWeight : 1f);
                yield return null;
            }

            Report("로딩 완료", 1f);
            IsLoading = false;
            loadingRoutine = null;

            onLoadingCompleted?.Invoke();
            LoadingCompleted?.Invoke();
        }

        private List<ILoadingTask> BuildTaskList()
        {
            List<ILoadingTask> tasks = new List<ILoadingTask>();

            foreach (MonoBehaviour taskBehaviour in taskBehaviours)
            {
                if (taskBehaviour == null)
                {
                    continue;
                }

                if (taskBehaviour is ILoadingTask task)
                {
                    tasks.Add(task);
                    continue;
                }

                Debug.LogWarning($"[LoadingManager] {taskBehaviour.name}은 ILoadingTask를 구현하지 않았습니다.", taskBehaviour);
            }

            return tasks;
        }

        private static float GetTotalWeight(IReadOnlyList<ILoadingTask> tasks)
        {
            float totalWeight = 0f;

            foreach (ILoadingTask task in tasks)
            {
                totalWeight += Mathf.Max(0f, task.Weight);
            }

            return totalWeight;
        }

        private void Report(string description, float progress)
        {
            CurrentDescription = description;
            CurrentProgress = Mathf.Clamp01(progress);

            if (progressSlider != null)
            {
                progressSlider.value = CurrentProgress;
            }

            if (descriptionText != null)
            {
                descriptionText.text = CurrentDescription;
            }

            if (percentText != null)
            {
                percentText.text = $"{Mathf.RoundToInt(CurrentProgress * 100f)}%";
            }

            ProgressChanged?.Invoke(new LoadingProgress(CurrentDescription, CurrentProgress));
        }
    }
}
