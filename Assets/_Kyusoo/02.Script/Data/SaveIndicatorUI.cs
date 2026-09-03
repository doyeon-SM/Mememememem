using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [멤] 세이브가 실행될 때 화면에 "저장중" 텍스트를 잠깐 띄워주는 표시기.
///
/// RecordManager.OnSaveStarted / OnSaveCompleted 이벤트만 구독하기 때문에,
/// 5분 자동저장이든 중요행동(여신상 해금 / 웨이포인트 등록 등) 즉시저장이든
/// "실제로 디스크에 쓰는 순간"에만 정확히 표시된다.
///
/// 세이브는 동기(블로킹) 작업이라 시작~완료가 같은 프레임에 끝난다. 그래서 완료를 기다렸다가
/// 숨기면 사람 눈에는 한 프레임도 안 보인다. 대신 minShowDuration(기본 1.5초) 동안 무조건
/// 띄워두고 페이드아웃한다.
///
/// [연결 방법]
/// 1) 아무것도 안 해도 된다 - RecordManager가 자기 오브젝트에 이 컴포넌트를 자동으로 붙이고,
///    전용 오버레이 캔버스를 런타임에 만들어 캐릭터 씬/영지 씬 양쪽에서 동일하게 보여준다.
///    (RecordManager는 DontDestroyOnLoad라 씬을 넘나들어도 유지된다.)
/// 2) 캐릭터HUD / 영지HUD 안의 특정 위치에 넣고 싶다면, HUD 오브젝트에 이 컴포넌트를 붙이고
///    targetLabel(TMP_Text)과 targetGroup(CanvasGroup)을 인스펙터에서 연결하면 된다.
///    이 경우 오버레이 캔버스는 만들지 않고 연결된 텍스트만 켜고 끈다.
/// </summary>
[DisallowMultipleComponent]
public class SaveIndicatorUI : MonoBehaviour
{
    [Header("[멤] 표시 문구")]
    [SerializeField] private string savingText = "저장중";

    [Header("[멤] 폰트 (비워두면 씬에서 한글 폰트를 자동으로 찾아 쓴다)")]
    [Tooltip("TMP 기본 폰트(LiberationSans)는 한글 글자가 없어 네모박스로 보인다. 비워두면 현재 씬의 다른 TMP 텍스트에서 한글 폰트를 가져온다.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("[멤] 연결 (비워두면 전용 오버레이를 자동 생성)")]
    [SerializeField] private CanvasGroup targetGroup;
    [SerializeField] private TMP_Text targetLabel;

    [Header("[멤] 연출 설정")]
    [Tooltip("저장이 끝나도 최소 이 시간만큼은 텍스트를 유지한다.")]
    [SerializeField, Min(0f)] private float minShowDuration = 1.5f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;

    [Header("[멤] 자동 생성 오버레이 설정")]
    [SerializeField] private Vector2 screenOffset = new Vector2(-48f, 48f);
    [SerializeField] private float fontSize = 26f;
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private int sortingOrder = 5000;

    private Coroutine showRoutine;
    private bool visualReady;

    /// <summary>
    /// [멤] RecordManager가 부팅할 때 호출한다. 이미 붙어있으면 그대로 쓰고, 없으면 새로 붙인다.
    /// </summary>
    /// <summary>
    /// [멤] RecordManager가 부팅할 때 호출한다. 이미 붙어있으면 그대로 쓰고, 없으면 새로 붙인다.
    /// </summary>
    public static SaveIndicatorUI EnsureAttached(GameObject host, TMP_FontAsset preferredFont = null)
    {
        if (host == null) return null;

        SaveIndicatorUI indicator = host.GetComponent<SaveIndicatorUI>();
        if (indicator == null)
        {
            indicator = host.AddComponent<SaveIndicatorUI>();
        }

        if (preferredFont != null)
        {
            indicator.fontAsset = preferredFont;
        }
        return indicator;
    }

    /// <summary>
    /// [멤] 표시 문구를 그려낼 수 있는 폰트를 보장한다.
    /// TMP 기본 폰트(LiberationSans SDF)에는 한글 글자가 없어서 "저장중"이 네모박스로 보인다.
    /// 그래서 인스펙터 지정 폰트 -> 현재 씬의 다른 TMP 텍스트 폰트 순으로 찾아서 가져온다(HUD 폰트 재사용).
    /// </summary>
    private void EnsureReadableFont()
    {
        if (targetLabel == null) return;

        if (fontAsset != null)
        {
            if (targetLabel.font != fontAsset) targetLabel.font = fontAsset;
            return;
        }

        if (CanRender(targetLabel.font, savingText)) return;

        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text == null || text == targetLabel) continue;
            if (!CanRender(text.font, savingText)) continue;

            fontAsset = text.font;
            targetLabel.font = fontAsset;
            return;
        }
    }

    private static bool CanRender(TMP_FontAsset font, string text)
    {
        if (font == null || string.IsNullOrEmpty(text)) return false;

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) continue;
            if (!font.HasCharacter(c, true)) return false;
        }
        return true;
    }

    private void Awake()
    {
        BuildVisualIfNeeded();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        RecordManager.OnSaveStarted += HandleSaveStarted;
    }

    private void OnDisable()
    {
        RecordManager.OnSaveStarted -= HandleSaveStarted;

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
        SetAlpha(0f);
    }

    private void HandleSaveStarted(RecordManager.SaveReason reason)
    {
        if (!isActiveAndEnabled) return;

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }
        showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (targetLabel != null)
        {
            EnsureReadableFont();
            targetLabel.text = savingText;
        }

        // 페이드 인
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(fadeInDuration <= 0f ? 1f : Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetAlpha(1f);

        // 최소 유지 시간 (게임이 멈춰 있어도 흐르도록 unscaled 사용)
        yield return new WaitForSecondsRealtime(minShowDuration);

        // 페이드 아웃
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(fadeOutDuration <= 0f ? 0f : 1f - Mathf.Clamp01(t / fadeOutDuration));
            yield return null;
        }
        SetAlpha(0f);

        showRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (targetGroup != null)
        {
            targetGroup.alpha = alpha;
            return;
        }

        if (targetLabel != null)
        {
            Color c = targetLabel.color;
            c.a = alpha;
            targetLabel.color = c;
        }
    }

    /// <summary>
    /// [멤] 인스펙터 연결이 없을 때만, 전용 오버레이 캔버스 + 텍스트를 런타임에 만들어준다.
    /// GraphicRaycaster를 붙이지 않으므로 클릭 입력을 절대 가로채지 않는다.
    /// </summary>
    private void BuildVisualIfNeeded()
    {
        if (visualReady) return;
        if (targetLabel != null)
        {
            visualReady = true;
            return;
        }

        GameObject canvasObject = new GameObject("MemSaveIndicatorCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject labelObject = new GameObject("SavingLabel");
        labelObject.transform.SetParent(canvasObject.transform, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = savingText;
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(300f, 48f);
        rect.anchoredPosition = screenOffset;

        targetLabel = label;
        targetGroup = labelObject.AddComponent<CanvasGroup>();
        targetGroup.blocksRaycasts = false;
        targetGroup.interactable = false;

        visualReady = true;
    }
}
