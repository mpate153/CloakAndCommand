using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Drop on a persistent object (e.g. Main Menu) with your Audio Mixer assigned.
/// Re-applies saved volumes when the scene loads.
/// </summary>
public class SettingsAudioBootstrap : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParam = "MasterVol";
    [SerializeField] private string musicParam = "MusicVol";
    [SerializeField] private string sfxParam = "SfxVol";

    private void Awake()
    {
        GameSettings.EnsureLoaded();
        GameSettings.ApplyAudio(mixer, masterParam, musicParam, sfxParam);
    }
}
