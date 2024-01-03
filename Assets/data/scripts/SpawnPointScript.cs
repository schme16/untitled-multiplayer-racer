using UnityEngine;

public class SpawnPointScript : MonoBehaviour
{
	public Player assignedPlayer = null;

	public Color colour;

	// Start is called before the first frame update
	void Start()
	{
		//Set the players colour
		GetComponent<MeshRenderer>().material.color = colour;
	}

	// Update is called once per frame
	void Update() { }
}