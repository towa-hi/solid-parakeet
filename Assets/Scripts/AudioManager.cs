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
    
    public void Initialize()
    {
        activeSource = musicSource1;
        inactiveSource = musicSource2;
        
        instance = this;
    }

    public static void PlayMusic(MusicTrack trackName)
    {
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
    
    public static void PlayButtonClick()
    {
        instance.effectSource.PlayOneShot(instance.buttonClickClip);
    }

    public static void PlaySmallButtonClick()
    {
        instance.effectSource.PlayOneShot(instance.smallButtonClip);
    }

    public static void PlayMidButtonClick()
    {
        instance.effectSource.PlayOneShot(instance.midButtonClip);
    }
    public static void PlayShatter()
    {
        instance.effectSource.PlayOneShot(instance.shatterClip);
    }
}

public enum MusicTrack
{
    START_MUSIC,
    MAIN_MENU_MUSIC,
    BATTLE_MUSIC,
}