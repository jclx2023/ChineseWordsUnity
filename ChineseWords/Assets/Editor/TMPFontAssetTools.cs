using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

public class TMPFontAssetTools
{
    [MenuItem("CONTEXT/TMP_FontAsset/Extract And Replace Atlas", false, 2100)]
    static void ExtractAndReplaceAtlas(MenuCommand command)
    {
        // 1. �õ���ǰ���Ҽ��� TMP_FontAsset
        TMP_FontAsset font = command.context as TMP_FontAsset;
        string fontPath = AssetDatabase.GetAssetPath(font);
        string folder = Path.GetDirectoryName(fontPath);
        string baseName = Path.GetFileNameWithoutExtension(fontPath);
        string pngPath = $"{folder}/{baseName} Atlas.png";

        // 2. �Ȱ�ԭ��ͼ��Ϊ�ɶ�������������ز��ɶ�
        var originalTex = font.material.GetTexture(ShaderUtilities.ID_MainTex) as Texture2D;
        var so = new SerializedObject(originalTex);
        so.FindProperty("m_IsReadable").boolValue = true;
        so.ApplyModifiedProperties();

        Texture2D copy = Object.Instantiate(originalTex);
        so.FindProperty("m_IsReadable").boolValue = false;
        so.ApplyModifiedProperties();

        // 3. ����Ϊ PNG
        File.WriteAllBytes(pngPath, copy.EncodeToPNG());
        Object.DestroyImmediate(copy);
        AssetDatabase.Refresh();

        // 4. ɾ��ԭ��Ƕ�������Դ���� Atlas��
        AssetDatabase.RemoveObjectFromAsset(originalTex);
        font.atlasTextures = new Texture2D[0];           // �������
        EditorUtility.SetDirty(font);

        // 5. ���µ���� PNG ��Ϊ�ⲿ��ͼ���ý���
        Texture2D newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (newTex != null)
        {
            font.atlasTextures = new Texture2D[] { newTex };
            font.material.SetTexture(ShaderUtilities.ID_MainTex, newTex);
            EditorUtility.SetDirty(font);
        }
        else
        {
            Debug.LogError($"û���ҵ��������ͼ��{pngPath}");
        }

        // 6. �������иĶ�
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[{font.name}] �ڲ� Atlas ���滻Ϊ�ⲿ�ļ���{pngPath}");
    }
}
