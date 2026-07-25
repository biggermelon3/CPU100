using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Shared 2D UI and software interaction sound player.</summary>
public class SystemInteractionAudio : MonoBehaviour
{
    public AudioClip deleteSfx;
    public AudioClip hoverSfx;
    public AudioClip installSfx;
    public AudioClip mouseClickSfx;
    public AudioClip pickupSfx;

    [Range(0f, 1f)] public float volume = 0.7f;
    [Range(0f, 1f)] public float hoverVolume = 0.45f;
    [Range(0f, 1f)] public float mouseClickVolume = 0.5f;

    AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        AudioClip[] clips = { deleteSfx, hoverSfx, installSfx, mouseClickSfx, pickupSfx };
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                clips[i].LoadAudioData();
        }
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            Play(mouseClickSfx, mouseClickVolume);
    }

    public void PlayDelete()
    {
        Play(deleteSfx, volume);
    }

    public void PlayHover()
    {
        Play(hoverSfx, hoverVolume);
    }

    public void PlayPickup()
    {
        Play(pickupSfx, volume);
    }

    public void PlayInstall()
    {
        Play(installSfx, volume);
    }

    void Play(AudioClip clip, float clipVolume)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip, clipVolume);
    }
}
