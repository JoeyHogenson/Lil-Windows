using UnityEngine;

/// <summary>
/// Base for the segregation systems: calls <see cref="OnDayStarted"/> once for each new day the
/// <see cref="GameController"/> starts, including a day that started before this component did.
/// </summary>
public abstract class SegDayListener : MonoBehaviour
{
    [Tooltip("Left empty, the scene's GameController is found automatically.")]
    [SerializeField] protected GameController game;

    /// <summary>The in-game day this system last handled; 0 before the first day starts.</summary>
    public int Today { get; private set; }

    protected virtual void OnEnable()
    {
        if(game == null) game = FindFirstObjectByType<GameController>();
        if(game != null) game.DayStarted += HandleDayStarted;
        else Debug.LogWarning("[" + GetType().Name + "] No GameController in the scene; days will never advance.");
    }

    protected virtual void OnDisable()
    {
        if(game != null) game.DayStarted -= HandleDayStarted;
    }

    protected virtual void Start()
    {
        //GameController.Start may have started day 1 before this component subscribed
        if(game != null && game.IsDayStarted) HandleDayStarted(game.dayCount);
    }

    private void HandleDayStarted(int day)
    {
        if(day == Today) return;
        Today = day;
        OnDayStarted(day);
    }

    protected abstract void OnDayStarted(int day);
}
