using System;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Owns bridge-completion progress and the lives system. Every arrow is equal — whichever one
    /// clears the board adds one segment to the bridge; once all arrows are gone the character
    /// walks across and the level is won. Clicking an arrow that turns out to be blocked is a
    /// wrong move: it costs one of the player's lives, and losing all of them fails the level
    /// (the player must restart from scratch).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public static event Action<int, int> ProgressChanged; // (removedCount, totalCount)
        public static event Action<int, int> LivesChanged;    // (remainingLives, totalLives)
        public static event Action Won;
        public static event Action LevelFailed;

        [SerializeField] private int totalArrows = 30;
        [SerializeField] private int totalLives = 3;
        [SerializeField] private Vector3 karaBLandingPosition = new Vector3(7f, 0f, 0f);

        public int RemovedCount { get; private set; }
        public int TotalArrows => totalArrows;
        public int TotalLives => totalLives;
        public int RemainingLives { get; private set; }
        public bool IsWon { get; private set; }
        public bool IsFailed { get; private set; }

        private PlayerCharacter character;

        public void Configure(Vector3 newKaraBLandingPosition, int newTotalArrows, int newTotalLives)
        {
            karaBLandingPosition = newKaraBLandingPosition;
            totalArrows = newTotalArrows;
            totalLives = newTotalLives;

            // Awake() does not run for components added via editor scripts outside Play mode
            // (the level builder and edit-mode tests both call Configure right after
            // AddComponent), so initialize here too — Awake still covers Play mode.
            RemainingLives = totalLives;
        }

        private void Awake()
        {
            Instance = this;
            RemainingLives = totalLives;
        }

        private void OnEnable()
        {
            ArrowController.ArrowExited += HandleArrowExited;
            ArrowController.ArrowBlocked += HandleArrowBlocked;
        }

        private void OnDisable()
        {
            ArrowController.ArrowExited -= HandleArrowExited;
            ArrowController.ArrowBlocked -= HandleArrowBlocked;
        }

        private void Start()
        {
            character = FindFirstObjectByType<PlayerCharacter>();
            ProgressChanged?.Invoke(RemovedCount, totalArrows);
            LivesChanged?.Invoke(RemainingLives, totalLives);
        }

        private void HandleArrowExited(ArrowController arrow)
        {
            if (IsWon || IsFailed) return;

            BridgeBuilder.Instance?.AddSegment();
            RemovedCount++;
            ProgressChanged?.Invoke(RemovedCount, totalArrows);

            if (RemovedCount >= totalArrows)
            {
                character?.WalkAcross(karaBLandingPosition, TriggerWin);
            }
        }

        private void HandleArrowBlocked(ArrowController arrow)
        {
            if (IsWon || IsFailed) return;

            RemainingLives = Mathf.Max(0, RemainingLives - 1);
            LivesChanged?.Invoke(RemainingLives, totalLives);
            Debug.Log($"Arrow Bridge: wrong move — {RemainingLives}/{totalLives} lives left.");

            if (RemainingLives <= 0)
            {
                IsFailed = true;
                Debug.Log("Arrow Bridge: FAIL - out of lives.");
                LevelFailed?.Invoke();
            }
        }

        private void TriggerWin()
        {
            if (IsWon || IsFailed) return;
            IsWon = true;
            Debug.Log("Arrow Bridge: WIN - bridge complete.");
            Won?.Invoke();
        }
    }
}
