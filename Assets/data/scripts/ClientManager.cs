using System.Collections.Generic;
using Mirror;

public class ClientManager : NetworkBehaviour
{
	//Enabled/disables the player inputs
	[SyncVar] public bool playersInputEnabled;

	//Indicates if the match ahs been won, and by who
	[SyncVar] public string playerFinished;

	//What state the game is in (connecting, waiting for server to start, etc)
	[SyncVar] public string state;

	//The race timer
	[SyncVar] public float timer;
}