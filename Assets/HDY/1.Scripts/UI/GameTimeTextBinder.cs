using UnityEngine;
using TMPro;
using HDY.Territory;

namespace HDY.UI
{
    /// <summary>
    /// GameTimeManager의 리얼타임 표시("00시 00분")에 이 오브젝트의 TMP_Text를 자동으로 등록하는 바인더.
    ///
    /// [왜 필요한가] GameTimeManager는 DontDestroyOnLoad라 씬이 바뀌어도 파괴되지 않지만, Awake도 다시
    /// 실행되지 않는다. 반면 실제 화면에 보이는 Text 오브젝트는 각 씬에 있는 평범한 UI 오브젝트라 씬이
    /// 바뀌면 파괴된다 - 즉 인스펙터로 realTimeText를 직접 연결해두면, 다음 씬으로 넘어가는 순간 그
    /// 참조가 끊긴다(파괴된 오브젝트를 가리키게 됨).
    ///
    /// [해결 방식] "매니저가 씬을 뒤져서 Text를 찾는" 방식 대신, "Text 쪽이 스스로 등록하는" 방식을 쓴다.
    /// 이 바인더는 씬에 새로 배치된 평범한 오브젝트라 씬이 바뀔 때마다 OnEnable이 새로 실행되므로, 그
    /// 시점에 GameTimeManager.Instance에 자기 자신(TMP_Text)을 등록한다 - 씬이 몇 번을 바뀌어도 항상 그
    /// 씬에 있는 Text로 자동 재연결된다.
    ///
    /// [인게임 시간은 지원하지 않음] 인게임 시간(00분 00초)은 더 이상 GameTimeManager가 Text로 직접
    /// 표시해주지 않는다. 필요하면 GameTimeManager.InGameTimeOfDaySeconds/OnInGameTimeTextChanged/
    /// GetInGameTimeText()를 직접 참조하는 화면 스크립트를 만들면 된다(예: DayNightClockUI).
    ///
    /// [사용법] 리얼타임 시계를 표시할 TMP_Text 오브젝트에 이 컴포넌트만 붙이면 끝 - GameTimeManager
    /// 쪽 인스펙터에 Text를 직접 연결할 필요가 없다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class GameTimeTextBinder : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(GameTimeManager.Resolve).")]
        [SerializeField] private GameTimeManager gameTimeManager;

        private TMP_Text text;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            gameTimeManager = GameTimeManager.Resolve(gameTimeManager);

            if (gameTimeManager == null)
            {
                Debug.LogWarning("[GameTimeTextBinder] GameTimeManager를 찾을 수 없어 등록하지 못했습니다.", this);
                return;
            }

            gameTimeManager.RegisterRealTimeText(text);
        }

        private void OnDisable()
        {
            if (gameTimeManager == null || text == null) return;

            gameTimeManager.UnregisterRealTimeText(text);
        }
    }
}
