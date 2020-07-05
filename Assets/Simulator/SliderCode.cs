using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SliderCode : MonoBehaviour
{
    float prevVal;
    Slider slider;

    void Start(){

        slider = GetComponent<Slider>();
        prevVal = slider.value;

        slider.onValueChanged.AddListener (delegate {ValueChangeCheck ();});

    }

    void ValueChangeCheck()
    {

       if (slider.value % 2 == 0) {   //check odd value

            slider.value = prevVal;

        } else {

            prevVal = slider.value;

        }

    }
}
