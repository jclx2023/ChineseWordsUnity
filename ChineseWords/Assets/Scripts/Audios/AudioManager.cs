// 简化的音频类型枚举
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;

namespace Audio
{
    /// <summary>
    /// 简化的音频类型枚举
    /// </summary>
    public enum AudioType
    {
        Music,      // 背景音乐
        SFX,        // 音效
        Voice,      // 语音
        UI          // UI音效
    }

    /// <summary>
    /// 简化的音频剪辑配置
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        public string id;                    // 音频ID
        public AudioClip clip;               // 音频剪辑
        public AudioType type;               // 音频类型
        [Range(0f, 1f)] public float volume = 1f;           // 音量
        [Range(0.1f, 3f)] public float pitch = 1f;          // 音调
        public bool loop = false;                            // 是否循环

        public AudioClipData(string id, AudioClip clip, AudioType type)
        {
            this.id = id;
            this.clip = clip;
            this.type = type;
        }
    }

    /// <summary>
    /// 简化的音频配置 - 使用Runtime创建模式，避免ScriptableObject的序列化问题
    /// </summary>
    public class AudioConfig
    {
        public List<AudioClipData> audioClips = new List<AudioClipData>();

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;

        public int musicSourceCount = 2;
        public int sfxSourceCount = 8;
        public int voiceSourceCount = 3;
        public int uiSourceCount = 4;

        /// <summary>
        /// 根据ID查找音频数据
        /// </summary>
        public AudioClipData GetAudioData(string id)
        {
            return audioClips.Find(data => data.id == id);
        }

        /// <summary>
        /// 根据类型获取音量
        /// </summary>
        public float GetVolumeByType(AudioType type)
        {
            switch (type)
            {
                case AudioType.Music: return musicVolume;
                case AudioType.SFX: return sfxVolume;
                case AudioType.Voice: return voiceVolume;
                case AudioType.UI: return uiVolume;
                default: return 1f;
            }
        }

        /// <summary>
        /// 从音频剪辑自动创建配置
        /// </summary>
        public void AutoCreateFromClips(AudioClip[] clips)
        {
            audioClips.Clear();

            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    var type = GuessAudioType(clip.name);
                    var clipData = new AudioClipData(clip.name, clip, type);
                    audioClips.Add(clipData);
                }
            }
        }

        /// <summary>
        /// 从指定类型创建音频数据
        /// </summary>
        public void CreateFromClips(AudioClip[] clips, AudioType audioType)
        {
            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    var clipData = new AudioClipData(clip.name, clip, audioType);
                    audioClips.Add(clipData);
                }
            }
        }

        private AudioType GuessAudioType(string clipName)
        {
            string name = clipName.ToLower();

            if (name.Contains("music") || name.Contains("bgm") || name.Contains("background"))
                return AudioType.Music;
            else if (name.Contains("ui") || name.Contains("button") || name.Contains("click"))
                return AudioType.UI;
            else if (name.Contains("voice") || name.Contains("speak") || name.Contains("talk"))
                return AudioType.Voice;
            else
                return AudioType.SFX;
        }
    }
}

namespace Audio
{
    /// <summary>
    /// 简化且稳定的音频管理器
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        #region 单例
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        #endregion

        #region 配置参数
        [Header("音频文件夹路径")]
        [SerializeField] private string musicFolderPath = "Audio/Music";     // 音乐文件夹
        [SerializeField] private string sfxFolderPath = "Audio/SFX";         // 音效文件夹
        [SerializeField] private string voiceFolderPath = "Audio/Voice";     // 语音文件夹
        [SerializeField] private string uiFolderPath = "Audio/UI";           // UI音效文件夹

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region 私有变量
        private AudioConfig audioConfig;
        private Dictionary<AudioType, Queue<AudioSource>> audioSourcePools;
        private Dictionary<AudioType, List<AudioSource>> activeAudioSources;
        private Dictionary<string, AudioClipData> audioDatabase;

        private AudioSource currentMusicSource;
        private Transform audioSourceContainer;
        #endregion

        #region 静态接口
        /// <summary>
        /// 播放音乐（可选择是否循环）
        /// </summary>
        public static void PlayMusic(string musicId, bool loop = true) => Instance.PlayMusicInternal(musicId, loop);

        /// <summary>
        /// 播放音效（通常不循环）
        /// </summary>
        public static void PlaySFX(string sfxId) => Instance.PlaySFXInternal(sfxId);

        /// <summary>
        /// 播放语音（通常不循环）
        /// </summary>
        public static void PlayVoice(string voiceId) => Instance.PlayVoiceInternal(voiceId);

        /// <summary>
        /// 播放UI音效（通常不循环）
        /// </summary>
        public static void PlayUI(string uiId) => Instance.PlayUIInternal(uiId);

