using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// GameTimeManager의 인게임 하루 진행도(InGameTimeOfDaySeconds / DayLengthSeconds)를 0~1로 정규화해서
    /// 시계 바늘을 회전시키고 DAY/NIGHT 상태를 표시하는 Canvas UI 컴포넌트.
    /// KMS팀의 UI Toolkit 시계(Assets/KMS/3.UI/HUD/KMSPlayerHUD.uxml, PlayerHUD.ApplyDayCycleProgress)와
    /// 동일한 규칙을 그대로 uGUI로 옮긴 것이다 - normalizedProgress가 0.5 미만이면 DAY, 이상이면 NIGHT이고,
    /// 배경(낮/밤 반원)은 고정된 이미지고 바늘(handPivot)만 0~360도로 회전한다.
    ///
    /// [매 프레임 직접 폴링] GameTimeManager.OnInGameTimeTextChanged 등의 이벤트는 표시 문자열이 초 단위로
    /// 바뀔 때만 발행되어 바늘이 뚝뚝 끊겨 보인다. 그래서 이벤트를 구독하지 않고 Update()에서 매 프레임
    /// InGameTimeOfDaySeconds를 직접 읽어 계산한다.
    ///
    /// [회전 방향] handPivot의 pivot은 (0.5, 0.5)이고 시계 원 중심에 anchoredPosition이 고정되어 있어야
    /// 한다(CSS의 transform-origin: 50% 50%에 대응). clockwise 옵션으로 회전 방향을 뒤집을 수 있다 - 만든
    /// 아트가 반대로 도는 것처럼 보이면 이 값을 꺼보면 된다.
    ///
    /// [상태 변화 시에만 갱신] isDay가 실제로 바뀐 프레임에만 라벨 텍스트/색과 마커 색을 다시 설정한다
    /// (GameTimeManager.RefreshInGameTime과 동일한 방식으로, 매 프레임 불필요한 대입을 피하기 위함).
    /// </summary>
    public class DayNightClockUI : MonoBehaviour
    {
        [Header("데이터 참조 (비어있으면 자동 탐색)")]
        [SerializeField] private GameTimeManager gameTimeManager;

        [Header("시계 바늘 (pivot 0.5,0.5 / 시계 원 중심에 고정되어 있어야 함)")]
        [SerializeField] private RectTransform handPivot;
        [Tooltip("체크 시 시간이 흐를수록 시계방향으로 회전. 아트가 반대로 도는 것처럼 보이면 꺼보세요.")]
        [SerializeField] private bool clockwise = true;

        [Header("바늘 끝 마커 (선택 사항)")]
        [SerializeField] private Image clockMarkerImage;
        [SerializeField] private Color dayMarkerColor = new Color(1f, 0.867f, 0.388f);
        [SerializeField] private Color nightMarkerColor = new Color(0.545f, 0.694f, 1f);

        [Header("DAY/NIGHT 라벨 (선택 사항)")]
        [SerializeField] private TMP_Text periodLabel;
        [SerializeField] private string dayText = "DAY";
        [SerializeField] private string nightText = "NIGHT";
        [SerializeField] private Color dayLabelColor = new Color(1f, 0.91f, 0.66f);
        [SerializeField] private Color nightLabelColor = new Color(0.68f, 0.78f, 1f);

        private bool hasAppliedOnce;
        private bool lastIsDay;

        private void Awake()
        {
            gameTimeManager = GameTimeManager.Resolve(gameTimeManager);

            if (gameTimeManager == null) Debug.LogWarning("[DayNightClockUI] GameTimeManager를 찾을 수 없습니다. 시계가 갱신되지 않습니다.", this);
            if (handPivot == null) Debug.LogWarning("[DayNightClockUI] handPivot이 비어있습니다.", this);
        }

        private void Update()
        {
            if (gameTimeManager == null)
            {
                gameTimeManager = GameTimeManager.Resolve(gameTimeManager);
                if (gameTimeManager == null) return;
            }

            float dayLength = Mathf.Max(0.0001f, gameTimeManager.DayLengthSeconds);
            float normalizedProgress = Mathf.Repeat(gameTimeManager.InGameTimeOfDaySeconds / dayLength, 1f);

            ApplyDayCycleProgress(normalizedProgress);
        }

        /// <summary>
        /// 0~1로 정규화된 하루 진행도를 받아 바늘 회전 + DAY/NIGHT 표시를 갱신한다. KMS의
        /// PlayerHUD.SetDayCycleProgress와 동일한 진입점 - 외부(에디터 프리뷰, 테스트 등)에서 직접 호출해도 된다.
        /// </summary>
        public void ApplyDayCycleProgress(float normalizedProgress)
        {
            normalizedProgress = Mathf.Repeat(normalizedProgress, 1f);
            bool isDay = normalizedProgress < 0.5f;

            if (handPivot != null)
            {
                float sign = clockwise ? -1f : 1f;
                handPivot.localRotation = Quaternion.Euler(0f, 0f, normalizedProgress * 360f * sign);
            }

            if (!hasAppliedOnce || isDay != lastIsDay)
            {
                hasAppliedOnce = true;
                lastIsDay = isDay;

                if (periodLabel != null)
                {
                    periodLabel.text = isDay ? dayText : nightText;
                    periodLabel.color = isDay ? dayLabelColor : nightLabelColor;
                }

                if (clockMarkerImage != null)
                {
                    clockMarkerImage.color = isDay ? dayMarkerColor : nightMarkerColor;
                }
            }
        }
    }
}
