using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central game settings: preferred load from <see cref="SaveGame"/> JSON when present,
/// otherwise PlayerPrefs. <see cref="Save"/> flushes both. Display applies on startup;
/// assign an AudioMixer on a bootstrap object for sound.
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
        if (SaveGame.TryLoad(out SaveGameData file) && file.settings != null
            && !LooksLikeJsonUtilityEmptySettings(file.settings))
        {
            ApplyFromSaveSettings(file.settings);
            return;
        }

        LoadSettingsFromPlayerPrefs();
    }

    /// <summary>Copies current in-memory settings into a save payload. Do not call <see cref="EnsureLoaded"/> here —
    /// it can reload the JSON file and overwrite values just set by sliders in the same frame.</summary>
    public static void CopyRuntimeStateTo(SettingsSaveData s)
    {
        if (s == null) return;
        s.settingsFormat = 1;
        s.masterVolume = _master;
        s.musicVolume = _music;
        s.sfxVolume = _sfx;
        s.fullscreen = _fullscreen;
        s.vsync = _vsync;
        s.qualityLevel = _quality >= 0 ? _quality : QualitySettings.GetQualityLevel();
    }

    /// <summary>
    /// JsonUtility leaves omitted fields at type defaults. A <c>settings: {}</c> object becomes all-zero audio
    /// and false/false/0 for display, which reads as broken rather than user intent.
    /// </summary>
    private static bool LooksLikeJsonUtilityEmptySettings(SettingsSaveData s)
    {
        if (s.settingsFormat >= 1)
            return false;
        const float tol = 1e-5f;
        bool allAudioZero = Mathf.Abs(s.masterVolume) < tol && Mathf.Abs(s.musicVolume) < tol && Mathf.Abs(s.sfxVolume) < tol;
        if (!allAudioZero)
            return false;
        return !s.fullscreen && !s.vsync && s.qualityLevel == 0;
    }

    private static void LoadSettingsFromPlayerPrefs()
    {
        _master = PlayerPrefs.GetFloat(KeyMaster, 1f);
        _music = PlayerPrefs.GetFloat(KeyMusic, 1f);
        _sfx = PlayerPrefs.GetFloat(KeySfx, 1f);
        _fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        _vsync = PlayerPrefs.GetInt(KeyVsync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        _quality = PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
        _loaded = true;
    }

    private static void ApplyFromSaveSettings(SettingsSaveData s)
    {
        int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
        _master = Mathf.Clamp01(s.masterVolume);
        _music = Mathf.Clamp01(s.musicVolume);
        _sfx = Mathf.Clamp01(s.sfxVolume);
        _fullscreen = s.fullscreen;
        _vsync = s.vsync;
        _quality = Mathf.Clamp(s.qualityLevel, 0, maxQ);

        PlayerPrefs.SetFloat(KeyMaster, _master);
        PlayerPrefs.SetFloat(KeyMusic, _music);
        PlayerPrefs.SetFloat(KeySfx, _sfx);
        PlayerPrefs.SetInt(KeyFullscreen, _fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyVsync, _vsync ? 1 : 0);
        PlayerPrefs.SetInt(KeyQuality, _quality);
        _loaded = true;
    }

    public static void SetMasterVolume(float linear01)
    {
        EnsureLoaded();
        _master = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(KeyMaster, _master);
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void SetMusicVolume(float linear01)
    {
        EnsureLoaded();
        _music = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(KeyMusic, _music);
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void SetSfxVolume(float linear01)
    {
        EnsureLoaded();
        _sfx = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(KeySfx, _sfx);
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void SetFullscreen(bool value)
    {
        EnsureLoaded();
        _fullscreen = value;
        PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void SetVsync(bool value)
    {
        EnsureLoaded();
        _vsync = value;
        PlayerPrefs.SetInt(KeyVsync, value ? 1 : 0);
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void SetQualityLevel(int index)
    {
        EnsureLoaded();
        index = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        _quality = index;
        PlayerPrefs.SetInt(KeyQuality, _quality);
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.Save();
        SaveGame.PersistSettingsFromRuntime();
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();
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
        QualitySettings.vSyncCount = _vsync ? 1 : 0;
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

    /// <summary>
    /// Linear gain cap used for mixer/fallback mapping. Set to 1 so 100% sliders are true full scale.
    /// </summary>
    internal const float MixerFullSliderLinearCap = 1f;

    private static void SetMixerGroupVolume(AudioMixer mixer, string parameter, float linear01)
    {
        if (string.IsNullOrEmpty(parameter)) return;
        float t = Mathf.Clamp01(linear01) * MixerFullSliderLinearCap;
        float dB = t > 0.0001f ? 20f * Mathf.Log10(t) : -80f;
        mixer.SetFloat(parameter, dB);
    }
}