        public static void StopMusic() => Instance.StopMusicInternal();
        public static void StopAllSFX() => Instance.StopAllSFXInternal();
        public static void StopAll() => Instance.StopAllInternal();

        // 暂停和恢复功能
        public static void PauseMusic() => Instance.PauseMusicInternal();
        public static void ResumeMusic() => Instance.ResumeMusicInternal();
        public static void PauseAll() => Instance.PauseAllInternal();
        public static void ResumeAll() => Instance.ResumeAllInternal();
        public static void PauseSFX() => Instance.PauseAllSFXInternal();
        public static void ResumeSFX() => Instance.ResumeAllSFXInternal();

        // 音量控制
        public static void SetMasterVolume(float volume) => Instance.SetMasterVolumeInternal(volume);
        public static void SetMusicVolume(float volume) => Instance.SetMusicVolumeInternal(volume);
        public static void SetSFXVolume(float volume) => Instance.SetSFXVolumeInternal(volume);

        // 状态查询
        public static bool IsMusicPlaying() => Instance.IsMusicPlayingInternal();
        public static bool IsMusicPaused() => Instance.IsMusicPausedInternal();
        #endregion

        #region Unity生命周期
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SyncVolumeFromPlayerPrefs();
        }
        #endregion

        #region 初始化
        private void Initialize()
        {
            CreateAudioConfig();
            LoadAudioClips();
            CreateAudioSourceContainer();
            InitializeAudioSourcePools();
            BuildAudioDatabase();

            LogDebug("简化AudioManager初始化完成");
        }

        private void CreateAudioConfig()
        {
            audioConfig = new AudioConfig();
        }

        private void LoadAudioClips()
        {
            LogDebug("开始从分类文件夹加载音频文件");

            // 从各个分类文件夹加载音频
            LoadAudioFromFolder(musicFolderPath, AudioType.Music);
            LoadAudioFromFolder(sfxFolderPath, AudioType.SFX);
            LoadAudioFromFolder(voiceFolderPath, AudioType.Voice);
            LoadAudioFromFolder(uiFolderPath, AudioType.UI);

            LogDebug($"音频加载完成，总计 {audioConfig.audioClips.Count} 个音频文件");
        }

        private void LoadAudioFromFolder(string folderPath, AudioType audioType)
        {
            var audioClips = Resources.LoadAll<AudioClip>(folderPath);

            foreach (var clip in audioClips)
            {
                if (clip != null)
                {
                    var clipData = new AudioClipData(clip.name, clip, audioType);
                    audioConfig.audioClips.Add(clipData);
                }
            }

            LogDebug($"从 {folderPath} 加载了 {audioClips.Length} 个 {audioType} 类型音频");
        }

        private void CreateAudioSourceContainer()
        {
            audioSourceContainer = new GameObject("AudioSources").transform;
            audioSourceContainer.SetParent(transform);
        }

        private void InitializeAudioSourcePools()
        {
            audioSourcePools = new Dictionary<AudioType, Queue<AudioSource>>();
            activeAudioSources = new Dictionary<AudioType, List<AudioSource>>();

            CreateAudioSourcePool(AudioType.Music, audioConfig.musicSourceCount);
            CreateAudioSourcePool(AudioType.SFX, audioConfig.sfxSourceCount);
            CreateAudioSourcePool(AudioType.Voice, audioConfig.voiceSourceCount);
            CreateAudioSourcePool(AudioType.UI, audioConfig.uiSourceCount);
        }

        private void CreateAudioSourcePool(AudioType type, int count)
        {
            var pool = new Queue<AudioSource>();
            var activeList = new List<AudioSource>();

            for (int i = 0; i < count; i++)
            {
                var sourceGO = new GameObject($"{type}Source_{i}");
                sourceGO.transform.SetParent(audioSourceContainer);

                var source = sourceGO.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 全部设为2D音效
                source.volume = audioConfig.GetVolumeByType(type);

                pool.Enqueue(source);
            }

            audioSourcePools[type] = pool;
            activeAudioSources[type] = activeList;
        }

        private void BuildAudioDatabase()
        {
            audioDatabase = new Dictionary<string, AudioClipData>();

            foreach (var clipData in audioConfig.audioClips)
            {
                if (!string.IsNullOrEmpty(clipData.id))
                {
                    audioDatabase[clipData.id] = clipData;
                }
            }

            LogDebug($"音频数据库构建完成，共 {audioDatabase.Count} 个音频");
        }
        #endregion

        #region 音频播放核心方法
        private AudioSource GetAudioSource(AudioType type)
        {
            // 先回收已完成的源
            RecycleFinishedSources(type);

            if (audioSourcePools[type].Count > 0)
            {
                var source = audioSourcePools[type].Dequeue();
                activeAudioSources[type].Add(source);
                return source;
            }

            LogDebug($"警告：{type} 类型音频源不足");
            return null;
        }

        private void RecycleFinishedSources(AudioType type)
        {
            var activeList = activeAudioSources[type];
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                if (!activeList[i].isPlaying)
                {
                    ReturnAudioSource(activeList[i], type);
                }
            }
        }

        private void ReturnAudioSource(AudioSource source, AudioType type)
        {
            source.Stop();
            source.clip = null;
            activeAudioSources[type].Remove(source);
            audioSourcePools[type].Enqueue(source);
        }

        private void PlayAudio(string audioId, AudioType audioType)
        {
            if (!audioDatabase.TryGetValue(audioId, out AudioClipData clipData))
            {
                LogDebug($"未找到音频: {audioId}");
                return;
            }

            var source = GetAudioSource(audioType);
            if (source == null) return;

            source.clip = clipData.clip;
            source.loop = clipData.loop;
            source.volume = clipData.volume * audioConfig.GetVolumeByType(audioType) * audioConfig.masterVolume;
            source.pitch = clipData.pitch;

            source.Play();
            LogDebug($"播放音频: {audioId} ({audioType})");
        }
        #endregion

        #region 内部实现方法
        private void PlayMusicInternal(string musicId, bool loop = true)
        {
            StopMusicInternal();

            if (!audioDatabase.TryGetValue(musicId, out AudioClipData clipData))
            {
                LogDebug($"未找到音乐: {musicId}");
                return;
            }

            var source = GetAudioSource(AudioType.Music);
            if (source == null) return;

            source.clip = clipData.clip;
            source.loop = loop;  // 使用传入的loop参数
            source.volume = clipData.volume * audioConfig.GetVolumeByType(AudioType.Music) * audioConfig.masterVolume;
            source.pitch = clipData.pitch;
            source.spatialBlend = 0f;  // 2D音效

            source.Play();
            currentMusicSource = source;

            LogDebug($"播放音乐: {musicId} (循环: {source.loop})");
        }

        private void PlaySFXInternal(string sfxId)
        {
            PlayAudio(sfxId, AudioType.SFX);
        }

        private void PlayVoiceInternal(string voiceId)
        {
            PlayAudio(voiceId, AudioType.Voice);
        }

        private void PlayUIInternal(string uiId)
        {
            PlayAudio(uiId, AudioType.UI);
        }

        private void StopMusicInternal()
        {
            if (currentMusicSource != null)
            {
                ReturnAudioSource(currentMusicSource, AudioType.Music);
                currentMusicSource = null;
            }
        }

        private void StopAllSFXInternal()
        {
            StopAllAudioOfType(AudioType.SFX);
        }

        private void StopAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)))
            {
                StopAllAudioOfType((AudioType)type);
            }
        }

        // 暂停和恢复内部实现
        private void PauseMusicInternal()
        {
            if (currentMusicSource != null && currentMusicSource.isPlaying)
            {
                currentMusicSource.Pause();
                LogDebug("音乐已暂停");
            }
        }

        private void ResumeMusicInternal()
        {
            if (currentMusicSource != null && !currentMusicSource.isPlaying)
            {
                currentMusicSource.UnPause();
                LogDebug("音乐已恢复");
            }
        }

        private void PauseAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)))
            {
                PauseAllAudioOfType((AudioType)type);
            }
            LogDebug("所有音频已暂停");
        }

        private void ResumeAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)))
            {
                ResumeAllAudioOfType((AudioType)type);
            }
            LogDebug("所有音频已恢复");
        }

        private void PauseAllSFXInternal()
        {
            PauseAllAudioOfType(AudioType.SFX);
            LogDebug("所有音效已暂停");
        }

        private void ResumeAllSFXInternal()
        {
            ResumeAllAudioOfType(AudioType.SFX);
            LogDebug("所有音效已恢复");
        }

        // 状态查询内部实现
        private bool IsMusicPlayingInternal()
        {
            return currentMusicSource != null && currentMusicSource.isPlaying;
        }

        private bool IsMusicPausedInternal()
        {
            return currentMusicSource != null && currentMusicSource.clip != null && !currentMusicSource.isPlaying;
        }

        private void SetMasterVolumeInternal(float volume)
        {
            audioConfig.masterVolume = Mathf.Clamp01(volume);
            UpdateAllSourcesVolume();
            PlayerPrefs.SetFloat("MasterVolume", volume);
        }

        private void SetMusicVolumeInternal(float volume)
        {
            audioConfig.musicVolume = Mathf.Clamp01(volume);
            UpdateAllSourcesVolume(AudioType.Music);
            PlayerPrefs.SetFloat("MusicVolume", volume);
        }

        private void SetSFXVolumeInternal(float volume)
        {
            audioConfig.sfxVolume = Mathf.Clamp01(volume);
            UpdateAllSourcesVolume(AudioType.SFX);
            PlayerPrefs.SetFloat("SFXVolume", volume);
        }
        #endregion

        #region 辅助方法
        private void StopAllAudioOfType(AudioType type)
        {
            var activeList = activeAudioSources[type];
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                ReturnAudioSource(activeList[i], type);
            }
        }

        private void PauseAllAudioOfType(AudioType type)
        {
            var activeList = activeAudioSources[type];
            foreach (var source in activeList)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                }
            }
        }

        private void ResumeAllAudioOfType(AudioType type)
        {
            var activeList = activeAudioSources[type];
            foreach (var source in activeList)
            {
                if (!source.isPlaying && source.clip != null)
                {
                    source.UnPause();
                }
            }
        }

        private void UpdateAllSourcesVolume(AudioType? specificType = null)
        {
            var typesToUpdate = specificType.HasValue ?
                new[] { specificType.Value } :
                new[] { AudioType.Music, AudioType.SFX, AudioType.Voice, AudioType.UI };

            foreach (var type in typesToUpdate)
            {
                float finalVolume = audioConfig.GetVolumeByType(type) * audioConfig.masterVolume;

                foreach (var source in activeAudioSources[type])
                {
                    if (source.clip != null)
                    {
                        var clipData = audioDatabase.Values.FirstOrDefault(c => c.clip == source.clip);
                        if (clipData != null)
                        {
                            source.volume = clipData.volume * finalVolume;
                        }
                    }
                }
            }
        }

        private void SyncVolumeFromPlayerPrefs()
        {
            audioConfig.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
            audioConfig.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            audioConfig.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

            UpdateAllSourcesVolume();
            LogDebug("已同步PlayerPrefs中的音量设置");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AudioManager] {message}");
            }
        }
        #endregion

        #region 公共工具方法
        /// <summary>
        /// 手动添加音频到配置中
        /// </summary>
        public static void AddAudioClip(string id, AudioClip clip, AudioType type)
        {
            var clipData = new AudioClipData(id, clip, type);
            Instance.audioConfig.audioClips.Add(clipData);
            Instance.audioDatabase[id] = clipData;
            Instance.LogDebug($"手动添加音频: {id}");
        }

        /// <summary>
        /// 获取当前配置的音频列表
        /// </summary>
        public static List<string> GetAudioList()
        {
            return new List<string>(Instance.audioDatabase.Keys);
        }

        /// <summary>
        /// 检查音频是否存在
        /// </summary>
        public static bool HasAudio(string audioId)
        {
            return Instance.audioDatabase.ContainsKey(audioId);
        }

        /// <summary>
        /// 获取当前音乐播放状态
        /// </summary>
        public static string GetMusicStatus()
        {
            if (Instance.currentMusicSource == null)
                return "无音乐";

            if (Instance.currentMusicSource.isPlaying)
                return $"播放中: {Instance.currentMusicSource.clip.name}";

            if (Instance.currentMusicSource.clip != null)
                return $"已暂停: {Instance.currentMusicSource.clip.name}";

            return "无音乐";
        }

        /// <summary>
        /// 获取音频播放统计
        /// </summary>
        public static AudioPlaybackStats GetPlaybackStats()
        {
            var stats = new AudioPlaybackStats();

            foreach (var kvp in Instance.activeAudioSources)
            {
                var type = kvp.Key;
                var sources = kvp.Value;

                int playingCount = sources.Count(s => s.isPlaying);
                int pausedCount = sources.Count(s => !s.isPlaying && s.clip != null);

                switch (type)
                {
                    case AudioType.Music:
                        stats.musicPlaying = playingCount;
                        stats.musicPaused = pausedCount;
                        break;
                    case AudioType.SFX:
                        stats.sfxPlaying = playingCount;
                        stats.sfxPaused = pausedCount;
                        break;
                    case AudioType.Voice:
                        stats.voicePlaying = playingCount;
                        stats.voicePaused = pausedCount;
                        break;
                    case AudioType.UI:
                        stats.uiPlaying = playingCount;
                        stats.uiPaused = pausedCount;
                        break;
                }
            }

            return stats;
        }
        #endregion

        /// <summary>
        /// 音频播放统计数据
        /// </summary>
        public struct AudioPlaybackStats
        {
            public int musicPlaying, musicPaused;
            public int sfxPlaying, sfxPaused;
            public int voicePlaying, voicePaused;
            public int uiPlaying, uiPaused;

            public int TotalPlaying => musicPlaying + sfxPlaying + voicePlaying + uiPlaying;
            public int TotalPaused => musicPaused + sfxPaused + voicePaused + uiPaused;
            public int TotalActive => TotalPlaying + TotalPaused;

            public override string ToString()
            {
                return $"播放中: {TotalPlaying}, 暂停中: {TotalPaused}, 总计: {TotalActive}";
            }
        }
    }
}