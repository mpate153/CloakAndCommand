using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolls a <see cref="RectTransform"/> upward. Resets start position after layout/TMP so the roll begins
/// below the visible area (not mid-scroll). Disables a sibling <see cref="ScrollRect"/> on the same object if present — it fights manual motion.
/// </summary>
[DefaultExecutionOrder(50)]
public class CreditsScroll : MonoBehaviour
{
    [Header("Credits block")]
    [Tooltip("If null, uses this object's RectTransform.")]
    [SerializeField] RectTransform creditsContent;

    [Tooltip("Pixels per second (anchored/local Y).")]
    [SerializeField] float scrollSpeed = 45f;

    [Tooltip("When true, Y is set so content starts just below this rect (or below the content's parent).")]
    [SerializeField] bool placeBelowVisibleArea = true;

    [Tooltip("Usually the panel/mask that clips credits. If empty, uses the content's parent RectTransform.")]
    [SerializeField] RectTransform visibleArea;

    [SerializeField] float paddingBelow = 48f;

    [Tooltip("Used when placeBelowVisibleArea is off, or as fallback.")]
    [SerializeField] float startOffsetY = -1600f;

    [SerializeField] bool useUnscaledTime = true;

    [Header("Scrolling background (RawImage)")]
    [SerializeField] RawImage tiledBackground;
    [SerializeField] Vector2 uvScrollPerSecond;

    [Header("Parallax")]
    [SerializeField] RectTransform[] parallaxLayers;
    [SerializeField] float parallaxSpeedScale = 0.3f;
    [SerializeField] float[] parallaxSpeeds;

    Rect _backgroundUvBase;
    bool _hasBackgroundUvBase;
    Coroutine _resetCo;
    bool _positionReady;

    void Awake()
    {
        if (tiledBackground != null)
        {
            _backgroundUvBase = tiledBackground.uvRect;
            _hasBackgroundUvBase = true;
        }

        var sr = GetComponent<ScrollRect>();
        if (sr != null)
            sr.enabled = false;
    }

    void OnEnable()
    {
        _positionReady = false;
        ResetBackgroundUv();
        if (_resetCo != null)
            StopCoroutine(_resetCo);
        _resetCo = StartCoroutine(ApplyStartAfterLayout());
    }

    void OnDisable()
    {
        if (_resetCo != null)
        {
            StopCoroutine(_resetCo);
            _resetCo = null;
        }
    }

    IEnumerator ApplyStartAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        var target = TargetContentRect;
        if (target != null)
        {
            var parent = target.parent as RectTransform;
            if (parent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        }

        ApplyStartPosition();
        _positionReady = true;
    }

    void ApplyStartPosition()
    {
        var target = TargetContentRect;
        if (target == null)
        {
            var pos = transform.localPosition;
            pos.y = startOffsetY;
            transform.localPosition = pos;
            return;
        }

        float y;
        if (placeBelowVisibleArea)
        {
            var view = visibleArea != null ? visibleArea : target.parent as RectTransform;
            if (view != null)
                y = -view.rect.height - paddingBelow;
            else
                y = startOffsetY;
        }
        else
            y = startOffsetY;

        var p = target.anchoredPosition;
        p.y = y;
        target.anchoredPosition = p;
    }

    void ResetBackgroundUv()
    {
        if (tiledBackground != null && _hasBackgroundUvBase)
            tiledBackground.uvRect = _backgroundUvBase;
    }

    RectTransform TargetContentRect => creditsContent != null ? creditsContent : transform as RectTransform;

    void Update()
    {
        if (!_positionReady)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        var rt = TargetContentRect;
        if (rt != null)
        {
            var p = rt.anchoredPosition;
            p.y += scrollSpeed * dt;
            rt.anchoredPosition = p;
        }
        else
        {
            var pos = transform.localPosition;
            pos.y += scrollSpeed * dt;
            transform.localPosition = pos;
        }

        ScrollBackgroundUv(dt);
        ScrollParallax(dt);
    }

    void ScrollBackgroundUv(float dt)
    {
        if (tiledBackground == null || uvScrollPerSecond == Vector2.zero) return;

        var r = tiledBackground.uvRect;
        r.x += uvScrollPerSecond.x * dt;
        r.y += uvScrollPerSecond.y * dt;
        tiledBackground.uvRect = r;
    }

    void ScrollParallax(float dt)
    {
        if (parallaxLayers == null) return;

        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;

            float speed = scrollSpeed * parallaxSpeedScale;
            if (parallaxSpeeds != null && i < parallaxSpeeds.Length)
                speed = parallaxSpeeds[i];

            var p = parallaxLayers[i].anchoredPosition;
            p.y += speed * dt;
            parallaxLayers[i].anchoredPosition = p;
        }
    }
}
