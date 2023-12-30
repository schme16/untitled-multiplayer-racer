using System;
using System.Collections.Generic;
using Cinemachine;
using Mirror;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	//This is the network manager, it handles connection, disconnection, etc
	public GameManagerNetworkManager networkManager;

	//This is the client's synced variable manager, it handles all synced variables
	public ClientManager clientManager;

	//This is the playable prefab
	public Player PlayerPrefab;

	//This is the playable prefab
	public string lastState;

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

	//Waiting for players text
	public TextMeshProUGUI uiWaitingForPlayersText;

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


	// Start is called before the first frame update
	void Start()
	{
		//Set the game to the connections screen 
		StartWaitingForConnection();
	}

	// Update is called once per frame
	void Update()
	{
		//If the clientManager state is different from the last state
		//This is stuff that's run just once
		if (clientManager.state != lastState)
		{
			//Set the lastState variable to the current state
			lastState = clientManager.state;

			//Reset the UI
			ResetUI();

			//Loop over each state
			switch (clientManager.state)
			{
				case "connections":

					//Start the waiting for player state
					StartWaitingForConnection();

					break;

				case "host":

					//Start hosting the game
					StartHosting();

					break;

				case "join":

					//Join a game
					JoinGame();

					break;

				case "waiting for players":

					//Start the waiting for player state
					StartWaitingForPlayers();

					break;

				case "start":

					//Start the race
					StartRace();
					break;

				case "finished":

					//Run the race finish function
					FinishRace();

					break;

				case "restart":

					//Run the race finish function
					RestartRace();

					break;
			}
		}


		//Loop over each state
		//This is run every frame
		switch (clientManager.state)
		{
			case "connections":

				break;

			case "waiting for players":

				break;

			case "racing":

				//If no player has won
				if (clientManager.playerFinished == null)
				{
					//Increase the timer
					clientManager.timer += Time.deltaTime;

					//Set the timer ui to the new time, with 2 decimal places
					uiTimerText.text = clientManager.timer.ToString("F2");
				}

				break;

			case "finished":

				break;
		}
	}

	//Hide all UI elements
	public void ResetUI()
	{
		//
		uiWaitingForPlayersText.gameObject.SetActive(false);

		//
		startGameButton.gameObject.SetActive(false);

		//
		uiTimerGroup.gameObject.SetActive(false);

		//
		uiSpeedo.gameObject.SetActive(false);

		//
		uiWaitingForPlayersText.gameObject.SetActive(false);

		//
		uiHostButton.gameObject.SetActive(false);

		//
		uiJoinButton.gameObject.SetActive(false);

		//
		uiHostnameInput.gameObject.SetActive(false);

		//
		uiRestartGameButton.gameObject.SetActive(false);

		//
	}

	public void StartWaitingForConnection()
	{
		//WebGL via websockets can't host games, so hide the hosting button on that platform
		if (Application.platform != RuntimePlatform.WebGLPlayer)
		{
			//Show the host button
			uiHostButton.gameObject.SetActive(true);
		}

		//Show the join button
		uiJoinButton.gameObject.SetActive(true);

		//Show the join ip input field
		uiHostnameInput.gameObject.SetActive(true);

		//Reset the timer
		clientManager.timer = 0;

		//Disable player inputs
		clientManager.playersInputEnabled = false;
	}

	public void StartWaitingForPlayers()
	{
		//Show the "waiting for players" text
		uiWaitingForPlayersText.gameObject.SetActive(true);

		//If this instance is the server
		if (clientManager.isServer)
		{
			//Show the start game button 
			startGameButton.gameObject.SetActive(true);
		}

		//Reset the timer
		clientManager.timer = 0;

		//Disable player inputs
		clientManager.playersInputEnabled = false;

		//Reset the playerFinished variable
		clientManager.playerFinished = null;
	}

	//This starts the race
	public void StartRace()
	{
		//Reset the timer
		clientManager.timer = 0;

		//Disable the player inputs
		clientManager.playersInputEnabled = true;

		//Reset if a player has finished or not 
		clientManager.playerFinished = null;

		//Show the game timer ui
		uiTimerText.gameObject.SetActive(true);

		//Show the player speed ui
		uiSpeedo.gameObject.SetActive(true);

		//Get all players
		foreach (Player player in GetAllocatedSpawnPoints())
		{
			//Set their input to enabled
			player.inputEnabled = true;
		}
	}

	//This resets the race, so that you can play again
	public void RestartRace()
	{
		clientManager.playerFinished = null;

		foreach (Player player in FindObjectsOfType<Player>())
		{
			player.ResetPlayer();
		}

		StartWaitingForPlayers();
	}

	//This finishes the race
	public void FinishRace()
	{
		//Is this instance is the host
		if (clientManager.isServer)
		{
			//Show the timer ui
			uiTimerText.gameObject.SetActive(true);

			//Show the player speed meter ui 
			uiSpeedo.gameObject.SetActive(true);


			//If this instance is the server
			if (clientManager.isServer)
			{
				//Show the restart button ui
				uiRestartGameButton.gameObject.SetActive(true);
			}
		}
	}

	//Start the hosting
	public void StartHosting()
	{
		//Set the network address to bind to all local ips
		networkManager.networkAddress = "0.0.0.0";

		//Start the hosting
		networkManager.StartHost();

		//Set the state to waiting for players
		clientManager.state = "waiting for players";
	}

	//Join a game
	public void JoinGame()
	{
		//Set the address to the hostname input field value
		string address = uiHostnameInput.GetComponentInChildren<TMP_InputField>().text;

		//If the string is empty or null
		if (string.IsNullOrEmpty(address))
		{
			//Set the address to localhost
			address = "localhost";
		}

		//Set the network connection address to address
		networkManager.networkAddress = "localhost";

		//Attempt to connect the the address
		networkManager.StartClient();
	}

	//Check all spawn points for if there's a player assigned, and hands back the first free one
	public SpawnPointScript GetFreeSpawnPoint()
	{
		//For each of the spawn points
		foreach (var sp in SpawnPoints)
		{
			//Check if it has no assigned player
			if (sp.assignedPlayer == null)
			{
				//Hand the player back if 
				return sp;
			}
		}

		return null;
	}

	public List<Player> GetAllocatedSpawnPoints()
	{
		List<Player> players = new();
		foreach (var sp in SpawnPoints)
		{
			if (sp.assignedPlayer != null)
			{
				players.Add(sp.assignedPlayer.GetComponent<Player>());
			}
			else
			{
				sp.assignedPlayer = null;
			}
		}

		return players;
	}

	Player createPlayer(SpawnPointScript spawnPoint, int id)
	{
		//Create the player from the player prefab
		Player newPlayer = Instantiate(PlayerPrefab).GetComponent<Player>();

		//Set the players id
		newPlayer.name = $"Player: {id}";

		//Assign the spawn point
		newPlayer.spawnPoint = spawnPoint.transform;
		spawnPoint.assignedPlayer = newPlayer.transform;

		//Hand the new player back
		return newPlayer;
	}

	//Listen for when players connect
	public void OnClientConnect(NetworkConnectionToClient conn, GameManagerNetworkManager.CreatePlayerJoinedMessage message)
	{
		//Try and fetch an unused spawn point
		SpawnPointScript spawnPoint = GetFreeSpawnPoint();

		//If the total players are less than the max players (as defined by the number of spawn points)
		if (spawnPoint != null)
		{
			Player player = createPlayer(spawnPoint, conn.connectionId);

			//Create the new player, and add it the the players connection as their player object
			NetworkServer.AddPlayerForConnection(conn, player.gameObject);
		}
	}

	//Listen for when players leave
	public void OnClientDisconnect(NetworkConnectionToClient conn)
	{
		GetAllocatedSpawnPoints();
	}

	public void PlayerEnteredFinishZone(Player player)
	{
		if (clientManager.playerFinished == null && player.inputEnabled)
		{
			clientManager.playerFinished = player.name;
			clientManager.state = "finished";
		}

		player.inputEnabled = false;
	}
}