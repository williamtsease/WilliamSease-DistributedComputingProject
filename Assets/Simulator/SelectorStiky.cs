using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectorStiky : MonoBehaviour
{
	public GameObject target;
	
	void Update()
    {
        if (target != null)
		{
			transform.position = new Vector3(target.transform.position.x, target.transform.position.y, -2f);
		}
    }
}
