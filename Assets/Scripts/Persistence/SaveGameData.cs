using System;
using UnityEngine;

/// <summary>Serializable settings block stored inside <see cref="SaveGameData"/>.</summary>
[Serializable]
public class SettingsSaveData
{
    /// <summary>Set to 1 when written by <see cref="GameSettings.CopyRuntimeStateTo"/>; 0 means JSON omitted it (JsonUtility empty object).</summary>
    public int settingsFormat;
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool fullscreen = true;
    public bool vsync = true;
    public int qualityLevel;
}

/// <summary>Top-level save document: extend with level/progress fields later.</summary>
[Serializable]
public class SaveGameData
{
    public int version = 1;
    public SettingsSaveData settings = new SettingsSaveData();
}
