using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Worker : MonoBehaviour
{
	// NOTE: All times are 100x normal (changing with the simulator's speed) to make the simulator's actions visible, and interactions possible
	
	NodeSimulator node;			// a reference we use to access our basic node functions (send a message, etc)
	// (for simulating how long the task takes, since we need it to run at a visible speed instead of instantly)
	float taskTimer;
	float simulatedTaskTime;	
	bool taskCompleted = true;	// (depending on how we break things down, the task might actually take real time to complete, in which case we don't want to risk the above simulated timer ticking over too early)

	string taskType = "";
	int taskNumber = -1;
	string[] taskFiles;			// (to return the results, duplicate them back to the master)

	// Update() runs every frame, so this is our while(true) central loop that runs the logic of the node
	public void update()
	{
		if (taskTimer > simulatedTaskTime && taskCompleted)
		{	// task is completed, request a new one
			taskTimer = 0f;
			simulatedTaskTime = 0.100f;	// wait 100 ms before we repeat our request
			
			for (int i = 0; i < node.masterCount; i++)
				node.sendMessage(i, "TASKFINISHED", taskType+"\n"+taskNumber, taskFiles);
			// (The first time this sent, the "task" we're reporting will be -1 (no task) and we're just requesting an intial "new" task)
		}
		
//		doReducing2();	// (since my stopgap fix for hitching involves breaking the sort into multiple parts, we need to call it every time)
	}
	
	public void receiveMessage(int fromID, string messageType, string payload)
	{	// This method is called remotely whenever a message arrives from another node

		// OPTION 1: MESSAGE IS A NEW TASK
		if (messageType.StartsWith("GIVETASK"))
		{	
			taskTimer = 0;
			simulatedTaskTime = UnityEngine.Random.Range(0.150f, 0.300f);	// an arbitrary task will take 150-300 (simulated) ms to complete
			taskType = payload.Split('\n')[0];
			
			if (taskType == "MAP")
			{
				taskNumber = int.Parse(payload.Split('\n')[1]);
				string taskFileName = payload.Split('\n')[2];
				taskFileName = node.directory + "\\" + taskFileName.Split('\\')[taskFileName.Split('\\').Length-1];
			
				// Begin the work!
				taskCompleted = false;
				doMapping(taskFileName);
			}
			if (taskType == "REDUCE")
			{
				taskNumber = int.Parse(payload.Split('\n')[1]);
				// Reduce tasks use intermediate files as input, as we can just construct those file names procedurally as we need them
				
				// Begin the work!
				taskCompleted = false;
				doReducing();
			}
		}
		
		// OPTION 2: MESSAGE WAS A WAIT COMMAND
		if (messageType.StartsWith("WAIT"))
		{	// (wait 100 ms before asking again)
			taskTimer = 0;
			simulatedTaskTime = 0.100f;
			
			taskType = "";
			taskNumber = -1;
		}
		
		// OPTION 3: MESSAGE WAS AN EXIT COMMAND, PURGE FILES AND SHUT DOWN THE WORKER
		if (messageType.StartsWith("EXIT"))
		{
			var tempFiles = Directory.EnumerateFiles(node.directory, "*.txt");
			foreach (var fileName in tempFiles)
				File.Delete(fileName);
			taskNumber = -1;
			node.crashed = true;
		}
	}
	
	void doMapping(string taskFileName)
	{	// A helper method for actually doing file mapping
		
		// (create the outfiles)
		StreamWriter[] writeFile = new StreamWriter[node.reduceCount];
		taskFiles = new string[node.reduceCount];
		for (int i = 0; i < node.reduceCount; i++)
		{
			string outFileName = node.directory + "\\" + "intermediate"+i+"-"+taskNumber+".txt";
			writeFile[i] = new StreamWriter(File.Create(outFileName));
			taskFiles[i] = outFileName;
		}
		
		// Read the file in!
		string rawText = File.ReadAllText(taskFileName).ToLower();
		// (reduce it to raw text)
		rawText = rawText.Replace("\n", " ").Replace(",", " ").Replace(".", " ").Replace("?", " ").Replace("!", " ").Replace(":", " ").Replace(";", " ").Replace("\"", " ").Replace("[", " ").Replace("]", " ").Replace("*", " ").Replace("-", " ").Replace("_", " ").Replace("(", " ").Replace(")", " ");
		while (rawText.Contains("  "))
			rawText = rawText.Replace("  ", " ");
		// (split it on the spaces)
		string[] words = rawText.Split(' ');
		simulatedTaskTime = ((words.Length/500.0f)*UnityEngine.Random.Range(0.9f, 1.1f))/1000f;	// now that we have more information, we can give a better simulated task time of 500 words per millisecond (with slight random variation)
		// (each word is sorted into the right file)
		for (int i = 0; i < words.Length; i++)
		{
			int hashValue = (words[i].GetHashCode())%10;
			if (hashValue < 0)
				hashValue *= -1;
			writeFile[hashValue].WriteLine(words[i]);
		}
		taskCompleted = true;
	}
	
	void doReducing()
	{	// A helper method for actually doing file reducing
	
		string rawText = "";
		for (int i = 0; i < node.mapCount; i++)
		{
			rawText += File.ReadAllText(node.directory + "\\intermediate" + taskNumber + "-" + i + ".txt");
		}
		// (split the text into words)
		string[] words = rawText.Split('\n');
		simulatedTaskTime = ((words.Length/300.0f)*UnityEngine.Random.Range(0.9f, 1.1f))/1000f;	// now that we have more information, we can give a better simulated task time of 300 words per millisecond (slower than mapping)
		// (sort them)
		Array.Sort(words);	// <<-- THIS IS SLOW!! This is where the "hitch" is! FIX THIS!!!
		
		// (count the words)
		int[] counts = new int[words.Length];
		for (int i = 0; i < counts.Length; i++)
			counts[i] = 1;
		for (int i = 0; i < words.Length; i++)
		{
			if (counts[i] == 0)
				continue;
			
			for (int j = i+1; j < words.Length && words[j] == words[i]; j++)
			{
				counts[i] += 1;
				counts[j] = 0;
			}
		}
		
		// Output to the file
		taskFiles = new string[1];	// (only one output file, but our format is an array, so a length 1 array it is)
		taskFiles[0] = node.directory + "\\" + "output"+taskNumber+".txt";
		StreamWriter writeFile = new StreamWriter(File.Create(taskFiles[0]));
		
		for (int i = 0; i < words.Length; i++)
		{
			if (counts[i] > 0)
			{
				string thisWord = words[i].Substring(0,words[i].Length-1);
				writeFile.WriteLine(thisWord + " " + counts[i]);
			}
		}
		
		taskCompleted = true;
	}
	
	
	
/*	THIS WAS AN ATTEMPT TO BREAK DOWN THE SORTING JOB INTO A BUNCH OF LITTLE PIECES SO THE WHOLE SIMULATOR DOESN"T HAVE TO WAIT FOR ITS COMPLETION
	(NEGATING THE "HITCHING" ISSUE)
	UNFORTUNATELY EVEN WITH ONLY 5 WORDS PER FRAME IT WAS TOO CHOPPY AND ONLY 5 WORDS/FRAME WOULD TAKE WAY TOO LONG

	string[] words;
	int sorted = 0;
	
	void doReducing()
	{	// ATTEMPT 2: A helper method for actually doing file reducing
	
		string rawText = "";
		for (int i = 0; i < node.mapCount; i++)
		{
			rawText += File.ReadAllText(node.directory + "\\intermediate" + taskNumber + "-" + i + ".txt");
		}
		// (split the text into words)
		words = rawText.Split('\n');
		simulatedTaskTime = ((words.Length/300.0f)*UnityEngine.Random.Range(0.9f, 1.1f))/1000f;	// now that we have more information, we can give a better simulated task time of 300 words per millisecond (slower than mapping)
		
		// (set up the sort)
		sorted = 0;
		
	}
	
	void doReducing2()
	{
		if (taskCompleted || !taskType.Equals("REDUCE"))
			return;
		
		if (sorted >= words.Length)
		{
			// We're done with the sorting! Move on to the last part
			
			// (count the words)
			int[] counts = new int[words.Length];
			for (int i = 0; i < counts.Length; i++)
				counts[i] = 1;
			for (int i = 0; i < words.Length; i++)
			{
				if (counts[i] == 0)
					continue;
			
				for (int j = i+1; j < words.Length && words[j] == words[i]; j++)
				{
					counts[i] += 1;
					counts[j] = 0;
				}
			}
		
			// Output to the file
			taskFiles = new string[1];	// (only one output file, but our format is an array, so a length 1 array it is)
			taskFiles[0] = node.directory + "\\" + "output"+taskNumber+".txt";
			StreamWriter writeFile = new StreamWriter(File.Create(taskFiles[0]));
		
			for (int i = 0; i < words.Length; i++)
			{
				if (counts[i] > 0)
				{
					string thisWord = words[i].Substring(0,words[i].Length-1);
					writeFile.WriteLine(thisWord + " " + counts[i]);
				}
			}
		
			taskCompleted = true;
			return;
		}
		
		Debug.Log("Sorting 5!");
		// otherwise, we're NOT done with the sorting
		// do X words per tick and then record our progress and stop
		for (int i = 0; i < 5 && sorted < words.Length; i++)
		{
			int minIndex = sorted;
			for (int j = sorted+1; j < words.Length; j++)
			{
				if (string.Compare(words[j], words[minIndex]) < 0)
					minIndex = j;
			}
			
			if (minIndex != sorted)
			{
				string tempword = words[minIndex];
				words[minIndex] = words[sorted];
				words[sorted] = tempword;
			}

			sorted += 1;
		}

	}*/
	
	
	
	
	


//
//	SIMULATION INTERACTIONS
//	(mostly dealing with importing initial values/setting up, or with running the simulation (timers/graphics)
//	I would have preferred to put these outside in Node Simulator, but unfortunately this code is worker-specific
//	

	public void setup()
	{
		node = GetComponent<NodeSimulator>();
		taskTimer = 0;
		simulatedTaskTime = UnityEngine.Random.Range(0.001f, 0.030f);	// initial task request after 1ms to 30ms
	}
	
	public Sprite crashedSprite;
	public Sprite masterSprite;
	
	public GameObject docImg;
	public GameObject docMultText;
	
	public GameObject progressText;
	
	public void advanceTimers()
	{	// Each frame, increment timers according to framerate
		if (node.crashed)
		{
			taskTimer = 0;
		}
		else if (node.paused)
		{
			// don't increment the timer if paused
		}
		else
		{
			float passedTime = Time.deltaTime / node.timeFactor;
			taskTimer += passedTime;
		}
		
		// (we may as well cheat and put the sprite-update here)
		if (node.crashed)
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = crashedSprite;
		}		
		else
		{
			gameObject.GetComponent<SpriteRenderer>().sprite = masterSprite;
		}
		
		if (taskNumber < 0)
		{
			progressText.GetComponent<TextMesh>().text = "  ";
		}
		else
		{
			float percentComplete = taskTimer/simulatedTaskTime;
			int percentage = (int)(percentComplete * 100f);
			progressText.GetComponent<TextMesh>().text = percentage + "%";	// At the moment, since I'm using the exact same code to count task simulated task completion and request repeat, this times both .. which is NOT what I want
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
