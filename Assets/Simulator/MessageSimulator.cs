using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageSimulator : MonoBehaviour
{
	public GameObject gameManager;
	
	public int fromID;
	Vector3 fromLocation;
	GameObject toNode;
	public int toID;
	Vector3 toLocation;
	
	public float timeFactor = 100f;		// (how many times slower does the simlulation run? (default x100) )
	float TRAVELTIME = 0.015f;	// (how long does it take messages to arrive in our simulation, in seconds) (so 15ms = 0.015 seconds, btw)
	public bool paused = true;	// if set to true, the simulation is paused and this message will do nothing
	
	float timeTravelled = 0f;	// how long has this message been travelling?
	public bool dropped = false;		// has this message been dropped?
	public float latency = 0.0f;		// has this message been delayed?
	
	string label = "";
	string payload = "";		// The message being delivered - just a single string (the receiver will have to decipher it)
	string[] files;				// any files that are being copied
	
    void Update()
    {
		if (timeTravelled > TRAVELTIME/2f && dropped)
		{	// let the message get halfway (visually) before dropping it
			Destroy(gameObject);
		}
		else if (timeTravelled > TRAVELTIME/2f && latency > 0.0)
		{	// let the message get halfway (visually) before delaying it
			latency -= Time.deltaTime;
			// set position to middle of route
		}
		else if (timeTravelled > TRAVELTIME)
		{	// Message has arrived! Pass payload to this node, and remove this message from the network
			
			if (files == null)
				toNode.GetComponent<NodeSimulator>().receiveMessage(fromID, label, payload);
			else
				toNode.GetComponent<NodeSimulator>().receiveMessage(fromID, label, payload, files);
			
			Destroy(gameObject);
		}
		else if (paused)
		{
			return;
		}
		else
		{
			timeTravelled += Time.deltaTime / timeFactor;
			// set position on route
			float percentTravelled = timeTravelled / TRAVELTIME;
			Vector3 thisLocation = new Vector3(fromLocation.x * (1-percentTravelled) + toLocation.x * percentTravelled, fromLocation.y * (1- percentTravelled) + toLocation.y * percentTravelled, -1);
			transform.position = thisLocation;
		}
		if (timeFactor < 8)
		{
			gameObject.GetComponent<SpriteRenderer>().color = Color.clear;
			fileImage.GetComponent<SpriteRenderer>().color = Color.clear;
			fileImageText.GetComponent<TextMesh>().text = "";
		}
		else
		{
			gameObject.GetComponent<SpriteRenderer>().color = Color.white;
			// if someone turns the speed up absurdly fast and turns it down before a message arrives, the "file" image will be missing; that's fine
		}
    }
	
	public Sprite basicSprite;
	public Sprite hbSprite;
	public Sprite yesSprite;
	public Sprite noSprite;
	public Sprite querySprite;
	
	public GameObject fileImage;
	public GameObject fileImageText;
	
	public void Setup(GameObject newFromNode, GameObject newToNode, string newType, string newPayload, string[] newFiles, float newLatency, bool newDropped, GameObject managerInput, float newTimeFactor)
	{
		fromID = newFromNode.GetComponent<NodeSimulator>().nodeID;
		fromLocation = newFromNode.transform.position;
		toNode = newToNode;
		toID = newToNode.GetComponent<NodeSimulator>().nodeID;
		toLocation = newToNode.transform.position;
		label = newType;
		payload = newPayload;
		files = newFiles;
		latency = newLatency;
		dropped = newDropped;
		timeFactor = newTimeFactor;
		if (newFromNode.GetComponent<NodeSimulator>().paused)
			paused = true;
		if (newToNode.GetComponent<NodeSimulator>().paused)
			paused = true;
		
		transform.position = fromLocation;
		
		TRAVELTIME *= Random.Range(0.85f, 1.15f);	// we want nondeterminism; so even with latency controls, randomize the message travel time +/-15% 
		
		paused = false;	// now that we're done setting up, we can tell the message to get going

		if (label.StartsWith("HEARTBEAT"))
			gameObject.GetComponent<SpriteRenderer>().sprite = hbSprite;
		else if (label.StartsWith("REQUESTVOTE") || label.StartsWith("TASKFINISHED") || label.StartsWith("REQUESTFILES"))
			gameObject.GetComponent<SpriteRenderer>().sprite = querySprite;
		else if (payload.Contains("TRUE") || label.StartsWith("GIVETASK"))
			gameObject.GetComponent<SpriteRenderer>().sprite = yesSprite;
		else if (payload.Contains("FALSE") || label.StartsWith("WAIT"))
			gameObject.GetComponent<SpriteRenderer>().sprite = noSprite;
		else
			gameObject.GetComponent<SpriteRenderer>().sprite = basicSprite;
		
		if (files == null)
		{
			fileImage.GetComponent<SpriteRenderer>().color = Color.clear;
			fileImageText.GetComponent<TextMesh>().text = "";
		}
		else
		{
			fileImage.GetComponent<SpriteRenderer>().color = Color.white;
			fileImageText.GetComponent<TextMesh>().text = "x" + files.Length;
		}
		
		gameManager = managerInput;
		
	}
	
	void OnMouseDown()
	{
		gameManager.GetComponent<SimulatorManager>().selectMessage(gameObject);
	}
}
