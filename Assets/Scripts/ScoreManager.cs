using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // We will use TextMeshPro for crisp VR text

public class ScoreManager : MonoBehaviour
{
    // The Singleton instance so any script can easily say "ScoreManager.Instance.AddScore()"
    public static ScoreManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI multiplierText;

    [Header("Current Stats")]
    public int currentScore = 0;
    public int currentCombo = 0;
    public int comboMultiplier = 1;

    // How many consecutive hits needed to reach the next multiplier tier
    private int[] multiplierThresholds = new int[] { 0, 2, 6, 14 }; 

    void Awake()
    {
        // Setup Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateUI();
    }

    // Called by BeatCube.cs when a block is successfully perfectly sliced
    public void AddScore(int pointsAdded)
    {
        // 1. Increase Combo
        currentCombo++;

        // 2. Check if we reached a new Multiplier (Max 8x)
        if (comboMultiplier < 8)
        {
            int nextTierIndex = GetMultiplierIndex(comboMultiplier) + 1;
            if (nextTierIndex < multiplierThresholds.Length && currentCombo >= multiplierThresholds[nextTierIndex])
            {
                comboMultiplier *= 2; // Goes 1x -> 2x -> 4x -> 8x
            }
        }

        // 3. Add to total score
        currentScore += (pointsAdded * comboMultiplier);

        UpdateUI();
        
        Debug.Log($"[Score] Block sliced for {pointsAdded}pts! Multiplier: {comboMultiplier}x. Total: {currentScore}");
    }

    // Called by BeatCube.cs if it gets behind the player without being cut
    public void BreakCombo()
    {
        currentCombo = 0;
        
        // Breaking a combo drops your multiplier by half in Beat Saber (e.g. 8x drops to 4x)
        if (comboMultiplier > 1)
        {
            comboMultiplier /= 2;
        }

        UpdateUI();
    }

    private int GetMultiplierIndex(int multiplier)
    {
        if (multiplier == 1) return 0;
        if (multiplier == 2) return 1;
        if (multiplier == 4) return 2;
        if (multiplier == 8) return 3;
        return 0; // fallback
    }

    private void UpdateUI()
    {
        // Update the screen text if you attached them in the Inspector
        if (scoreText != null) scoreText.text = currentScore.ToString();
        if (comboText != null) comboText.text = currentCombo.ToString();
        if (multiplierText != null) multiplierText.text = comboMultiplier.ToString() + "x";
    }
}
