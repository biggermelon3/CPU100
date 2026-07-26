using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a crisp 5x7 dot-matrix title as UI geometry.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MainMenuPixelTitleGraphic : MaskableGraphic
{
    public string title = "CPU 100%";
    public float pixelSize = 14f;
    public float pixelGap = 2f;

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'C', new[] { "11111", "10000", "10000", "10000", "10000", "10000", "11111" } },
        { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
        { 'U', new[] { "10001", "10001", "10001", "10001", "10001", "10001", "11111" } },
        { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
        { '0', new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
        { '%', new[] { "11001", "11010", "00100", "01000", "10011", "00011", "00000" } },
        { ' ', new[] { "000", "000", "000", "000", "000", "000", "000" } }
    };

    public float PreferredWidth
    {
        get
        {
            return Mathf.Max(1f, GetAdvanceWidth(title) - pixelSize);
        }
    }

    public float GetAdvanceWidth(string value)
    {
        float width = 0f;
        for (int i = 0; i < value.Length; i++)
        {
            int columns = value[i] == ' ' ? 3 : 5;
            width += columns * (pixelSize + pixelGap) + pixelSize;
        }
        return width;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        float startX = -PreferredWidth * 0.5f;
        float startY = 3f * (pixelSize + pixelGap);
        float cursor = startX;

        for (int character = 0; character < title.Length; character++)
        {
            char key = char.ToUpperInvariant(title[character]);
            if (!Glyphs.TryGetValue(key, out string[] rows)) continue;
            int columns = key == ' ' ? 3 : 5;

            for (int row = 0; row < 7; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (rows[row][column] != '1') continue;
                    AddPixel(vertexHelper,
                        cursor + column * (pixelSize + pixelGap),
                        startY - row * (pixelSize + pixelGap));
                }
            }
            cursor += columns * (pixelSize + pixelGap) + pixelSize;
        }
    }

    void AddPixel(VertexHelper vertexHelper, float x, float y)
    {
        int index = vertexHelper.currentVertCount;
        Color32 vertexColor = color;
        vertexHelper.AddVert(new Vector3(x, y), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(x + pixelSize, y), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(x + pixelSize, y + pixelSize), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(x, y + pixelSize), vertexColor, Vector2.zero);
        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index + 2, index + 3, index);
    }
}
