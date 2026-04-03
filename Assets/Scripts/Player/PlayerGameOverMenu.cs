using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-scene game over UI: bind a retry <see cref="Button"/> (inspector or <see cref="BindRetryButton"/>).
/// Reloads <see cref="PlayerTracker.GetLastLevelScene"/>.
/// </summary>
public sealed class PlayerGameOverMenu : MonoBehaviour
{
    [Tooltip("Optional. If null and auto-bind is on, uses first Button in children.")]
    [SerializeField] Button retryButton;

    [Tooltip("If true and retryButton is null, binds first Button in children on Start.")]
    [SerializeField] bool autoBindFirstChildButtonOnStart = true;

    [Tooltip("If last level is unknown, load this (e.g. MainMenu).")]
    [SerializeField] string fallbackSceneName = "MainMenu";

    [Tooltip("Delete saved layout JSON for the retry level before loading (fresh layout).")]
    [SerializeField] bool deleteLayoutSaveForRetryLevel = true;

    [Tooltip("Clear SceneNavigator history so back-stack does not point at Game Over.")]
    [SerializeField] bool clearSceneNavigatorHistory = true;

    Button _boundRetry;

    void Start()
    {
        if (!autoBindFirstChildButtonOnStart)
            return;
        if (retryButton == null)
            retryButton = GetComponentInChildren<Button>(true);
        if (retryButton != null)
            BindRetryButton(retryButton);
    }

    void OnDestroy()
    {
        if (_boundRetry != null)
            _boundRetry.onClick.RemoveListener(RetryLastLevel);
    }

    public void BindRetryButton(Button button)
    {
        if (button == null)
            return;
        if (_boundRetry != null && _boundRetry != button)
            _boundRetry.onClick.RemoveListener(RetryLastLevel);
        _boundRetry = button;
        retryButton = button;
        button.onClick.RemoveListener(RetryLastLevel);
        button.onClick.AddListener(RetryLastLevel);
    }

    public void RetryLastLevel()
    {
        SceneNavigator nav = FindFirstObjectByType<SceneNavigator>();
        if (nav != null)
        {
            nav.GoBackToPreviousSceneGameOver();
            return;
        }

        string retry = PlayerTracker.GetLastLevelScene();
        if (string.IsNullOrEmpty(retry))
        {
            if (!string.IsNullOrEmpty(fallbackSceneName))
                SceneManager.LoadScene(fallbackSceneName);
            return;
        }

        if (deleteLayoutSaveForRetryLevel)
        {
            try
            {
                string path = SaveManager.GetSavedLayoutPathForScene(retry);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerGameOverMenu] Could not delete layout save: {e.Message}");
            }
        }

        if (clearSceneNavigatorHistory)
            SceneNavigator.ClearHistory();

        Time.timeScale = 1f;
        SceneManager.LoadScene(retry);
    }

    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
