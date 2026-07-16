using HDY.Territory;
using TMPro;
using UnityEngine;

namespace KMS
{
    public sealed class KMSGameClockUI : MonoBehaviour
    {
        [Header("Clock References")]
        [SerializeField] private KMSGameClockGraphic clockGraphic;
        [SerializeField] private TMP_Text periodLabel;
        [SerializeField] private GameTimeManager gameTimeManager;

        [Header("Standalone Scene Support")]
        [SerializeField] private bool createTimeSystemIfMissing = true;

        private void Awake()
        {
            EnsureGameTimeManager();
            RefreshClock();
        }

        private void Update()
        {
            EnsureGameTimeManager();
            RefreshClock();
        }

        //todo: 타이머 terrtorydata에서 분리필요
        private void EnsureGameTimeManager()
        {
            if (gameTimeManager != null) return;

            gameTimeManager = FindFirstObjectByType<GameTimeManager>();
            if (gameTimeManager != null || !createTimeSystemIfMissing) return;

            //TerritoryData territoryData = FindFirstObjectByType<TerritoryData>();
            GameObject timeSystemObject;

            /*if (territoryData != null)
            {
                timeSystemObject = territoryData.gameObject;
            }
            else
            {
                timeSystemObject = new GameObject("KMS Time System");
                territoryData = timeSystemObject.AddComponent<TerritoryData>();
            }*/
            timeSystemObject = new GameObject("KMS Time System");

            gameTimeManager = timeSystemObject.GetComponent<GameTimeManager>();
            if (gameTimeManager == null)
            {
                gameTimeManager = timeSystemObject.AddComponent<GameTimeManager>();
            }
        }

        private void RefreshClock()
        {
            if (clockGraphic == null || gameTimeManager == null) return;

            float dayLength = Mathf.Max(0.0001f, gameTimeManager.DayLengthSeconds);
            clockGraphic.SetProgress(gameTimeManager.InGameTimeOfDaySeconds / dayLength);

            if (periodLabel == null) return;

            bool isDay = clockGraphic.IsDay;
            periodLabel.text = isDay ? "DAY" : "NIGHT";
            periodLabel.color = isDay
                ? new Color(1f, 0.91f, 0.66f)
                : new Color(0.68f, 0.78f, 1f);
        }
    }
}
