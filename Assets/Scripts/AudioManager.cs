using System;
using System.Collections;
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
    
    public AudioSource effectSource;
    
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
    }

    public static void PlayMusic(MusicTrack trackName)
    {
        Debug.Log($"AudioManager.PlayMusic: {trackName}");
        // dont do anything if we're already playing the same track
        if (instance.currentMusicTrack == trackName)
        {
            return;
        }
        instance.currentMusicTrack = trackName;
        instance.StopAllCoroutines();
        AudioClip clip = trackName switch
        {
            MusicTrack.START_MUSIC => instance.startMusicClip,
            MusicTrack.MAIN_MENU_MUSIC => instance.mainMenuMusicClip,
            MusicTrack.BATTLE_MUSIC => instance.battleMusicClip,
            _ => throw new ArgumentOutOfRangeException(nameof(trackName), trackName, null)
        };

        if (clip != instance.currentMusicClip)
        {
            instance.StartCoroutine(instance.FadeToNewTrack(clip, 2f));
        }
    }

    IEnumerator FadeToNewTrack(AudioClip newClip, float duration)
    {
        inactiveSource.clip = newClip;
        inactiveSource.Play();
        float time = 0f;
        while (time < duration)
        {
            float t = time / duration;
            activeSource.volume = Mathf.Lerp(musicVolume, 0f, t);
            inactiveSource.volume = Mathf.Lerp(0f, musicVolume, t);
            time += Time.deltaTime;
            yield return null;
        }
        activeSource.Stop();
        activeSource.volume = musicVolume;
        (activeSource, inactiveSource) = (inactiveSource, activeSource);
    }

    public static void PlayShatter()
    {
        instance.effectSource.PlayOneShot(instance.shatterClip);
    }

    public static void PlayButtonHover()
    {
        instance.effectSource.PlayOneShot(ResourceRoot.Instance.buttonHoverClip);
    }

    public static void PlayButtonClick(ButtonClickType type)
    {
        Debug.Log($"PlayButtonClick: {type}");
        switch (type)
        {
            case ButtonClickType.AFFIRMATIVE:
                instance.effectSource.PlayOneShot(ResourceRoot.Instance.buttonClickAffirmativeClip);
                break;
            case ButtonClickType.NEGATIVE:
                instance.effectSource.PlayOneShot(ResourceRoot.Instance.buttonClickNegativeClip);
                break;
            case ButtonClickType.NEUTRAL:
                instance.effectSource.PlayOneShot(ResourceRoot.Instance.buttonClickNeutralClip);
                break;
            case ButtonClickType.BACK:
                instance.effectSource.PlayOneShot(ResourceRoot.Instance.buttonClickBackClip);
                break;
            case ButtonClickType.NONE:
                break;
        }
    }

    public static void PlayOneShot(AudioClip clip)
    {
        Debug.Log($"PlayOneShot: {clip.name}");
        instance.effectSource.PlayOneShot(clip);
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
