using UnityEngine;

public class OpenLinks : MonoBehaviour
{
    void Awake()
    {
        Application.OpenURL("http://unity3d.com/");
    }

}
