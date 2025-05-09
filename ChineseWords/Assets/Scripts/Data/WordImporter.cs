using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// 工具类：向 SQLite 数据库中批量插入 Word 和 WordChar 数据，自动跳过已存在的词
/// </summary>
public static class WordImporter
{
    // 数据库连接字符串（指向 Assets/StreamingAssets/Temp.db）
    private static readonly string ConnString =
        "URI=file:" + Application.streamingAssetsPath + "/Temp.db";

    /// <summary>
    /// 向 Word、WordChar 表插入一条词语及其拆字数据
    /// 若 Word.text 已存在，则跳过。
    /// </summary>
    /// <param name="word">要插入的词语，例如“叶公好龙”</param>
    /// <param name="freq">词频权重，默认 1</param>
    public static void InsertWord(string word, int freq = 1)
    {
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();

            // 1) 检查是否已存在
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(1) FROM Word WHERE text = @text";
                checkCmd.Parameters.AddWithValue("@text", word);
                long exists = (long)checkCmd.ExecuteScalar();
                if (exists > 0)
                {
                    Debug.Log($"Skipping duplicate word: '{word}'");
                    return;
                }
            }

            using (var tx = conn.BeginTransaction())
            {
                // 2) 插入 Word 表
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO Word(text, length, freq) VALUES(@text, @len, @freq);";
                    cmd.Parameters.AddWithValue("@text", word);
                    cmd.Parameters.AddWithValue("@len", word.Length);
                    cmd.Parameters.AddWithValue("@freq", freq);
                    cmd.ExecuteNonQuery();
                }

                // 3) 获取自增主键
                long wordId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT last_insert_rowid();";
                    wordId = (long)cmd.ExecuteScalar();
                }

                // 4) 拆分字符并插入 WordChar 表
                for (int i = 0; i < word.Length; i++)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText =
                            "INSERT INTO WordChar(word_id, char, pos) VALUES(@id, @ch, @pos);";
                        cmd.Parameters.AddWithValue("@id", wordId);
                        cmd.Parameters.AddWithValue("@ch", word[i].ToString());
                        cmd.Parameters.AddWithValue("@pos", i);
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                Debug.Log($"Inserted word '{word}' with id {wordId}");
            }

            conn.Close();
        }
    }
}
