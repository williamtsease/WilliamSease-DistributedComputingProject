using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SimulatorManager : MonoBehaviour
{
	int masterCount;
	public GameObject masterPrefab;
	public GameObject[] masters;
	
	int workerCount;
	public GameObject workerPrefab;
	public GameObject[] workers;
	
	// each link connects two nodes, and we want to be able to refer to it from either end
	public GameObject linkPrefab;
	public GameObject[,] links;
	// (note link[a,b] and link[b,a] are both references to the same link object, which links a and b (if a=b, the reference is null - no self-link object exists)
	
	int mapTaskCount = 5;
	int reduceTaskCount = 10;
	
	void Start()
    {
		int totalNodeCounter = 0;	// to give each node a unique ID number
		
		// Make the Masters!
		masterCount = SetupManager.numberOfMasters;
        masters = new GameObject[masterCount];
		for (int i = 0; i < masterCount; i++)
		{
			float degrees = i*(2*Mathf.PI)/masterCount;	// space the servers around a circle (2pi/masterCount radians apart)
			float radius = 1f + (0.1f)*masterCount;		// raidus from the centre of the circle
			if (masterCount < 2)
				radius = 0;		// (if there's just one, put it in the middle)
			Vector3 newLocation = new Vector3(radius*Mathf.Cos(degrees), radius*Mathf.Sin(degrees), 0);	// (convert from radial coordinates to x/y)
			masters[i] = Instantiate(masterPrefab, newLocation, Quaternion.identity);
			
			// (set fields)
			masters[i].GetComponent<NodeSimulator>().setup(totalNodeCounter, masterCount, workerCount, mapTaskCount, reduceTaskCount, gameObject);
			totalNodeCounter ++;
		}
		
		// Make the Workers!
		workerCount = SetupManager.numberOfWorkers;
		workers = new GameObject[workerCount];
		for (int i = 0; i < workerCount; i++)
		{
			Vector3 newLocation = new Vector3(5f+(1.5f*(i/5)), 3f-(1.5f*(i%5)), 0);		// logic for placing the workers in vertical rows of 5
			workers[i] = Instantiate(workerPrefab, newLocation, Quaternion.identity);
			
			// (set fields)
			workers[i].GetComponent<NodeSimulator>().setup(totalNodeCounter, masterCount, workerCount, mapTaskCount, reduceTaskCount, gameObject);
			totalNodeCounter ++;
		}
		
		// Make links between all pairs of nodes!
		// 1. Link master to master
		links = new GameObject[masterCount+workerCount,masterCount+workerCount];
		for (int i = 0; i < masterCount; i++)
		{
			for (int j = i+1; j < masterCount; j++)
			{
				links[i,j] = Instantiate(linkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
				links[j,i] = links[i,j];
				// (set up the link with references to the two nodes)
				links[i,j].GetComponent<LinkSimulator>().setupLink(masters[i], masters[j], gameObject);
			}
		}
		// 2. Link master to worker
		for (int i = 0; i < masterCount; i++)
		{
			for (int j = 0; j < workerCount; j++)
			{
				links[i,j+masterCount] = Instantiate(linkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
				links[j+masterCount,i] = links[i,j];
				// (set up the link with references to the two nodes)
				links[i,j+masterCount].GetComponent<LinkSimulator>().setupLink(masters[i], workers[j], gameObject);
			}
		}
		// 3. Link worker to worker
		for (int i = 0; i < workerCount; i++)
		{
			for (int j = i+1; j < workerCount; j++)
			{
				links[i+masterCount,j+masterCount] = Instantiate(linkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
				links[j+masterCount,i+masterCount] = links[i,j];
				// (set up the link with references to the two nodes)
				links[i+masterCount,j+masterCount].GetComponent<LinkSimulator>().setupLink(workers[i], workers[j], gameObject);
	/*			links[i+masterCount,j+masterCount].GetComponent<LinkSimulator>().nodeA = workers[i];
				links[i+masterCount,j+masterCount].GetComponent<LinkSimulator>().nodeAindex = workers[i].GetComponent<NodeSimulator>().nodeID;
				links[i+masterCount,j+masterCount].GetComponent<LinkSimulator>().nodeB = workers[j];
				links[i+masterCount,j+masterCount].GetComponent<LinkSimulator>().nodeBindex = workers[j].GetComponent<NodeSimulator>().nodeID;
				links[i+masterCount,j+masterCount].GetComponent<LinkSimulator>().manager = gameObject;*/
			}
		}
		// All nodes need a list of links for easy reference
		for (int i = 0; i < masterCount; i++)
		{
			masters[i].GetComponent<NodeSimulator>().links = new GameObject[masterCount+workerCount];
			for (int j = 0; j < masterCount+workerCount; j++)
			{
				masters[i].GetComponent<NodeSimulator>().links[j] = links[i,j];
			}
		}
		for (int i = 0; i < workerCount; i++)
		{
			workers[i].GetComponent<NodeSimulator>().links = new GameObject[masterCount+workerCount];
			for (int j = 0; j < masterCount+workerCount; j++)
			{
				workers[i].GetComponent<NodeSimulator>().links[j] = links[i,j];
			}
		}
		
		mapTaskCount = SetupManager.mapCount;
        reduceTaskCount = SetupManager.reduceCount;
        
		// Get the starting target files
		var tempFiles = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "pg-*.txt");
		mapTaskCount = 0;
		foreach (var file in tempFiles)
			mapTaskCount += 1;
		string[] files = new string[mapTaskCount];
		int tempCounter = 0;
		foreach (var file in tempFiles)
		{
			files[tempCounter] = file;
			masters[0].GetComponent<NodeSimulator>().copyFile(files[tempCounter]);		// (copy them manually into server0, the initial master-leader)
			tempCounter += 1;
		}
		// *sigh* okay so now they're in an array isntead of an "iterator"
		// TODO pass the array into server0, which then 
		
		
    }
	
	public bool paused = false;
	public void pauseSim()
	{	// pause/unpause the simulation (pause all nodes and messages)
		if (paused)
		{
			paused = false;
			for (int i = 0; i < masterCount; i++)
			{
				masters[i].GetComponent<NodeSimulator>().paused = false;
			}
			for (int i = 0; i < workerCount; i++)
			{
				workers[i].GetComponent<NodeSimulator>().paused = false;
			}
			// I got lazy and didn't list messages, so we'll have to find them all ourselves
			GameObject[] messages = GameObject.FindGameObjectsWithTag("message");
			for (int i = 0; i < messages.Length; i++)
			{
				messages[i].GetComponent<MessageSimulator>().paused = false;
			}
		}
		else
		{
			paused = true;
			for (int i = 0; i < masterCount; i++)
			{
				masters[i].GetComponent<NodeSimulator>().paused = true;
			}
			for (int i = 0; i < workerCount; i++)
			{
				workers[i].GetComponent<NodeSimulator>().paused = true;
			}
			GameObject[] messages = GameObject.FindGameObjectsWithTag("message");
			for (int i = 0; i < messages.Length; i++)
			{
				messages[i].GetComponent<MessageSimulator>().paused = true;
			}
		}
	}
	
	//
	// FIELDS/FUNCTIONS THAT CONTROL THE SELECTING OF NODES / MESSAGES FOR CRASHING AND OTHER MANIPULATION
	//
	public GameObject selectLabel;
	public GameObject crashButton;
	public GameObject selectorVisual;
	public Sprite messageSelectSprite;
	public Sprite nodeSelectSprite;
	
	public GameObject linkLabelPrefab;
	public GameObject breakButtonPrefab;
	
	GameObject selected = null;
	
	public void selectNode(GameObject thisNode)
	{
		if (thisNode == selected)
		{
			deselect();
			return;
		}
		deselect();
		selected = thisNode;
		crashButton.GetComponent<BtnCrash>().setup(thisNode);
		crashButton.transform.position = new Vector3(-8.5f, 3.75f, 0);
		selectorVisual.GetComponent<SelectorStiky>().target = thisNode;
		selectorVisual.GetComponent<SpriteRenderer>().sprite = nodeSelectSprite;
		selectLabel.GetComponent<TextMesh>().text = "Node " + thisNode.GetComponent<NodeSimulator>().nodeID;
		int tempOffset = 0;
		for (int i = 0; i < selected.GetComponent<NodeSimulator>().links.Length; i++)
		{
			if (i == selected.GetComponent<NodeSimulator>().nodeID)
			{
				tempOffset = 1;
				continue;
			}
			GameObject tempLabel = Instantiate(linkLabelPrefab, new Vector3(-10.5f, 3.0f - (0.25f * (i-tempOffset)), 1), Quaternion.identity);
			tempLabel.GetComponent<TextMesh>().text = "Link: "+selected.GetComponent<NodeSimulator>().nodeID+" to "+i;
			GameObject tempButton = Instantiate(breakButtonPrefab, new Vector3(-8.5f, 3.0f - (0.25f * (i-tempOffset)), 1), Quaternion.identity);
			tempButton.GetComponent<BtnBreakLink>().setup(selected.GetComponent<NodeSimulator>().links[i]);
		}
	}
	
	public void selectMessage(GameObject thisMessage)
	{
		if (thisMessage == selected)
		{
			deselect();
			return;
		}
		deselect();
		selected = thisMessage;
		crashButton.GetComponent<BtnCrash>().setup(thisMessage);
		crashButton.transform.position = new Vector3(-8.5f, 3.75f, 0f);
		selectorVisual.GetComponent<SelectorStiky>().target = thisMessage;
		selectorVisual.GetComponent<SpriteRenderer>().sprite = messageSelectSprite;
		selectLabel.GetComponent<TextMesh>().text = "Message " + thisMessage.GetComponent<MessageSimulator>().fromID + " to " + thisMessage.GetComponent<MessageSimulator>().toID;
	}

    void deselect()
	{
		selected = null;
		selectLabel.GetComponent<TextMesh>().text = "";
		crashButton.transform.position = new Vector3(9999, 9999, 0);
		selectorVisual.GetComponent<SelectorStiky>().target = null;
		selectorVisual.transform.position = new Vector3(999f, 999f, 999f);
		
		GameObject[] enemies = GameObject.FindGameObjectsWithTag("selectedLink");
		foreach(GameObject thingy in enemies)
			GameObject.Destroy(thingy);
	}
	
	void Update()
	{
		if (selected == null)
			deselect();
	}
}
