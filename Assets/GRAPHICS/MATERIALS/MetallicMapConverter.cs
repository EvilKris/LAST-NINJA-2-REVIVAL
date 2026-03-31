using UnityEngine;
using UnityEditor;
using System.IO;

public class MetallicMapConverter
{
    [MenuItem("Assets/Convert to Unity Metallic Map", true)]
    static bool ValidateConvert()
    {
        return Selection.activeObject is Texture2D;
    }

    [MenuItem("Assets/Convert to Unity Metallic Map")]
    static void Convert()
    {
        Texture2D sourceTex = Selection.activeObject as Texture2D;
        string path = AssetDatabase.GetAssetPath(sourceTex);

        // Make sure texture is readable
        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(path);
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.sRGBTexture = false; // IMPORTANT
            importer.SaveAndReimport();
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        int width = tex.width;
        int height = tex.height;

        Texture2D newTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color[] pixels = tex.GetPixels();
        Color[] newPixels = new Color[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            float metallic = pixels[i].b;
            float smoothness = 1.0f - pixels[i].g;

            newPixels[i] = new Color(metallic, 0f, 0f, smoothness);
        }

        newTex.SetPixels(newPixels);
        newTex.Apply();

        // Save new file
        string newPath = Path.GetDirectoryName(path) + "/" +
                         Path.GetFileNameWithoutExtension(path) + "_UnityMetallic.png";

        File.WriteAllBytes(newPath, newTex.EncodeToPNG());

        AssetDatabase.Refresh();

        Debug.Log("Converted Metallic Map saved to: " + newPath);
    }
}