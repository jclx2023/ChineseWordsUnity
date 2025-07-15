// 1. 音频类型枚举
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

namespace Audio
{
    /// <summary>
    /// 音频类型枚举
    /// </summary>
    public enum AudioType
    {
        Music,      // 背景音乐
        SFX,        // 音效
        Voice,      // 语音
        UI          // UI音效
    }

    /// <summary>
    /// 音频淡入淡出类型
    /// </summary>
    public enum FadeType
    {
        None,       // 无淡入淡出
        FadeIn,     // 淡入
        FadeOut,    // 淡出
        CrossFade   // 交叉淡入淡出
    }
}


namespace Audio
{
    /// <summary>
    /// 单个音频剪辑配置
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        [Header("基础配置")]
        public string id;                    // 音频ID
        public AudioClip clip;               // 音频剪辑
        public AudioType type;               // 音频类型

        [Header("播放设置")]
        [Range(0f, 1f)] public float volume = 1f;           // 音量
        [Range(0.1f, 3f)] public float pitch = 1f;          // 音调
        public bool loop = false;                            // 是否循环
        public float delay = 0f;                            // 延迟播放

        [Header("3D音效设置")]
        public bool is3D = false;                           // 是否为3D音效
        [Range(0f, 500f)] public float maxDistance = 50f;  // 最大距离

        [Header("随机化")]
        public bool randomizeVolume = false;                // 随机化音量
        [Range(0f, 0.3f)] public float volumeVariation = 0.1f;    // 音量变化范围
        public bool randomizePitch = false;                 // 随机化音调
        [Range(0f, 0.3f)] public float pitchVariation = 0.1f;     // 音调变化范围

        public AudioClipData(string id, AudioClip clip, AudioType type)
        {
            this.id = id;
            this.clip = clip;
            this.type = type;
        }
    }

    /// <summary>
    /// 音频配置资源 - ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/Audio Config", order = 1)]
    public class AudioConfig : ScriptableObject
    {
        [Header("音频剪辑列表")]
        public List<AudioClipData> audioClips = new List<AudioClipData>();

        [Header("全局音量设置")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;

        [Header("音频池设置")]
        public int musicSourceCount = 2;     // 音乐源数量（支持交叉淡入淡出）
        public int sfxSourceCount = 10;      // 音效源数量
        public int voiceSourceCount = 3;     // 语音源数量
        public int uiSourceCount = 5;        // UI音效源数量

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
    }
}



namespace Audio
{
    /// <summary>
    /// 全局音频管理器 - 单例模式
    /// 使用方式：AudioManager.PlaySFX("button_click");
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
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        #endregion

        #region 配置参数
        [Header("音频配置")]
        [SerializeField] private AudioConfig audioConfig;
        [SerializeField] private bool autoLoadConfig = true;
        [SerializeField] private string configPath = "Audio/AudioConfig";

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showAudioSourcesInHierarchy = false;
        #endregion

        #region 私有变量
        private Dictionary<AudioType, Queue<AudioSource>> audioSourcePools;
        private Dictionary<AudioType, List<AudioSource>> activeAudioSources;
        private Dictionary<string, AudioClipData> audioDatabase;

        private AudioSource currentMusicSource;
        private AudioSource fadingMusicSource;
        private Coroutine musicFadeCoroutine;

        private Transform audioSourceContainer;
        #endregion

        #region 静态接口 - 音乐
        public static void PlayMusic(string musicId) => Instance.PlayMusicInternal(musicId);
        public static void PlayMusic(string musicId, float fadeTime) => Instance.PlayMusicInternal(musicId, fadeTime);
        public static void StopMusic() => Instance.StopMusicInternal();
        public static void StopMusic(float fadeTime) => Instance.StopMusicInternal(fadeTime);
        public static void PauseMusic() => Instance.PauseMusicInternal();
        public static void ResumeMusic() => Instance.ResumeMusicInternal();
        public static void SetMusicVolume(float volume) => Instance.SetMusicVolumeInternal(volume);
        #endregion

