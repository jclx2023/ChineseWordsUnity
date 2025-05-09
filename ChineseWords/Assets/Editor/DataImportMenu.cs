using UnityEditor;
using UnityEngine;
using System.IO;

public static class DataImportMenu
{
    [MenuItem("Tools/导入题库/从 Text 文件导入")]
    public static void ImportFromText()
    {
        // 1) 读文件
        string path = Application.dataPath + "/StreamingAssets/words.txt";
        if (!File.Exists(path))
        {
            Debug.LogError("找不到词表文件：" + path);
            return;
        }
        string[] lines = File.ReadAllLines(path);

        // 2) 逐条调用
        int count = 0;
        foreach (var raw in lines)
        {
            string word = raw.Trim();
            if (string.IsNullOrEmpty(word)) continue;
            WordImporter.InsertWord(word);
            count++;
        }

        Debug.Log($"[DataImport] 成功导入 {count} 条词语到 SQLite。");
    }
}
