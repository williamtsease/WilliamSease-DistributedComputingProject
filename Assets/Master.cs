using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	int updateCounter = -1;		// = number of completed tasks (-1 means we haven't yet received the duplicated files)
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
				candidateVotes = 1; votedFor = node.nodeID;	// (vote for self)
				
				for (int i = 0; i < masterCount; i++)
				{
					node.sendMessage(i, "REQUESTVOTE", raftTerm + "\n" + updateCounter);
				}
			}
		}
	}
	
	public void receiveMessage(int fromID, string messageType, string payload)
	{	// This method is called remotely whenever a message arrives from another node
		if (node.crashed)
			return;
	
		// MESSAGE IS A LEADER HEARTBEAT
		if (messageType.StartsWith("HEARTBEAT"))
		{	
			int theirTerm = int.Parse(payload.Split('\n')[0]);
			if (theirTerm > raftTerm)
				raftTerm = theirTerm;
			int theirUpdate = int.Parse(payload.Split('\n')[1]);
			Debug.Log("Their update is " + theirUpdate+ ", mine is " + updateCounter);
			if (leader)
			{	// If a leader receives a heartbeat message ...
				if (theirUpdate < updateCounter)
				{	// they are behind us; reject their heartbeat and immediately send one of our own (they will give way as below)
					raftTimer = raftTimeout + 1;
				}
				else
				{	// they are NOT behind us; give way
					becomeFollower();
				}
				return;
			}
			else
			{	// If a follower receives a heartbeat message, reset the timeout and read their update
				if (theirUpdate < updateCounter)
				{	// (unless they are behind us; then reject their heartbeat and become candidate)
					raftTimer = raftTimeout + 1;
					return;
				}
				
				raftTimer = 0;
				// if they have the original starting files and we don't, their update is not useful to us; we need the files
				if (updateCounter < 0 && theirUpdate >= 0)
				{
					node.sendMessage(fromID, "REQUESTFILES", "-1");
					return;
				}
				
				// otherwise, update everything
				string mapUpdates = payload.Split('\n')[2];
				for (int i = 0; i < mapUpdates.Length; i++)
				{
					if (mapUpdates[i] == '1')
					{
						mapTasks[i].complete = true;
					}
				}
				string reduceUpdates = payload.Split('\n')[3];
				for (int i = 0; i < reduceUpdates.Length; i++)
				{
					if (reduceUpdates[i] == '1')
					{
						reduceTasks[i].complete = true;
					}
				}
				
				updateUC();
				printTaskProgress();
			}
		}
		
		// MESSAGE IS A REQUEST FOR VOTES
		if (messageType.StartsWith("REQUESTVOTE"))
		{
			int tempTerm = int.Parse(payload.Split('\n')[0]);
			int tempCounter = int.Parse(payload.Split('\n')[1]);
			// (from an older term; decline)
			if (tempTerm < raftTerm)
				node.sendMessage(fromID, "VOTED", "FALSE");
			if (tempTerm > raftTerm)
			{	// update the term, undo vote if it's newer term
				raftTerm = tempTerm;
				votedFor = -1;
			}
			// (grant it unless we've already voted for someone else, or if the new potential is out of date)
			if (tempCounter < updateCounter)
			{
				node.sendMessage(fromID, "VOTED", "FALSE");
			}
			else if (votedFor < 0 || votedFor == fromID)
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
		
		// MESSAGE IS A VOTE RESPONSE
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
		
		// MESSAGE IS A REPORT OF/REQUEST FOR A MAP OR REDUCE TASK
		if (messageType.StartsWith("TASKFINISHED"))
		{
			string tempType = payload.Split('\n')[0];
			int tempJobNumber = int.Parse(payload.Split('\n')[1]);
			
			// update completed task
			if (tempJobNumber > -1)
			{
				if (tempType.Equals("MAP"))
				{
					mapTasks[tempJobNumber].complete = true;
				}
				else if (tempType.Equals("REDUCE"))
				{
					reduceTasks[tempJobNumber].complete = true;
				}
			}
			updateUC();
			
			if (!leader)
				return; //(only the leader responds to workers)
			
			if (!mappingDone())
			{	// assuming we're not done with mapping tasks, hand out the next one
				int thisTask = getMapTask();
				if (thisTask < 0)
					node.sendMessage(fromID, "WAIT", "");		// no map task available at this time, wait for them to finish
				else
				{
					node.sendMessage(fromID, "GIVETASK", "MAP" + "\n" + thisTask + "\n" + mapTasks[thisTask].filename, mapTasks[thisTask].filename);
					mapTasks[thisTask].timer = 1.000f;	// set timeout to 1.0s
				}
			}
			else if (!reducingDone())
			{	// All map tasks are done, so hand out a reduce task
				int thisTask = getReduceTask();
				if (thisTask < 0)
					node.sendMessage(fromID, "WAIT", "");		// no reduce task available at this time, wait for them to finish
				else
				{
					string[] tempReduceFiles = new string[node.mapCount];
					for (int i = 0; i < node.mapCount; i++)
						tempReduceFiles[i] = node.directory + "\\" + "intermediate"+thisTask+"-"+i+".txt";
					node.sendMessage(fromID, "GIVETASK", "REDUCE" + "\n" + thisTask, tempReduceFiles);
					reduceTasks[thisTask].timer = 1.000f;	// set timeout to 1.0s
				}
			}
			else
			{	// Both map AND reduce tasks are done, tell the worker to exit
				node.sendMessage(fromID, "EXIT", "");
			}
//			printTaskProgress();
		}
		
		// MESSAGE IS A REQUEST TO DUPLICATE FILES
		if (messageType.StartsWith("REQUESTFILES"))
		{
			string[] whichFiles = payload.Split('\n');
			
			// The other server needs the base starting files
			if (whichFiles[0] == "-1")
			{
				sendBaseFiles(fromID);
				return;
			}
			
			// Otherwise, it's sets of intermediate files that are needed
			sendIntermediateFiles(fromID, whichFiles);
		}
		
		// MESSAGE IS THE INITIAL STARTING FILES BEING GIVEN TO US
		if (messageType.StartsWith("STARTINGFILES"))
		{
			setFiles();
			updateCounter = 0;
		}
	}
	
	void sendHeartbeat()
	{	// We are the leader, remind them so (also update their task logs)
		string completedTasks = "";
		for (int i = 0; i < mapTasks.Length; i++)
		{
			if (mapTasks[i].complete)
				completedTasks += "1";
			else
				completedTasks += "0";
		}
		completedTasks += "\n";
		for (int i = 0; i < reduceTasks.Length; i++)
		{
			if (reduceTasks[i].complete)
				completedTasks += "1";
			else
				completedTasks += "0";
		}
	
		for (int i = 0; i < masterCount; i++)
		{
			if (i != node.nodeID)
				node.sendMessage(i, "HEARTBEAT", raftTerm + "\n" + updateCounter + "\n" + completedTasks);
		}
	}
	
	void sendBaseFiles(int updateServer)
	{	// send the server the original mapping files
		if (!leader)
			return;
	
		string fileNames = "";
		string[] startingFiles = new string[mapTasks.Length];
		for (int i = 0; i < mapTasks.Length; i++)
		{
			startingFiles[i] = mapTasks[i].filename;
			fileNames += mapTasks[i].filename.Split('\\')[mapTasks[i].filename.Split('\\').Length-1] + "\n";
		}
		node.sendMessage(updateServer, "STARTINGFILES", "", startingFiles);
	}
	
	void sendIntermediateFiles(int updateServer, string[] taskNumbers)
	{
		if (!leader)
			return;
		
		Debug.Log("Sending "+taskNumbers.Length+" files!");
		int tempSize = 0;
		for (int i = 0; i < taskNumbers.Length; i++)
		{
			Debug.Log(taskNumbers[i]);
			if (taskNumbers[i].StartsWith("m"))
			{
				tempSize += reduceTasks.Length;
			}
			else if (taskNumbers[i].StartsWith("r"))
			{
				tempSize += 1;
			}
		}
		string[] allFiles = new string[tempSize];
		tempSize = 0;	// (now serving as a counter)
		for (int i = 0; i < taskNumbers.Length; i++)
		{
			int number = int.Parse(taskNumbers[i].Substring(1));
			if (taskNumbers[i].StartsWith("m"))
			{
				for (int j = 0; j < reduceTasks.Length; j++)
				{
					allFiles[tempSize] = node.directory + "\\intermediate" + j + "-" + number + ".txt";
					tempSize ++;
				}
			}
			else if (taskNumbers[i].StartsWith("r"))
			{
				allFiles[tempSize] = node.directory + "\\" + "output"+number+".txt";
				tempSize ++;
			}
		}
		node.sendMessage(updateServer, "", "", allFiles);	// no type or payload needed, since only purpose of this message is to duplicate files
		
	}
	
	void becomeLeader()
	{	// (this helper function is used to concisely call the "I'm now leader" initialization)
		leader = true;
		candidate = false;
		candidateVotes = 0;
		raftTimeout = 0.1f; // (leader timeout is 100ms)
		raftTimer = raftTimeout; // (starting leader immediately sends a heartbeat)
	}
	
	void becomeFollower()
	{
		candidate = false;
		leader = false;
		candidateVotes = 0;
		raftTimeout = (Random.Range(0.150f, 0.300f)); // (follower timeout is 150-250ms)
		raftTimer = 0;
	}

	// a simple helper method that returns true if all map tasks are done, false otherwise
	bool mappingDone()
	{
		for (int i = 0; i < mapTasks.Length; i++)
		{
			if (!mapTasks[i].complete)
				return false;
		}
		return true;
	}

	//  a simple helper method that gets the first map task that is not completed and not in progress (or timed out)
	int getMapTask()
	{
		for (int i = 0; i < mapTasks.Length; i++)
		{
			if (!mapTasks[i].complete && mapTasks[i].timer <= 0)
				return i;
		}
		return -1;
	}

	//  a simple helper method that returns true if all reduce tasks are done, false otherwise
	bool reducingDone()
	{
		for (int i = 0; i < reduceTasks.Length; i++)
		{
			if (!reduceTasks[i].complete)
				return false;
		}
		return true;
	}

	//  a simple helper method that gets the first map task that is not completed and not in progress (or timed out)
	int getReduceTask()
	{
		for (int i = 0; i < reduceTasks.Length; i++)
		{
			if (!reduceTasks[i].complete && reduceTasks[i].timer <= 0)
				return i;
		}
		return -1;
	}
	
	//  a helper method that updates the counter
	void updateUC()
	{
		if (updateCounter < 0)	// We haven't been properly initialized or received the base files; no point tracking
			return;
		
		string taskFilesNeeded = "";
		
		updateCounter = 0;
		for (int i = 0; i < mapTasks.Length; i++)
		{
			if (mapTasks[i].complete)
			{
				// we've marked it complete; do we have all the right files?
				bool haveFiles = true;
				for (int j = 0; j < reduceTasks.Length; j++)
				{
					if (!File.Exists(node.directory + "\\" + "intermediate"+j+"-"+i+".txt"))
					{
						haveFiles = false;
						break;
					}
				}
				
				if (haveFiles)
					updateCounter += 1;	// all good
				else
					taskFilesNeeded  += "m"+i+"\n";
			}
		}
		for (int i = 0; i < reduceTasks.Length; i++)
		{
			if (reduceTasks[i].complete)
			{
				// we've marked it complete; do we have the right file?
				
				if (File.Exists(node.directory + "\\" + "output"+i+".txt"))
					updateCounter += 1;	// all good
				else
					taskFilesNeeded += "r"+i+"\n";
			}
		}
		
		if (taskFilesNeeded.Length > 1)
		{
			taskFilesNeeded = taskFilesNeeded.Substring(0, taskFilesNeeded.Length-1);	// truncate the final \n
			for (int i = 0; i < masterCount; i++)
				node.sendMessage(i, "REQUESTFILES", taskFilesNeeded);
		}
	}

	//  (for debugging)
	void printTaskProgress()
	{
		string tempOutput = "MAPPING: ";
		for (int i = 0; i < mapTasks.Length; i++)
		{
			if (mapTasks[i].complete)
				tempOutput += "DONE; ";
			else
				tempOutput += mapTasks[i].timer + "; ";
		}
		tempOutput += "\nREDUCE: ";
		for (int i = 0; i < reduceTasks.Length; i++)
		{
			if (reduceTasks[i].complete)
				tempOutput += "DONE; ";
			else
				tempOutput += reduceTasks[i].timer + "; ";
		}
		Debug.Log(tempOutput);
	}












//
//	SIMULATION INTERACTIONS
//	(mostly dealing with importing initial values/setting up, or with running the simulation (timers/graphics)
//	
	public void setup()
	{	// (invoked remotely to get things started)
		node = GetComponent<NodeSimulator>();
		updateCounter = -1;
		
		if (node.nodeID == 0)		// node 0 is the starting leader, the one from which the user invoked the map-reduce operation
		{
			updateCounter = 0;
			becomeLeader();
		}
		else
		{
			becomeFollower();
		}
		masterCount = node.masterCount;
		workerCount = node.workerCount;
		
		mapTasks = new TaskData[node.mapCount];
		for (int i = 0; i < mapTasks.Length; i++)
			mapTasks[i] = new TaskData();
		reduceTasks = new TaskData[node.reduceCount];
		for (int i = 0; i < reduceTasks.Length; i++)
			reduceTasks[i] = new TaskData();
	}
	
	public void setFiles()
	{	// Get the starting target files, stick them into the map tasks
		var tempFiles = Directory.EnumerateFiles(node.directory, "pg-*.txt");
		int fileCounter = 0;
		foreach (var file in tempFiles)
		{
			mapTasks[fileCounter].filename = file;
			fileCounter += 1;
		}
	}
	
	public Sprite crashedSprite;
	public Sprite leaderSprite;
	public Sprite masterSprite;
	
	public GameObject docImg;
	public GameObject docMultText;
	
	void advanceTimers()
	{	// Each frame, increment timers according to framerate
		if (node.crashed)
		{
			raftTimer = 0;
			for (int i = 0; i < mapTasks.Length; i++)
				mapTasks[i].timer = -1;
			for (int i = 0; i < reduceTasks.Length; i++)
				reduceTasks[i].timer = -1;
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
		
		var tempFiles = Directory.EnumerateFiles(node.directory, "*.txt");
		int fileCount = 0;
		foreach (var file in tempFiles)
			fileCount += 1;
		if (fileCount < 1)
		{
			docImg.GetComponent<SpriteRenderer>().color = Color.clear;
			docMultText.GetComponent<TextMesh>().text = " ";
		}
		else
		{	
			docImg.gameObject.GetComponent<SpriteRenderer>().color = Color.white;
			docMultText.GetComponent<TextMesh>().text = "x"+fileCount;
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