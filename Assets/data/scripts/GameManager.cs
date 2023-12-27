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

	//This is the winner/loser indicator prefab
	public WinLoseIndicatorScript winLoseIndicatorPrefab;

	//This is the transform where all spawn players are added
	public Transform playerHolder;

	//This is the primary camera
	public Camera cam;

	//This is the cinemachine controller for the main camera 
	public CinemachineVirtualCamera virtualCamera;
	public List<SpawnPointScript> SpawnPoints;


	//UI
	public TextMeshProUGUI uiTimerText;
	public TextMeshProUGUI uiWaitingForPlayersText;
	public TextMeshProUGUI uiSpeedo;
	public Transform startGameButton;
	public Transform uiRestartGameButton;
	public Transform uiHostButton;
	public Transform uiJoinButton;
	public Transform uiHostnameInput;
	public TextMeshProUGUI uiHostnameText;


	// Start is called before the first frame update
	void Start()
	{
		cam = Camera.main;
		StartWaitingForConnection();
	}

	// Update is called once per frame
	void Update()
	{
		switch (clientManager.state)
		{
			case "connections":

				uiTimerText.gameObject.SetActive(false);
				uiWaitingForPlayersText.gameObject.SetActive(false);
				uiSpeedo.gameObject.SetActive(false);
				startGameButton.gameObject.SetActive(false);

				uiHostButton.gameObject.SetActive(true);
				uiJoinButton.gameObject.SetActive(true);
				uiHostnameInput.gameObject.SetActive(true);
				uiRestartGameButton.gameObject.SetActive(false);

				break;

			case "waiting for players":
				//Show the "waiting for players" text
				uiWaitingForPlayersText.gameObject.SetActive(true);

				uiTimerText.gameObject.SetActive(false);
				uiSpeedo.gameObject.SetActive(false);
				uiWaitingForPlayersText.gameObject.SetActive(false);

				uiHostButton.gameObject.SetActive(false);
				uiJoinButton.gameObject.SetActive(false);
				uiHostnameInput.gameObject.SetActive(false);
				uiRestartGameButton.gameObject.SetActive(false);

				if (clientManager.isServer)
				{
					startGameButton.gameObject.SetActive(true);
				}

				break;

			case "racing":
				uiWaitingForPlayersText.gameObject.SetActive(false);

				startGameButton.gameObject.SetActive(false);
				uiTimerText.gameObject.SetActive(true);
				uiSpeedo.gameObject.SetActive(true);
				uiWaitingForPlayersText.gameObject.SetActive(false);

				uiHostButton.gameObject.SetActive(false);
				uiJoinButton.gameObject.SetActive(false);
				uiHostnameInput.gameObject.SetActive(false);
				uiRestartGameButton.gameObject.SetActive(false);


				if (clientManager.playerFinished == null)
				{
					clientManager.timer += Time.deltaTime;
					uiTimerText.text = clientManager.timer.ToString("F1");
				}


				break;

			case "finished":
				if (clientManager.isServer)
				{
					uiWaitingForPlayersText.gameObject.SetActive(false);

					startGameButton.gameObject.SetActive(false);
					uiTimerText.gameObject.SetActive(true);
					uiSpeedo.gameObject.SetActive(true);
					uiWaitingForPlayersText.gameObject.SetActive(false);

					uiHostButton.gameObject.SetActive(false);
					uiJoinButton.gameObject.SetActive(false);
					uiHostnameInput.gameObject.SetActive(false);

					if (clientManager.isServer)
					{
						uiRestartGameButton.gameObject.SetActive(true);
					}
				}

				break;
		}
	}

	public void StartWaitingForConnection()
	{
		//Set the current state
		clientManager.state = "connections";

		//Reset the timer
		clientManager.timer = 0;

		//Disable player inputs
		clientManager.playersInputEnabled = false;
	}

	public void StartWaitingForPlayers()
	{
		//Set the current state
		clientManager.state = "waiting for players";

		//Reset the timer
		clientManager.timer = 0;

		//Disable player inputs
		clientManager.playersInputEnabled = false;

		clientManager.playerFinished = null;
	}

	public void StartRace()
	{
		//Set the current state
		clientManager.state = "racing";

		//Reset the timer
		clientManager.timer = 0;

		//Disable the player inputs
		clientManager.playersInputEnabled = true;

		//Reset if a player has finished or not 
		clientManager.playerFinished = null;

		foreach (Player player in GetAllocatedSpawnPoints())
		{
			player.inputEnabled = true;
		}
	}

	public void RestartRace()
	{
		clientManager.playerFinished = null;

		foreach (Player player in FindObjectsOfType<Player>())
		{
			player.ResetPlayer();
		}

		StartWaitingForPlayers();
	}

	public void StartHosting()
	{
		networkManager.networkAddress = "127.0.0.1:7777";
		networkManager.StartHost();

		StartWaitingForPlayers();
	}

	public void JoinGame()
	{
		networkManager.StartClient();

		networkManager.networkAddress = uiHostnameText.GetComponentInChildren<TextMeshProUGUI>().text;

		// only show a port field if we have a port transport
		// we can't have "IP:PORT" in the address field since this only
		// works for IPV4:PORT.
		// for IPV6:PORT it would be misleading since IPV6 contains ":":
		// 2001:0db8:0000:0000:0000:ff00:0042:8329
		if (Transport.active is PortTransport portTransport)
		{
			portTransport.Port = 5400;
		}
	}

	public SpawnPointScript GetFreeSpawnPoint()
	{
		foreach (var sp in SpawnPoints)
		{
			if (sp.assignedPlayer == null)
			{
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
		Debug.Log("------");
		Debug.Log(conn.identity);
		Debug.Log("------");
		GetAllocatedSpawnPoints();
	}

	public void playerEnteredFinishZone(Player player)
	{
		if (clientManager.playerFinished == null && player.inputEnabled)
		{
			clientManager.playerFinished = player.name;
			clientManager.state = "finished";
		}

		player.inputEnabled = false;
	}
}