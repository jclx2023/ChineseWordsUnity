﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using Core;
using Core.Network;

namespace GameLogic.Choice
{
    /// <summary>
    /// 词语解释选择题管理器 - UI架构重构版
    /// 
    /// 功能说明：
    /// - 从WordExplanationChoice表随机取一条记录
    /// - 显示题干和四个选项，玩家选择后进行验证
    /// - 支持选项随机打乱，确保题目公平性
    /// - Host端生成题目数据和选项验证信息
    /// </summary>
    public class ExplanationChoiceQuestionManager : NetworkQuestionManagerBase, IQuestionDataProvider
    {
        private string dbPath;
        // 当前题目数据
        private string correctOption;
        public QuestionType QuestionType => QuestionType.ExplanationChoice;

        #region Unity生命周期

        protected override void Awake()
        {
            base.Awake();
            dbPath = Application.streamingAssetsPath + "/dictionary.db";

            LogDebug("ExplanationChoice题型管理器初始化完成");
        }

        #endregion

        #region 题目数据生成

        /// <summary>
        /// 获取题目数据（IQuestionDataProvider接口实现）
        /// 为Host端抽题使用，生成词语解释选择题目和选项数据
        /// </summary>
        public NetworkQuestionData GetQuestionData()
        {
            LogDebug("开始生成ExplanationChoice题目数据");

            try
            {
                string stem = null;
                List<string> choices = new List<string>(4);
                string correctAnswer = null;

                // 从数据库获取题目数据
                if (!GetQuestionFromDatabase(out stem, out correctAnswer, out choices))
                {
                    LogError("无法从数据库获取题目数据");
                    return null;
                }

                // 随机打乱选项
                ShuffleChoices(choices);

                // 创建题目数据
                var questionData = new NetworkQuestionData
                {
                    questionType = QuestionType.ExplanationChoice,
                    questionText = stem,
                    correctAnswer = correctAnswer,
                    options = choices.ToArray(),
                    timeLimit = 30f,
                    additionalData = JsonUtility.ToJson(new ExplanationChoiceAdditionalData
                    {
                        source = "ExplanationChoiceQuestionManager"
                    })
                };

                LogDebug($"ExplanationChoice题目生成成功: {stem}");
                return questionData;
            }
            catch (System.Exception e)
            {
                LogError($"生成ExplanationChoice题目失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从数据库获取题目数据
        /// </summary>
        private bool GetQuestionFromDatabase(out string stem, out string correctAnswer, out List<string> choices)
        {
            stem = null;
            correctAnswer = null;
            choices = new List<string>(4);

            try
            {
                using (var conn = new SqliteConnection("URI=file:" + dbPath))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT stem, [true], false1, false2, false3
                            FROM WordExplanationChoice
                            ORDER BY RANDOM()
                            LIMIT 1";

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stem = reader.GetString(0);
                                correctAnswer = reader.GetString(1);

                                choices.Add(reader.GetString(1)); // true
                                choices.Add(reader.GetString(2)); // false1
                                choices.Add(reader.GetString(3)); // false2
                                choices.Add(reader.GetString(4)); // false3

                                return true;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"数据库查询失败: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// 随机打乱选项
        /// </summary>
        private void ShuffleChoices(List<string> choices)
        {
            for (int i = choices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string temp = choices[i];
                choices[i] = choices[j];
                choices[j] = temp;
            }
        }

        #endregion

        #region 网络题目处理

        /// <summary>
        /// 加载网络题目数据
        /// </summary>
        protected override void LoadNetworkQuestion(NetworkQuestionData networkData)
        {
            LogDebug("加载网络ExplanationChoice题目");

            if (!ValidateNetworkData(networkData))
            {
                LogError("网络题目数据验证失败");
                return;
            }

            if (!ParseQuestionData(networkData))
            {
                LogError("解析网络题目数据失败");
                return;
            }

            LogDebug($"网络ExplanationChoice题目加载完成: {networkData.questionText}");
        }

        /// <summary>
        /// 验证网络数据
        /// </summary>
        private bool ValidateNetworkData(NetworkQuestionData networkData)
        {
            if (networkData == null)
            {
                LogError("网络题目数据为空");
                return false;
            }

            if (networkData.questionType != QuestionType.ExplanationChoice)
            {
                LogError($"题目类型不匹配，期望: {QuestionType.ExplanationChoice}, 实际: {networkData.questionType}");
                return false;
            }

            if (networkData.options == null || networkData.options.Length == 0)
            {
                LogError("选项数据为空");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析题目数据
        /// </summary>
        private bool ParseQuestionData(NetworkQuestionData questionData)
        {
            try
            {
                correctOption = questionData.correctAnswer;

                LogDebug($"题目数据解析成功: 正确答案={correctOption}");
                return true;
            }
            catch (System.Exception e)
            {
                LogError($"解析题目数据失败: {e.Message}");
                return false;
            }
        }

        #endregion

        #region 答案验证

        /// <summary>
        /// 静态答案验证方法 - 供Host端调用
        /// 基于题目数据验证答案，不依赖实例状态
        /// </summary>
        public static bool ValidateAnswerStatic(string answer, NetworkQuestionData question)
        {
            Debug.Log($"[ExplanationChoice] 静态验证答案: {answer}");

            if (string.IsNullOrEmpty(answer?.Trim()) || question == null)
            {
                Debug.Log("[ExplanationChoice] 静态验证: 答案为空或题目数据无效");
                return false;
            }

            if (question.questionType != QuestionType.ExplanationChoice)
            {
                Debug.LogError($"[ExplanationChoice] 静态验证: 题目类型不匹配，期望ExplanationChoice，实际{question.questionType}");
                return false;
            }

            try
            {
                string correctAnswer = question.correctAnswer?.Trim();
                if (string.IsNullOrEmpty(correctAnswer))
                {
                    Debug.Log("[ExplanationChoice] 静态验证: 题目数据中缺少正确答案");
                    return false;
                }

                bool isCorrect = answer.Trim().Equals(correctAnswer, System.StringComparison.Ordinal);
                Debug.Log($"[ExplanationChoice] 静态验证结果: 用户答案='{answer.Trim()}', 正确答案='{correctAnswer}', 结果={isCorrect}");

                return isCorrect;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ExplanationChoice] 静态验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 本地答案验证（用于单机模式或网络模式的本地验证）
        /// </summary>
        protected override void CheckLocalAnswer(string answer)
        {
            LogDebug($"验证本地答案: {answer}");

            bool isCorrect = ValidateLocalAnswer(answer);
            OnAnswerResult?.Invoke(isCorrect);
        }

        /// <summary>
        /// 验证本地答案
        /// </summary>
        private bool ValidateLocalAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctOption))
            {
                LogDebug("答案或正确选项为空");
                return false;
            }

            bool isCorrect = answer.Trim().Equals(correctOption.Trim(), System.StringComparison.Ordinal);
            LogDebug($"本地验证结果: 用户答案='{answer.Trim()}', 正确答案='{correctOption.Trim()}', 结果={isCorrect}");

            return isCorrect;
        }

        #endregion

        #region 网络结果处理

        /// <summary>
        /// 显示网络答题结果（由网络系统调用）
        /// 重要：此方法必须保留，网络系统会通过反射调用
        /// </summary>
        public void ShowNetworkResult(bool isCorrect, string correctAnswer)
        {
            LogDebug($"收到网络答题结果: {(isCorrect ? "正确" : "错误")}");

            correctOption = correctAnswer;
            OnAnswerResult?.Invoke(isCorrect);
        }

        #endregion

        #region 单机模式兼容（保留高耦合方法）

        /// <summary>
        /// 加载本地题目（单机模式）
        /// 保留此方法因为可能被其他系统调用
        /// </summary>
        protected override void LoadLocalQuestion()
        {
            LogDebug("加载本地ExplanationChoice题目");

            var questionData = GetQuestionData();
            if (questionData == null)
            {
                LogError("无法生成本地题目");
                return;
            }

            ParseQuestionData(questionData);
            LogDebug($"本地题目加载完成: {questionData.questionText}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            Debug.Log($"[ExplanationChoice] {message}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[ExplanationChoice] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 词语解释选择题附加数据结构
    /// </summary>
    [System.Serializable]
    public class ExplanationChoiceAdditionalData
    {
        public string source; // 数据来源标识
    }
}