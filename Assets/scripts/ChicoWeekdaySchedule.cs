using UnityEngine;

// Attach to Chico alongside his Animator. Drives day-of-week animator state:
// Monday is his sleeping holiday, Tuesday brings him back to his normal awake animations.
// Add bool parameters "IsSleeping" and "IsAwake" (plus matching states/clips) to Chico's
// Animator Controller to receive these - this script only sets the parameters.
public class ChicoWeekdaySchedule : MonoBehaviour
{
    public DCGameController dayController;
    public Animator animator;

    private System.DayOfWeek _lastDay = (System.DayOfWeek)(-1);

    void Update()
    {
        if (dayController == null || animator == null) return;

        System.DayOfWeek today = dayController.GetDayOfWeek();
        if (today == _lastDay) return;
        _lastDay = today;

        animator.SetBool("IsSleeping", today == System.DayOfWeek.Monday);
        animator.SetBool("IsAwake", today == System.DayOfWeek.Tuesday);
    }
}
