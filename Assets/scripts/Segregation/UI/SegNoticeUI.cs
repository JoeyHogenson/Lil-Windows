using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Where the segregation systems send player-facing messages ("Commissary bag at the door...").</summary>
public static class SegNotice
{
    public static event Action<string> Posted;

    public static void Post(string message)
    {
        Debug.Log("[Seg] " + message);
        Posted?.Invoke(message);
    }
}

/// <summary>Shows <see cref="SegNotice"/> messages one at a time in a strip at the top of the screen.</summary>
public class SegNoticeUI : MonoBehaviour
{
    [SerializeField] private float secondsPerNotice = 4f;
    [Tooltip("Above the panels so notices stay readable while a form is open.")]
    [SerializeField] private int sortingOrder = 60;

    private readonly Queue<string> queue = new Queue<string>();
    private GameObject strip;
    private TextMeshProUGUI label;
    private float hideAt;

    void Awake()
    {
        Canvas canvas = SegUIKit.CreateCanvas("Seg Notice Canvas", sortingOrder);
        canvas.transform.SetParent(transform, false);

        RectTransform rect = SegUIKit.Rect("Notice", canvas.transform);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(1100f, 70f);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        background.raycastTarget = false;

        label = SegUIKit.Label(rect, "", 24f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
        SegUIKit.Stretch(label.rectTransform, 12f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 24f;

        strip = rect.gameObject;
        strip.SetActive(false);
    }

    void OnEnable()
    {
        SegNotice.Posted += Enqueue;
    }

    void OnDisable()
    {
        SegNotice.Posted -= Enqueue;
    }

    void Update()
    {
        //unscaled so notices still clear if a menu pauses time
        if(strip.activeSelf && Time.unscaledTime < hideAt) return;
        if(queue.Count > 0)
        {
            label.text = queue.Dequeue();
            strip.SetActive(true);
            hideAt = Time.unscaledTime + secondsPerNotice;
        }
        else if(strip.activeSelf)
        {
            strip.SetActive(false);
        }
    }

    private void Enqueue(string message)
    {
        queue.Enqueue(message);
    }
}
