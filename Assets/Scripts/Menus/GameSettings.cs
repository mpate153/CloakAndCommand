using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central game settings loaded/saved via PlayerPrefs. Display applies on startup;
/// assign an AudioMixer (Expose Volume as float in dB) on a bootstrap object for sound.
/// </summary>
public static class GameSettings
{
    private const string KeyMaster = "st_master_vol";
    private const string KeyMusic = "st_music_vol";
    private const string KeySfx = "st_sfx_vol";
    private const string KeyFullscreen = "st_fullscreen";
    private const string KeyVsync = "st_vsync";
    private const string KeyQuality = "st_quality";

    private static bool _loaded;
    private static float _master = 1f;
    private static float _music = 1f;
    private static float _sfx = 1f;
    private static bool _fullscreen = true;
    private static bool _vsync = true;
    private static int _quality = -1;

    public static float MasterVolume => _master;
    public static float MusicVolume => _music;
    public static float SfxVolume => _sfx;
    public static bool Fullscreen => _fullscreen;
    public static bool Vsync => _vsync;
    public static int QualityLevel => _quality >= 0 ? _quality : QualitySettings.GetQualityLevel();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoApplyDisplay()
    {
        EnsureLoaded();
        ApplyDisplay();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _master = PlayerPrefs.GetFloat(KeyMaster, 1f);
        _music = PlayerPrefs.GetFloat(KeyMusic, 1f);
        _sfx = PlayerPrefs.GetFloat(KeySfx, 1f);
        _fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        _vsync = PlayerPrefs.GetInt(KeyVsync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        _quality = PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
        _loaded = true;
    }

    public static void SetMasterVolume(float linear01)
    {
        _master = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(KeyMaster, _master);
    }

    public static void SetMusicVolume(float linear01)
    {
        _music = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(KeyMusic, _music);
    }

    public static void SetSfxVolume(float linear01)
    {
        _sfx = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(KeySfx, _sfx);
    }

    public static void SetFullscreen(bool value)
    {
        _fullscreen = value;
        PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
    }

    public static void SetVsync(bool value)
    {
        _vsync = value;
        PlayerPrefs.SetInt(KeyVsync, value ? 1 : 0);
    }

    public static void SetQualityLevel(int index)
    {
        index = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        _quality = index;
        PlayerPrefs.SetInt(KeyQuality, _quality);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    public static void ResetToDefaults()
    {
        _master = _music = _sfx = 1f;
        _fullscreen = true;
        _vsync = true;
        _quality = QualitySettings.names.Length - 1;
        PlayerPrefs.SetFloat(KeyMaster, _master);
        PlayerPrefs.SetFloat(KeyMusic, _music);
        PlayerPrefs.SetFloat(KeySfx, _sfx);
        PlayerPrefs.SetInt(KeyFullscreen, 1);
        PlayerPrefs.SetInt(KeyVsync, 1);
        PlayerPrefs.SetInt(KeyQuality, _quality);
        Save();
        ApplyDisplay();
    }

    public static void ApplyDisplay()
    {
        EnsureLoaded();
        QualitySettings.SetQualityLevel(_quality);
        QualitySettings.vSyncCount = _vsync ? 1 : 0;
        Screen.fullScreen = _fullscreen;
    }

    /// <summary>
    /// Call from a bootstrap with your mixer. Expose parameters as floats (dB),
    /// e.g. &quot;MasterVol&quot;, &quot;MusicVol&quot;, &quot;SfxVol&quot;.
    /// </summary>
    public static void ApplyAudio(AudioMixer mixer,
        string masterParam = "MasterVol",
        string musicParam = "MusicVol",
        string sfxParam = "SfxVol")
    {
        EnsureLoaded();
        if (mixer == null) return;

        SetMixerGroupVolume(mixer, masterParam, _master);
        SetMixerGroupVolume(mixer, musicParam, _music);
        SetMixerGroupVolume(mixer, sfxParam, _sfx);
    }

    private static void SetMixerGroupVolume(AudioMixer mixer, string parameter, float linear01)
    {
        if (string.IsNullOrEmpty(parameter)) return;
        float dB = linear01 > 0.0001f ? 20f * Mathf.Log10(linear01) : -80f;
        mixer.SetFloat(parameter, dB);
    }
}
