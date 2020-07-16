using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BtnBreakLink : MonoBehaviour
{
    GameObject link;
	
	public void setup(GameObject newLink)
	{
		link = newLink;
		if (link.GetComponent<LinkSimulator>().broken)
			GetComponent<SpriteRenderer>().color = Color.grey;
		else if (!link.GetComponent<LinkSimulator>().broken)
			GetComponent<SpriteRenderer>().color = Color.white;
	}
	
    void OnMouseDown()
	{
		if (link == null)
			return;
		
		if (link.GetComponent<LinkSimulator>().broken)
		{
			link.GetComponent<LinkSimulator>().broken = false;
			GetComponent<SpriteRenderer>().color = Color.white;
		}
		else if (!link.GetComponent<LinkSimulator>().broken)
		{
			link.GetComponent<LinkSimulator>().broken = true;
			GetComponent<SpriteRenderer>().color = Color.grey;
		}
	}
   
}
