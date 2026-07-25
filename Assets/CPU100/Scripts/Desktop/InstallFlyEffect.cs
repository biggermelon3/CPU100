using System.Collections;
using UnityEngine;

/// <summary>
/// One-shot "software flies into the taskbar" effect: a shrinking copy of the icon
/// sprite arcs from the drop position to the target slot with a trail behind it.
/// Purely cosmetic - the inventory install has already happened when this spawns.
/// Spawned by CursorInteractor via the static Play entry point.
/// </summary>
public class InstallFlyEffect : MonoBehaviour
{
    const float FlightDuration = 0.45f;
    const float TrailTime = 0.3f;

    public static void Play(Sprite sprite, Vector3 startScale, Vector3 fromWorld,
        RectTransform targetSlot, Camera cam)
    {
        if (targetSlot == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Overlay-canvas rect positions are screen pixels; project onto the world plane.
        Vector3 slotScreen = targetSlot.position;
        Vector3 to = cam.ScreenToWorldPoint(new Vector3(slotScreen.x, slotScreen.y, -cam.transform.position.z));
        to.z = 0f;

        var go = new GameObject("InstallFlyFX");
        go.transform.position = fromWorld;
        go.AddComponent<InstallFlyEffect>().Launch(sprite, startScale, fromWorld, to);
    }

    void Launch(Sprite sprite, Vector3 startScale, Vector3 from, Vector3 to)
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 500;

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = TrailTime;
        trail.startWidth = 0.3f;
        trail.endWidth = 0.02f;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.sortingOrder = 499;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.25f, 0.5f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = grad;

        StartCoroutine(Fly(sr, trail, startScale, from, to));
    }

    IEnumerator Fly(SpriteRenderer sr, TrailRenderer trail, Vector3 startScale, Vector3 from, Vector3 to)
    {
        // Arc control point: lift the path up and sideways so the flight reads as a
        // toss into the bar instead of a straight slide.
        Vector3 mid = (from + to) * 0.5f;
        Vector3 dir = to - from;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f).normalized;
        Vector3 control = mid + Vector3.up * 0.8f + side * 0.4f;

        Vector3 endScale = startScale * 0.12f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / FlightDuration;
            float k = Mathf.Clamp01(t);
            k = k * k * (3f - 2f * k); // smoothstep ease
            Vector3 a = Vector3.Lerp(from, control, k);
            Vector3 b = Vector3.Lerp(control, to, k);
            transform.position = Vector3.Lerp(a, b, k);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, k);
            yield return null;
        }

        // Let the trail fade out where the sprite vanished, then clean up.
        sr.enabled = false;
        trail.emitting = false;
        yield return new WaitForSeconds(TrailTime);
        Destroy(gameObject);
    }
}
