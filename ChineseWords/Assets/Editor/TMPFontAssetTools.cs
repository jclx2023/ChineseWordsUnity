using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

public class TMPFontAssetTools
{
    [MenuItem("CONTEXT/TMP_FontAsset/Extract And Replace Atlas", false, 2100)]
    static void ExtractAndReplaceAtlas(MenuCommand command)
    {
        // 1. 拿到当前被右键的 TMP_FontAsset
        TMP_FontAsset font = command.context as TMP_FontAsset;
        string fontPath = AssetDatabase.GetAssetPath(font);
        string folder = Path.GetDirectoryName(fontPath);
        string baseName = Path.GetFileNameWithoutExtension(fontPath);
        string pngPath = $"{folder}/{baseName} Atlas.png";

        // 2. 先把原贴图设为可读、拷贝、再设回不可读
        var originalTex = font.material.GetTexture(ShaderUtilities.ID_MainTex) as Texture2D;
        var so = new SerializedObject(originalTex);
        so.FindProperty("m_IsReadable").boolValue = true;
        so.ApplyModifiedProperties();

        Texture2D copy = Object.Instantiate(originalTex);
        so.FindProperty("m_IsReadable").boolValue = false;
        so.ApplyModifiedProperties();

        // 3. 导出为 PNG
        File.WriteAllBytes(pngPath, copy.EncodeToPNG());
        Object.DestroyImmediate(copy);
        AssetDatabase.Refresh();

        // 4. 删除原来嵌入的子资源（旧 Atlas）
        AssetDatabase.RemoveObjectFromAsset(originalTex);
        font.atlasTextures = new Texture2D[0];           // 清空引用
        EditorUtility.SetDirty(font);

        // 5. 把新导入的 PNG 作为外部贴图引用进来
        Texture2D newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (newTex != null)
        {
            font.atlasTextures = new Texture2D[] { newTex };
            font.material.SetTexture(ShaderUtilities.ID_MainTex, newTex);
            EditorUtility.SetDirty(font);
        }
        else
        {
            Debug.LogError($"没能找到导入的贴图：{pngPath}");
        }

        // 6. 保存所有改动
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[{font.name}] 内部 Atlas 已替换为外部文件：{pngPath}");
    }
}
