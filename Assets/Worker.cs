using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Worker : MonoBehaviour
{
	// TO DO: Worker logic is not written yet - implement the MR part and not just the master's raft
	
	// NOTE: All times are 100x normal to make the simulator's actions visible, and interactions possible
	
	NodeSimulator node;	// a reference we use to access our basic node functions (send a message, etc)

	// Update() runs every frame, so this is our while(true) central loop that runs the logic of the node
	void Update()
	{

	}
	
	public void receiveMessage(int fromID, string messageType, string payload)
	{	// This method is called remotely whenever a message arrives from another node
		
	}
	
	
	
	
	


//
//	SIMULATION INTERACTIONS
//	
	
	public void setup()
	{
		node = GetComponent<NodeSimulator>();
	}
	
}
