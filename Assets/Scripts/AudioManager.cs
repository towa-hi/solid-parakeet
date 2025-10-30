using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    static AudioManager instance;
    
    public AudioListener listener;
    public AudioClip currentMusicClip;
    public AudioSource musicSource1;
    public AudioSource musicSource2;
    public AudioSource activeSource;
    AudioSource inactiveSource;
    Coroutine battleMusicSequenceRoutine;
    
    public AudioSource effectSource;
    
    // Track clips played per frame to prevent duplicates
    private HashSet<AudioClip> clipsPlayedThisFrame = new HashSet<AudioClip>();
    private int lastFrameCount = -1;
    
    public AudioClip buttonClickClip;
    public AudioClip shatterClip;
    public AudioClip smallButtonClip;
    public AudioClip midButtonClip;

    public AudioClip mainMenuMusicClip;
    public AudioClip startMusicClip;
    public AudioClip battleMusicClip;
    public AudioClip battleMusicClip2;
    public float masterVolume;
    public float effectsVolume;
    public float musicVolume;
    public MusicTrack currentMusicTrack;
    public void Initialize()
    {
        activeSource = musicSource1;
        inactiveSource = musicSource2;
        currentMusicTrack = MusicTrack.START_MUSIC;
        instance = this;
        ApplyVolumesFromSettings();
        SettingsManager.OnSettingChanged += HandleSettingChanged;
    }

    public static void PlayMusic(MusicTrack trackName)
    {
        if (instance == null)
        {
            Debug.LogWarning($"AudioManager.PlayMusic called before initialization: {trackName}");
            return;
        }

        Debug.Log($"AudioManager.PlayMusic: {trackName}");

        (AudioClip clip, bool shouldLoop) = ResolveClipAndLoop(trackName);
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager.PlayMusic: clip for {trackName} is null");
            return;
        }

        if (instance.currentMusicTrack == trackName &&
            instance.currentMusicClip == clip &&
            instance.activeSource != null &&
            instance.activeSource.isPlaying)
        {
            return;
        }

        instance.StopAllCoroutines();
        instance.battleMusicSequenceRoutine = null;
        instance.currentMusicTrack = trackName;

        if (instance.activeSource != null &&
            instance.activeSource.clip == clip &&
            instance.activeSource.isPlaying)
        {
            instance.activeSource.loop = shouldLoop;
            instance.activeSource.volume = instance.musicVolume;
            instance.currentMusicClip = clip;
            return;
        }

        instance.currentMusicClip = clip;
        instance.StartCoroutine(instance.FadeToNewTrack(clip, 2f, shouldLoop));
    }

    public static void PlayBattleMusicWithIntro()
    {
        if (instance == null)
        {
            Debug.LogWarning("AudioManager.PlayBattleMusicWithIntro called before initialization");
            return;
        }

        if (instance.startMusicClip == null)
        {
            Debug.LogWarning("AudioManager.PlayBattleMusicWithIntro: startMusicClip is null, playing battle music directly");
            PlayMusic(MusicTrack.BATTLE_MUSIC);
            return;
        }

        instance.StopAllCoroutines();
        instance.currentMusicTrack = MusicTrack.START_MUSIC;
        instance.currentMusicClip = instance.startMusicClip;
        instance.battleMusicSequenceRoutine = instance.StartCoroutine(instance.PlayBattleMusicSequence());
    }

    static (AudioClip clip, bool shouldLoop) ResolveClipAndLoop(MusicTrack trackName)
    {
        return trackName switch
        {
            MusicTrack.START_MUSIC => (instance.startMusicClip, false),
            MusicTrack.MAIN_MENU_MUSIC => (instance.mainMenuMusicClip, true),
            MusicTrack.BATTLE_MUSIC => (instance.battleMusicClip, true),
            _ => throw new ArgumentOutOfRangeException(nameof(trackName), trackName, null)
        };
    }

    IEnumerator PlayBattleMusicSequence()
    {
        yield return FadeToNewTrack(startMusicClip, 2f, false);

        yield return new WaitWhile(() => activeSource != null && activeSource.isPlaying && activeSource.clip == startMusicClip);

        if (activeSource == null || activeSource.clip != startMusicClip)
        {
            battleMusicSequenceRoutine = null;
            yield break;
        }

        battleMusicSequenceRoutine = null;

        if (battleMusicClip == null)
        {
            Debug.LogWarning("AudioManager.PlayBattleMusicSequence: battleMusicClip is null, cannot continue sequence");
            yield break;
        }

        PlayMusic(MusicTrack.BATTLE_MUSIC);
    }

    IEnumerator FadeToNewTrack(AudioClip newClip, float duration, bool loop)
    {
        if (newClip == null)
        {
            yield break;
        }

        inactiveSource.clip = newClip;
        inactiveSource.loop = loop;
        inactiveSource.volume = 0f;
        inactiveSource.Play();
        float time = 0f;
        while (time < duration)
        {
            float t = duration <= 0f ? 1f : Mathf.Clamp01(time / duration);
            activeSource.volume = Mathf.Lerp(musicVolume, 0f, t);
            inactiveSource.volume = Mathf.Lerp(0f, musicVolume, t);
            time += Time.deltaTime;
            yield return null;
        }
        activeSource.Stop();
        activeSource.volume = musicVolume;
        inactiveSource.volume = musicVolume;
        (activeSource, inactiveSource) = (inactiveSource, activeSource);
        activeSource.loop = loop;
        currentMusicClip = activeSource.clip;
    }

    public static void PlayShatter()
    {
        PlayOneShot(instance.shatterClip);
    }

    public static void PlayButtonHover()
    {
        PlayOneShot(ResourceRoot.Instance.buttonHoverClip);
    }

    public static void PlayButtonClick(ButtonClickType type)
    {
        //Debug.Log($"PlayButtonClick: {type}");
        switch (type)
        {
            case ButtonClickType.AFFIRMATIVE:
                PlayOneShot(ResourceRoot.Instance.buttonClickAffirmativeClip);
                break;
            case ButtonClickType.NEGATIVE:
                PlayOneShot(ResourceRoot.Instance.buttonClickNegativeClip);
                break;
            case ButtonClickType.NEUTRAL:
                PlayOneShot(ResourceRoot.Instance.buttonClickNeutralClip);
                break;
            case ButtonClickType.BACK:
                PlayOneShot(ResourceRoot.Instance.buttonClickBackClip);
                break;
            case ButtonClickType.NONE:
                break;
        }
    }

    public static void PlayOneShot(AudioClip clip)
    {
        //Debug.Log($"PlayOneShot: {clip.name}");
        if (instance == null || instance.effectSource == null || clip == null)
        {
            return;
        }
        
        // Check if we're in a new frame and clear the tracking set
        int currentFrame = Time.frameCount;
        if (currentFrame != instance.lastFrameCount)
        {
            instance.clipsPlayedThisFrame.Clear();
            instance.lastFrameCount = currentFrame;
        }
        
        // Prevent playing the same clip more than once per frame
        if (instance.clipsPlayedThisFrame.Contains(clip))
        {
            return;
        }
        
        // Mark this clip as played this frame and play it
        instance.clipsPlayedThisFrame.Add(clip);
        instance.effectSource.PlayOneShot(clip, Mathf.Clamp01(instance.effectsVolume));
    }

    public static float EffectsScalar => instance != null ? Mathf.Clamp01(instance.effectsVolume) : 1f;

    void OnDestroy()
    {
        SettingsManager.OnSettingChanged -= HandleSettingChanged;
        if (instance == this)
        {
            instance = null;
        }
    }

    void HandleSettingChanged(SettingsKey key, int val)
    {
        if (key == SettingsKey.MASTERVOLUME || key == SettingsKey.MUSICVOLUME || key == SettingsKey.EFFECTSVOLUME)
        {
            ApplyVolumesFromSettings();
        }
    }

    void ApplyVolumesFromSettings()
    {
        WarmancerSettings settings = SettingsManager.Load();
        float master = Mathf.Clamp01(settings.masterVolume / 100f);
        float music = Mathf.Clamp01(settings.musicVolume / 100f);
        float effects = Mathf.Clamp01(settings.effectsVolume / 100f);

        masterVolume = master;
        musicVolume = master * music;
        effectsVolume = master * effects;

        if (activeSource != null)
        {
            activeSource.volume = musicVolume;
        }
        if (inactiveSource != null)
        {
            inactiveSource.volume = musicVolume;
        }
        if (effectSource != null)
        {
            // Keep base at 1 and scale per one-shot so individual calls respect current settings
            effectSource.volume = 1f;
        }
    }
}

public enum MusicTrack
{
    START_MUSIC,
    MAIN_MENU_MUSIC,
    BATTLE_MUSIC,
}

public enum ButtonClickType
{
    NEUTRAL,
    AFFIRMATIVE,
    NEGATIVE,
    BACK,
    NONE,
}
