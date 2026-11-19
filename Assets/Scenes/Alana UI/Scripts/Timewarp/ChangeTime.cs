using UnityEngine;
using UnityEngine.UI;
public class ChangeTime : MonoBehaviour
{
    public MarsTimeOfDay Time;
    public Slider timeSlider;

    public Toggle Animate;

    void Awake()
    {
        timeSlider.value = Time.timeOfDay;
    }
    public void setTime(float t)
    {
        Time.timeOfDay = t;
    }
    public void toggleAnimate(bool t)
    {
        Time.animate = t;
    }

    public void setInterval(float t)
    {
        Time.dayLengthSeconds = t;
    }
}