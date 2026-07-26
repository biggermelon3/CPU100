using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Darkens every visual belonging to a main-menu button and reveals its selection
/// arrow while the pointer is over the button.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Range(0f, 1f)] public float hoverBrightness = 0.68f;
    public Image selectionArrow;
    public AudioClip hoverSfx;
    [Range(0f, 1f)] public float hoverSfxVolume = 0.8f;

    Graphic[] graphics;
    Color[] normalColors;

    void Awake()
    {
        AlignRaycastToButton();
        CacheVisuals();
        SetHovered(false);
    }

    void OnDisable()
    {
        SetHovered(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);

        AudioSource menuAudio = GetComponentInParent<AudioSource>();
        if (menuAudio != null && hoverSfx != null)
            menuAudio.PlayOneShot(hoverSfx, hoverSfxVolume);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    void CacheVisuals()
    {
        Graphic[] allGraphics = GetComponentsInChildren<Graphic>(true);
        int count = 0;
        for (int i = 0; i < allGraphics.Length; i++)
            if (allGraphics[i] != selectionArrow) count++;

        graphics = new Graphic[count];
        normalColors = new Color[count];
        int index = 0;
        for (int i = 0; i < allGraphics.Length; i++)
        {
            if (allGraphics[i] == selectionArrow) continue;
            graphics[index] = allGraphics[i];
            normalColors[index] = allGraphics[i].color;
            index++;
        }
    }

    void AlignRaycastToButton()
    {
        Graphic buttonGraphic = GetComponent<Graphic>();
        Graphic[] allGraphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < allGraphics.Length; i++)
            allGraphics[i].raycastTarget = allGraphics[i] == buttonGraphic;
    }

    void SetHovered(bool hovered)
    {
        if (graphics == null || normalColors == null) CacheVisuals();

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null) continue;
            Color color = normalColors[i];
            if (hovered)
            {
                color.r *= hoverBrightness;
                color.g *= hoverBrightness;
                color.b *= hoverBrightness;
            }
            graphics[i].color = color;
        }

        if (selectionArrow != null)
            selectionArrow.gameObject.SetActive(hovered);
    }
}
