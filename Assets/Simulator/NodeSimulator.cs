using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class NodeSimulator : MonoBehaviour
{
	public GameObject gameManager;
	
	// All nodes have a unique ID number
    public int nodeID;
	public GameObject idLabelField;
	public GameObject[] links;
	
	// For running the simulation
	public bool crashed = false;	// if set to true, this node will do nothing
	public bool paused = false;		// if set to true, this node will do nothing but will preserve its state
	
	public float timeFactor = 100f;		// (how many times slower does the simlulation run? (default x100) )
	public string directory = "";
	
	public int masterCount;
	public int workerCount;
	
	// (how many map/reduce tasks are there? this is dictated by the user/input, so it has to be managed globally by the simulator)
	public int mapCount;
	public int reduceCount;
	
	void Update()
	{	// This ought to be an asynchronous call to avoid "hitching", but I don't know what I'm doing so it isn't
		try
		{
			GetComponent<Master>().advanceTimers();
			GetComponent<Master>().update();
		} catch { }
		try
		{
			GetComponent<Worker>().advanceTimers();
			GetComponent<Worker>().update();
		} catch { }
	}
	
	public void sendMessage(int targetIndex, string tempLabel, string tempMessage)
	{	// send a message to another node
		if (crashed)
		{
			return;
		}
		if (nodeID == targetIndex)
		{
			return;
		}
		else if (nodeID < targetIndex)
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageAB(tempLabel, tempMessage);
		}
		else
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageBA(tempLabel, tempMessage);
		}
	}
	
	public void sendMessage(int targetIndex, string tempLabel, string tempMessage, string file)
	{	// send a message to another node with one attached file (overload method)
		if (crashed)
		{
			return;
		}
		if (nodeID == targetIndex)
		{
			return;
		}
		else if (nodeID < targetIndex)
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageAB(tempLabel, tempMessage, new string[] {file});
		}
		else
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageBA(tempLabel, tempMessage, new string[] {file});
		}
	}
	
	public void sendMessage(int targetIndex, string tempLabel, string tempMessage, string[] files)
	{	// send a message to another node with multiple attached files (overload method) (to be fair, it might be an array of one file - that's fine too)
		if (crashed)
			return;
		if (nodeID == targetIndex)
		{
			return;
		}
		else if (nodeID < targetIndex)
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageAB(tempLabel, tempMessage, files);
		}
		else
		{
			links[targetIndex].GetComponent<LinkSimulator>().sendMessageBA(tempLabel, tempMessage, files);
		}
	}
	
	public void receiveMessage(int fromNumber, string label, string payload, string[] files)
	{	// If the message has a set of files attached, copy them into our directory before interpreting the message (overload method)
		if (crashed)
			return;
		for (int i = 0; i < files.Length; i++)
			copyFile(files[i]);
		receiveMessage(fromNumber, label, payload);
	}

	delegate string AsyncMethodCaller(int callDuration);
	public void receiveMessage(int fromNumber, string label, string payload)
	{	// We don't know whether this node is a master or a worker, so try to pass the message along both ways
		if (crashed)
			return;
		
		doReceive(fromNumber, label, payload);
		//Task<string> tempTask = doReceive(fromNumber, label, payload);
		//await Task<int>.run(doReceive(fromNumber, label, payload));
		//tempTask.WaitAndUnwrapException();
	}
	
	async void doReceive(int fromNumber, string label, string payload)
	{	// This ought to be an asynchronous call to avoid "hitching", but I don't know what I'm doing so it doesn't
		try
		{
			GetComponent<Master>().receiveMessage(fromNumber, label, payload);
			//await Task.Run(() => GetComponent<Master>().receiveMessage(fromNumber, label, payload));
		} catch { }
		try
		{
			GetComponent<Worker>().receiveMessage(fromNumber, label, payload);
			//await Task.Run(() => GetComponent<Worker>().receiveMessage(fromNumber, label, payload));
		} catch { }
	}
	
	public void setup(int newID, int newMC, int newWC, int newMpC, int newRdC, GameObject managerInput)
	{	// Setup this Node
		nodeID = newID;
		idLabelField.GetComponent<TextMesh>().text = "" + nodeID;
		masterCount = newMC;
		workerCount = newWC;
		mapCount = newMpC;
		reduceCount = newRdC;
		try
		{
			GetComponent<Master>().setup();
		} catch { }
		try
		{
			GetComponent<Worker>().setup();
		} catch { }
		
		// Create a directory to store the files this server has duplicated locally
		directory = Directory.GetCurrentDirectory() + "\\Server" + nodeID;
		if (Directory.Exists(directory))
			Directory.Delete(directory, true);
		Directory.CreateDirectory(directory);
		
		gameManager = managerInput;
	}
	
	public void copyFile(string path)
	{	// A helper method for getting a specified file and copying it into this server's directory (used by receiveMessage() when it has files)
		string fileName = path.Split('\\')[path.Split('\\').Length-1];
		try {
			File.Delete(directory + "\\" + fileName);
		} catch { }
		File.Copy(path, directory + "\\" + fileName);
	}
	
	void OnMouseDown()
	{	// Clicking on this node selects it
		gameManager.GetComponent<SimulatorManager>().selectNode(gameObject);
	}
	
	public void setSpeed(float newSpeed)
	{	// This method is called externally by Simulator Manager whenver the speed of the simulation changes
		timeFactor = newSpeed;
		for (int i = nodeID+1; i < links.Length; i++)
		{	// Set the speed on all links that haven't already been set by a previous node (remember, links are two-ended and each connect to two nodes)
			links[i].GetComponent<LinkSimulator>().timeFactor = newSpeed;
		}
	}
}
