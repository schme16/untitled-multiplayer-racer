using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Cinemachine;
using NativeWebSocket;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
	//This is the network manager, it handles connection, disconnection, etc
	public string serverUrl; //wss://cube-run.shanegadsby.com

	//This is the playable prefab
	public Player PlayerPrefab;

	//This is the playable prefab
	public RoomVariable lastRoom;

	//This is the winner/loser indicator prefab
	public WinLoseIndicatorScript winLoseIndicatorPrefab;

	//This is the transform where all spawn players are added
	public Transform playerHolder;

	//This is the cinemachine controller for the main camera 
	public CinemachineVirtualCamera virtualCamera;

	//List of all available spawn point objects
	public List<SpawnPointScript> SpawnPoints;


	/*         UI         */

	//Game timer group text
	public GameObject uiTimerGroup;

	//Game timer value text
	public TextMeshProUGUI uiTimerText;

	//Waiting on host text
	public TextMeshProUGUI uiWaitingOnHostText;

	//Waiting for room text
	public TextMeshProUGUI uiWaitingForRoomText;


	//Player speed meter text
	public TextMeshProUGUI uiSpeedo;

	//Start game button
	public Transform startGameButton;

	//Restart game button
	public Transform uiRestartGameButton;

	//Start hosting button
	public Transform uiHostButton;

	//Join game button
	public Transform uiJoinButton;

	//Text input for what ip to connect to
	public Transform uiHostnameInput;

	//Start a private hosting session button
	public Transform uiHostPrivateButton;

	//Join game a public button
	public Transform uiJoinPublicButton;

	//Game RoomCode group text
	public GameObject uiRoomCodeGroup;

	//Game RoomCode value text
	public TextMeshProUGUI uiRoomCodeText;

	public List<Player> PlayerList;


	//This is the websocket connection object
	private WebSocket ws;

	//This is just a flag to show if the websocket is connected
	public bool wsConnected = false;
	public bool reloadUI = false;
	public bool roomVariableChanged = false;
	public bool gameClosing = false;

	public SocketPacket localPlayer;

	public RoomVariable room;


	public struct RoomVariable
	{
		public string type;
		public string roomID;
		public string state;
		public string host;
		public bool playersInputEnabled;
		public string playerFinished;
		public float timer;
		public float countdownStarted;
	}

	public struct NewRoomPacket
	{
		public string type;
		public bool privateGame;
		public bool autoJoin;
	}

	public struct JoinRoomPacket
	{
		public string type;
		public string roomID;
	}
	public struct VariableSyncPacket
	{
		public string type;
		public string state;
		public bool playersInputEnabled;
		public float timer;
		public string playerFinished;
		public float countdownStarted;
	}

	public struct Vector3Json
	{
		public float x;
		public float y;
		public float z;
		public float w;
	}

	public struct SocketPacket
	{
		public string type;
		public string state;
		public bool playersInputEnabled;
		public string playerFinished;
		public float timer;
		public string roomID;
		public string playerID;
		public int playerIndex;
		public string newHost;
		public string name;
		public bool autoJoin;
		public bool privateGame;
		public bool isHost;
		public string message;
		public Vector3Json pos;
		public Vector3Json rot;
		public float countdownStarted;
	}

	public struct PositionSyncSocket
	{
		public string type;
		public Vector3Json pos;
		public Vector3Json rot;
	}

	// Start is called before the first frame update
	void Start()
	{
		room = new RoomVariable();
		lastRoom = new RoomVariable();

		//Set the game to the connections screen 
		StartWaitingForConnection();
	}

	// Update is called once per frame
	void Update()
	{
		#if (!UNITY_WEBGL || UNITY_EDITOR)
			if (ws != null)
			{
				ws.DispatchMessageQueue();
			}
		#endif

		Loop();
	}

	private void FixedUpdate()
	{
		SyncRoomVariables();
	}


	public void Loop()
	{
		//This is stuff that's run just once when the states change
		if ((room.state != lastRoom.state && !String.IsNullOrEmpty(room.state)) || reloadUI)
		{

			//Set the lastState variable to the current state
			lastRoom.state = room.state;

			//Signal that the room variables have changed
			roomVariableChanged = true;

			//Reset the UI
			ResetUI();

			//Loop over each state
			switch (room.state)
			{
				case "connections":

					//Start the waiting for player state
					StartWaitingForConnection();

					break;

				case "host":

					//Start hosting the game
					StartHosting();

					break;

				case "host private":

					//Start hosting a private game
					StartHosting(true);

					break;

				case "join":

					//Join a game
					JoinGame();

					break;

				case "join random":

					//Join a game
					JoinGame(true);

					break;

				case "waiting for players":

					//Start the waiting for player state
					StartWaitingOnHost(reloadUI);

					break;

				case "waiting for room":

					//Start the waiting for room state
					StartWaitingForRoom(reloadUI);

					break;

				case "countdown":

					//Start the race
					StartCountdown(reloadUI);
					break;

				case "start":

					//Start the race
					StartRace(reloadUI);
					break;

				case "racing":

					//Show the game timer ui
					uiTimerGroup.gameObject.SetActive(true);

					//Show the room code
					uiRoomCodeGroup.gameObject.SetActive(true);

					//Show the player speed ui
					uiSpeedo.gameObject.SetActive(true);
					break;

				case "finished":

					//Run the race finish function
					FinishRace(reloadUI);

					break;

				case "restart":

					//Run the race finish function
					RestartRace(false);

					break;
			}

			if (wsConnected) { }


			reloadUI = false;
		}


		//Loop over each state
		//This is run every frame
		switch (room.state)
		{
			case "connections":

				break;

			case "waiting for players":

				break;

			case "countdown":

				break;

			case "racing":

				//If no player has won
				if (string.IsNullOrEmpty(room.playerFinished))
				{
					//Increase the timer
					room.timer += Time.deltaTime;

					//Signal that the room variables have changed
					//roomVariableChanged = true;

					//Set the timer ui to the new time, with 2 decimal places
					uiTimerText.text = room.timer.ToString("F2");
				}
				else
				{
					SetState("finished");
				}

				break;

			case "finished":

				break;
		}
	}


	//Hide all UI elements
	public void ResetRoomVariables()
	{
		//Reset the timer
		room.timer = 0;

		//Disable player inputs
		room.playersInputEnabled = false;

		//Reset the playerFinished variable
		room.playerFinished = null;

		//Signal that the room variables have changed
		roomVariableChanged = true;
	}

	//Hide all UI elements
	public void ResetUI()
	{
		//
		uiWaitingOnHostText.gameObject.SetActive(false);

		//
		uiWaitingForRoomText.gameObject.SetActive(false);

		//
		startGameButton.gameObject.SetActive(false);

		//
		uiTimerGroup.gameObject.SetActive(false);

		//
		uiSpeedo.gameObject.SetActive(false);


		//
		uiHostButton.gameObject.SetActive(false);

		//
		uiJoinButton.gameObject.SetActive(false);

		//
		uiHostnameInput.gameObject.SetActive(false);

		//
		uiRestartGameButton.gameObject.SetActive(false);

		//
		uiHostPrivateButton.gameObject.SetActive(false);

		//
		uiJoinPublicButton.gameObject.SetActive(false);

		//
		uiRoomCodeGroup.gameObject.SetActive(false);
	}

	public async void StartWaitingForConnection()
	{
		if (ws != null)
		{
			await ws.Close();
			ws = null;
		}

		//Show the host button
		uiHostButton.gameObject.SetActive(true);

		//Show the join specific room button
		uiJoinButton.gameObject.SetActive(true);

		//Show host private button
		uiHostPrivateButton.gameObject.SetActive(true);

		//Show join random public private button
		uiJoinPublicButton.gameObject.SetActive(true);

		//Show the join ip input field
		uiHostnameInput.gameObject.SetActive(true);

		ResetRoomVariables();
	}

	public void StartWaitingForRoom(bool uiOnly)
	{
		//Show the "waiting for players" text
		uiWaitingForRoomText.gameObject.SetActive(true);

		ResetRoomVariables();
	}

	public void StartWaitingOnHost(bool uiOnly)
	{
		//Show the room code
		uiRoomCodeGroup.gameObject.SetActive(true);
		//Show the "waiting for players" text
		uiWaitingOnHostText.gameObject.SetActive(true);

		//If this instance is the server
		if (localPlayer.isHost)
		{
			//Show the start game button 
			startGameButton.gameObject.SetActive(true);
		}

		ResetRoomVariables();
	}

	//This starts the race countdown
	public void StartCountdown(bool uiOnly)
	{
		//Reset the room
		ResetRoomVariables();

		room.countdownStarted = Time.time * 1000;

		//Signal that the room variables have changed
		roomVariableChanged = true;
	}
	
	//This starts the race
	public void StartRace(bool uiOnly)
	{
		

		//Enable the player controls
		room.playersInputEnabled = true;

		//Enable local player's inputs
		foreach (Player player in PlayerList)
		{
			//Set their input to enabled
			player.inputEnabled = true;
		}

		SetState("racing");
	}

	//This resets the race, so that you can play again
	public void RestartRace(bool uiOnly)
	{
		room.playerFinished = null;

		//Signal that the room variables have changed
		roomVariableChanged = true;

		foreach (Player player in FindObjectsOfType<Player>())
		{
			player.ResetPlayer();
		}

		SetState("waiting for players");
	}

	//This finishes the race
	public void FinishRace(bool uiOnly)
	{
		//Is this instance is the host
		if (localPlayer.isHost)
		{
			//Show the timer ui
			uiTimerText.gameObject.SetActive(true);

			//Show the player speed meter ui 
			uiSpeedo.gameObject.SetActive(true);


			//If this instance is the server
			if (localPlayer.isHost)
			{
				//Show the restart button ui
				uiRestartGameButton.gameObject.SetActive(true);
			}
		}
	}

	WebSocket ConnectToServer()
	{
		WebSocket websocket = new WebSocket(serverUrl);

		websocket.OnOpen += () => { wsConnected = true; };

		websocket.OnError += (e) =>
		{
			Debug.LogError("Error! " + e);
		};

		websocket.OnClose += (e) =>
		{
			if (!gameClosing)
			{
				SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
			}
			wsConnected = false;
		};

		websocket.OnMessage += (bytes) =>
		{
			// getting the message as a SocketPacket struct
			SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(System.Text.Encoding.UTF8.GetString(bytes));
			switch (packet.type)
			{
				case "room-joined":
					localPlayer = packet;
					OnJoinedRoom(packet, false);

					room.roomID = packet.roomID;
					uiRoomCodeText.text = room.roomID;

					//Set the state to waiting for players
					SetState("waiting for players");
					break;

				case "player-joined":

					//SetState(packet.state);

					OnJoinedRoom(packet, true);
					break;

				case "player-left":
					Player p = SpawnPoints[packet.playerIndex].assignedPlayer.GetComponent<Player>();
					PlayerList.Remove(p);
					SpawnPoints[packet.playerIndex].assignedPlayer = null;
					Destroy(p.indicator);
					Destroy(p.gameObject);

					if (localPlayer.playerID == packet.newHost)
					{
						localPlayer.isHost = true;
						reloadUI = true;
					}

					break;

				case "sync-remote-player":
					if (SpawnPoints[packet.playerIndex].assignedPlayer != null)
					{
						Player remotePlayer = SpawnPoints[packet.playerIndex].assignedPlayer.GetComponent<Player>();
						remotePlayer.transform.position = new Vector3(packet.pos.x, packet.pos.y, packet.pos.z);
						//remotePlayer.transform.localScale = new Vector3(packet.scale.x, packet.scale.y, packet.scale.z);
						remotePlayer.transform.rotation = new Quaternion(packet.rot.x, packet.rot.y, packet.rot.z, packet.rot.w);
					}

					break;

				case "sync-variables":
					room.playersInputEnabled = packet.playersInputEnabled;
					room.timer = packet.timer;
					room.countdownStarted = packet.countdownStarted;
					SetState(packet.state, true);
					Loop();

					break;

				case "connection-error":
					Debug.Log(packet.message);
					break;
			}
		};

		websocket.Connect();

		return websocket;
	}

	//Start the hosting
	public void StartHosting(bool privateGame = false)
	{
		room = new RoomVariable();
		ws = ConnectToServer();

		ws.OnOpen += () =>
		{
			if (ws.State == WebSocketState.Open)
			{
				NewRoomPacket packet = new NewRoomPacket();
				packet.type = "new-room";
				packet.autoJoin = true;
				packet.privateGame = privateGame;
				ws.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet)));
			}
		};
	}

	//Join a game
	public void JoinGame(bool joinRandom = false)
	{
		ws = ConnectToServer();
		ws.OnOpen += () =>
		{
			if (ws.State == WebSocketState.Open)
			{
				JoinRoomPacket packet = new JoinRoomPacket();
				packet.type = "join-room";
				packet.roomID = joinRandom ? "matchmaking" : uiHostnameInput.GetComponentInChildren<TMP_InputField>().text;

				ws.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet)));
			}

			//Set the state to waiting for players
			if (joinRandom)
			{
				SetState("waiting for room");
			}
			else
			{
				SetState("waiting for players");
			}
		};
	}

	private async void OnApplicationQuit()
	{
		if (ws != null)
		{
			gameClosing = true;
			await ws.Close();
		}
	}

	public void OnJoinedRoom(SocketPacket player, bool remote)
	{
		//Sync the room variables up front
		if (!remote)
		{
			room.playersInputEnabled = player.playersInputEnabled;
			room.timer = player.timer;
			room.countdownStarted = player.countdownStarted;
			room.playerFinished = player.playerFinished;

			//Signal that the room variables have changed
			roomVariableChanged = true;
		}

		//If the total players are less than the max players (as defined by the number of spawn points)
		if (player.playerIndex > -1)
		{
			//Try and fetch the players spawn point spawn point
			SpawnPointScript spawnPoint = SpawnPoints[player.playerIndex];

			//Create the new player, and add it the the players connection as their player object
			Player newPlayer = Instantiate(PlayerPrefab).GetComponent<Player>();

			//Set the players id
			newPlayer.playerObjectID = $"Player: {player.playerID}";


			//Assign the spawn point to a player
			newPlayer.spawnPoint = spawnPoint.transform;

			//Assign the player to a spawn point 
			spawnPoint.assignedPlayer = newPlayer;

			//Flag if this is the local player
			newPlayer.isLocalPlayer = player.playerID == localPlayer.playerID;

			//Add the new player to the player holder
			newPlayer.manager = this;

			//Set the rigidbody variable
			newPlayer.rb = newPlayer.GetComponent<Rigidbody>();

			if (remote)
			{
				//Place the player at their spawn points position
				newPlayer.transform.position = spawnPoint.transform.position;

				//Reset the position, rotation, and velocity values
				newPlayer.transform.rotation = new Quaternion(player.rot.x, player.rot.y, player.rot.z, player.rot.w);

				newPlayer.rb.isKinematic = true;
			}

			else
			{
				//Place the player at their spawn points position
				newPlayer.transform.position = spawnPoint.transform.position;


				//Reset the position, rotation, and velocity values
				newPlayer.transform.rotation = Quaternion.identity;
				newPlayer.rb.velocity = Vector3.zero;
				newPlayer.rb.angularVelocity = Vector3.zero;
			}

			//Add the new player to the player holder
			newPlayer.transform.parent = playerHolder;

			//Set the main camera
			newPlayer.cam = Camera.main;

			//Set the spawn point script
			newPlayer.spawnPointScript = spawnPoint.GetComponent<SpawnPointScript>();

			//Set the players colour
			newPlayer.colour = spawnPoint.GetComponent<MeshRenderer>().material.color;

			//Add a win/lose indicator for the player
			newPlayer.indicator = Instantiate(winLoseIndicatorPrefab).GetComponent<WinLoseIndicatorScript>();

			//Set the follow target for the win/lose indicator to this player
			newPlayer.indicator.GetComponent<FollowTarget>().target = newPlayer.transform;

			//Sync the input enabled status from the client manager
			newPlayer.inputEnabled = room.playersInputEnabled;

			//Get the meshes renderer
			MeshRenderer mesh = newPlayer.GetComponent<MeshRenderer>();

			//Instance the material so that it can be individually altered
			Material mat = new Material(mesh.material);

			//Set the material colour, but invisible
			mat.SetColor("_BaseColor", newPlayer.spawnPointScript.colour);

			//Assign the instanced material
			mesh.material = mat;

			PlayerList.Add(newPlayer);
		}
	}

	//Syncs the local player back to the server
	public void SyncLocalPlayer(Transform trans, Rigidbody rb, bool positionOnly)
	{
		if (wsConnected)
		{
			PositionSyncSocket packet = new PositionSyncSocket();
			packet.type = "sync-player";

			packet.pos.x = (Mathf.Round(trans.position.x * 1000)) / 1000;
			packet.pos.y = (Mathf.Round(trans.position.y * 1000)) / 1000;
			packet.pos.z = (Mathf.Round(trans.position.z * 1000)) / 1000;

			packet.rot.x = (Mathf.Round(trans.rotation.x * 1000)) / 1000;
			packet.rot.y = (Mathf.Round(trans.rotation.y * 1000)) / 1000;
			packet.rot.z = (Mathf.Round(trans.rotation.z * 1000)) / 1000;
			packet.rot.w = (Mathf.Round(trans.rotation.w * 1000)) / 1000;


			ws.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet)));
		}
	}

	public void SyncRoomVariables()
	{
		if (wsConnected && localPlayer.isHost)
		{
			if (roomVariableChanged)
			{
				VariableSyncPacket packet = new VariableSyncPacket();
				packet.type = "sync-variables";
				packet.state = room.state;
				packet.playersInputEnabled = room.playersInputEnabled;
				packet.timer = room.timer;
				packet.playerFinished = room.playerFinished;
				packet.countdownStarted = room.countdownStarted;
				
				ws.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet)));

			}

			roomVariableChanged = false;
		}
	}

	public void SetState(string newState)
	{
		SetState(newState, false);
	}

	public void SetState(string newState, bool skipSync)
	{
		if (!String.IsNullOrEmpty(newState))
		{
			room.state = newState;

			if (!skipSync)
			{
				//Signal that the room variables have changed
				roomVariableChanged = true;
				SyncRoomVariables();
			}
		}
	}

	public void PlayerEnteredFinishZone(Player player)
	{
		if (room.playerFinished == null && player.inputEnabled)
		{
			room.playerFinished = player.playerObjectID;

			//Signal that the room variables have changed
			roomVariableChanged = true;
		}

		player.inputEnabled = false;
	}
}