using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BtnCrash : MonoBehaviour
{
	GameObject selected;
	
	public void setup(GameObject newSelected)
	{
		selected = newSelected;
		try
		{
			if (selected.GetComponent<NodeSimulator>().crashed)
				GetComponent<SpriteRenderer>().color = Color.grey;
			else if (!selected.GetComponent<NodeSimulator>().crashed)
				GetComponent<SpriteRenderer>().color = Color.white;
		} catch { }
		try
		{
			if (selected.GetComponent<MessageSimulator>().dropped)
				GetComponent<SpriteRenderer>().color = Color.grey;
			else if (!selected.GetComponent<MessageSimulator>().dropped)
				GetComponent<SpriteRenderer>().color = Color.white;
		} catch { }
	}
	
    void OnMouseDown()
	{
		if (selected == null)
			return;
		
		try
		{
			if (selected.GetComponent<NodeSimulator>().crashed)
			{
				selected.GetComponent<NodeSimulator>().crashed = false;
				GetComponent<SpriteRenderer>().color = Color.white;
			}
			else if (!selected.GetComponent<NodeSimulator>().crashed)
			{
				selected.GetComponent<NodeSimulator>().crashed = true;
				GetComponent<SpriteRenderer>().color = Color.grey;
			}
		} catch { }
		try
		{
			if (selected.GetComponent<MessageSimulator>().dropped)
			{
				selected.GetComponent<MessageSimulator>().dropped = false;
				GetComponent<SpriteRenderer>().color = Color.white;
			}
			else if (!selected.GetComponent<MessageSimulator>().dropped)
			{
				selected.GetComponent<MessageSimulator>().dropped = true;
				GetComponent<SpriteRenderer>().color = Color.grey;
			}
		} catch { }
	}
}
