using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot software install effect rendered on an overlay canvas above the taskbar.
/// Purely cosmetic - the inventory install has already happened when this spawns.
/// </summary>
public class InstallFlyEffect : MonoBehaviour
{
    const float FlightDuration = 0.45f;
    const float TrailTime = 0.3f;
    const int OverlaySortingOrder = 1000;

    RectTransform iconRect;
    Image iconImage;
    InstallTrailGraphic trail;

    public static void Play(Sprite sprite, Bounds worldBounds, RectTransform targetSlot, Camera cam)
    {
        if (sprite == null || targetSlot == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector2 fromScreen = cam.WorldToScreenPoint(worldBounds.center);
        Vector2 toScreen = targetSlot.position;

        // Project the renderer's actual bounds instead of rebuilding them from the
        // sprite and lossyScale. This stays correct across build resolutions,
        // authored child offsets and parent transforms.
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            cam.WorldToScreenPoint(new Vector3(min.x, min.y, worldBounds.center.z)),
            cam.WorldToScreenPoint(new Vector3(min.x, max.y, worldBounds.center.z)),
            cam.WorldToScreenPoint(new Vector3(max.x, min.y, worldBounds.center.z)),
            cam.WorldToScreenPoint(new Vector3(max.x, max.y, worldBounds.center.z))
        };

        float minX = corners[0].x;
        float maxX = corners[0].x;
        float minY = corners[0].y;
        float maxY = corners[0].y;
        for (int i = 1; i < corners.Length; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            maxX = Mathf.Max(maxX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        Vector2 screenSize = new Vector2(maxX - minX, maxY - minY);
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        if (screenSize.y < 24f)
            screenSize.y = Mathf.Max(24f, screenSize.x / aspect);
        if (screenSize.x < 24f)
            screenSize.x = Mathf.Max(24f, screenSize.y * aspect);

        var go = new GameObject("InstallFlyFX", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        go.AddComponent<InstallFlyEffect>().Launch(sprite, screenSize, fromScreen, toScreen);
    }

    void Launch(Sprite sprite, Vector2 screenSize, Vector2 from, Vector2 to)
    {
        var trailGo = new GameObject("Trail", typeof(RectTransform));
        RectTransform trailRect = (RectTransform)trailGo.transform;
        trailRect.SetParent(transform, false);
        trailRect.anchorMin = Vector2.zero;
        trailRect.anchorMax = Vector2.one;
        trailRect.offsetMin = Vector2.zero;
        trailRect.offsetMax = Vector2.zero;
        trail = trailGo.AddComponent<InstallTrailGraphic>();
        trail.raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconRect = (RectTransform)iconGo.transform;
        iconRect.SetParent(transform, false);
        iconRect.sizeDelta = screenSize;
        iconRect.position = from;

        iconImage = iconGo.GetComponent<Image>();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        StartCoroutine(Fly(from, to));
    }

    IEnumerator Fly(Vector2 from, Vector2 to)
    {
        // Screen-space arc so the effect remains above all regular canvas UI.
        Vector2 mid = (from + to) * 0.5f;
        Vector2 dir = to - from;
        Vector2 side = dir.sqrMagnitude > 0.01f
            ? new Vector2(-dir.y, dir.x).normalized
            : Vector2.right;
        Vector2 control = mid + Vector2.up * 90f + side * 35f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / FlightDuration;
            float raw = Mathf.Clamp01(t);
            float k = raw * raw * (3f - 2f * raw);
            Vector2 a = Vector2.Lerp(from, control, k);
            Vector2 b = Vector2.Lerp(control, to, k);
            Vector2 position = Vector2.Lerp(a, b, k);

            iconRect.position = position;
            iconRect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.12f, k);
            trail.AddPoint(position);
            trail.Tick();
            yield return null;
        }

        iconImage.enabled = false;
        float fade = 0f;
        while (fade < TrailTime)
        {
            fade += Time.deltaTime;
            trail.Tick();
            yield return null;
        }

        Destroy(gameObject);
    }
}

/// <summary>Small allocation-free UI mesh used for the install trail.</summary>
class InstallTrailGraphic : MaskableGraphic
{
    const float Lifetime = 0.3f;
    const float MinPointDistance = 4f;

    struct TrailPoint
    {
        public Vector2 position;
        public float time;
    }

    readonly List<TrailPoint> points = new List<TrailPoint>(32);

    protected override void Awake()
    {
        base.Awake();
        color = Color.white;
    }

    public void AddPoint(Vector2 screenPosition)
    {
        Vector2 local = screenPosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (points.Count > 0 &&
            (points[points.Count - 1].position - local).sqrMagnitude <
            MinPointDistance * MinPointDistance)
            return;

        points.Add(new TrailPoint { position = local, time = Time.unscaledTime });
        SetVerticesDirty();
    }

    public void Tick()
    {
        float cutoff = Time.unscaledTime - Lifetime;
        while (points.Count > 0 && points[0].time < cutoff)
            points.RemoveAt(0);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 2) return;

        float now = Time.unscaledTime;
        for (int i = 1; i < points.Count; i++)
        {
            TrailPoint p0 = points[i - 1];
            TrailPoint p1 = points[i];
            Vector2 delta = p1.position - p0.position;
            if (delta.sqrMagnitude < 0.01f) continue;

            float age0 = Mathf.Clamp01((now - p0.time) / Lifetime);
            float age1 = Mathf.Clamp01((now - p1.time) / Lifetime);
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            float width0 = Mathf.Lerp(1f, 8f, 1f - age0);
            float width1 = Mathf.Lerp(1f, 8f, 1f - age1);

            Color c0 = new Color(0.25f, 0.5f, 1f, 1f - age0);
            Color c1 = new Color(0.45f, 0.9f, 1f, 1f - age1);
            int first = vh.currentVertCount;
            AddVertex(vh, p0.position - normal * width0, c0);
            AddVertex(vh, p0.position + normal * width0, c0);
            AddVertex(vh, p1.position + normal * width1, c1);
            AddVertex(vh, p1.position - normal * width1, c1);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }
    }

    static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
