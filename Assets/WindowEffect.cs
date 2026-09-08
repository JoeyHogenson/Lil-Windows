using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class WindowEffect : MonoBehaviour
{
    public bool windows = false;
    public PostProcessVolume postProcessVolume;
    float t = 0f;
    float target = 2f;
    void Start()
    {
    }
    void Update()
    {
        if(windows == true)
        {
            if (t < 1f) 
            {
                t += Time.deltaTime;
                postProcessVolume.profile.GetSetting<ColorGrading>().postExposure.value = Mathf.Lerp(0f, target, t);
                Debug.Log("this is working");
            }
            else 
            {
                Invoke("TurnWindowEffectOff", 1.5f);
                windows = false;
            }
        }
        
        
    }
    public void TurnWindowEffectOff()
    {
        postProcessVolume.profile.GetSetting<ColorGrading>().postExposure.value = 0f;
    }
    public void SetWindows()
    {
        windows = true;
        Debug.Log("this is working 232323423114");
    }
}
