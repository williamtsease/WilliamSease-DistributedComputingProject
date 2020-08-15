using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Master : MonoBehaviour
{
	bool DEBUGPRINTING = false;
	// NOTE: All times are 100x normal (changing with the simulator's speed) to make the simulator's actions visible, and interactions possible
	
	NodeSimulator node;		// a reference we use to access our basic node functions (send a message, etc)
	int masterCount = 5;	// masters are numbered 0 to count-1
	int workerCount = 7;	// workers are numbered masterCount to masterCount+workerCount-1
	
	// RAFT fields (measured in (fraction) seconds, rather than ms)
	float raftTimer = 0.0f;		// (when timer exceeds timeout (below), take RAFT action based on current status)
	float raftTimeout = 1.0f;	// (timer (in seconds) which is set to various values depending on node type)
	bool leader = false;		// (am I leader?)
	int raftTerm = 1;			// (raft term)
	bool candidate = false;		// (am I candidate? (overruled by leader above, in case both true))
		int candidateVotes = 0;		// (and if so, how many votes do I have?)
	int votedFor = -1;			// (who have I voted for this term? -1 = none)
		
	// MAP REDUCE fields (only modified by the master-leader, then updated on other masters)
	int updateCounter = -1;		// = number of currently completed tasks (-1 means we haven't yet received the duplicated files)
	TaskData[] mapTasks;		// Data on map tasks (worker timeout, completion flag, etc) -- TaskData class is at the end of this file 
	TaskData[] reduceTasks;		// Data on reduce tasks (worker timeout, completion flag, etc) -- TaskData class is at the end of this file 
	
	// update() is called every frame, so this is our while(true) central loop that runs the logic of the node
	public void update()
	{
		if (raftTimer > raftTimeout)
		{	// Time is up! Take action ...
			raftTimer = 0f;
			
			if (leader)
			{	// If leader, send heartbeat
				sendHeartbeat();
			}
			else
			{	// Otherwise, we've timed out! Become candidate and request votes as prospective new leader
				// (note: we can enter this state either as a follower master or as a master that was already a candidate but didn't get enough votes)
				candidate = true;
				raftTerm += 1;
				candidateVotes = 1; votedFor = node.nodeID;	// (vote for self, that's 1 vote)
				raftTimeout = (Random.Range(0.125f, 0.175f)); // (election timeout is 125-175ms, the same as follower timeout)
				
				for (int i = 0; i < masterCount; i++)
				{
					node.sendMessage(i, "REQUESTVOTE", raftTerm + "\n" + updateCounter);
				}
			}
		}
	}
	
	public void receiveMessage(int fromID, string messageType, string payload)
	{	// This method is called remotely whenever a message arrives from another node
		// So, what is the message?
		
		// OPTION 1: MESSAGE IS A LEADER HEARTBEAT
		if (messageType.StartsWith("HEARTBEAT"))
		{	
			int theirTerm = int.Parse(payload.Split('\n')[0]);
			if (theirTerm > raftTerm)
				raftTerm = theirTerm;
			int theirUpdate = int.Parse(payload.Split('\n')[1]);
			if (DEBUGPRINTING) { Debug.Log("Their update is " + theirUpdate+ ", mine is " + updateCounter); }
			if (leader)
			{	// If a leader receives a heartbeat message ...
				if (theirUpdate < updateCounter)
				{	// they are behind us; reject their heartbeat and immediately send one of our own (they will give way, as seen immediately below)
					raftTimer = raftTimeout + 1;
				}
				else
				{	// they are NOT behind us; give way
					becomeFollower();
				}
			}
			else
			{	// If a follower receives a heartbeat message ...
				if (theirUpdate < updateCounter)
				{	// (unless they are behind us; then reject their heartbeat and become candidate - ie, timeout immediately)
					raftTimer = raftTimeout + 1;
					return;
				}
				
				// Otherwise, it's a legal heartbeat so reset our timer
				raftTimer = 0;
				// if they have the original starting files and we don't, their update is not useful to us; we need the files
				if (updateCounter < 0 && theirUpdate >= 0)
				{
					node.sendMessage(fromID, "REQUESTFILES", "-1");
					return;
				}
				
				// Update our task logs
				// There will be two strings of 1s and 0s in the payload (after the term and counter)
				string mapUpdates = payload.Split('\n')[2];		// First come the map tasks, 1s are complete and 0s are incomplete
				for (int i = 0; i < mapUpdates.Length; i++)
				{
					if (mapUpdates[i] == '1')
					{
						mapTasks[i].complete = true;
					}
					// NOTE: If they claim a task is incomplete, we will not mark ours incomplete -- this way we can do a logical-AND merge on our two logs (kinda)
				}
				string reduceUpdates = payload.Split('\n')[3];
				for (int i = 0; i < reduceUpdates.Length; i++)
				{
					if (reduceUpdates[i] == '1')
					{
						reduceTasks[i].complete = true;
					}
				}
				
				updateUC();		// (any tasks that we don't have the files for will be marked incomplete again, and we'll request the files -- that won't be an issue unless we've missed some worker updates)
				printTaskProgress();
			}
		}
		
		// OPTION 2: MESSAGE IS A REQUEST FOR VOTES
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
			// (grant it unless we've already voted for someone else, or if the new potential's log is out of date)
			if ((votedFor >= 0 && votedFor != fromID ) || tempCounter < updateCounter)
			{
				node.sendMessage(fromID, "VOTED", "FALSE");
			}
			else
			{
				votedFor = fromID;
				becomeFollower();
				node.sendMessage(fromID, "VOTED", "TRUE");
			}
		}
		
		// OPTION 3: MESSAGE IS A VOTE RESPONSE
		if (messageType.StartsWith("VOTED"))
		{
			// If we're no longer a candidate, ignore it
			if (!candidate)
				return;
			
			// If they voted for us, increment our vote and see if we have enough votes to become leader
			if (payload.Contains("TRUE"))
			{
				candidateVotes += 1;
				if (candidateVotes > ((float)masterCount)/2.0f)
					becomeLeader();
			}
			
			// If they denied our vote, we don't increment or indeed do anything
		}
		
		// OPTION 4: MESSAGE IS FROM A WORKER, REPORTING/REQUESTING A MAP OR REDUCE TASK
		if (messageType.StartsWith("TASKFINISHED"))
		{
			// (only the leader responds to workers - if we are not the leader, we will keep the duplicated files but take no further action)
				// (also, if we've somehow managed to elect a master-leader without the starting files, it can't respond)
				// (this will only happen if the initial leader becomes unresponsive BEFORE it has a chance to duplicate them safely, in which case the system will be legitimately stuck)
			if (!leader || updateCounter < 0)
				return; 		
			
			string tempType = payload.Split('\n')[0];
			int tempJobNumber = int.Parse(payload.Split('\n')[1]);
			
			// update completed task, if there was one
			if (tempJobNumber > -1)
			{
				if (tempType.Equals("MAP"))
					mapTasks[tempJobNumber].complete = true;
				else if (tempType.Equals("REDUCE"))
					reduceTasks[tempJobNumber].complete = true;
				// (this master doesn't know how to cope with any other task type, since they don't exist in this system)
			}
			updateUC();
			
			if (!mappingDone())
			{	// assuming we're not done with all the mapping tasks, hand out the next one
				int thisTask = getMapTask();
				if (thisTask < 0)
					node.sendMessage(fromID, "WAIT", "");		// no map task available at this time, tell the worker to wait for the rest of them to finish or for one to time out
				else
				{
					node.sendMessage(fromID, "GIVETASK", "MAP" + "\n" + thisTask + "\n" + mapTasks[thisTask].filename, mapTasks[thisTask].filename);
					mapTasks[thisTask].timer = 0.500f;	// set worker timeout to 0.5s
				}
			}
			else if (!reducingDone())
			{	// if we ARE done with all mapping tasks, so hand out a reduce task
				int thisTask = getReduceTask();
				if (thisTask < 0)
					node.sendMessage(fromID, "WAIT", "");		// no map task available at this time, tell the worker to wait for the rest of them to finish or for one to time out
				else
				{
					string[] tempReduceFiles = new string[node.mapCount];		// we'll need to send it a whole bundle of intermediate files to act as input
					for (int i = 0; i < node.mapCount; i++)
						tempReduceFiles[i] = node.directory + "\\" + "intermediate"+thisTask+"-"+i+".txt";
					
					node.sendMessage(fromID, "GIVETASK", "REDUCE" + "\n" + thisTask, tempReduceFiles);
					reduceTasks[thisTask].timer = 0.500f;	// set worker timeout to 0.5s
				}
			}
			else
			{	// Both map AND reduce tasks are done, tell the worker to exit
				Debug.Log("JOB COMPLETE: " + (int)(GameObject.Find("SceneManager").GetComponent<SimulatorManager>().msPassed*1000) + "ms");	// (print the current time to the console, for testing purposes)
				node.sendMessage(fromID, "EXIT", "");
			}
			if (DEBUGPRINTING) printTaskProgress();
		}
		
		// OPTION 5: MESSAGE IS A REQUEST TO DUPLICATE FILES
		if (messageType.StartsWith("REQUESTFILES"))
		{
			string[] whichFiles = payload.Split('\n');
			
			// The other server needs the base starting files
			if (whichFiles[0] == "-1")
			{
				sendBaseFiles(fromID);
				return;
			}
			
			// Otherwise, it's sets of intermediate/final output files that are needed - that's complicated, so we wrote a submethod
			sendIntermediateFiles(fromID, whichFiles);
		}
		
		// OPTION 6: MESSAGE IS THE INITIAL STARTING FILES BEING GIVEN TO US
		if (messageType.StartsWith("STARTINGFILES"))
		{
			// We are now an operational master!
			setFiles();
			updateCounter = 0;
		}
	}
	
	void sendHeartbeat()
	{	// We are the leader, remind them so (also update their task logs)
	
		// Since the payload of a message is just a string, we'll use a set of 1s and 0s to indicate completed/incomplete tasks, as explained above in receiveMessage under the heartbeat section
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
		{	// (send it to every master node other than ourself)
			if (i != node.nodeID)
				node.sendMessage(i, "HEARTBEAT", raftTerm + "\n" + updateCounter + "\n" + completedTasks);
		}
	}
	
	void sendBaseFiles(int updateServer)
	{	// This server is in need of the original mapping input files; pass them along
		
		// Note: If we are receiving this message, it means the leader has a counter of at least 0; therefore the current leader does have the files
		// If we are not the leader, however, we might not have them ourself
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
	{	// Despite the name, this method is for sending the output files of a set of tasks (whatever the current leader has told us they have, but we don't)
		// In the event that we're missing map output, we'll need sets of intermediate files
		// If we're missing reduce output, we'll need the final output files (of which only one is produced per task)
		
		// As above, we only know the current leader has the specified files
		if (!leader)
			return;
		
		if(DEBUGPRINTING) { Debug.Log("Sending "+taskNumbers.Length+" files!"); }
		
		// The task number array takes the form of strings that are formed "m2" "m3" "r5" etc, telling us whether the task is map or reduce and also the task number
		int tempSize = 0;
		for (int i = 0; i < taskNumbers.Length; i++)
		{
			if (taskNumbers[i].StartsWith("m"))
			{	// If it's a map task, we need to send a number of intermediate files equal to the reduce task count
				tempSize += reduceTasks.Length;
			}
			else if (taskNumbers[i].StartsWith("r"))
			{	// If it's a reduce task, just send one
				tempSize += 1;
			}
		}
		// We know how many total files we're sending; so we can make an array and start attaching them
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
		node.sendMessage(updateServer, "", "", allFiles);	// no type or payload needed, since only purpose of this message is to duplicate files, and that happens before receiveMessage() starts parsing contents
	}
	
	void becomeLeader()
	{	// (this helper function is used to concisely call the "I'm now leader" initialization)
		leader = true;
		candidate = false;
		candidateVotes = 0;
		raftTimeout = 0.050f; // (leader heartbeat timeout is 50ms)
		raftTimer = raftTimeout; // (starting leader immediately sends a heartbeat)
	}
	
	void becomeFollower()
	{
		candidate = false;
		leader = false;
		candidateVotes = 0;
		raftTimeout = (Random.Range(0.125f, 0.175f)); // (follower timeout is 125-175ms, long enough for two heartbeats)
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
		if(DEBUGPRINTING) { Debug.Log(tempOutput); }
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
	











//
//	SIMULATION INTERACTIONS
//	(mostly dealing with importing initial values/setting up, or with running the simulation (timers/graphics)
//	I would have preferred to put these outside in Node Simulator, but unfortunately this code is master-specific
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
	
	// Sprites for setting the master image based on its state
	public Sprite crashedSprite;
	public Sprite leaderSprite;			// <<-- if it weren't for THIS sprite, we could have put all this code in NodeSimulator, but no, workers don't have a leader : P
	public Sprite masterSprite;
	// (The background object that shows whether we have files and how many)
	public GameObject docImg;
	public GameObject docMultText;
	
	public void advanceTimers()
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