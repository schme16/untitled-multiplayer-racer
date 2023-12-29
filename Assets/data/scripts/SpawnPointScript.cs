
using Mirror;
using UnityEngine;

public class SpawnPointScript : NetworkBehaviour
{
    [SyncVar] public Transform assignedPlayer = null;

    [SyncVar] public Color colour;
    
    // Start is called before the first frame update
    void Start()
    {
		//Set the players colour
		GetComponent<MeshRenderer>().material.color = colour;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
