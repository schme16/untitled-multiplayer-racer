using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Cinemachine;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using NativeWebSocket;


public class GameManager : MonoBehaviour
{
	[DllImport("__Internal")]
	static extern void passCopyToBrowser(string str);


	//This is the network manager, it handles connection, disconnection, etc
	public string serverUrl; //wss://cube-run.shanegadsby.com

	//This is the playable prefab
	public Player PlayerPrefab;

	//This is the playable prefab
	public string lastState;

	//This is the amount of time to count in before a game starts
	public long countIn = 3000;
	public long serverTime = 0;
	public bool useWebsockets = false;

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

	//Waiting on host text
	public TextMeshProUGUI uiConnectingToServerText;

	//Waiting for room text
	public TextMeshProUGUI uiWaitingForRoomText;

	//Round countdown timer text
	public TextMeshProUGUI uiCountdownTimer;

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

	//The gameplay controls
	public Transform uiControlsButton;

	//The gameplay controls
	public Transform uiControls;

	//The back button on the gameplay controls screen
	public Transform uiBackToConnections;

	//The back button on the gameplay controls screen
	public Volume postProcessing;

	public UnityEngine.Rendering.Universal.ChromaticAberration chromaticAberration;

	//Holds a list of all players
	public List<Player> PlayerList;

	//This is just a flag to show if the websocket is connected
	public bool wsConnected = false;
	public bool reloadUI = false;
	public bool roomVariableChanged = false;
	public bool gameClosing = false;

	public SocketPacket localPlayer;

	private WebSocket ws;

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
		public long countdownStarted;
		public long countdownFinished;
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

