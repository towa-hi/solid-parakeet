using System;
using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioListener listener;

    public AudioSource musicSource1;
    public AudioSource musicSource2;
    public AudioSource activeSource;
    private AudioSource inactiveSource;
    
    public AudioSource effectSource;
    
    public AudioClip buttonClickClip;
    public AudioClip shatterClip;
    public AudioClip smallButtonClip;
    public AudioClip midButtonClip;
    public static AudioManager instance;

    public AudioClip mainMenuMusicClip;
    public AudioClip startMusicClip;
    public AudioClip battleMusicClip;
    
    void Awake()
    {
        instance = this;
        activeSource = musicSource1;
        inactiveSource = musicSource2;
        
    }

    public void PlayMusic(MusicTrack trackName)
    {
        StopAllCoroutines();
        AudioClip clip;
        switch (trackName)
        {
            case MusicTrack.START_MUSIC:
                clip = startMusicClip;
                break;
            case MusicTrack.MAIN_MENU_MUSIC:
                clip = mainMenuMusicClip;
                break;
            case MusicTrack.BATTLE_MUSIC:
                clip = battleMusicClip;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(trackName), trackName, null);
        }
        StartCoroutine(FadeToNewTrack(clip, 2f));
    }

    IEnumerator FadeToNewTrack(AudioClip newClip, float duration)
    {
        inactiveSource.clip = newClip;
        inactiveSource.Play();
        float time = 0f;
        while (time < duration)
        {
            float t = time / duration;
            activeSource.volume = Mathf.Lerp(1f, 0f, t);
            inactiveSource.volume = Mathf.Lerp(0f, 1f, t);
            time += Time.deltaTime;
            yield return null;
        }
        activeSource.Stop();
        activeSource.volume = 1f;
        (activeSource, inactiveSource) = (inactiveSource, activeSource);
    }
    
    public void PlayButtonClick()
    {
        effectSource.PlayOneShot(buttonClickClip);
    }

    public void PlaySmallButtonClick()
    {
        effectSource.PlayOneShot(smallButtonClip);
    }

    public void PlayMidButtonClick()
    {
        effectSource.PlayOneShot(midButtonClip);
    }
    public void PlayShatter()
    {
        effectSource.PlayOneShot(shatterClip);
    }
}

public enum MusicTrack
{
    START_MUSIC,
    MAIN_MENU_MUSIC,
    BATTLE_MUSIC,
}