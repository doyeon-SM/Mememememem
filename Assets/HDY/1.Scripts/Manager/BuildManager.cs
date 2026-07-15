using UnityEngine;

namespace HDY.Bootstrap
{
    public class BuildManager : MonoBehaviour
    {
        [Tooltip("제한할 목표 프레임 레이트. 기본 60")]
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Debug.Log($"[BuildManager] targetFrameRate = {targetFrameRate}, VSync 꺼짐");
        }
    }
}
