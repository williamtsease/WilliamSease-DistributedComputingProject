using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Worker : MonoBehaviour
{
	// NOTE: All times are 100x normal to make the simulator's actions visible, and interactions possible
	
	NodeSimulator node;	// a reference we use to access our basic node functions (send a message, etc)
	float taskTimer;
	float simulatedTaskTime;	// (for simulating how long the task takes, since we need it to run at a visible speed instead of instantly)
	bool taskCompleted = true;

	string taskType = "";
	int taskNumber = -1;
	string[] taskFiles;			// (to return the results, duplicate them back to the master)

	// Update() runs every frame, so this is our while(true) central loop that runs the logic of the node
	void Update()
	{
		advanceTimers();

		if (taskTimer > simulatedTaskTime && taskCompleted)
		{	// task is completed, request a new one
			taskTimer = 0f;
			simulatedTaskTime = 0.100f;	// wait 50 ms before we repeat our request
			
			for (int i = 0; i < node.masterCount; i++)
				node.sendMessage(i, "TASKFINISHED", taskType+"\n"+taskNumber, taskFiles);
		}
	}
	
	public void receiveMessage(int fromID, string messageType, string payload)
	{	// This method is called remotely whenever a message arrives from another node
		if (node.crashed)
			return;
	
		// MESSAGE IS A NEW TASK
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
				
				// Begin the work!
				taskCompleted = false;
				doReducing();
			}
		}
		
		// MESSAGE WAS A WAIT COMMAND
		if (messageType.StartsWith("WAIT"))
		{	// (wait 100 ms before asking again)
			taskTimer = 0;
			simulatedTaskTime = 0.100f;
			
			taskType = "";
			taskNumber = -1;
		}
		
		// MESSAGE WAS AN EXIT COMMAND, SHUT DOWN THE WORKER
		if (messageType.StartsWith("EXIT"))
		{
			var tempFiles = Directory.EnumerateFiles(node.directory, "*.txt");
			foreach (var fileName in tempFiles)
				File.Delete(fileName);
			node.crashed = true;
		}
	}
	
	void doMapping(string taskFileName)
	{
		// (create the outfiles)
		StreamWriter[] writeFile = new StreamWriter[node.reduceCount];
		taskFiles = new string[node.reduceCount];
		for (int i = 0; i < node.reduceCount; i++)
		{
			string outFileName = node.directory + "\\" + "intermediate"+i+"-"+taskNumber+".txt";
			writeFile[i] = new StreamWriter(File.Create(outFileName));
			taskFiles[i] = outFileName;
		}
		string rawText = File.ReadAllText(taskFileName).ToLower();
		// (reduce it to raw text)
		rawText = rawText.Replace("\n", " ").Replace(",", " ").Replace(".", " ").Replace("?", " ").Replace("!", " ").Replace(":", " ").Replace(";", " ").Replace("\"", " ").Replace("[", " ").Replace("]", " ").Replace("*", " ").Replace("-", " ").Replace("_", " ").Replace("(", " ").Replace(")", " ");
		while (rawText.Contains("  "))
			rawText = rawText.Replace("  ", " ");
		// (split it on the spaces)
		string[] words = rawText.Split(' ');
		simulatedTaskTime = ((words.Length/500.0f)*UnityEngine.Random.Range(0.9f, 1.1f))/1000f;	// with more information, we can give a better simulated task time of 500 words per millisecond (with slight random variation)
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
	{
		string rawText = "";
		for (int i = 0; i < node.mapCount; i++)
		{
			rawText += File.ReadAllText(node.directory + "\\intermediate" + taskNumber + "-" + i + ".txt");
		}
		// (split the text into words)
		string[] words = rawText.Split('\n');
		simulatedTaskTime = ((words.Length/300.0f)*UnityEngine.Random.Range(0.9f, 1.1f))/1000f;	// with more information, we can give a better simulated task time of 300 words per millisecond (slower than mapping) (with slight random variation)
		// (sort them)
		Array.Sort(words);
		
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
		
		taskFiles = new string[1];
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
	
	
	
	
	
	
	
	
	


//
//	SIMULATION INTERACTIONS
//	
	
	public void setup()
	{
		node = GetComponent<NodeSimulator>();
		taskTimer = 0;
		simulatedTaskTime = UnityEngine.Random.Range(0.001f, 0.030f);
	}
	
	public Sprite crashedSprite;
	public Sprite masterSprite;
	
	public GameObject docImg;
	public GameObject docMultText;
	
	void advanceTimers()
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
