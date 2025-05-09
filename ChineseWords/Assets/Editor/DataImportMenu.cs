using UnityEditor;
using UnityEngine;
using System.IO;

public static class DataImportMenu
{
    [MenuItem("Tools/�������/�� Text �ļ�����")]
    public static void ImportFromText()
    {
        // 1) ���ļ�
        string path = Application.dataPath + "/StreamingAssets/words.txt";
        if (!File.Exists(path))
        {
            Debug.LogError("�Ҳ����ʱ��ļ���" + path);
            return;
        }
        string[] lines = File.ReadAllLines(path);

        // 2) ��������
        int count = 0;
        foreach (var raw in lines)
        {
            string word = raw.Trim();
            if (string.IsNullOrEmpty(word)) continue;
            WordImporter.InsertWord(word);
            count++;
        }

        Debug.Log($"[DataImport] �ɹ����� {count} �����ﵽ SQLite��");
    }
}
