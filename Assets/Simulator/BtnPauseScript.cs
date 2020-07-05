using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BtnPauseScript : MonoBehaviour
{
	public GameObject gameManager;
	
    void OnMouseDown()
	{
		gameManager.GetComponent<SimulatorManager>().pauseSim();
		if (gameManager.GetComponent<SimulatorManager>().paused)
		{
			GetComponent<SpriteRenderer>().color = Color.grey;
		}
		else
		{
			GetComponent<SpriteRenderer>().color = Color.white;
		}
	}
}
