using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Master : MonoBehaviour
{
	// NOTE: All times are 100x normal to make the simulator's actions visible, and interactions possible
	
	NodeSimulator node;	// a reference we use to access our basic node functions (send a message, etc)
	int masterCount = 5;	// masters are numbered 0 to count-1
	int workerCount = 7;	// workers are numbered masterCount to masterCount+workerCount-1
	
	// RAFT fields (measured in (fraction) seconds, rather than ms)
	float raftTimer = 0.0f;
	float raftTimeout = 1.0f;
	bool leader = false;		// (am I leader?)
	int raftTerm = 1;
	int votedFor = -1;
	bool candidate = false;		// (am I candidate? (overruled by above, in case both true))
		int candidateVotes = 0;
		
	// For managing the Map Reduce (only modified by the master-leader, then updated on other masters)
	int updateCounter = 0;
	
	TaskData[] mapTasks;
	
	TaskData[] reduceTasks;
	
	// Update() runs every frame, so this is our while(true) central loop that runs the logic of the node
	void Update()
	{
		advanceTimers();
		
		if (raftTimer > raftTimeout)
		{
			raftTimer = 0f;
			
			if (leader)
			{	// If leader, send heartbeat
				sendHeartbeat();
			}
			else
			{	// Otherwise, we've timed out! Become candidate and request votes as leader
				candidate = true;
				raftTerm += 1;
				votedFor = node.nodeID; candidateVotes = 1;	// (vote for self)
								
				for (int i = 0; i < masterCount; i++)
				{
					node.sendMessage(i, "REQUESTVOTE", raftTerm + "");
				}
			}
		}
	}
	
	public void receiveMessage(int fromID, string messageType, string payload)
	{	// This method is called remotely whenever a message arrives from another node
		if (node.crashed)
			return;
	
		// If the message is a heartbeat from a leader:
		if (messageType.StartsWith("HEARTBEAT"))
		{	
			if (leader)
			{	// If a leader receives a heartbeat message, ... do what?
				// TODO
				return;
			}
			else
			{	// If a follower receives a heartbeat message, reset the timeout
				raftTimer = 0;
				return;
			}
		}
		
		// If the message is a request for votes:
		if (messageType.StartsWith("REQUESTVOTE"))
		{
			int tempTerm = int.Parse(payload);
			// (from an older term; decline)
			if (tempTerm < raftTerm)
				node.sendMessage(fromID, "VOTED", "FALSE");
			Debug.Log("REQUEST 2!");
			if (tempTerm > raftTerm)
			{	// update the term, undo vote if it's newer term
				raftTerm = tempTerm;
				votedFor = -1;
			}
			Debug.Log("REQUEST 3!");
			// (grant it unless we've already voted for someone else)
			if (votedFor < 0 || votedFor == fromID)
			{
				votedFor = fromID;
				becomeFollower();
				node.sendMessage(fromID, "VOTED", "TRUE");
			}
			else
			{
				node.sendMessage(fromID, "VOTED", "FALSE");	
			}
		}
		
		// If it is a response to a request for votes:
		if (messageType.StartsWith("VOTED"))
		{
			if (!candidate)
				return;
			
			if (payload.Contains("TRUE"))
			{
				candidateVotes += 1;
				if (candidateVotes > ((float)masterCount)/2.0f)
					becomeLeader();
			}
			// (if it's false, do nothing)
		}
	}
	
	void sendHeartbeat()
	{	// We are the leader, remind them so
		for (int i = 0; i < masterCount; i++)
		{
			if (i != node.nodeID)
				node.sendMessage(i, "HEARTBEAT", raftTerm + "");
		}
	}
	
	void becomeLeader()
	{	// (this helper function is used to concisely call the "I'm now leader" initialization)
		leader = true;
		candidate = false;
		raftTimeout = 0.1f; // (leader timeout is 100ms)
		raftTimer = raftTimeout; // (starting leader immediately sends a heartbeat)
	}
	
	void becomeFollower()
	{
		candidate = false;
		leader = false;
		raftTimeout = (Random.Range(0.150f, 0.300f)); // (follower timeout is 150-250ms)
		raftTimer = 0;
	}


//
//	SIMULATION INTERACTIONS
//	(mostly dealing with importing initial values/setting up, or with running the simulation (timers/graphics)
//	
	public void setup()
	{	// (invoked remotely to get things started)
		node = GetComponent<NodeSimulator>();
		if (node.nodeID == 0)		// node 0 is the starting leader, the one from which the user invoked the map-reduce operation
		{
			becomeLeader();
		}
		else
		{
			becomeFollower();
		}
		masterCount = node.masterCount;
		workerCount = node.workerCount;
		
		updateCounter = 0;
		mapTasks = new TaskData[node.mapCount];
		for (int i = 0; i < mapTasks.Length; i++)
			mapTasks[i] = new TaskData();
		reduceTasks = new TaskData[node.reduceCount];
		for (int i = 0; i < reduceTasks.Length; i++)
			reduceTasks[i] = new TaskData();
		
	}
	
	public Sprite crashedSprite;
	public Sprite leaderSprite;
	public Sprite masterSprite;
	
	void advanceTimers()
	{	// Each frame, increment timers according to framerate
		if (node.crashed)
		{
			raftTimer = 0;
			for (int i = 0; i < mapTasks.Length; i++)
				mapTasks[i].timer = 0;
			for (int i = 0; i < reduceTasks.Length; i++)
				reduceTasks[i].timer = 0;
		}
		else if (node.paused)
		{
			// don't increment the timer if paused
		}
		else
		{
			float passedTime = Time.deltaTime / node.timeFactor;
			raftTimer += passedTime;
			for (int i = 0; i < mapTasks.Length; i++)
			{
				if (mapTasks[i].timer > 0)
					mapTasks[i].timer -= passedTime;
			}
			for (int i = 0; i < reduceTasks.Length; i++)
			{
				if (reduceTasks[i].timer > 0)
					reduceTasks[i].timer -= passedTime;
			}
		}
		
		// (we may as well cheat and put the sprite-update here)
		if (node.crashed)
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = crashedSprite;
			becomeFollower();
		}		
		else if (leader)
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = leaderSprite;
		}
		else
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = masterSprite;
		}
		
		if (leader || !candidate)
		{
			candidateVotes = 0;	// (this shouldn't ever come up; but just in case)
		}
	}
}

class TaskData
{
	public bool complete;
	public string filename;
	public float timer;
	
	public TaskData()
	{
		complete = false;
		filename = "";
		timer = -1.0f;
	}
}