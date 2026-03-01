using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int id = 1; // playerIndex 1..4 OR teamIndex 1..2 (depending on mode)
    public int Id => id;

    [Header("UI Refs")]
    [SerializeField] private TMP_Text leftLabelTMP;
    [SerializeField] private TMP_Text rankTMP;
    [SerializeField] private TMP_Text scoreTMP;          // only shown for local player (or optional for teams)
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private Graphic highlightGraphicForName;  
    [SerializeField] private Graphic highlightGraphicForRank;  

    [Header("Bar (Slider as handle anchor)")]
    [SerializeField] private Slider slider;              // used only for handle positioning
    [SerializeField] private RectTransform handleRect;

    [Header("Layout Tuning")]
    [Tooltip("Reserve some bar space so score text doesn't overlap the left label.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float barStartNormalized = 0.12f;

    [SerializeField] private float handleTextPadding = 10f; // px
    [SerializeField] private float minFillPx = 10f;         // px

    private RectTransform _rt;

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (slider != null && handleRect == null)
            handleRect = slider.handleRect;
    }

    public void SetId(int newId) => id = newId;

    public void SetLeftLabel(string s)
    {
        if (leftLabelTMP) leftLabelTMP.text = s;
    }

    public void SetRank(string s, bool isFirst)
    {
        if (rankTMP) rankTMP.text = s;
        if (crownIcon) crownIcon.SetActive(isFirst);
    }

    public void SetScoreVisible(bool visible)
    {
        if (scoreTMP) scoreTMP.gameObject.SetActive(visible);
    }

    public void SetScoreValue(int score)
    {
        if (scoreTMP) scoreTMP.text = score.ToString() + " pts";
    }

    public void SetHighlight(bool on)
    {
        if (highlightGraphicForName) highlightGraphicForName.enabled = on;
        if (highlightGraphicForRank) highlightGraphicForRank.enabled = on;
    }

    /// <summary>
    /// score01 is 0..1 relative to maxScore. We remap to [barStartNormalized..1].
    /// This keeps the handle away from 0 so score text has room.
    /// </summary>
    public void SetBarNormalized(float score01)
    {
        score01 = Mathf.Clamp01(score01);

        float remapped = Mathf.Lerp(barStartNormalized, 1f, score01);

        if (slider != null)
        {
            // Keep slider in 0..1 mode; use normalizedValue directly.
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = remapped;
        }

        UpdateHandleAnchoredTexts();
    }

    /// <summary>
    /// Put score text left of handle, rank text right of handle.
    /// Both move with handle. Rank always visible. Score may be hidden per-row.
    /// </summary>
    private void UpdateHandleAnchoredTexts()
    {
        if (handleRect == null) return;

        // Convert handle world position into THIS entry's local UI space
        Vector3 handleWorld = handleRect.TransformPoint(handleRect.rect.center);
        Vector2 localHandlePos = ((RectTransform)transform).InverseTransformPoint(handleWorld);

        // Rank sits right of handle
        if (rankTMP != null)
        {
            var rrt = rankTMP.rectTransform;
            Vector2 p = localHandlePos;
            p.x += handleTextPadding;
            rrt.anchoredPosition = p;
            rrt.pivot = new Vector2(0f, 0.5f);
        }

        if(highlightGraphicForRank != null)
        {
            var rrt = highlightGraphicForRank.rectTransform;
            Vector2 p = localHandlePos;
            p.x += handleTextPadding;
            rrt.anchoredPosition = p;
            rrt.pivot = new Vector2(0f, 0.5f);
        }
    }

    public RectTransform Rect => _rt;
}
