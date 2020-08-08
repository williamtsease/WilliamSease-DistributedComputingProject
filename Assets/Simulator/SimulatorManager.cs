using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class SimulatorManager : MonoBehaviour
{
	int masterCount;
	public GameObject masterPrefab;
	int workerCount;
	public GameObject workerPrefab;
	public GameObject[] nodes;
		
	// each link connects two nodes, and we want to be able to refer to it from either end
	public GameObject linkPrefab;
	public GameObject[,] links;
	// (note link[a,b] and link[b,a] are both references to the same link object, which links a and b (if a=b, the reference is null - no self-link object exists)
	
	int mapTaskCount = 5;
	int reduceTaskCount = 10;
	
	public float timeFactor = 100f;		// (how many times slower than real time does the simlulation run? (default x100) )
	
	void Start()
    {
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
			tempCounter += 1;
		}
		// *sigh* okay so now they're in an array isntead of an "iterator"
		
		masterCount = SetupManager.numberOfMasters;
        workerCount = SetupManager.numberOfWorkers;
		reduceTaskCount = SetupManager.reduceCount;
		
		nodes = new GameObject[masterCount+workerCount];
		// Make the Masters!
		for (int i = 0; i < masterCount; i++)
		{
			float degrees = i*(2*Mathf.PI)/masterCount;	// space the servers around a circle (2pi/masterCount radians apart)
			float radius = 1f + (0.1f)*masterCount;		// raidus from the centre of the circle
			if (masterCount < 2)
				radius = 0;		// (if there's just one, put it in the middle)
			Vector3 newLocation = new Vector3(radius*Mathf.Cos(degrees), radius*Mathf.Sin(degrees), 0);	// (convert from radial coordinates to x/y)
			nodes[i] = Instantiate(masterPrefab, newLocation, Quaternion.identity);
			
			// (set fields)
			nodes[i].GetComponent<NodeSimulator>().setup(i, masterCount, workerCount, mapTaskCount, reduceTaskCount, gameObject);
		}
		
		// Make the Workers!
		for (int i = masterCount; i < masterCount + workerCount; i++)
		{
			Vector3 newLocation = new Vector3(5f+(1.5f*((i-masterCount)/5)), 3f-(1.5f*((i-masterCount)%5)), 0);		// logic for placing the workers in vertical rows of 5
			nodes[i] = Instantiate(workerPrefab, newLocation, Quaternion.identity);
			
			// (set fields)
			nodes[i].GetComponent<NodeSimulator>().setup(i, masterCount, workerCount, mapTaskCount, reduceTaskCount, gameObject);
		}
		
		// Make links between all pairs of nodes!
		// (note - we are linking self-to-self because we don't want a self-message to be an undefined crash)
		links = new GameObject[masterCount+workerCount,masterCount+workerCount];
		for (int i = 0; i < masterCount+workerCount; i++)
		{
			for (int j = i; j < masterCount+workerCount; j++)
			{
				links[i,j] = Instantiate(linkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
				links[j,i] = links[i,j];
				// (set up the link with references to the two nodes)
				links[i,j].GetComponent<LinkSimulator>().setupLink(nodes[i], nodes[j], gameObject);
			}
		}
		// All nodes need a list of links for easy reference
		for (int i = 0; i < masterCount+workerCount; i++)
		{
			nodes[i].GetComponent<NodeSimulator>().links = new GameObject[masterCount+workerCount];
			for (int j = 0; j < masterCount+workerCount; j++)
			{
				nodes[i].GetComponent<NodeSimulator>().links[j] = links[i,j];
			}
		}
		
		mapTaskCount = SetupManager.mapCount;
        reduceTaskCount = SetupManager.reduceCount;
        
		for (int i = 0; i < files.Length; i++)
		{
			nodes[0].GetComponent<NodeSimulator>().copyFile(files[i]);		// (copy the files manually into server0, the initial master-leader)
		}
		nodes[0].GetComponent<Master>().setFiles();	// load the files properly
    }
	
	public bool paused = false;
	public void pauseSim()
	{	// pause/unpause the simulation (pause all nodes and messages)
		if (paused)
		{
			paused = false;
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i].GetComponent<NodeSimulator>().paused = false;
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
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i].GetComponent<NodeSimulator>().paused = true;
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
	
	public GameObject timeText;
	float msPassed = 0;
	void Update()
	{
		if (selected == null)
			deselect();
		
		if (msPassed < 10)
			timeText.GetComponent<TextMesh>().text = (int)(msPassed*1000f) + "ms";
		else
			timeText.GetComponent<TextMesh>().text = ((float)(int)(msPassed*10f))/10f + "s";
		
		if (!paused)
			msPassed += Time.deltaTime / timeFactor;
	}
	
	public Slider speedSetter;
	public Text speedWarning;
	public void setSpeed()
	{	// set the speed factor of the simulation
		float newSpeed = speedSetter.value;
		timeFactor = newSpeed;
		for (int i = 0; i < nodes.Length; i++)
			nodes[i].GetComponent<NodeSimulator>().setSpeed(newSpeed);
		// I got lazy and didn't list messages, so we'll have to find them all ourselves
		GameObject[] messages = GameObject.FindGameObjectsWithTag("message");
		for (int i = 0; i < messages.Length; i++)
			messages[i].GetComponent<MessageSimulator>().timeFactor = newSpeed;
		
		if (timeFactor < 8)
			speedWarning.text = "(Messages are invisible at this speed)";
		else
			speedWarning.text = "";
	}
}
