using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SetupManager : MonoBehaviour
{
	public Slider masterCountSlider;
	public Slider workerCountSlider;

    public static int numberOfMasters;
	public static int numberOfWorkers;
	
	public Slider reduceCountSlider;
	
	public static int mapCount;	// = number of files in the directory, so set automatically
	public static int reduceCount;
	
	void Start()
	{
		numberOfMasters = 3;
		numberOfWorkers = 5;
		
		mapCount = 5;	// TODO
		reduceCount = 10;
		
		// Delete all directories left over from the last run
		for (int i = 0; i < 999; i++)
		{
			string directory = Directory.GetCurrentDirectory() + "\\Server" + i;
			if (Directory.Exists(directory))
				Directory.Delete(directory, true);
			else
				break;
		}
	}
	
	public void setMasterCount()
    {
		numberOfMasters = (int)masterCountSlider.value;
    }
	
	public void setWorkerCount()
    {
		numberOfWorkers = (int)workerCountSlider.value;
    }
	
	public void setReduceCount()
	{
		reduceCount = (int)reduceCountSlider.value;
	}
	
	public void beginSimulation()
	{
		SceneManager.LoadScene(1);
	}
}
