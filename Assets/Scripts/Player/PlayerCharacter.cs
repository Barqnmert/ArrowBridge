using System;
using System.Collections;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Waits at Kara A until the bridge is complete, then crosses to Kara B. Movement is a simple
    /// constant-speed glide of this root; the life comes from the rigged character model parented
    /// under it — an Animator blends Idle to Running while crossing, and the model turns to face
    /// its travel direction. (The old procedural bobbing is gone; the walk cycle does that now.)
    /// </summary>
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField] private float walkDuration = 3.5f;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform modelRoot;

        private static readonly int IsWalkingParam = Animator.StringToHash("IsWalking");

        public void Configure(Vector3 startPosition, Animator newAnimator, Transform newModelRoot)
        {
            transform.position = startPosition;
            animator = newAnimator;
            modelRoot = newModelRoot;
        }

        public void WalkAcross(Vector3 targetPosition, Action onComplete = null)
        {
            StopAllCoroutines();
            StartCoroutine(WalkRoutine(targetPosition, onComplete));
        }

        private IEnumerator WalkRoutine(Vector3 endPosition, Action onComplete)
        {
            FaceToward(endPosition);
            if (animator != null) animator.SetBool(IsWalkingParam, true);

            Vector3 start = transform.position;
            float elapsed = 0f;
            while (elapsed < walkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / walkDuration);
                transform.position = Vector3.Lerp(start, endPosition, t);
                yield return null;
            }

            transform.position = endPosition;
            if (animator != null) animator.SetBool(IsWalkingParam, false);
            onComplete?.Invoke();
        }

        /// <summary>Yaws the model left/right along the bridge axis. glTF models face +Z, so ±90° points them along ±X.</summary>
        private void FaceToward(Vector3 target)
        {
            if (modelRoot == null) return;
            float deltaX = target.x - transform.position.x;
            modelRoot.localRotation = Quaternion.Euler(0f, deltaX >= 0f ? 90f : -90f, 0f);
        }
    }
}
