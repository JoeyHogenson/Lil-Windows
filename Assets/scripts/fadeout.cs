using UnityEngine;
using System.Collections;

public class FreezePlayerWithFade : MonoBehaviour
{
    public GameObject player;                  // Reference to player GameObject
    public MonoBehaviour playerController;     // Reference to the movement script
    public CanvasGroup fadeObject;             // UI element to fade in/out
    public float freezeDuration = 2f;          // How long the player stays frozen
    public float fadeTime = 1f;                // How long fade in/out takes

    public void TriggerEffect()
    {
        StartCoroutine(FreezeAndFadeRoutine());
    }
    private void Start()
    {
        
    }
    private IEnumerator FreezeAndFadeRoutine()
    {
        // Freeze player
        if (playerController != null)
            playerController.enabled = false;

        // Fade in
        yield return StartCoroutine(FadeCanvasGroup(fadeObject, 0f, 1f, fadeTime));

        // Wait
        yield return new WaitForSeconds(freezeDuration);

        // Fade out
        yield return StartCoroutine(FadeCanvasGroup(fadeObject, 1f, 0f, fadeTime));

        // Unfreeze player
        if (playerController != null)
            playerController.enabled = true;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
    {
        float elapsed = 0f;
        cg.blocksRaycasts = true;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        cg.alpha = end;
        if (end == 0f) cg.blocksRaycasts = false;
    }
}
