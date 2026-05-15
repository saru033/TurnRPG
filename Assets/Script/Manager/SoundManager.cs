using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public enum BgmType
{
    MainLobby,
    Battle,
}

public enum SfxType
{
    Click,
    Reroll,
    Newskill,
    UpgradeSkill,
    Win,
    Lose,
    hit,
    attackPunch,
    attackShoot,
    Mark,
    Shopreroll,
    Coindrop,
    keyboard,
    reward,
    stateChange,
    BattleStart
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;

    [Header("Audio Clips")]
    [SerializeField] private List<BgmClipData> bgmClips;
    [SerializeField] private List<SfxClipData> sfxClips;

    [Header("Sound Slider")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider voiceVolumeSlider;

    [System.Serializable]
    public struct BgmClipData { public BgmType type; public AudioClip clip; }
    [System.Serializable]
    public struct SfxClipData { public SfxType type; public AudioClip clip; }

    private Dictionary<BgmType, AudioClip> _bgmDict = new();
    private Dictionary<SfxType, AudioClip> _sfxDict = new();
    private BgmType _currentBgmType = (BgmType)(-1);

    // [추가] SFX 중복 재생 방지 (쿨타임)
    private Dictionary<SfxType, float> _lastPlayTime = new();
    private const float SFX_COOLDOWN = 0.05f; // 0.05초 내 동일 SFX 무시

    private void Awake()
    {
        // ... (생략된 기존 Awake 로직) ...
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        foreach (var data in bgmClips) _bgmDict[data.type] = data.clip;
        foreach (var data in sfxClips) _sfxDict[data.type] = data.clip;

        if (masterVolumeSlider) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (bgmVolumeSlider) bgmVolumeSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxVolumeSlider) sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        if (voiceVolumeSlider) voiceVolumeSlider.onValueChanged.AddListener(SetVoiceVolume);
    }


    private void Start()
    {
        PlayBGM(BgmType.MainLobby, true);
        StartCoroutine(InitVolumeDelayed());
    }

    private IEnumerator InitVolumeDelayed()
    {
        // AudioMixer는 Awake 시점에 완벽히 초기화되지 않을 수 있으므로 한 프레임 대기
        yield return null;

        SetMasterVolume(1f);
        SetBGMVolume(1f);
        SetSFXVolume(1f);
        SetVoiceVolume(1f);

        // 슬라이더가 있다면 슬라이더 값도 동기화
        if (masterVolumeSlider) masterVolumeSlider.value = 1f;
        if (bgmVolumeSlider) bgmVolumeSlider.value = 1f;
        if (sfxVolumeSlider) sfxVolumeSlider.value = 1f;
        if (voiceVolumeSlider) voiceVolumeSlider.value = 1f;
    }

    public void PlayBGM(BgmType type, bool loop = true)
    {
        if (_bgmDict.TryGetValue(type, out var clip))
        {
            if (_currentBgmType == type) return;

            _currentBgmType = type;
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.Play();
        }
    }

    public void PlaySFX(SfxType type)
    {
        if (_sfxDict.TryGetValue(type, out var clip))
        {
            // [추가] 광역기 등에서 소리가 겹쳐 깨지는 현상 방지
            if (_lastPlayTime.TryGetValue(type, out float lastTime))
            {
                if (Time.time - lastTime < SFX_COOLDOWN) return;
            }
            _lastPlayTime[type] = Time.time;

            sfxSource.PlayOneShot(clip);
        }
    }

    public BgmType GetBGM()
    {
        return _currentBgmType;
    }

    public void PlayVoice(AudioClip clip)
    {
        if (clip == null) return;
        // 보이스는 다른 효과음보다 명확하게 들려야 하므로 PlayOneShot에 볼륨 가중치(1.2f) 추가 가능
        voiceSource.PlayOneShot(clip, 1.2f);
    }

    // ── Volume Control (Auto-Mute Logic) ─────────────────────────
    private void SetVolume(string parameter, float value)
    {
        if (value <= 0.015f)
        {
            audioMixer.SetFloat(parameter, -80f); // Mute
        }
        else
        {
            // 기본 수식: Mathf.Log10(value) * 20f
            float db = Mathf.Log10(value) * 20f;

            // [추가] SFX 파라미터일 경우 기본적으로 6dB 감쇄
            if (parameter == "SFX") db -= 6f;

            audioMixer.SetFloat(parameter, db);
        }
    }

    public void SetMasterVolume(float volume) => SetVolume("Master", volume);
    public void SetBGMVolume(float volume) => SetVolume("BGM", volume);
    public void SetSFXVolume(float volume) => SetVolume("SFX", volume);
    public void SetVoiceVolume(float volume) => SetVolume("Voice", volume);
}
