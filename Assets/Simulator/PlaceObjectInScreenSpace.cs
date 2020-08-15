using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaceObjectInScreenSpace : MonoBehaviour
{
	public float xRight = 0f; // how far do we move right from the upper left corner, proportionally?
	public float yDown = 0f; // how far do we move down from the upper left corner, proportionally?
	
	// A simple little program to get our gamespace objects to orient themselves by the screenspace isntead
    void Start()
    {
		place();
    }
	
	public void place()
	{
		Vector3 oldLocation = new Vector3(0f + xRight*Screen.width, Screen.height - yDown*Screen.height, 0f);		// find the upper left corner
		Vector3 newLocation = new Vector3(Camera.main.ScreenToWorldPoint(oldLocation).x, Camera.main.ScreenToWorldPoint(oldLocation).y, 0f);	// convert it, but leave z cooridinate as 0
        transform.position = newLocation;
	}
}
