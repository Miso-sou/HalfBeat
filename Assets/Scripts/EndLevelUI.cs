using TMPro;
using UnityEngine;

/// <summary>
/// Attach to a GameObject in your level (e.g. under Canvas). Assign the "Level finished" TextMeshProUGUI.
/// BeatSpawner calls ShowLevelFinished() when every block in the beatmap has been hit or missed.
/// </summary>
public class EndLevelUI : MonoBehaviour
{
    [Tooltip("TMP text shown when the level ends (hidden until then).")]
    public TextMeshProUGUI levelFinishedText;

    [Tooltip("Optional: whole panel to show (e.g. background + text). If set, this is activated instead of only the text.")]
    public GameObject levelFinishedPanel;

    public static EndLevelUI Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        HideNow();
    }

    private void Start()
    {
        HideNow();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HideNow()
    {
        if (levelFinishedPanel != null)
            levelFinishedPanel.SetActive(false);
        if (levelFinishedText != null)
            levelFinishedText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called by BeatSpawner when all cubes for the level are resolved (hit or missed).
    /// </summary>
    public void ShowLevelFinished()
    {
        if (levelFinishedPanel != null)
            levelFinishedPanel.SetActive(true);
        if (levelFinishedText != null)
        {
            levelFinishedText.gameObject.SetActive(true);
            if (string.IsNullOrEmpty(levelFinishedText.text))
                levelFinishedText.text = "Level finished";
        }
    }
}
