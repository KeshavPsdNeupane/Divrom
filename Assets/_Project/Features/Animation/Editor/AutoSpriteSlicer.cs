using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using System.Collections.Generic;
using Codice.Client.BaseCommands.WkStatus.Printers;

public class GridAutoSlicer : EditorWindow
{
    private Texture2D spriteSheet;
    private SpriteRowNamingData namingData;

    private int cellWidth = 32;
    private int cellHeight = 32;

    [MenuItem("Tools/Grid Auto Slicer")]
    static void Open() => GetWindow<GridAutoSlicer>("Grid Auto Slicer");

    private void OnGUI()
    {
        spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", spriteSheet, typeof(Texture2D), false);
        namingData = (SpriteRowNamingData)EditorGUILayout.ObjectField("Row Naming Data", namingData, typeof(SpriteRowNamingData), false);

        cellWidth = EditorGUILayout.IntField("Cell Width", cellWidth);
        cellHeight = EditorGUILayout.IntField("Cell Height", cellHeight);

        if (GUILayout.Button("Slice & Auto Name"))
        {
            if (spriteSheet && namingData)
                Slice();
            else
                Debug.LogWarning("Assign both a spritesheet and a naming data asset.");
        }
    }

    private void Slice()
    {
        string path = AssetDatabase.GetAssetPath(spriteSheet);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.isReadable = true;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        int totalRows = spriteSheet.height / cellHeight;
        int totalCols = spriteSheet.width / cellWidth;
        List<SpriteRect> rects = new();

        for (int row = 0; row < totalRows; row++)
        {
            SpriteRowDataSpecial rowSettings = new();
            int searchAcc = 0;

            foreach (var r in namingData.rows)
            {
                int subCount = (r.rowData.subCategory == null || r.rowData.subCategory.Count == 0) ? 1 : r.rowData.subCategory.Count;
                if (row >= searchAcc && row < searchAcc + subCount)
                {
                    rowSettings = r;
                    break;
                }
                searchAcc += subCount;
            }

            int subCatIndex = row - searchAcc;
            string normCategory = rowSettings.rowData.category;
            string normSub = (rowSettings.rowData.subCategory != null && rowSettings.rowData.subCategory.Count > subCatIndex)
                             ? rowSettings.rowData.subCategory[subCatIndex] : "";

            int colFrameCounter = 0; // Absolute column index
            for (int col = 0; col < totalCols; col++)
            {
                Rect rect = new Rect(col * cellWidth, (totalRows - 1 - row) * cellHeight, cellWidth, cellHeight);
                if (IsRectTransparent(spriteSheet, rect)) continue;

                string finalName;

                bool isSpecial = rowSettings.hasSpecialSprites &&
                                 colFrameCounter >= rowSettings.specialStartIndex &&
                                 colFrameCounter < (rowSettings.specialStartIndex + rowSettings.specialSize);

                if (isSpecial)
                {
                    // 1. SPECIAL NAMING
                    int specFrameIdx = colFrameCounter - rowSettings.specialStartIndex;
                    string specCat = rowSettings.specialData.category;
                    string specSub = (rowSettings.specialData.subCategory != null && rowSettings.specialData.subCategory.Count > subCatIndex)
                                     ? rowSettings.specialData.subCategory[subCatIndex] : "";

                    string baseName = string.IsNullOrEmpty(specSub) ? specCat : $"{specCat}_{specSub}";

                    // Rule: If size is 1, ignore "_index"
                    finalName = (rowSettings.specialSize == 1) ? baseName : $"{baseName}_{specFrameIdx}";
                }
                else
                {
                    // 2. NORMAL NAMING with RESET INDEX
                    int normalIdx;

                    if (rowSettings.hasSpecialSprites && colFrameCounter >= (rowSettings.specialStartIndex + rowSettings.specialSize))
                    {
                        // If we are AFTER the special frames, start from 0
                        normalIdx = colFrameCounter - (rowSettings.specialStartIndex + rowSettings.specialSize);
                    }
                    else
                    {
                        // Before the special frames (or if no special frames exist)
                        normalIdx = colFrameCounter;
                    }

                    finalName = string.IsNullOrEmpty(normSub) ? $"{normCategory}_{normalIdx}" : $"{normCategory}_{normSub}_{normalIdx}";
                }

                rects.Add(new SpriteRect
                {
                    name = finalName,
                    rect = rect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });

                colFrameCounter++;
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        provider.Apply();
        importer.SaveAndReimport();
        Debug.Log("Slicing complete with mapped subcategories and reset indices.");
    }

    private bool IsRectTransparent(Texture2D texture, Rect rect)
    {
        int xMin = Mathf.RoundToInt(rect.x);
        int yMin = Mathf.RoundToInt(rect.y);
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        Color32[] pixels = texture.GetPixels32();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int px = xMin + x;
                int py = yMin + y;
                int index = py * texture.width + px;

                if (pixels[index].a != 0)
                    return false; // has visible pixels
            }
        }

        return true; // fully transparent
    }
}
