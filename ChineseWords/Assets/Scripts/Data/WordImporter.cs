using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// 工具类：向 SQLite 数据库中批量插入 Word 和 WordChar 数据
/// </summary>
public static class WordImporter
{
    // 数据库连接字符串（指向 Assets/Resources/Questions/Temp.db）
    private static readonly string ConnString =
        "URI=file:" + Application.dataPath + "/Resources/Questions/Temp.db";
    /// <summary>
    /// 向 Word、WordChar 表插入一条词语及其拆字数据
    /// </summary>
    /// <param name="word">要插入的词语，例如“叶公好龙”</param>
    /// <param name="freq">词频权重，默认 1</param>
    public static void InsertWord(string word, int freq = 1)
    {
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                // 1) 插入 Word 表
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO Word(text, length, freq) VALUES(@text, @len, @freq);";
                    cmd.Parameters.AddWithValue("@text", word);
                    cmd.Parameters.AddWithValue("@len", word.Length);
                    cmd.Parameters.AddWithValue("@freq", freq);
                    cmd.ExecuteNonQuery();
                }

                // 2) 取出刚插入的自增主键，通过 SQL 查询 last_insert_rowid()
                long wordId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT last_insert_rowid();";
                    wordId = (long)cmd.ExecuteScalar();
                }

                // 3) 拆分字符并插入 WordChar 表
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

                // 提交事务
                tx.Commit();

                Debug.Log($"Inserted word '{word}' with id {wordId}");
            }
            conn.Close();
        }
    }
}
