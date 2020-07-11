using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageSimulator : MonoBehaviour
{
	int fromID;
	Vector3 fromLocation;
	GameObject toNode;
	int toID;
	Vector3 toLocation;
	
	float TRAVELTIME = 1.5f;	// (how long does it take messages to arrive in our simulation, in seconds)
	public bool paused = true;	// if set to true, the simulation is paused and this message will do nothing
	
	float timeTravelled = 0f;	// how long has this message been travelling?
	public bool dropped = false;		// has this message been dropped?
	public float latency = 0.0f;		// has this message been delayed?
	
	string label = "";
	string payload = "";		// The message being delivered - just a single string (the receiver will have to decipher it)
	
    void Update()
    {
		if (paused)
			return;
		
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
			
			toNode.GetComponent<NodeSimulator>().receiveMessage(fromID, label, payload);
			
			Destroy(gameObject);
		}
		else
		{
			timeTravelled += Time.deltaTime;
			// set position on route
			float percentTravelled = timeTravelled / TRAVELTIME;
			Vector3 thisLocation = new Vector3(fromLocation.x * (1-percentTravelled) + toLocation.x * percentTravelled, fromLocation.y * (1- percentTravelled) + toLocation.y * percentTravelled, -1);
			transform.position = thisLocation;
		}
    }
	
	public Sprite basicSprite;
	public Sprite hbSprite;
	public Sprite yesSprite;
	public Sprite noSprite;
	public Sprite querySprite;
	
	public void Setup(GameObject newFromNode, GameObject newToNode, string newType, string newPayload, float newLatency, bool newDropped)
	{
		fromID = newFromNode.GetComponent<NodeSimulator>().nodeID;
		fromLocation = newFromNode.transform.position;
		toNode = newToNode;
		toID = newToNode.GetComponent<NodeSimulator>().nodeID;
		toLocation = newToNode.transform.position;
		label = newType;
		payload = newPayload;
		latency = newLatency;
		dropped = newDropped;
		
		transform.position = fromLocation;
		
		TRAVELTIME *= Random.Range(0.85f, 1.15f);	// we want nondeterminism; so even with latency controls, randomize the message travel tim a bit

		paused = false;	// now that we're done setting up, we can tell the message to get going

		if (label.StartsWith("HEARTBEAT"))
			gameObject.GetComponent<SpriteRenderer>().sprite = hbSprite;
		else if (label.StartsWith("REQUESTVOTE"))
			gameObject.GetComponent<SpriteRenderer>().sprite = querySprite;
		else if (payload.Contains("TRUE"))
			gameObject.GetComponent<SpriteRenderer>().sprite = yesSprite;
		else if (payload.Contains("FALSE"))
			gameObject.GetComponent<SpriteRenderer>().sprite = noSprite;
		else
			gameObject.GetComponent<SpriteRenderer>().sprite = basicSprite;
		
	}
}
