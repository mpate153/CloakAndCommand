using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Loads a Markdown <see cref="TextAsset"/> (.md) into TextMeshPro on enable.
/// See <see cref="CreditsMarkdown"/> for supported syntax.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CreditsTextImporter : MonoBehaviour
{
    [Tooltip("Your credits Markdown file (.md). Drag it from the Project window.")]
    [FormerlySerializedAs("creditsFile")]
    [SerializeField] TextAsset creditsMarkdown;

    [Tooltip("Which TextMeshPro to fill. Leave empty if you put this component on the same GameObject as your TextMeshProUGUI (or TextMeshPro 3D). Otherwise drag the credits text object here.")]
    [SerializeField] TMP_Text creditsLabel;

    void OnEnable()
    {
        ApplyCreditsText();
    }

#if UNITY_EDITOR
    [ContextMenu("Apply credits from Markdown now")]
    void ApplyCreditsTextEditor() => ApplyCreditsText();
#endif

    void ApplyCreditsText()
    {
        if (creditsMarkdown == null) return;

        var tmp = creditsLabel != null ? creditsLabel : GetComponent<TMP_Text>();
        if (tmp == null)
        {
            Debug.LogWarning($"{nameof(CreditsTextImporter)}: assign {nameof(creditsLabel)} or add TextMeshPro on this GameObject.", this);
            return;
        }

        string body = creditsMarkdown.text ?? string.Empty;
        body = CreditsMarkdown.ToTmp(body);

        tmp.richText = true;
        tmp.text = body;
    }
}
