namespace KMS.Audio
{
    /// <summary>
    /// UI 코드가 오디오 카탈로그나 AudioSource를 직접 알지 않도록 하는 공통 진입점입니다.
    /// </summary>
    public static class KMSUIAudio
    {
        private static int lastPanelOpenFrame = -1;
        private static int lastPanelCloseFrame = -1;

        public static void PlayClick()
        {
            KMSAudioService.Play2D(GameSfxId.UIClick);
        }

        public static void PlayPanelOpen()
        {
            if (lastPanelOpenFrame == UnityEngine.Time.frameCount)
            {
                return;
            }

            lastPanelOpenFrame = UnityEngine.Time.frameCount;
            KMSAudioService.Play2D(GameSfxId.UIPanelOpen);
        }

        public static void PlayPanelClose()
        {
            if (lastPanelCloseFrame == UnityEngine.Time.frameCount)
            {
                return;
            }

            lastPanelCloseFrame = UnityEngine.Time.frameCount;
            KMSAudioService.Play2D(GameSfxId.UIPanelClose);
        }
    }
}
