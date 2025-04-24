using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioListener listener;

    public AudioSource musicSource;
    public AudioSource effectSource;
    
    public AudioClip startMusicClip;
    public AudioClip gameMusicClip;

    public AudioClip buttonClickClip;
    public AudioClip shatterClip;
    public AudioClip smallButtonClip;

    public static AudioManager instance;
    void Awake()
    {
        instance = this;
    }
    
    public void PlayButtonClick()
    {
        effectSource.PlayOneShot(buttonClickClip);
    }

    public void PlaySmallButtonClick()
    {
        effectSource.PlayOneShot(smallButtonClip);
    }
    public void PlayShatter()
    {
        effectSource.PlayOneShot(shatterClip);
    }
}
