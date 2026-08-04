using System.Collections.Generic;
using UnityEngine;

public class FacilityCollectUI : MonoBehaviour
{
    public static FacilityCollectUI Instance { get; private set; }

    [Header("프리팹 및 컨테이너")]
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private Transform bubbleParentContainer;

    private Camera mainCamera;
    private Dictionary<MonoBehaviour, FacilityBubbleUI> activeBubbles = new Dictionary<MonoBehaviour, FacilityBubbleUI>();
    private Queue<FacilityBubbleUI> bubblePool = new Queue<FacilityBubbleUI>();

    [Header("위치 조절 설정")]
    [Tooltip("시설 기준 버블의 높이 오프셋입니다. 수치를 줄이면 아래로 내려옵니다.")]
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0f, 1.0f, 0f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        foreach (var pair in activeBubbles)
        {
            MonoBehaviour facility = pair.Key;
            FacilityBubbleUI bubble = pair.Value;

            if (facility != null && bubble != null && bubble.gameObject.activeInHierarchy)
            {
                Vector3 worldPos = facility.transform.position + bubbleOffset;
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

                if (screenPos.z > 0)
                {
                    bubble.transform.position = screenPos;
                }
            }
        }
    }

    public void ShowBubble(MonoBehaviour facility, Sprite icon, Vector3 worldPos)
    {
        if (facility == null) return;

        if (!activeBubbles.TryGetValue(facility, out FacilityBubbleUI bubble))
        {
            bubble = GetBubbleFromPool();
            activeBubbles[facility] = bubble;
        }

        bubble.Setup(facility, icon, (f) => FacilityCollectManager.Instance.CollectSingleFacility(f));
        bubble.PlayPopShowAnimation();
    }

    public void HideBubble(MonoBehaviour facility)
    {
        if (facility == null) return;

        if (activeBubbles.TryGetValue(facility, out FacilityBubbleUI bubble))
        {
            activeBubbles.Remove(facility);

            if (bubble != null && bubble.gameObject.activeInHierarchy)
            {
                bubble.PlayCollectAnimation(() => ReturnToPool(bubble));
            }
            else
            {
                ReturnToPool(bubble);
            }
        }
    }

    public void RemoveBubble(MonoBehaviour facility)
    {
        HideBubble(facility);
    }

    /// <summary>
    /// 🌟 특정 시설의 말풍선 하나만 수령 애니메이션 재생 후 닫기
    /// </summary>
    public void AnimateCollectSingleBubble(MonoBehaviour facility)
    {
        if (facility == null) return;

        if (activeBubbles.TryGetValue(facility, out FacilityBubbleUI bubble))
        {
            activeBubbles.Remove(facility);
            bubble.PlayCollectAnimation(() => ReturnToPool(bubble));
        }
    }

    private FacilityBubbleUI GetBubbleFromPool()
    {
        FacilityBubbleUI bubble = null;
        if (bubblePool.Count > 0)
        {
            bubble = bubblePool.Dequeue();
        }
        else
        {
            GameObject obj = Instantiate(bubblePrefab, bubbleParentContainer != null ? bubbleParentContainer : transform);
            bubble = obj.GetComponent<FacilityBubbleUI>();
        }

        bubble.gameObject.SetActive(true);
        return bubble;
    }

    private void ReturnToPool(FacilityBubbleUI bubble)
    {
        if (bubble == null) return;
        bubble.gameObject.SetActive(false);
        if (!bubblePool.Contains(bubble))
        {
            bubblePool.Enqueue(bubble);
        }
    }
}