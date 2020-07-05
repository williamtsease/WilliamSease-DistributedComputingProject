using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Master : MonoBehaviour
{
	// NOTE: All times are 100x normal to make the simulator's actions visible, and interactions possible
	
	NodeSimulator node;	// a reference we use to access our basic node functions (send a message, etc)
	int masterCount = 5;	// masters are numbered 0 to count-1
	
	// RAFT fields (measured in (fraction) seconds, rather than ms)
	float raftTimer;
	float raftTimeout;
	bool leader;
	
	// Update() runs every frame, so this is our while(true) central loop that runs the logic of the node
	void Update()
	{
		advanceTimers();
		
		if (raftTimer > raftTimeout)
		{
			raftTimer = 0f;
			
			if (leader)
			{
				sendHeartbeat();
			}
			else
			{
				// become candidate
			}
			
			
		//	if (node.nodeID == 0)
		//		node.sendMessage(1, "THIS IS A TEST");
		}
	}
	
	public void receiveMessage(int fromID, string payload)
	{	// This method is called remotely whenever a message arrives from another node
	
		// If the message is a heartbeat from a leader:
		if (payload.StartsWith("HEARTBEAT"))
		{	// If a follower receives a heartbeat message ...
			if (leader)
			{
				// we were the leader? TODO
			}
			else
			{
				raftTimer = 0;
				return;
			}
		}
	}
	
	void sendHeartbeat()
	{	// We are the leader, remind them so
		for (int i = 0; i < masterCount; i++)
		{
			if (i != node.nodeID)
				node.sendMessage(i, "HEARTBEAT\n1");
		}
	}
	
	


//
//	SIMULATION INTERACTIONS
//	
	
	void Start()
	{
		initializeNode();
		Random rnd = new Random();
		raftTimer = 0;
		if (node.nodeID == 0)		// node 0 is the starting leader, the one from which the user invoked the map-reduce operation
		{
			leader = true;
			raftTimeout = 0.1f*100f; // (leader timeout is 100ms (x100))
			raftTimer = raftTimeout; // (starting leader immediately sends a heartbeat)
		}
		else
		{
			leader = false;
			raftTimeout = (Random.Range(0.150f, 0.300f))*100f; // (follower timeout is 150-250ms (x100))
		}
	}
	
	public Sprite crashedSprite;
	public Sprite leaderSprite;
	public Sprite masterSprite;
	
	void advanceTimers()
	{	// Each frame, increment timers according to framerate
		if (node.crashed)
		{
			raftTimer = 0;
		}
		else if (node.paused)
		{
			// don't increment the timer if paused
		}
		else
		{
			raftTimer += Time.deltaTime;
		}
		
		// (we may as well cheat and put the sprite-update here)
		if (node.crashed)
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = crashedSprite;
		}		
		else if (leader)
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = leaderSprite;
		}
		else
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = masterSprite;
		}
	}
	
	void initializeNode()
	{
		node = GetComponent<NodeSimulator>();
	}
}