        #region 静态接口 - 音效
        public static void PlaySFX(string sfxId) => Instance.PlaySFXInternal(sfxId);
        public static void PlaySFX(string sfxId, Vector3 position) => Instance.PlaySFX3DInternal(sfxId, position);
        public static void PlaySFX(string sfxId, float volume) => Instance.PlaySFXInternal(sfxId, volume);
        public static void PlaySFX(string sfxId, float volume, float pitch) => Instance.PlaySFXInternal(sfxId, volume, pitch);
        public static void StopAllSFX() => Instance.StopAllSFXInternal();
        public static void SetSFXVolume(float volume) => Instance.SetSFXVolumeInternal(volume);
        #endregion

        #region 静态接口 - 语音和UI
        public static void PlayVoice(string voiceId) => Instance.PlayVoiceInternal(voiceId);
        public static void PlayUI(string uiId) => Instance.PlayUIInternal(uiId);
        public static void StopAllVoice() => Instance.StopAllVoiceInternal();
        public static void SetVoiceVolume(float volume) => Instance.SetVoiceVolumeInternal(volume);
        public static void SetUIVolume(float volume) => Instance.SetUIVolumeInternal(volume);
        #endregion

        #region 静态接口 - 全局控制
        public static void SetMasterVolume(float volume) => Instance.SetMasterVolumeInternal(volume);
        public static void StopAll() => Instance.StopAllInternal();
        public static void PauseAll() => Instance.PauseAllInternal();
        public static void ResumeAll() => Instance.ResumeAllInternal();
        public static bool IsPlaying(string audioId) => Instance.IsPlayingInternal(audioId);
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
            // 从MainMenuManager同步音量设置
            SyncVolumeFromPlayerPrefs();
        }
        #endregion

        #region 初始化
        private void Initialize()
        {
            LoadAudioConfig();
            CreateAudioSourceContainer();
            InitializeAudioSourcePools();
            BuildAudioDatabase();

            LogDebug("AudioManager 初始化完成");
        }

        private void LoadAudioConfig()
        {
            if (audioConfig == null && autoLoadConfig)
            {
                audioConfig = Resources.Load<AudioConfig>(configPath);
                if (audioConfig == null)
                {
                    LogDebug($"未找到音频配置文件: {configPath}，使用默认设置");
                    CreateDefaultConfig();
                }
            }
        }

        private void CreateAudioSourceContainer()
        {
            audioSourceContainer = new GameObject("AudioSources").transform;
            audioSourceContainer.SetParent(transform);

            if (!showAudioSourcesInHierarchy)
                audioSourceContainer.gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        private void InitializeAudioSourcePools()
        {
            audioSourcePools = new Dictionary<AudioType, Queue<AudioSource>>();
            activeAudioSources = new Dictionary<AudioType, List<AudioSource>>();

            // 初始化各类型音频源池
            CreateAudioSourcePool(AudioType.Music, audioConfig?.musicSourceCount ?? 2);
            CreateAudioSourcePool(AudioType.SFX, audioConfig?.sfxSourceCount ?? 10);
            CreateAudioSourcePool(AudioType.Voice, audioConfig?.voiceSourceCount ?? 3);
            CreateAudioSourcePool(AudioType.UI, audioConfig?.uiSourceCount ?? 5);
        }

        private void CreateAudioSourcePool(AudioType type, int count)
        {
            var pool = new Queue<AudioSource>();
            var activeList = new List<AudioSource>();

            for (int i = 0; i < count; i++)
            {
                GameObject sourceGO = new GameObject($"{type}Source_{i}");
                sourceGO.transform.SetParent(audioSourceContainer);

                AudioSource source = sourceGO.AddComponent<AudioSource>();
                ConfigureAudioSource(source, type);

                pool.Enqueue(source);
            }

            audioSourcePools[type] = pool;
            activeAudioSources[type] = activeList;
        }

        private void ConfigureAudioSource(AudioSource source, AudioType type)
        {
            source.playOnAwake = false;
            source.volume = audioConfig?.GetVolumeByType(type) ?? 1f;

            // 3D音效配置
            if (type == AudioType.SFX)
            {
                source.spatialBlend = 0f; // 默认2D，可在播放时调整
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.maxDistance = 50f;
            }
        }

        private void BuildAudioDatabase()
        {
            audioDatabase = new Dictionary<string, AudioClipData>();

            if (audioConfig?.audioClips != null)
            {
                foreach (var clipData in audioConfig.audioClips)
                {
                    if (!string.IsNullOrEmpty(clipData.id))
                    {
                        audioDatabase[clipData.id] = clipData;
                    }
                }
            }

            LogDebug($"音频数据库构建完成，共 {audioDatabase.Count} 个音频");
        }

        private void CreateDefaultConfig()
        {
            // 如果没有配置文件，创建默认配置
            audioConfig = ScriptableObject.CreateInstance<AudioConfig>();
            audioConfig.name = "DefaultAudioConfig";
        }
        #endregion

        #region 音频播放核心方法
        private AudioSource GetAudioSource(AudioType type)
        {
            if (audioSourcePools[type].Count > 0)
            {
                var source = audioSourcePools[type].Dequeue();
                activeAudioSources[type].Add(source);
                return source;
            }

            // 如果池中没有可用源，尝试回收已完成的源
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

        private void PlayAudio(string audioId, AudioType? forceType = null, float? volumeOverride = null, float? pitchOverride = null, Vector3? position = null)
        {
            if (!audioDatabase.TryGetValue(audioId, out AudioClipData clipData))
            {
                LogDebug($"未找到音频: {audioId}");
                return;
            }

            AudioType audioType = forceType ?? clipData.type;
            AudioSource source = GetAudioSource(audioType);

            if (source == null) return;

            // 配置音频源
            source.clip = clipData.clip;
            source.loop = clipData.loop;

            // 音量计算：基础音量 * 类型音量 * 主音量 * 覆盖音量
            float volume = clipData.volume;
            volume *= audioConfig?.GetVolumeByType(audioType) ?? 1f;
            volume *= audioConfig?.masterVolume ?? 1f;
            volume *= volumeOverride ?? 1f;

            // 随机化音量
            if (clipData.randomizeVolume)
            {
                volume *= Random.Range(1f - clipData.volumeVariation, 1f + clipData.volumeVariation);
            }

            source.volume = Mathf.Clamp01(volume);

            // 音调设置
            float pitch = pitchOverride ?? clipData.pitch;
            if (clipData.randomizePitch)
            {
                pitch *= Random.Range(1f - clipData.pitchVariation, 1f + clipData.pitchVariation);
            }
            source.pitch = pitch;

            // 3D音效设置
            if (clipData.is3D && position.HasValue)
            {
                source.spatialBlend = 1f;
                source.maxDistance = clipData.maxDistance;
                source.transform.position = position.Value;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            // 播放
            if (clipData.delay > 0)
            {
                source.PlayDelayed(clipData.delay);
            }
            else
            {
                source.Play();
            }

            LogDebug($"播放音频: {audioId} ({audioType})");
        }
        #endregion

        #region 内部实现方法
        // 音乐相关
        private void PlayMusicInternal(string musicId, float fadeTime = 0f)
        {
            if (fadeTime > 0f && currentMusicSource != null && currentMusicSource.isPlaying)
            {
                StartCoroutine(CrossFadeMusic(musicId, fadeTime));
            }
            else
            {
                StopMusicInternal();
                PlayAudio(musicId, AudioType.Music);
                currentMusicSource = activeAudioSources[AudioType.Music].LastOrDefault();
            }
        }

        private void StopMusicInternal(float fadeTime = 0f)
        {
            if (currentMusicSource != null)
            {
                if (fadeTime > 0f)
                {
                    StartCoroutine(FadeOutMusic(fadeTime));
                }
                else
                {
                    currentMusicSource.Stop();
                    ReturnAudioSource(currentMusicSource, AudioType.Music);
                    currentMusicSource = null;
                }
            }
        }

        private void PauseMusicInternal()
        {
            currentMusicSource?.Pause();
        }

        private void ResumeMusicInternal()
        {
            currentMusicSource?.UnPause();
        }

        private void SetMusicVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.musicVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.Music);
            }
        }

        // 音效相关
        private void PlaySFXInternal(string sfxId, float? volume = null, float? pitch = null)
        {
            PlayAudio(sfxId, AudioType.SFX, volume, pitch);
        }

        private void PlaySFX3DInternal(string sfxId, Vector3 position)
        {
            PlayAudio(sfxId, AudioType.SFX, position: position);
        }

        private void StopAllSFXInternal()
        {
            StopAllAudioOfType(AudioType.SFX);
        }

        private void SetSFXVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.sfxVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.SFX);
            }
        }

        // 语音相关
        private void PlayVoiceInternal(string voiceId)
        {
            PlayAudio(voiceId, AudioType.Voice);
        }

        private void StopAllVoiceInternal()
        {
            StopAllAudioOfType(AudioType.Voice);
        }

        private void SetVoiceVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.voiceVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.Voice);
            }
        }

        // UI音效相关
        private void PlayUIInternal(string uiId)
        {
            PlayAudio(uiId, AudioType.UI);
        }

        private void SetUIVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.uiVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.UI);
            }
        }

        // 全局控制
        private void SetMasterVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.masterVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume();
            }
        }

        private void StopAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)).Cast<AudioType>())
            {
                StopAllAudioOfType(type);
            }
        }

        private void PauseAllInternal()
        {
            foreach (var activeList in activeAudioSources.Values)
            {
                foreach (var source in activeList)
                {
                    if (source.isPlaying) source.Pause();
                }
            }
        }

        private void ResumeAllInternal()
        {
            foreach (var activeList in activeAudioSources.Values)
            {
                foreach (var source in activeList)
                {
                    source.UnPause();
                }
            }
        }

        private bool IsPlayingInternal(string audioId)
        {
            if (!audioDatabase.TryGetValue(audioId, out AudioClipData clipData))
                return false;

            var activeList = activeAudioSources[clipData.type];
            return activeList.Any(source => source.clip == clipData.clip && source.isPlaying);
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

        private void UpdateAllSourcesVolume(AudioType? specificType = null)
        {
            var typesToUpdate = specificType.HasValue ?
                new[] { specificType.Value } :
                System.Enum.GetValues(typeof(AudioType)).Cast<AudioType>();

            foreach (var type in typesToUpdate)
            {
                float typeVolume = audioConfig?.GetVolumeByType(type) ?? 1f;
                float masterVolume = audioConfig?.masterVolume ?? 1f;
                float finalVolume = typeVolume * masterVolume;

                foreach (var source in activeAudioSources[type])
                {
                    source.volume = finalVolume;
                }

                foreach (var source in audioSourcePools[type])
                {
                    source.volume = finalVolume;
                }
            }
        }

        private void SyncVolumeFromPlayerPrefs()
        {
            if (audioConfig != null)
            {
                audioConfig.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
                audioConfig.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
                audioConfig.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

                UpdateAllSourcesVolume();
                LogDebug("已同步PlayerPrefs中的音量设置");
            }
        }
        #endregion

        #region 音乐淡入淡出协程
        private IEnumerator CrossFadeMusic(string newMusicId, float fadeTime)
        {
            // 保存当前音乐源作为淡出源
            fadingMusicSource = currentMusicSource;

            // 播放新音乐
            PlayAudio(newMusicId, AudioType.Music);
            currentMusicSource = activeAudioSources[AudioType.Music].LastOrDefault();

            if (currentMusicSource != null)
                currentMusicSource.volume = 0f;

            float elapsed = 0f;
            float originalVolume = fadingMusicSource?.volume ?? 0f;
            float targetVolume = audioConfig?.musicVolume ?? 1f;
            targetVolume *= audioConfig?.masterVolume ?? 1f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / fadeTime;

                // 淡出旧音乐
                if (fadingMusicSource != null)
                    fadingMusicSource.volume = Mathf.Lerp(originalVolume, 0f, progress);

                // 淡入新音乐
                if (currentMusicSource != null)
                    currentMusicSource.volume = Mathf.Lerp(0f, targetVolume, progress);

                yield return null;
            }

            // 停止淡出的音乐
            if (fadingMusicSource != null)
            {
                ReturnAudioSource(fadingMusicSource, AudioType.Music);
                fadingMusicSource = null;
            }

            // 确保新音乐音量正确
            if (currentMusicSource != null)
                currentMusicSource.volume = targetVolume;
        }

        private IEnumerator FadeOutMusic(float fadeTime)
        {
            if (currentMusicSource == null) yield break;

            float startVolume = currentMusicSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                currentMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }

            currentMusicSource.Stop();
            ReturnAudioSource(currentMusicSource, AudioType.Music);
            currentMusicSource = null;
        }
        #endregion

        #region 调试方法
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AudioManager] {message}");
            }
        }
        #endregion
    }
}