		public long serverTime;
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
		public long countdownStarted;
		public long countdownFinished;
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
		public long diff;
		public long clientTimestamp;
		public long serverTimestamp;
	}

	public struct TimeSync
	{
		public string type;
		public string roomID;
	}

	// Start is called before the first frame update
	void Start()
	{
		useWebsockets = Application.isEditor;

		postProcessing.profile.TryGet<UnityEngine.Rendering.Universal.ChromaticAberration>(out chromaticAberration);

		room = new RoomVariable();

		//Set the game to the title screen
		SetState("connections");
		initWebRTCComms(serverDisconnected, RoomJoined, RoomSync, PlayerJoined, PlayerSync, PlayerLeft, ConnectingToServer);
	}

	// Update is called once per frame
	void Update()
	{
		if (useWebsockets)
		{
			if (ws != null)
			{
				ws.DispatchMessageQueue();
			}
		}

		Loop();
	}

	WebSocket ConnectToWebSocketServer(bool host, string roomID, bool privateGame)
	{
		WebSocket websocket = new WebSocket(serverUrl);

		websocket.OnOpen += () =>
		{
			wsConnected = true;
			if (host)
			{
				NewRoomPacket packet = new NewRoomPacket();
				packet.type = "room-new";
				packet.autoJoin = true;
				packet.privateGame = privateGame;
				ws.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet)));
			}
			else if (!string.IsNullOrEmpty(roomID))
			{
				JoinRoomPacket packet = new JoinRoomPacket();
				packet.type = "room-join";
				packet.roomID = roomID;
				ws.Send(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet)));
			}
		};

		websocket.OnError += (e) => { Debug.LogError("Error! " + e); };

		websocket.OnClose += (e) =>
		{
			wsConnected = false;
			DisconnectFromServerMeta();
		};

		websocket.OnMessage += (bytes) =>
		{
			string data = System.Text.Encoding.UTF8.GetString(bytes);
			//Getting the message as a SocketPacket struct
			SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(data);
			switch (packet.type)
			{
				case "room-joined":
					//localPlayer = packet;

					RoomJoined(data);
					break;

				case "player-joined":
					PlayerJoined(data);
					break;

				case "player-left":
					PlayerLeft(data);
					break;

				case "player-sync":
					PlayerSync(data);
					break;

				case "room-sync":
					RoomSync(data);
					break;

				case "time-sync":
					TimeSync(data);
					Debug.Log(packet.message);
					break;

				case "connection-error":
					Debug.Log(packet.message);
					break;
			}
		};

		websocket.Connect();

		return websocket;
	}


	private void FixedUpdate()
	{
		SyncRoomVariables();
	}

	public void Loop()
	{
		if (serverTime > 0)
		{
			serverTime += Convert.ToInt64(Time.deltaTime * 1000);
		}

		//This is stuff that's run just once when the states change
		if ((room.state != lastState && !String.IsNullOrEmpty(room.state)) || reloadUI)
		{
			//Set the lastState variable to the current state
			lastState = room.state;

			//Signal that the room variables have changed
			roomVariableChanged = true;

			//Reset the UI
			ResetUI();

			//Loop over each state
			switch (room.state)
			{
				case "controls":

					//Start the waiting for player state
					StartControlsScreen();

					break;

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

				case "connecting to server":

					//Start the waiting for player state
					StartConnectingToServer(reloadUI);

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
					uiCountdownTimer.gameObject.SetActive(true);

					//Show the game timer ui
					uiTimerGroup.gameObject.SetActive(true);

					//Show the room code
					uiRoomCodeGroup.gameObject.SetActive(true);

					//Show the player speed ui
					uiSpeedo.gameObject.SetActive(true);
					break;

				case "finished":
					//Show the game timer ui
					uiTimerGroup.gameObject.SetActive(true);

					//Show the room code
					uiRoomCodeGroup.gameObject.SetActive(true);

					//Show the player speed ui
					uiSpeedo.gameObject.SetActive(true);
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

				long countdown = countIn - (serverTime - room.countdownStarted);


				uiCountdownTimer.text = ((countdown / 1000) + 1).ToString();

				if (countdown <= 0)
				{
					uiCountdownTimer.text = "Go!";
					if (localPlayer.isHost)
					{
						SetState("start");
					}
				}

				break;

			case "racing":


				//If no player has won
				if (string.IsNullOrEmpty(room.playerFinished))
				{
					countdown = countIn - (serverTime - room.countdownStarted);

					uiCountdownTimer.text = ((countdown / 1000) + 1).ToString();

					if (countdown <= 0)
					{
						uiCountdownTimer.text = "Go!";
						uiCountdownTimer.alpha = Mathf.Clamp(uiCountdownTimer.alpha - Time.deltaTime, 0, 1);
					}


					if (room.countdownStarted > 0)
					{
						long offset = room.countdownFinished > 0 ? room.countdownFinished : serverTime;
						//Set the timer ui to the new time, with 2 decimal places
						uiTimerText.text = (((float)(offset - (room.countdownStarted + countIn)) / 1000)).ToString("F2");
					}
					else
					{
						uiTimerText.text = "";
					}
				}
				else
				{
					long _offset = room.countdownFinished > 0 ? room.countdownFinished : serverTime;
					uiTimerText.text = (((float)(_offset - (room.countdownStarted + countIn)) / 1000)).ToString("F2");

					SetState("finished");
				}

				break;

			case "finished":
				long __offset = room.countdownFinished > 0 ? room.countdownFinished : serverTime;
				uiTimerText.text = (((float)(__offset - (room.countdownStarted + countIn)) / 1000)).ToString("F2");
				break;
		}
	}

	//Hide all UI elements
	public void ResetRoomVariables()
	{
		//Reset the timers
		room.countdownStarted = 0;

		room.countdownFinished = 0;

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
		uiCountdownTimer.gameObject.SetActive(false);

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

		//
		uiControlsButton.gameObject.SetActive(false);

		//
		uiControls.gameObject.SetActive(false);

		//
		uiBackToConnections.gameObject.SetActive(false);

		//
		uiConnectingToServerText.gameObject.SetActive(false);
	}

	public void StartWaitingForConnection()
	{
		ResetRoomVariables();
		DisconnectFromServerMeta();

		//Show the controls button
		uiControlsButton.gameObject.SetActive(true);

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
	}

	public void StartControlsScreen()
	{
		ResetRoomVariables();

		DisconnectFromServerJS();
		uiControls.gameObject.SetActive(true);
		uiBackToConnections.gameObject.SetActive(true);
	}

	public void StartWaitingForRoom(bool uiOnly)
	{
		//Show the "waiting for players" text
		uiWaitingForRoomText.gameObject.SetActive(true);
	}

	public void StartConnectingToServer(bool uiOnly)
	{
		//Show the "waiting for players" text
		uiConnectingToServerText.gameObject.SetActive(true);
	}

	public void StartWaitingOnHost(bool uiOnly)
	{
		//Show the room code
		if (String.IsNullOrEmpty(room.roomID) == false)
		{
			uiRoomCodeGroup.gameObject.SetActive(true);
		}

		//Show the "waiting for players" text
		uiWaitingOnHostText.gameObject.SetActive(true);

		//If this instance is the server
		if (localPlayer.isHost)
		{
			//Show the start game button 
			startGameButton.gameObject.SetActive(true);
		}
	}

	//This starts the race countdown
	public void StartCountdown(bool uiOnly)
	{
		room.countdownStarted = serverTime;

		//Reset the room
		if (localPlayer.isHost)
		{
			//Signal that the room variables have changed
			roomVariableChanged = true;
		}


		uiCountdownTimer.text = (countIn / 1000).ToString();

		uiCountdownTimer.gameObject.SetActive(true);

		uiRoomCodeGroup.gameObject.SetActive(true);

		uiCountdownTimer.alpha = 1;
	}

	//This starts the race
	public void StartRace(bool uiOnly)
	{
		//Enable the player controls
		room.playersInputEnabled = true;

		uiCountdownTimer.gameObject.SetActive(true);

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

		room.countdownFinished = 0;

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

	//Start the hosting
	public void StartHosting(bool privateGame = false)
	{
		room = new RoomVariable();
		CreateRoomMeta(privateGame);
	}

	//Join a game
	public void JoinGame(bool joinRandom = false)
	{
		JoinRoomJS(joinRandom ? "matchmaking" : uiHostnameInput.GetComponentInChildren<TMP_InputField>().text);
		/*if (joinRandom)
		{
			SetState("waiting for room");
		}
		else
		{
			SetState("waiting for players");
		}*/
	}

	public void OnJoinedRoom(SocketPacket player, bool remote)
	{
		//Sync the room variables up front
		if (remote == false)
		{
			room.playersInputEnabled = player.playersInputEnabled;
			room.countdownStarted = player.countdownStarted;
			room.countdownFinished = player.countdownFinished;
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
		Vector3 pos = trans.position;
		Quaternion rot = trans.rotation;

		pos.x = (Mathf.Round(pos.x * 1000)) / 1000;
		pos.y = (Mathf.Round(pos.y * 1000)) / 1000;
		pos.z = (Mathf.Round(pos.z * 1000)) / 1000;

		rot.x = (Mathf.Round(rot.x * 1000)) / 1000;
		rot.y = (Mathf.Round(rot.y * 1000)) / 1000;
		rot.z = (Mathf.Round(rot.z * 1000)) / 1000;
		rot.w = (Mathf.Round(rot.w * 1000)) / 1000;


		SyncPlayerPositionJS(pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w);
	}

	public void SyncRoomVariables()
	{
		if (localPlayer.isHost && roomVariableChanged)
		{
			SyncRoomVariablesMeta(room.state, room.playersInputEnabled, room.playerFinished, room.countdownStarted.ToString(), room.countdownFinished.ToString());
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

	public void CopyRoomCode()
	{
		GUIUtility.systemCopyBuffer = room.roomID;
		passCopyToBrowser(GUIUtility.systemCopyBuffer);

		Debug.Log(GUIUtility.systemCopyBuffer);
	}

	public void PlayerEnteredFinishZone(Player player)
	{
		if (room.playerFinished == null && player.inputEnabled)
		{
			if (localPlayer.isHost)
			{
				room.playerFinished = player.playerObjectID;

				room.countdownFinished = serverTime;

				//Signal that the room variables have changed
				roomVariableChanged = true;
			}
		}

		player.inputEnabled = false;
	}

	private async void OnApplicationQuit()
	{
		gameClosing = true;
		DisconnectFromServerMeta();
	}


	/*This is all the network stuff, should be rearranged when finalized*/

	[DllImport("__Internal")]
	public static extern void initWebRTCComms(Action<string> serverDisconnected, Action<string> roomJoined, Action<string> roomSync, Action<string> playerJoined, Action<string> playerSync, Action<string> playerLeft, Action<int> ConnectingToServer);

	[DllImport("__Internal")]
	public static extern void SyncPlayerPositionJS(float x, float y, float z, float rX, float rY, float rZ, float rW);

	[DllImport("__Internal")]
	public static extern void SyncRoomVariablesJS(string state, bool playersInputEnabled, string playerFinished, string countdownStarted, string countdownFinished);

	[DllImport("__Internal")]
	public static extern void CreateRoomJS(bool privateGame);

	[DllImport("__Internal")]
	public static extern void DisconnectFromServerJS();

	[DllImport("__Internal")]
	public static extern void JoinRoomJS(string roomID);

	public void CreateRoomWebSocket()
	{
		ConnectToWebSocketServer();
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void serverDisconnected()
	{
		serverDisconnected(" ");
	}


	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void serverDisconnected(string data)
	{
		GameManager manager = FindObjectOfType<GameManager>();
		if (!manager.gameClosing)
		{
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void RoomJoined(string data)
	{
		SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(data);
		GameManager manager = FindObjectOfType<GameManager>();

		manager.serverTime = packet.serverTime;

		Debug.Log(111111);
		Debug.Log(manager.serverTime);

		manager.localPlayer = packet;
		manager.room.roomID = packet.roomID;
		manager.room.countdownStarted = packet.countdownStarted;
		manager.room.countdownFinished = packet.countdownFinished;
		manager.uiRoomCodeText.text = manager.room.roomID;

		manager.OnJoinedRoom(packet, false);

		//Set the state to waiting for players
		manager.SetState(packet.state ?? "waiting for players");

		manager.reloadUI = true;
		manager.Loop();
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void RoomSync(string data)
	{
		SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(data);
		GameManager manager = FindObjectOfType<GameManager>();

		manager.room.playersInputEnabled = packet.playersInputEnabled;
		manager.room.countdownStarted = packet.countdownStarted;
		manager.room.countdownFinished = packet.countdownFinished;
		manager.room.playerFinished = packet.playerFinished;

		Debug.Log(22222);
		Debug.Log(packet.state);
		Debug.Log(packet.countdownStarted);
		manager.SetState(packet.state, true);

		manager.reloadUI = true;
		manager.Loop();
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void PlayerJoined(string data)
	{
		GameManager manager = FindObjectOfType<GameManager>();
		SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(data);
		//Debug.Log($"Remote player joined (unity side)");
		manager.OnJoinedRoom(packet, true);
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void PlayerSync(string data)
	{
		SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(data);
		GameManager manager = FindObjectOfType<GameManager>();
		if (packet.playerIndex != manager.localPlayer.playerIndex && manager.SpawnPoints[packet.playerIndex].assignedPlayer != null)
		{
			Player remotePlayer = manager.SpawnPoints[packet.playerIndex].assignedPlayer.GetComponent<Player>();
			remotePlayer.transform.position = new Vector3(packet.pos.x, packet.pos.y, packet.pos.z);
			remotePlayer.transform.rotation = new Quaternion(packet.rot.x, packet.rot.y, packet.rot.z, packet.rot.w);
		}
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void ConnectingToServer(int data)
	{
		GameManager manager = FindObjectOfType<GameManager>();
		manager.SetState("connecting to server");
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void PlayerLeft(string data)
	{
		SocketPacket packet = JsonConvert.DeserializeObject<SocketPacket>(data);
		GameManager manager = FindObjectOfType<GameManager>();

		Player p = manager.SpawnPoints[packet.playerIndex].assignedPlayer.GetComponent<Player>();
		manager.PlayerList.Remove(p);
		manager.SpawnPoints[packet.playerIndex].assignedPlayer = null;
		Destroy(p.indicator.gameObject);
		Destroy(p.gameObject);

		if (manager.localPlayer.playerID == packet.newHost)
		{
			manager.localPlayer.isHost = true;
			manager.reloadUI = true;
		}
	}

	[MonoPInvokeCallback(typeof(Action<string>))]
	public static void TimeSync(string data)
	{
		TimeSync packet = JsonConvert.DeserializeObject<TimeSync>(data);
		GameManager manager = FindObjectOfType<GameManager>();
		

	}


	public void SyncRoomVariablesMeta(string state, bool playersInputEnabled, string playerFinished, string countdownStarted, string countdownFinished)
	{
		if (Application.isEditor || useWebsockets)
		{
			Debug.Log(11111);
		}
		else
		{
			SyncRoomVariablesJS(state, playersInputEnabled, playerFinished, countdownStarted, countdownFinished);
			Debug.Log(22222);
		}
	}

	public void CreateRoomMeta(bool privateGame)
	{
		if (Application.isEditor || useWebsockets)
		{
			Debug.Log(11111);
			CreateRoomWebSocket(privateGame);
		}
		else
		{
			CreateRoomJS(privateGame);
			Debug.Log(22222);
		}
	}

	public void DisconnectFromServerMeta()
	{
		serverDisconnected();
	}

	public void JoinRoomMeta()
	{
		if (Application.isEditor)
		{
			Debug.Log(11111);
		}
	}
}