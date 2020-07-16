using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

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
	
	public void sendMessage(int targetIndex, string tempLabel, string tempMessage)
	{	// send a message to another node
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
	
	public void receiveMessage(int fromNumber, string label, string payload)
	{	// We don't know whether this node is a master or a worker, so try to pass the message along both ways
		try
		{
			GetComponent<Master>().receiveMessage(fromNumber, label, payload);
		} catch { }
		try
		{
			GetComponent<Worker>().receiveMessage(fromNumber, label, payload);
		} catch { }
	}
	
	public void receiveMessage(int fromNumber, string label, string payload, string[] files)
	{	// If the message has a set of files attached, copy them into our directory before interpreting the message
		for (int i = 0; i < files.Length; i++)
			copyFile(files[i]);
		receiveMessage(fromNumber, label, payload);
	}
	
	public void setup(int newID, int newMC, int newWC, int newMpC, int newRdC, GameObject managerInput)
	{
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
	
	// A helper method for getting a specified file and copying it into this server's directory
	public void copyFile(string path)
	{
		string fileName = path.Split('\\')[path.Split('\\').Length-1];
		Debug.Log(fileName);
		File.Copy(path, directory + "\\" + fileName);
	}
	
	void OnMouseDown()
	{
		gameManager.GetComponent<SimulatorManager>().selectNode(gameObject);
	}
}
