using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene loads via <see cref="LoadScene"/> remember the scene you left. <see cref="GoBackToPreviousScene"/> pops and loads it.
/// Use <see cref="LoadSettingsAdditive"/> from gameplay or main menu so the previous scene stays loaded (no full reload for settings).
/// </summary>
public class SceneNavigator : MonoBehaviour
{
    static readonly Stack<string> PreviousScenes = new Stack<string>();

    static readonly List<EventSystem> EventSystemsDisabledForSettingsOverlay = new List<EventSystem>(4);

    [SerializeField] string playSceneName = "StealthOpen";

    [Header("Back")]
    [Tooltip("When the history stack is empty, Go Back loads this scene (e.g. main menu).")]
    [SerializeField] string backFallbackSceneName = "MainMenu";

    [Header("Settings overlay")]
    [Tooltip("Loaded additively so gameplay/menu underneath is not unloaded.")]
    [SerializeField] string settingsSceneName = "SettingsMenu";

    [Header("Game over retry")]
    [Tooltip("When using GoBackToPreviousSceneGameOver, delete saved layout for the level so the run restarts fresh.")]
    [SerializeField] bool deleteLayoutSaveOnGameOverRetry = true;

    /// <summary>Remembers the active scene, then loads <paramref name="sceneName"/> (single mode — replaces current).</summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        string current = SceneManager.GetActiveScene().name;
        if (current != sceneName)
            PreviousScenes.Push(current);

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Loads the settings scene on top; previous scene(s) stay loaded. Back / <see cref="GoBackToPreviousScene"/> unloads only the overlay.</summary>
    public void LoadSettingsAdditive()
    {
        if (string.IsNullOrEmpty(settingsSceneName))
            return;

        Scene existing = SceneManager.GetSceneByName(settingsSceneName);
        if (existing.IsValid() && existing.isLoaded)
            return;

        SceneManager.LoadScene(settingsSceneName, LoadSceneMode.Additive);
        // Synchronous load: run immediately so this works while paused (timeScale 0).
        SuppressEventSystemsOutsideScene(settingsSceneName);
    }

    static void SuppressEventSystemsOutsideScene(string overlaySceneName)
    {
        RestoreEventSystemsDisabledForSettingsOverlay();

        if (string.IsNullOrEmpty(overlaySceneName))
            return;

        Scene settings = SceneManager.GetSceneByName(overlaySceneName);
        if (!settings.IsValid() || !settings.isLoaded)
            return;

        foreach (EventSystem es in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (es == null)
                continue;
            if (es.gameObject.scene == settings)
                continue;
            if (!es.enabled)
                continue;
            es.enabled = false;
            EventSystemsDisabledForSettingsOverlay.Add(es);
        }
    }

    static void RestoreEventSystemsDisabledForSettingsOverlay()
    {
        for (int i = 0; i < EventSystemsDisabledForSettingsOverlay.Count; i++)
        {
            EventSystem es = EventSystemsDisabledForSettingsOverlay[i];
            if (es != null)
                es.enabled = true;
        }

        EventSystemsDisabledForSettingsOverlay.Clear();
    }

    /// <summary>If the settings scene is open additively, closes it and restores input. Otherwise pops history and loads the previous scene.</summary>
    public void GoBackToPreviousScene()
    {
        if (TryUnloadSettingsOverlayIfOpen())
            return;

        if (PreviousScenes.Count == 0)
        {
            string fb = string.IsNullOrEmpty(backFallbackSceneName) ? "MainMenu" : backFallbackSceneName;
            if (!string.IsNullOrEmpty(fb))
                SceneManager.LoadScene(fb);
            return;
        }

        string previous = PreviousScenes.Pop();
        if (!string.IsNullOrEmpty(previous))
            SceneManager.LoadScene(previous);
    }

    /// <summary>
    /// Hook this to the Game Over retry button. Uses <see cref="PlayerTracker.GetLastLevelScene"/> (set on death),
    /// otherwise pops one entry from history if any. Deletes layout save when <see cref="deleteLayoutSaveOnGameOverRetry"/> is on,
    /// clears history, then loads the level. If nothing is found, loads <see cref="backFallbackSceneName"/>.
    /// </summary>
    public void GoBackToPreviousSceneGameOver()
    {
        if (TryUnloadSettingsOverlayIfOpen())
            return;

        string target = PlayerTracker.GetLastLevelScene();
        if (string.IsNullOrEmpty(target) && PreviousScenes.Count > 0)
            target = PreviousScenes.Pop();

        if (string.IsNullOrEmpty(target))
        {
            string fb = string.IsNullOrEmpty(backFallbackSceneName) ? "MainMenu" : backFallbackSceneName;
            if (!string.IsNullOrEmpty(fb))
                SceneManager.LoadScene(fb);
            return;
        }

        if (deleteLayoutSaveOnGameOverRetry)
        {
            try
            {
                string path = SaveManager.GetSavedLayoutPathForScene(target);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SceneNavigator] Could not delete layout save for game over retry: {e.Message}");
            }
        }

        ClearHistory();
        Time.timeScale = 1f;
        SceneManager.LoadScene(target);
    }

    bool TryUnloadSettingsOverlayIfOpen()
    {
        if (string.IsNullOrEmpty(settingsSceneName))
            return false;

        Scene settings = SceneManager.GetSceneByName(settingsSceneName);
        if (!settings.IsValid() || !settings.isLoaded)
            return false;

        if (SceneManager.sceneCount <= 1)
            return false;

        RestoreEventSystemsDisabledForSettingsOverlay();
        SceneManager.UnloadSceneAsync(settings);
        return true;
    }

    /// <summary>Clears remembered scenes (e.g. after loading main menu from gameplay).</summary>
    public static void ClearHistory()
    {
        PreviousScenes.Clear();
    }

    /// <summary>True if a scene-layout save exists for <see cref="playSceneName"/> (used to enable Continue).</summary>
    public bool HasSaveToContinue()
    {
        if (string.IsNullOrEmpty(playSceneName))
            return false;
        string path = SaveManager.GetSavedLayoutPathForScene(playSceneName);
        return File.Exists(path);
    }

    /// <summary>Loads the play scene; <see cref="SaveManager"/> applies saved layout if a file exists.</summary>
    public void ContinueGame()
    {
        if (!string.IsNullOrEmpty(playSceneName))
            LoadScene(playSceneName);
    }

    /// <summary>Deletes the saved layout for the play scene, then loads it fresh from the scene file.</summary>
    public void NewGame()
    {
        if (string.IsNullOrEmpty(playSceneName))
            return;
        try
        {
            string path = SaveManager.GetSavedLayoutPathForScene(playSceneName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SceneNavigator] Could not delete layout save: {e.Message}");
        }

        LoadScene(playSceneName);
    }

    /// <summary>Same as <see cref="ContinueGame"/> (keeps older menu buttons working).</summary>
    public void PlayGame()
    {
        ContinueGame();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
