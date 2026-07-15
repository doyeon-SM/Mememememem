using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GH.Loading
{
    /// <summary>
    /// 등록된 <see cref="ILoadingTask"/>를 실행하고 로딩 UI, 진행률, 랜덤 팁을 관리합니다.
    /// 웨이포인트 씬 이동 요청은 실제 작업을 0~90%, 연출 시간을 90~100%로 표시한 뒤
    /// 새 씬의 목적지 웨이포인트로 플레이어를 배치합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingManager : MonoBehaviour
    {
        /// <summary>현재 유지되고 있는 로딩 매니저 인스턴스입니다.</summary>
        public static LoadingManager Instance { get; private set; }

        [Header("Dynamic UI")]
        [FormerlySerializedAs("loadingRoot")]
        [Tooltip("Canvas가 없는 로딩 Panel 프리팹입니다. 현재 활성 씬의 루트 Canvas 아래에 런타임 생성됩니다.")]
        [SerializeField] private GameObject loadingPanelPrefab;

        [Header("Legacy UI Migration")]
        [HideInInspector, SerializeField] private Slider progressSlider;
        [HideInInspector, SerializeField] private TMP_Text descriptionText;
        [HideInInspector, SerializeField] private TMP_Text percentText;
        [HideInInspector, SerializeField] private TMP_Text tipText;

        [Header("Presentation")]
        [Range(0.5f, 0.99f)]
        [SerializeField] private float actualLoadingProgressPortion = 0.9f;
        [SerializeField] private Vector2 presentationDurationRange = new Vector2(3f, 4f);
        [TextArea]
        [SerializeField] private string[] loadingTips = Array.Empty<string>();
        [SerializeField] private string presentationDescription = "월드 배치 중";

        [Header("Tasks")]
        [Tooltip("ILoadingTask를 구현한 MonoBehaviour를 순서대로 등록합니다.")]
        [SerializeField] private List<MonoBehaviour> taskBehaviours = new List<MonoBehaviour>();
        [SerializeField] private bool playOnStart;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onLoadingStarted;
        [SerializeField] private UnityEvent onLoadingCompleted;

        /// <summary>전체 진행률 또는 설명이 갱신될 때 발생합니다.</summary>
        public event Action<LoadingProgress> ProgressChanged;

        /// <summary>씬 활성화와 목적지 플레이어 배치까지 모두 끝난 뒤 발생합니다.</summary>
        public event Action LoadingCompleted;

        /// <summary>현재 로딩 루틴이 실행 중인지 나타냅니다.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>0~1 범위의 현재 전체 진행률입니다.</summary>
        public float CurrentProgress { get; private set; }

        /// <summary>현재 실행 중인 작업 또는 연출 구간 설명입니다.</summary>
        public string CurrentDescription { get; private set; }

        private Coroutine loadingRoutine;
        private LoadingPanelView loadingPanelInstance;
        private bool showTipInDescription;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PrepareLegacyLoadingPanelTemplate();

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

        private void OnDestroy()
        {
            DestroyLoadingPanelInstance();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 인스펙터의 Task Behaviours에 등록된 작업을 현재 설정값으로 실행합니다.
        /// 이미 로딩 중이면 요청을 무시합니다.
        /// </summary>
        public void StartLoading()
        {
            TryStartLoading(LoadingContext.Empty, false);
        }

        /// <summary>
        /// 씬 이동을 요청합니다. 등록된 <see cref="ISceneLoadingTask"/>가 요청을 처리하며,
        /// 씬 활성화 후 목적지 ID가 있으면 해당 웨이포인트로 플레이어를 이동시킵니다.
        /// </summary>
        /// <param name="sceneName">Build Settings에 등록된 대상 씬 이름입니다.</param>
        /// <param name="targetWayPointId">새 씬에서 도착 위치로 사용할 웨이포인트 ID입니다.</param>
        /// <returns>요청이 시작되었으면 참, 설정 누락·중복 요청·씬 누락이면 거짓입니다.</returns>
        public bool LoadScene(string sceneName, string targetWayPointId)
        {
            if (IsLoading || string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[LoadingManager] Build Settings에서 씬을 찾을 수 없습니다: {sceneName}", this);
                return false;
            }

            LoadingContext context = new LoadingContext(sceneName, targetWayPointId);
            return TryStartLoading(context, true);
        }

        /// <summary>
        /// 현재 로딩 코루틴을 중단하고 로딩 UI를 숨깁니다.
        /// 이미 시작된 Unity 씬 비동기 작업 자체를 취소하지는 않습니다.
        /// </summary>
        public void StopLoading()
        {
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
                loadingRoutine = null;
            }

            IsLoading = false;
            DestroyLoadingPanelInstance();
        }

        private bool TryStartLoading(LoadingContext context, bool requireSceneLoadingTask)
        {
            if (IsLoading)
            {
                return false;
            }

            List<ILoadingTask> tasks = BuildTaskList();
            if (requireSceneLoadingTask && !ContainsSceneLoadingTask(tasks))
            {
                Debug.LogError(
                    "[LoadingManager] taskBehaviours에 ISceneLoadingTask가 등록되지 않았습니다.",
                    this);
                return false;
            }

            if (!TryCreateLoadingPanelInstance())
            {
                return false;
            }

            IsLoading = true;
            loadingRoutine = StartCoroutine(RunLoading(context, tasks));
            return true;
        }

        private IEnumerator RunLoading(LoadingContext context, List<ILoadingTask> tasks)
        {
            ShowRandomTip();
            onLoadingStarted?.Invoke();

            float totalWeight = GetTotalWeight(tasks);
            float completedWeight = 0f;

            Report("로딩 준비 중", 0f);

            foreach (ILoadingTask task in tasks)
            {
                float taskWeight = Mathf.Max(0f, task.Weight);
                string description = string.IsNullOrWhiteSpace(task.Description)
                    ? "로딩 중"
                    : task.Description;

                yield return task.Run(context, localProgress =>
                {
                    float weightedProgress = completedWeight + Mathf.Clamp01(localProgress) * taskWeight;
                    float normalizedProgress = totalWeight > 0f ? weightedProgress / totalWeight : 1f;
                    Report(description, normalizedProgress * actualLoadingProgressPortion);
                });

                completedWeight += taskWeight;
                float actualProgress = totalWeight > 0f ? completedWeight / totalWeight : 1f;
                Report(description, actualProgress * actualLoadingProgressPortion);
                yield return null;
            }

            yield return RunPresentationProgress();
            yield return CompleteDeferredTasks(tasks);
            yield return null;

            MovePlayerToDestinationWayPoint(context.DestinationId);
            Report("로딩 완료", 1f);
            IsLoading = false;
            loadingRoutine = null;
            DestroyLoadingPanelInstance();

            onLoadingCompleted?.Invoke();
            LoadingCompleted?.Invoke();
        }

        private IEnumerator RunPresentationProgress()
        {
            float minDuration = Mathf.Max(0f, presentationDurationRange.x);
            float maxDuration = Mathf.Max(minDuration, presentationDurationRange.y);
            float duration = UnityEngine.Random.Range(minDuration, maxDuration);

            if (duration <= 0f)
            {
                Report(presentationDescription, 1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Lerp(
                    actualLoadingProgressPortion,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                Report(presentationDescription, progress);
                yield return null;
            }
        }

        private static IEnumerator CompleteDeferredTasks(IReadOnlyList<ILoadingTask> tasks)
        {
            foreach (ILoadingTask task in tasks)
            {
                if (task is IDeferredCompletionTask deferredTask && deferredTask.HasDeferredCompletion)
                {
                    yield return deferredTask.CompleteDeferredWork();
                }
            }
        }

        private static bool ContainsSceneLoadingTask(IReadOnlyList<ILoadingTask> tasks)
        {
            foreach (ILoadingTask task in tasks)
            {
                if (task is ISceneLoadingTask)
                {
                    return true;
                }
            }

            return false;
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

                Debug.LogWarning(
                    $"[LoadingManager] {taskBehaviour.name}은 ILoadingTask를 구현하지 않았습니다.",
                    taskBehaviour);
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

        private void MovePlayerToDestinationWayPoint(string destinationWayPointId)
        {
            if (string.IsNullOrWhiteSpace(destinationWayPointId))
            {
                return;
            }

            WayPointStone destinationStone = null;
            WayPointStone[] stones = FindObjectsByType<WayPointStone>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (WayPointStone stone in stones)
            {
                if (stone != null && stone.Id == destinationWayPointId)
                {
                    destinationStone = stone;
                    break;
                }
            }

            GameObject playerObject = PlayerReferenceResolver.FindPlayerObject();

            if (destinationStone == null || playerObject == null)
            {
                Debug.LogWarning(
                    $"[LoadingManager] 목적지 배치 실패. waypoint={destinationWayPointId}",
                    this);
                return;
            }

            CharacterController controller = PlayerReferenceResolver
                .FindComponentInPlayerHierarchy<CharacterController>(playerObject);
            if (controller != null)
            {
                controller.enabled = false;
            }

            playerObject.transform.position = destinationStone.SpawnPosition;

            if (controller != null)
            {
                controller.enabled = true;
            }

            WorldChunkManager chunkManager = FindFirstObjectByType<WorldChunkManager>(FindObjectsInactive.Include);
            if (chunkManager != null)
            {
                chunkManager.SetPlayer(playerObject.transform);
            }

            if (WayPointManager.Instance != null)
            {
                WayPointManager.Instance.SetPlayer(playerObject.transform);
            }
        }

        // 이전 프리팹에 내장된 Canvas_Loading은 런타임에 Panel 부분만 분리해
        // 새 동적 패널 구조의 템플릿으로 사용한다. 씬/프리팹 파일 자체는 변경하지 않는다.
        private void PrepareLegacyLoadingPanelTemplate()
        {
            if (loadingPanelPrefab == null && progressSlider != null)
            {
                loadingPanelPrefab = FindLegacyPanelTemplate(progressSlider.transform);
            }

            if (loadingPanelPrefab == null || !loadingPanelPrefab.scene.IsValid())
            {
                return;
            }

            Canvas embeddedCanvas = loadingPanelPrefab.GetComponent<Canvas>();
            GameObject panelTemplate = loadingPanelPrefab;

            if (embeddedCanvas != null)
            {
                Transform panelTransform = FindLegacyPanelRoot(embeddedCanvas.transform);
                if (panelTransform == null)
                {
                    Debug.LogError(
                        "[LoadingManager] 기존 Canvas_Loading 안에서 로딩 Panel 루트를 찾지 못했습니다.",
                        this);
                    return;
                }

                panelTemplate = panelTransform.gameObject;
                panelTemplate.SetActive(false);
                panelTransform.SetParent(transform, false);
                Destroy(embeddedCanvas.gameObject);
            }
            else if (!panelTemplate.transform.IsChildOf(transform))
            {
                panelTemplate.transform.SetParent(transform, false);
            }

            LoadingPanelView panelView = panelTemplate.GetComponent<LoadingPanelView>();
            if (panelView == null)
            {
                panelView = panelTemplate.AddComponent<LoadingPanelView>();
            }

            panelView.Configure(progressSlider, descriptionText, percentText, tipText);
            panelTemplate.SetActive(false);
            loadingPanelPrefab = panelTemplate;
        }

        private GameObject FindLegacyPanelTemplate(Transform source)
        {
            Transform current = source;
            while (current != null && current.parent != null)
            {
                Transform parent = current.parent;
                if (parent == transform || parent.GetComponent<Canvas>() != null)
                {
                    return current.gameObject;
                }

                current = parent;
            }

            return null;
        }

        private Transform FindLegacyPanelRoot(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return null;
            }

            for (int i = 0; i < canvasTransform.childCount; i++)
            {
                Transform child = canvasTransform.GetChild(i);
                if (progressSlider == null || progressSlider.transform.IsChildOf(child))
                {
                    return child;
                }
            }

            return canvasTransform.childCount > 0 ? canvasTransform.GetChild(0) : null;
        }

        private bool TryCreateLoadingPanelInstance()
        {
            DestroyLoadingPanelInstance();

            if (loadingPanelPrefab == null)
            {
                Debug.LogError("[LoadingManager] Loading Panel Prefab이 지정되지 않았습니다.", this);
                return false;
            }

            Canvas sceneCanvas = FindActiveSceneCanvas();
            if (sceneCanvas == null)
            {
                Debug.LogError(
                    $"[LoadingManager] 활성 씬 '{SceneManager.GetActiveScene().name}'에서 사용할 루트 Canvas를 찾지 못했습니다.",
                    this);
                return false;
            }

            GameObject panelObject = Instantiate(loadingPanelPrefab, sceneCanvas.transform, false);
            panelObject.name = loadingPanelPrefab.name;

            if (panelObject.GetComponent<Canvas>() != null)
            {
                Debug.LogError(
                    "[LoadingManager] Loading Panel Prefab에는 Canvas를 넣지 마세요. 씬 Canvas의 자식 Panel이어야 합니다.",
                    panelObject);
                Destroy(panelObject);
                return false;
            }

            loadingPanelInstance = panelObject.GetComponent<LoadingPanelView>();
            if (loadingPanelInstance == null)
            {
                Debug.LogError(
                    "[LoadingManager] Loading Panel Prefab에 LoadingPanelView가 없습니다.",
                    panelObject);
                Destroy(panelObject);
                return false;
            }

            if (panelObject.transform is RectTransform panelRect)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.localRotation = Quaternion.identity;
                panelRect.localScale = Vector3.one;
            }

            panelObject.transform.SetAsLastSibling();
            panelObject.SetActive(true);
            return true;
        }

        private Canvas FindActiveSceneCanvas()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Canvas selectedCanvas = null;
            int candidateCount = 0;

            foreach (Canvas canvas in canvases)
            {
                if (canvas == null
                    || canvas.gameObject.scene != activeScene
                    || !canvas.gameObject.activeInHierarchy
                    || !canvas.isRootCanvas
                    || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                candidateCount++;
                if (selectedCanvas == null || canvas.sortingOrder > selectedCanvas.sortingOrder)
                {
                    selectedCanvas = canvas;
                }
            }

            if (candidateCount > 1)
            {
                Debug.LogWarning(
                    $"[LoadingManager] 활성 씬에 루트 Canvas가 {candidateCount}개 있습니다. " +
                    $"Sorting Order가 가장 높은 '{selectedCanvas.name}'을 사용합니다.",
                    selectedCanvas);
            }

            return selectedCanvas;
        }

        private void DestroyLoadingPanelInstance()
        {
            if (loadingPanelInstance != null)
            {
                Destroy(loadingPanelInstance.gameObject);
            }

            loadingPanelInstance = null;
            showTipInDescription = false;
        }

        private void ShowRandomTip()
        {
            string tip = GetRandomTip();
            showTipInDescription = loadingPanelInstance != null
                && !loadingPanelInstance.HasTipText
                && !string.IsNullOrWhiteSpace(tip);

            loadingPanelInstance?.SetTip(tip, showTipInDescription);
        }

        private string GetRandomTip()
        {
            if (loadingTips == null || loadingTips.Length == 0)
            {
                return string.Empty;
            }

            int startIndex = UnityEngine.Random.Range(0, loadingTips.Length);
            for (int i = 0; i < loadingTips.Length; i++)
            {
                string tip = loadingTips[(startIndex + i) % loadingTips.Length];
                if (!string.IsNullOrWhiteSpace(tip))
                {
                    return tip;
                }
            }

            return string.Empty;
        }

        private void Report(string description, float progress)
        {
            CurrentDescription = description;
            CurrentProgress = Mathf.Clamp01(progress);

            loadingPanelInstance?.SetProgress(
                CurrentDescription,
                CurrentProgress,
                !showTipInDescription);

            ProgressChanged?.Invoke(new LoadingProgress(CurrentDescription, CurrentProgress));
        }
    }
}
