#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Audio.Editor
{
    /// <summary>
    /// 音频配置的自定义编辑器
    /// </summary>
    [CustomEditor(typeof(AudioConfig))]
    public class AudioConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty audioClips;
        private SerializedProperty masterVolume, musicVolume, sfxVolume, voiceVolume, uiVolume;
        private SerializedProperty musicSourceCount, sfxSourceCount, voiceSourceCount, uiSourceCount;

        private bool showAudioClips = true;
        private bool showVolumeSettings = true;
        private bool showPoolSettings = true;
        private bool showPreview = true;

        private string searchFilter = "";
        private AudioType filterType = AudioType.Music;
        private bool useTypeFilter = false;

        // 预览相关
        private AudioSource previewSource;
        private string currentPreviewId = "";

        private void OnEnable()
        {
            audioClips = serializedObject.FindProperty("audioClips");

            masterVolume = serializedObject.FindProperty("masterVolume");
            musicVolume = serializedObject.FindProperty("musicVolume");
            sfxVolume = serializedObject.FindProperty("sfxVolume");
            voiceVolume = serializedObject.FindProperty("voiceVolume");
            uiVolume = serializedObject.FindProperty("uiVolume");

            musicSourceCount = serializedObject.FindProperty("musicSourceCount");
            sfxSourceCount = serializedObject.FindProperty("sfxSourceCount");
            voiceSourceCount = serializedObject.FindProperty("voiceSourceCount");
            uiSourceCount = serializedObject.FindProperty("uiSourceCount");

            CreatePreviewSource();
        }

        private void OnDisable()
        {
            StopPreview();
            if (previewSource != null)
            {
                DestroyImmediate(previewSource.gameObject);
            }
        }

        public override void OnInspectorGUI()
        {
            // 在开始时强制更新SerializedObject
            serializedObject.Update();

            try
            {
                EditorGUILayout.Space();
                DrawHeader();
                EditorGUILayout.Space();

                DrawVolumeSettings();
                EditorGUILayout.Space();

                DrawPoolSettings();
                EditorGUILayout.Space();

                DrawAudioClipsSection();
                EditorGUILayout.Space();

                DrawPreviewSection();
                EditorGUILayout.Space();

                DrawUtilityButtons();

                // 应用修改
                if (serializedObject.hasModifiedProperties)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"编辑器出现错误: {e.Message}", MessageType.Error);

                Debug.LogError($"AudioConfigEditor错误: {e}");
            }
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 16;
            headerStyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField("音频配置管理器", headerStyle);

            // 显示统计信息
            var config = target as AudioConfig;
            if (config != null && config.audioClips != null)
            {
                int totalClips = config.audioClips.Count;
                int musicCount = config.audioClips.Count(c => c.type == AudioType.Music);
                int sfxCount = config.audioClips.Count(c => c.type == AudioType.SFX);
                int voiceCount = config.audioClips.Count(c => c.type == AudioType.Voice);
                int uiCount = config.audioClips.Count(c => c.type == AudioType.UI);

                EditorGUILayout.LabelField($"总音频: {totalClips} | 音乐: {musicCount} | 音效: {sfxCount} | 语音: {voiceCount} | UI: {uiCount}",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawVolumeSettings()
        {
            showVolumeSettings = EditorGUILayout.Foldout(showVolumeSettings, "音量设置", true);
            if (showVolumeSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(masterVolume, new GUIContent("主音量"));
                EditorGUILayout.PropertyField(musicVolume, new GUIContent("音乐音量"));
                EditorGUILayout.PropertyField(sfxVolume, new GUIContent("音效音量"));
                EditorGUILayout.PropertyField(voiceVolume, new GUIContent("语音音量"));
                EditorGUILayout.PropertyField(uiVolume, new GUIContent("UI音量"));

                // 快速设置按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全部最大", GUILayout.Width(80)))
                {
                    SetAllVolumes(1f);
                }
                if (GUILayout.Button("全部一半", GUILayout.Width(80)))
                {
                    SetAllVolumes(0.5f);
                }
                if (GUILayout.Button("全部静音", GUILayout.Width(80)))
                {
                    SetAllVolumes(0f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawPoolSettings()
        {
            showPoolSettings = EditorGUILayout.Foldout(showPoolSettings, "音频源池设置", true);
            if (showPoolSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(musicSourceCount, new GUIContent("音乐源数量", "同时播放的音乐源数量，用于交叉淡入淡出"));
                EditorGUILayout.PropertyField(sfxSourceCount, new GUIContent("音效源数量", "同时播放的音效数量"));
                EditorGUILayout.PropertyField(voiceSourceCount, new GUIContent("语音源数量", "同时播放的语音数量"));
                EditorGUILayout.PropertyField(uiSourceCount, new GUIContent("UI音效源数量", "同时播放的UI音效数量"));

                // 警告提示
                if (musicSourceCount.intValue < 2)
                {
                    EditorGUILayout.HelpBox("音乐源数量建议至少为2，以支持交叉淡入淡出", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawAudioClipsSection()
        {
            showAudioClips = EditorGUILayout.Foldout(showAudioClips, $"音频剪辑列表 ({audioClips.arraySize})", true);
            if (showAudioClips)
            {
                EditorGUI.indentLevel++;

                // 搜索和过滤
                DrawSearchAndFilter();

                // 添加新音频按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("添加音频剪辑", GUILayout.Height(25)))
                {
                    audioClips.InsertArrayElementAtIndex(audioClips.arraySize);
                    var newElement = audioClips.GetArrayElementAtIndex(audioClips.arraySize - 1);
                    InitializeNewAudioClip(newElement);
                }

                if (GUILayout.Button("批量导入", GUILayout.Height(25), GUILayout.Width(80)))
                {
                    BatchImportAudioClips();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // 绘制音频剪辑列表
                DrawAudioClipsList();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawSearchAndFilter()
        {
            EditorGUILayout.BeginHorizontal();

            // 搜索框
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(40));
            searchFilter = EditorGUILayout.TextField(searchFilter);

            // 类型过滤
            useTypeFilter = EditorGUILayout.Toggle("类型过滤:", useTypeFilter, GUILayout.Width(80));
            GUI.enabled = useTypeFilter;
            filterType = (AudioType)EditorGUILayout.EnumPopup(filterType, GUILayout.Width(80));
            GUI.enabled = true;

            if (GUILayout.Button("清除", GUILayout.Width(50)))
            {
                searchFilter = "";
                useTypeFilter = false;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAudioClipsList()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            // 确保SerializedProperty和实际列表同步
            serializedObject.Update();

            int arraySize = audioClips.arraySize;
            int listCount = config.audioClips.Count;

            // 安全检查：使用较小的数值作为循环上限
            int safeCount = Mathf.Min(arraySize, listCount);

            for (int i = 0; i < safeCount; i++)
            {
                try
                {
                    var element = audioClips.GetArrayElementAtIndex(i);
                    var clipData = config.audioClips[i];

                    // 安全检查：确保clipData不为null
                    if (clipData == null) continue;

                    // 应用搜索和过滤
                    if (!ShouldShowClip(clipData)) continue;

                    EditorGUILayout.BeginVertical("box");

                    // 绘制音频剪辑项
                    DrawAudioClipItem(element, i, clipData);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                catch (System.Exception e)
                {
                    // 如果单个项目出错，显示错误信息但继续处理其他项目
                    EditorGUILayout.HelpBox($"音频项 {i} 出现错误: {e.Message}", MessageType.Error);

                    // 提供修复按钮
                    if (GUILayout.Button($"修复音频项 {i}"))
                    {
                        FixAudioClipItem(i);
                    }
                }
            }

            // 如果数组大小不一致，显示警告和修复选项
            if (arraySize != listCount)
            {
                EditorGUILayout.HelpBox($"检测到数据不一致: SerializedProperty大小({arraySize}) != 列表大小({listCount})", MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("同步数据"))
                {
                    SynchronizeAudioClipsData();
                }
                if (GUILayout.Button("重建列表"))
                {
                    RebuildAudioClipsList();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool ShouldShowClip(AudioClipData clipData)
        {
            // 安全检查：如果clipData为null，不显示
            if (clipData == null) return false;

            // 搜索过滤
            if (!string.IsNullOrEmpty(searchFilter))
            {
                bool matchesId = !string.IsNullOrEmpty(clipData.id) &&
                                clipData.id.ToLower().Contains(searchFilter.ToLower());
                bool matchesClipName = clipData.clip != null &&
                                      clipData.clip.name.ToLower().Contains(searchFilter.ToLower());

                if (!matchesId && !matchesClipName)
                {
                    return false;
                }
            }

            // 类型过滤
            if (useTypeFilter && clipData.type != filterType)
            {
                return false;
            }

            return true;
        }

        private void DrawAudioClipItem(SerializedProperty element, int index, AudioClipData clipData)
        {
            // 确保element不为null
            if (element == null) return;

            // 头部：ID、类型、删除按钮
            EditorGUILayout.BeginHorizontal();

            // ID字段
            var idProp = element.FindPropertyRelative("id");
            if (idProp != null)
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("ID"), GUILayout.MinWidth(150));
            }

            // 类型枚举
            var typeProp = element.FindPropertyRelative("type");
            if (typeProp != null)
            {
                EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(80));
            }

            // 预览按钮
            bool isPlaying = currentPreviewId == clipData.id && previewSource != null && previewSource.isPlaying;
            string buttonText = isPlaying ? "停止" : "预览";
            if (GUILayout.Button(buttonText, GUILayout.Width(50)))
            {
                if (isPlaying)
                {
                    StopPreview();
                }
                else
                {
                    PreviewClip(clipData);
                }
            }

            // 删除按钮
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("×", GUILayout.Width(25), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("删除确认", $"确定要删除音频 '{clipData.id}' 吗？", "删除", "取消"))
                {
                    audioClips.DeleteArrayElementAtIndex(index);
                    return;
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // 音频剪辑字段
            var clipProp = element.FindPropertyRelative("clip");
            if (clipProp != null)
            {
                EditorGUILayout.PropertyField(clipProp, new GUIContent("音频剪辑"));
            }

            // 基础设置（在一行显示）- 修复滑条显示
            EditorGUILayout.BeginHorizontal();

            // 音量滑条 (0-1)
            var volumeProp = element.FindPropertyRelative("volume");
            if (volumeProp != null)
            {
                EditorGUILayout.LabelField("音量", GUILayout.Width(30));
                volumeProp.floatValue = EditorGUILayout.Slider(volumeProp.floatValue, 0f, 1f, GUILayout.MinWidth(80));
            }

            // 音调滑条 (0.1-3)
            var pitchProp = element.FindPropertyRelative("pitch");
            if (pitchProp != null)
            {
                EditorGUILayout.LabelField("音调", GUILayout.Width(30));
                pitchProp.floatValue = EditorGUILayout.Slider(pitchProp.floatValue, 0.1f, 3f, GUILayout.MinWidth(80));
            }

            EditorGUILayout.EndHorizontal();

            // 第二行：循环和延迟
            EditorGUILayout.BeginHorizontal();

            // 循环开关
            var loopProp = element.FindPropertyRelative("loop");
            if (loopProp != null)
            {
                loopProp.boolValue = EditorGUILayout.Toggle("循环", loopProp.boolValue, GUILayout.Width(60));
            }

            // 延迟时间
            var delayProp = element.FindPropertyRelative("delay");
            if (delayProp != null)
            {
                EditorGUILayout.LabelField("延迟", GUILayout.Width(30));
                delayProp.floatValue = EditorGUILayout.FloatField(delayProp.floatValue, GUILayout.Width(50));
                EditorGUILayout.LabelField("秒", GUILayout.Width(15));
            }

            EditorGUILayout.EndHorizontal();

            // 3D音效设置
            var is3DProp = element.FindPropertyRelative("is3D");
            if (is3DProp != null)
            {
                is3DProp.boolValue = EditorGUILayout.Toggle("3D音效", is3DProp.boolValue);

                if (is3DProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    var maxDistanceProp = element.FindPropertyRelative("maxDistance");
                    if (maxDistanceProp != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("最大距离", GUILayout.Width(60));
                        maxDistanceProp.floatValue = EditorGUILayout.Slider(maxDistanceProp.floatValue, 1f, 500f);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 随机化设置
            EditorGUILayout.BeginHorizontal();

            var randomizeVolumeProp = element.FindPropertyRelative("randomizeVolume");
            if (randomizeVolumeProp != null)
            {
                randomizeVolumeProp.boolValue = EditorGUILayout.Toggle("随机音量", randomizeVolumeProp.boolValue, GUILayout.Width(80));

                if (randomizeVolumeProp.boolValue)
                {
                    var volumeVariationProp = element.FindPropertyRelative("volumeVariation");
                    if (volumeVariationProp != null)
                    {
                        EditorGUILayout.LabelField("±", GUILayout.Width(15));
                        volumeVariationProp.floatValue = EditorGUILayout.Slider(volumeVariationProp.floatValue, 0f, 0.5f, GUILayout.Width(80));
                    }
                }
            }

            var randomizePitchProp = element.FindPropertyRelative("randomizePitch");
            if (randomizePitchProp != null)
            {
                randomizePitchProp.boolValue = EditorGUILayout.Toggle("随机音调", randomizePitchProp.boolValue, GUILayout.Width(80));

                if (randomizePitchProp.boolValue)
                {
                    var pitchVariationProp = element.FindPropertyRelative("pitchVariation");
                    if (pitchVariationProp != null)
                    {
                        EditorGUILayout.LabelField("±", GUILayout.Width(15));
                        pitchVariationProp.floatValue = EditorGUILayout.Slider(pitchVariationProp.floatValue, 0f, 0.5f, GUILayout.Width(80));
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            // 显示当前设置的预览信息
            if (clipData.clip != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"时长: {clipData.clip.length:F2}s", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField($"频率: {clipData.clip.frequency}Hz", EditorStyles.miniLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField($"声道: {clipData.clip.channels}", EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPreviewSection()
        {
            showPreview = EditorGUILayout.Foldout(showPreview, "预览控制", true);
            if (showPreview)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"当前预览: {currentPreviewId}", EditorStyles.miniLabel);

                if (GUILayout.Button("停止所有预览", GUILayout.Width(100)))
                {
                    StopPreview();
                }
                EditorGUILayout.EndHorizontal();

                // 音量控制
                if (previewSource != null)
                {
                    float previewVolume = EditorGUILayout.Slider("预览音量", previewSource.volume, 0f, 1f);
                    previewSource.volume = previewVolume;
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawUtilityButtons()
        {
            EditorGUILayout.LabelField("实用工具", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("验证配置"))
            {
                ValidateConfiguration();
            }

            if (GUILayout.Button("生成代码常量"))
            {
                GenerateCodeConstants();
            }

            if (GUILayout.Button("导出配置"))
            {
                ExportConfiguration();
            }

            if (GUILayout.Button("清理空引用"))
            {
                CleanupEmptyReferences();
            }

            EditorGUILayout.EndHorizontal();
        }

        #region 辅助方法

        private void SetAllVolumes(float volume)
        {
            masterVolume.floatValue = volume;
            musicVolume.floatValue = volume;
            sfxVolume.floatValue = volume;
            voiceVolume.floatValue = volume;
            uiVolume.floatValue = volume;
        }

        private void InitializeNewAudioClip(SerializedProperty element)
        {
            if (element == null) return;

            var idProp = element.FindPropertyRelative("id");
            var typeProp = element.FindPropertyRelative("type");
            var volumeProp = element.FindPropertyRelative("volume");
            var pitchProp = element.FindPropertyRelative("pitch");
            var loopProp = element.FindPropertyRelative("loop");
            var delayProp = element.FindPropertyRelative("delay");
            var is3DProp = element.FindPropertyRelative("is3D");
            var maxDistanceProp = element.FindPropertyRelative("maxDistance");
            var randomizeVolumeProp = element.FindPropertyRelative("randomizeVolume");
            var volumeVariationProp = element.FindPropertyRelative("volumeVariation");
            var randomizePitchProp = element.FindPropertyRelative("randomizePitch");
            var pitchVariationProp = element.FindPropertyRelative("pitchVariation");

            // 安全地设置默认值
            if (idProp != null)
                idProp.stringValue = $"new_audio_{System.DateTime.Now.Ticks % 1000}";
            if (typeProp != null)
                typeProp.enumValueIndex = 0;
            if (volumeProp != null)
                volumeProp.floatValue = 1f;
            if (pitchProp != null)
                pitchProp.floatValue = 1f;
            if (loopProp != null)
                loopProp.boolValue = false;
            if (delayProp != null)
                delayProp.floatValue = 0f;
            if (is3DProp != null)
                is3DProp.boolValue = false;
            if (maxDistanceProp != null)
                maxDistanceProp.floatValue = 50f;
            if (randomizeVolumeProp != null)
                randomizeVolumeProp.boolValue = false;
            if (volumeVariationProp != null)
                volumeVariationProp.floatValue = 0.1f;
            if (randomizePitchProp != null)
                randomizePitchProp.boolValue = false;
            if (pitchVariationProp != null)
                pitchVariationProp.floatValue = 0.1f;
        }

        private void BatchImportAudioClips()
        {
            string[] guids = Selection.assetGUIDs;
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("批量导入", "请先在Project窗口中选择要导入的音频文件", "确定");
                return;
            }

            try
            {
                var config = target as AudioConfig;
                if (config?.audioClips == null) return;

                int importCount = 0;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

                    if (clip != null)
                    {
                        // 检查是否已存在相同ID的音频
                        string clipId = clip.name;
                        bool exists = config.audioClips.Any(data => data.id == clipId);

                        if (exists)
                        {
                            if (!EditorUtility.DisplayDialog("重复ID",
                                $"音频ID '{clipId}' 已存在，是否覆盖？", "覆盖", "跳过"))
                            {
                                continue;
                            }

                            // 移除现有项
                            for (int i = config.audioClips.Count - 1; i >= 0; i--)
                            {
                                if (config.audioClips[i].id == clipId)
                                {
                                    config.audioClips.RemoveAt(i);
                                    break;
                                }
                            }
                        }

                        // 添加新项
                        var newClipData = new AudioClipData(clipId, clip, (AudioType)GuessAudioType(clip.name));
                        config.audioClips.Add(newClipData);
                        importCount++;
                    }
                }

                // 同步SerializedProperty
                serializedObject.Update();
                audioClips.arraySize = config.audioClips.Count;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                EditorUtility.DisplayDialog("批量导入完成", $"成功导入 {importCount} 个音频文件", "确定");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("导入错误", $"批量导入失败: {e.Message}", "确定");
                Debug.LogError($"批量导入音频失败: {e}");
            }
        }

        private int GuessAudioType(string clipName)
        {
            string name = clipName.ToLower();

            if (name.Contains("music") || name.Contains("bgm") || name.Contains("background"))
                return (int)AudioType.Music;
            else if (name.Contains("ui") || name.Contains("button") || name.Contains("click"))
                return (int)AudioType.UI;
            else if (name.Contains("voice") || name.Contains("speak") || name.Contains("talk"))
                return (int)AudioType.Voice;
            else
                return (int)AudioType.SFX;
        }

        private void CreatePreviewSource()
        {
            if (previewSource == null)
            {
                var go = new GameObject("AudioConfigPreview");
                go.hideFlags = HideFlags.HideAndDontSave;
                previewSource = go.AddComponent<AudioSource>();
                previewSource.playOnAwake = false;
            }
        }

        private void PreviewClip(AudioClipData clipData)
        {
            if (previewSource != null && clipData.clip != null)
            {
                StopPreview();

                previewSource.clip = clipData.clip;
                previewSource.volume = clipData.volume;
                previewSource.pitch = clipData.pitch;
                previewSource.loop = false; // 预览时不循环
                previewSource.Play();

                currentPreviewId = clipData.id;
            }
        }

        private void StopPreview()
        {
            if (previewSource != null && previewSource.isPlaying)
            {
                previewSource.Stop();
            }
            currentPreviewId = "";
        }

        private void ValidateConfiguration()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            var issues = new System.Collections.Generic.List<string>();
            var ids = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < config.audioClips.Count; i++)
            {
                var clipData = config.audioClips[i];

                // 检查ID
                if (string.IsNullOrEmpty(clipData.id))
                {
                    issues.Add($"第 {i + 1} 项: ID为空");
                }
                else if (ids.Contains(clipData.id))
                {
                    issues.Add($"第 {i + 1} 项: ID '{clipData.id}' 重复");
                }
                else
                {
                    ids.Add(clipData.id);
                }

                // 检查音频剪辑
                if (clipData.clip == null)
                {
                    issues.Add($"第 {i + 1} 项 '{clipData.id}': 音频剪辑为空");
                }

                // 检查音量范围
                if (clipData.volume < 0 || clipData.volume > 1)
                {
                    issues.Add($"第 {i + 1} 项 '{clipData.id}': 音量超出范围 (0-1)");
                }
            }

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("配置验证", "配置验证通过，没有发现问题！", "确定");
            }
            else
            {
                string message = "发现以下问题:\n\n" + string.Join("\n", issues);
                EditorUtility.DisplayDialog("配置验证", message, "确定");
            }
        }

        private void GenerateCodeConstants()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            var code = new System.Text.StringBuilder();
            code.AppendLine("// 自动生成的音频ID常量");
            code.AppendLine("namespace Audio");
            code.AppendLine("{");
            code.AppendLine("    public static class AudioIDs");
            code.AppendLine("    {");

            foreach (var clipData in config.audioClips)
            {
                if (!string.IsNullOrEmpty(clipData.id))
                {
                    string constantName = clipData.id.ToUpper().Replace(" ", "_").Replace("-", "_");
                    code.AppendLine($"        public const string {constantName} = \"{clipData.id}\";");
                }
            }

            code.AppendLine("    }");
            code.AppendLine("}");

            string path = EditorUtility.SaveFilePanel("保存音频ID常量", "Assets/Scripts", "AudioIDs", "cs");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, code.ToString());
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("生成完成", $"音频ID常量已保存到: {path}", "确定");
            }
        }

        private void ExportConfiguration()
        {
            var config = target as AudioConfig;
            if (config == null) return;

            string json = JsonUtility.ToJson(config, true);
            string path = EditorUtility.SaveFilePanel("导出音频配置", "Assets", config.name, "json");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("导出完成", $"配置已导出到: {path}", "确定");
            }
        }

        private void CleanupEmptyReferences()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            int removedCount = 0;
            for (int i = config.audioClips.Count - 1; i >= 0; i--)
            {
                if (config.audioClips[i].clip == null || string.IsNullOrEmpty(config.audioClips[i].id))
                {
                    config.audioClips.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                EditorUtility.SetDirty(config);
                EditorUtility.DisplayDialog("清理完成", $"清理了 {removedCount} 个空引用", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("清理完成", "没有发现空引用", "确定");
            }
        }

        #region 数据修复方法

        /// <summary>
        /// 修复单个音频项
        /// </summary>
        private void FixAudioClipItem(int index)
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            try
            {
                // 确保索引有效
                if (index >= 0 && index < audioClips.arraySize)
                {
                    var element = audioClips.GetArrayElementAtIndex(index);
                    InitializeNewAudioClip(element);

                    // 如果列表中对应位置的数据为空，创建新的
                    if (index >= config.audioClips.Count)
                    {
                        while (config.audioClips.Count <= index)
                        {
                            config.audioClips.Add(new AudioClipData("", null, AudioType.SFX));
                        }
                    }

                    EditorUtility.SetDirty(target);
                    Debug.Log($"已修复音频项 {index}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"修复音频项 {index} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 同步音频剪辑数据
        /// </summary>
        private void SynchronizeAudioClipsData()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            try
            {
                serializedObject.Update();

                int arraySize = audioClips.arraySize;
                int listCount = config.audioClips.Count;

                Debug.Log($"同步前: SerializedProperty大小={arraySize}, 列表大小={listCount}");

                // 如果SerializedProperty更大，调整列表大小
                if (arraySize > listCount)
                {
                    while (config.audioClips.Count < arraySize)
                    {
                        config.audioClips.Add(new AudioClipData("", null, AudioType.SFX));
                    }
                }
                // 如果列表更大，调整SerializedProperty大小
                else if (listCount > arraySize)
                {
                    audioClips.arraySize = listCount;
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                Debug.Log($"同步后: SerializedProperty大小={audioClips.arraySize}, 列表大小={config.audioClips.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"同步数据失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重建音频剪辑列表
        /// </summary>
        private void RebuildAudioClipsList()
        {
            if (EditorUtility.DisplayDialog("重建列表", "这将清空当前列表并重新创建，确定继续吗？", "确定", "取消"))
            {
                var config = target as AudioConfig;
                if (config != null)
                {
                    config.audioClips.Clear();
                    audioClips.arraySize = 0;

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);

                    Debug.Log("音频剪辑列表已重建");
                }
            }
        }
    }

        #endregion

        // 创建音频配置的快捷菜单
namespace Audio.Editor
    {
        public static class AudioConfigMenuItems
        {
            [MenuItem("Assets/Create/Audio/Audio Config", priority = 1)]
            public static void CreateAudioConfig()
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (string.IsNullOrEmpty(path))
                    path = "Assets";
                else if (System.IO.Path.HasExtension(path))
                    path = path.Replace(System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");

                string configPath = AssetDatabase.GenerateUniqueAssetPath(path + "/NewAudioConfig.asset");

                var config = ScriptableObject.CreateInstance<AudioConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                AssetDatabase.SaveAssets();

                EditorUtility.FocusProjectWindow();
                Selection.activeObject = config;
            }

            [MenuItem("Tools/Audio/Open Audio Manager Settings")]
            public static void OpenAudioManagerSettings()
            {
                var audioManager = Object.FindObjectOfType<AudioManager>();
                if (audioManager != null)
                {
                    Selection.activeObject = audioManager;
                    EditorGUIUtility.PingObject(audioManager);
                }
                else
                {
                    EditorUtility.DisplayDialog("Audio Manager", "场景中没有找到AudioManager", "确定");
                }
            }

            [MenuItem("Tools/Audio/Create Audio Manager")]
            public static void CreateAudioManager()
            {
                var existing = Object.FindObjectOfType<AudioManager>();
                if (existing != null)
                {
                    EditorUtility.DisplayDialog("Audio Manager", "场景中已存在AudioManager", "确定");
                    return;
                }

                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();

                EditorUtility.DisplayDialog("Audio Manager", "AudioManager已创建", "确定");
            }
        }
    }
}
#endregion
#endif