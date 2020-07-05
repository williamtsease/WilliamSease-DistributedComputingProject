using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodeSimulator : MonoBehaviour
{
	// All nodes have a unique ID number
    public int nodeID;
	public GameObject[] links;
	
	// For running the simulation
	public bool crashed = false;	// if set to true, this node will do nothing
	public bool paused = false;		// if set to true, this node will do nothing but will preserve its state
	
	public void sendMessage(int targetIndex, string tempMessage)
	{	// send a message to another node
		if (nodeID == targetIndex)
		{
			return;
		}
		else if (nodeID < targetIndex)
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageAB(tempMessage);
		}
		else
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageBA(tempMessage);
		}
	}
	
	public void receiveMessage(int fromNumber, string payload)
	{	// We don't know whether this node is a master or a worker, so try to pass the message along both ways
		try
		{
			GetComponent<Master>().receiveMessage(fromNumber, payload);
		} catch { }
		try
		{
			GetComponent<Worker>().receiveMessage(fromNumber, payload);
		} catch { }
	}
}
