using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using System.Collections.Generic;

namespace Kope.ModularSpriteAnimation.Editor
{
    public class GridAutoSlicer : EditorWindow
    {
        private Texture2D spriteSheet;
        private SpriteRowNamingData namingData;

        private int cellWidth = 64;
        private int cellHeight = 64;

        [MenuItem("Tools/Grid Auto Slicer")]
        static void Open() => GetWindow<GridAutoSlicer>("Grid Auto Slicer");

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
             "Slices a spritesheet into a grid and auto-names each frame.\n" +
             "Supports special frames, subcategories, top-down row mapping,\n" +
             "automatic bottom padding compensation, overflow rows,\n" +
             "and counts fully transparent frames so indices remain consistent.\n" +
             "Note: sprites must be top-aligned; this slicer does NOT handle extra top padding.",
             EditorStyles.helpBox
             );

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
            // AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            int totalRows = spriteSheet.height / cellHeight;
            int totalCols = spriteSheet.width / cellWidth;

            int extraBottom = spriteSheet.height % cellHeight;

            List<SpriteRect> rects = new();

            // Calculate total rows defined in SO
            int definedRowCount = 0;
            foreach (var r in namingData.rows)
            {
                int subCount = (r.rowData.subCategory == null || r.rowData.subCategory.Count == 0) ? 1 : r.rowData.subCategory.Count;
                definedRowCount += subCount;
            }

            // Get filename without extension
            string filename = System.IO.Path.GetFileNameWithoutExtension(path);

            for (int row = 0; row < totalRows; row++)
            {
                SpriteRowDataSpecial rowSettings = new();
                int searchAcc = 0;
                bool isOverflowRow = row >= definedRowCount;
                int overflowIndex = row - definedRowCount;

                if (!isOverflowRow)
                {
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
                }

                int subCatIndex = row - searchAcc;
                string normCategory = rowSettings.rowData.category;
                string normSub = (rowSettings.rowData.subCategory != null && rowSettings.rowData.subCategory.Count > subCatIndex)
                                 ? rowSettings.rowData.subCategory[subCatIndex] : "";

                int colFrameCounter = 0; // Absolute column index
                for (int col = 0; col < totalCols; col++)
                {
                    float y = extraBottom + (totalRows - 1 - row) * cellHeight;
                    Rect rect = new(col * cellWidth, y, cellWidth, cellHeight);

                    if (IsRectTransparent(spriteSheet, rect))
                    {
                        /// Skip transparent frames without creating rects
                        /// but still increment frame counter, so naming stays consistent.
                        /// and maintain correct naming indices.
                        colFrameCounter++;
                        continue;          // skip creating a rect
                    }


                    string finalName;

                    if (isOverflowRow)
                    {
                        // 3. OVERFLOW ROW NAMING (beyond SO definition)
                        finalName = $"{filename}_{overflowIndex}_{colFrameCounter}";
                    }
                    else
                    {
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
            importer.isReadable = false;
            importer.SaveAndReimport();
            Debug.Log("Slicing complete with mapped subcategories, reset indices, and overflow rows handled.");
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
}