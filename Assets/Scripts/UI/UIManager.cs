using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArrowBridge
{
    /// <summary>
    /// Minimal HUD: a row of life hearts (a wrong move — clicking a blocked arrow — costs one),
    /// a win panel, and a fail panel shown when all lives are gone. Both panels restart the level
    /// via the same button logic. There is deliberately no arrow counter or progress bar — the
    /// bridge visibly growing across the water IS the progress indicator.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private List<Image> lifePips = new List<Image>();
        [SerializeField] private GameObject winPanel;
        [SerializeField] private TMP_Text winText;
        [SerializeField] private Button restartButton;
        [SerializeField] private GameObject failPanel;
        [SerializeField] private TMP_Text failText;
        [SerializeField] private Button failRestartButton;

        public void Configure(
            List<Image> newLifePips,
            GameObject newWinPanel, TMP_Text newWinText, Button newRestartButton,
            GameObject newFailPanel, TMP_Text newFailText, Button newFailRestartButton)
        {
            lifePips = newLifePips;
            winPanel = newWinPanel;
            winText = newWinText;
            restartButton = newRestartButton;
            failPanel = newFailPanel;
            failText = newFailText;
            failRestartButton = newFailRestartButton;

            winPanel.SetActive(false);
            failPanel.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.LivesChanged += HandleLivesChanged;
            GameManager.Won += HandleWon;
            GameManager.LevelFailed += HandleLevelFailed;

            // Button.onClick.AddListener() registers a non-persistent (runtime-only) listener —
            // Unity never serializes it, so it must be re-wired every enable rather than once
            // from Configure() at edit time (and dropped in OnDisable to avoid stacking).
            if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
            if (failRestartButton != null) failRestartButton.onClick.AddListener(RestartLevel);
        }

        private void OnDisable()
        {
            GameManager.LivesChanged -= HandleLivesChanged;
            GameManager.Won -= HandleWon;
            GameManager.LevelFailed -= HandleLevelFailed;

            if (restartButton != null) restartButton.onClick.RemoveListener(RestartLevel);
            if (failRestartButton != null) failRestartButton.onClick.RemoveListener(RestartLevel);
        }

        private void HandleLivesChanged(int remaining, int total)
        {
            for (int i = 0; i < lifePips.Count; i++)
            {
                if (lifePips[i] != null) lifePips[i].color = i < remaining ? GamePalette.LifeFull : GamePalette.LifeEmpty;
            }
        }

        private void HandleWon()
        {
            winText.text = "Tamamlandı!";
            winPanel.SetActive(true);
        }

        private void HandleLevelFailed()
        {
            failText.text = "Can bitti!";
            failPanel.SetActive(true);
        }

        private void RestartLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
