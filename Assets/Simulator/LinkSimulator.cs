﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinkSimulator : MonoBehaviour
{
	// Which two nodes are connected by this link?
    public int nodeAindex;
	public GameObject nodeA;
	public int nodeBindex;
	public GameObject nodeB;
	
	public GameObject messagePrefab;
	public GameObject manager;
	
	public float timeFactor = 100f;		// (how many times slower does the simlulation run? (default x100) )
	
	// For running the simulation
	public bool broken = false;		// if set to true, this link will drop all packets that go along it
	public int latency = 0;		// how many milliseconds for packets to delay mid-transit
	
	public void setupLink(GameObject newNodeA, GameObject newNodeB, GameObject newManager)
	{
		nodeA = newNodeA;
		nodeAindex = nodeA.GetComponent<NodeSimulator>().nodeID;
		nodeB = newNodeB;
		nodeBindex = nodeB.GetComponent<NodeSimulator>().nodeID;
		manager = newManager;
	}
	
	public void sendMessageAB(string label, string payload)
	{	// Send message A->B
		GameObject thisMessage = Instantiate(messagePrefab, new Vector3(nodeA.transform.position.x, nodeA.transform.position.y, 1), Quaternion.identity);
		thisMessage.GetComponent<MessageSimulator>().Setup(nodeA, nodeB, label, payload, null, latency, broken, manager, timeFactor);
		
	}
	public void sendMessageBA(string label, string payload)
	{	// Send message B->A
		GameObject thisMessage = Instantiate(messagePrefab, new Vector3(nodeB.transform.position.x, nodeB.transform.position.y, 1), Quaternion.identity);
		thisMessage.GetComponent<MessageSimulator>().Setup(nodeB, nodeA, label, payload, null, latency, broken, manager, timeFactor);
	}
	public void sendMessageAB(string label, string payload, string[] files)
	{	// Send message A->B
		GameObject thisMessage = Instantiate(messagePrefab, new Vector3(nodeA.transform.position.x, nodeA.transform.position.y, 1), Quaternion.identity);
		thisMessage.GetComponent<MessageSimulator>().Setup(nodeA, nodeB, label, payload, files, latency, broken, manager, timeFactor);
		
	}
	public void sendMessageBA(string label, string payload, string[] files)
	{	// Send message B->A
		GameObject thisMessage = Instantiate(messagePrefab, new Vector3(nodeB.transform.position.x, nodeB.transform.position.y, 1), Quaternion.identity);
		thisMessage.GetComponent<MessageSimulator>().Setup(nodeB, nodeA, label, payload, files, latency, broken, manager, timeFactor);
	}
}
