using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SetupManager : MonoBehaviour
{
	public Slider masterCountSlider;
	public Slider workerCountSlider;

    public static int numberOfMasters;
	public static int numberOfWorkers;
	
	void Start()
	{
		numberOfMasters = 5;
		numberOfWorkers = 8;
	}
	
	public void setMasterCount()
    {
		numberOfMasters = (int)masterCountSlider.value;
    }
	
	public void setWorkerCount()
    {
		numberOfWorkers = (int)workerCountSlider.value;
    }
	
	public void beginSimulation()
	{
		SceneManager.LoadScene(1);
	}
}
