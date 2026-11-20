using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right HUD: the in-game clock ("Sun 6:40 AM - Day 1"), the next scheduled activity ("Next: Count at
/// 12:00 PM"), and any urgent timers other systems post (e.g. OG's medicine). Built in code and created
/// automatically in any scene with a <see cref="GameController"/>.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    private GameController game;
    private TextMeshProUGUI clockLabel;
    private TextMeshProUGUI nextLabel;
    private RectTransform urgentColumn;
    private readonly Dictionary<string, TextMeshProUGUI> urgent = new Dictionary<string, TextMeshProUGUI>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if(Instance != null) return;
        GameController controller = FindFirstObjectByType<GameController>();
        if(controller == null) return;
        var go = new GameObject("Game HUD");
        Instance = go.AddComponent<GameHUD>();
        Instance.game = controller;
    }

    void Awake()
    {
        Instance = this;
        Canvas canvas = SegUIKit.CreateCanvas("Game HUD Canvas", 10);
        canvas.transform.SetParent(transform, false);

        RectTransform panel = SegUIKit.Rect("Clock Panel", canvas.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-24f, -20f);
        panel.sizeDelta = new Vector2(520f, 0f);
        //translucent backing keeps it readable over any background (UI Shadow effects do nothing on TMP text)
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        VerticalLayoutGroup column = SegUIKit.Column(panel, 4f, 12);
        column.childAlignment = TextAnchor.UpperRight;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        clockLabel = SegUIKit.Label(panel, "", 30f, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
        nextLabel = SegUIKit.Label(panel, "", 20f, new Color(0.85f, 0.85f, 0.8f), FontStyles.Normal, TextAlignmentOptions.Right);
        urgentColumn = SegUIKit.Rect("Urgent", panel);
        SegUIKit.Column(urgentColumn, 2f, 0).childAlignment = TextAnchor.UpperRight;
    }

    void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    void Update()
    {
        if(game == null) game = FindFirstObjectByType<GameController>();
        if(game == null) return;
        clockLabel.text = game.ClockText + "  -  Day " + game.dayCount;
        nextLabel.text = game.TryGetNextActivity(out string activity, out float hour)
            ? "Next: " + activity + " at " + GameController.FormatHour(hour)
            : "";
    }

    /// <summary>Shows or updates an urgent line (by key). Severity 0..1 fades it from amber to red.</summary>
    public void SetUrgent(string key, string text, float severity)
    {
        if(!urgent.TryGetValue(key, out TextMeshProUGUI label))
        {
            label = SegUIKit.Label(urgentColumn, "", 22f, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
            urgent[key] = label;
        }
        label.text = text;
        label.color = Color.Lerp(new Color(1f, 0.78f, 0.3f), new Color(1f, 0.25f, 0.2f), Mathf.Clamp01(severity));
        //pulse when it's close
        if(severity > 0.75f) label.alpha = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
        label.gameObject.SetActive(true);
    }

    public void ClearUrgent(string key)
    {
        if(urgent.TryGetValue(key, out TextMeshProUGUI label)) label.gameObject.SetActive(false);
    }

}
