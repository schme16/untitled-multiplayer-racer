using System;
using System.Collections;
using Mirror;
using UnityEngine;

public class Player : NetworkBehaviour
{
	public GameManager manager;

	public WinLoseIndicatorScript indicator;

	public Rigidbody rb;

	public Color colour;

	public float jumpPower = 5;

	public float rotatePower = 50;

	private Vector3 prevPos;

	[SyncVar] public float speed;

	private bool isJumping = true;

	[SyncVar] public Transform spawnPoint;

	[SyncVar] public bool inputEnabled = false;

	[SyncVar] public string name;

	void Start()
	{
		//Add the new player to the player holder
		manager = FindObjectOfType<GameManager>();

		//Place the player at their spawn points position
		transform.position = spawnPoint.transform.position;

		//Add the new player to the player holder
		transform.parent = manager.playerHolder;
		
		
		//Add the new player to the player holder
		spawnPoint.GetComponent<SpawnPointScript>().assignedPlayer = transform;

		//Set the players colour
		colour = spawnPoint.GetComponent<MeshRenderer>().material.color;
		
		//Add a win/lose indicator for the player
		indicator = Instantiate(manager.winLoseIndicatorPrefab).GetComponent<WinLoseIndicatorScript>();

		//Set the follow target for the win/lose indicator to this player
		indicator.GetComponent<FollowTarget>().target = transform;

		//Sync the input enabled status from the client manager
		inputEnabled = manager.clientManager.playersInputEnabled;

		//Get the meshes renderer
		MeshRenderer mesh = GetComponent<MeshRenderer>();

		//Instance the material so that it can be individually altered
		Material mat = new Material(mesh.material);

		//Set the material colour, but invisible
		mat.SetColor("_BaseColor", new Color(colour.r, colour.g, colour.b, 0));

		//Assign the instanced material
		mesh.material = mat;

		//Set the material colour properly
		mat.SetColor("_BaseColor", colour);


		//Set the rigidbody var
		rb = gameObject.GetComponent<Rigidbody>();
	}

	// Update is called once per frame
	void Update()
	{
		gameObject.name = name;
		

		//Only the local player can run this
		if (isLocalPlayer)
		{
			float X = Input.GetAxis("Horizontal");
			float Y = Input.GetAxis("Vertical");
			bool XDown = Input.GetButtonDown("Horizontal");
			bool YDown = Input.GetButtonDown("Vertical");

			//Make the camera follow the player
			manager.virtualCamera.Follow = transform;


			if (manager.clientManager.playersInputEnabled && inputEnabled)
			{
				//Up action triggered?
				if (YDown && Y > 0 && !isJumping)
				{
					rb.AddForce(manager.cam.transform.up * jumpPower, ForceMode.Impulse);
					isJumping = true;
				}

				//Up action triggered?
				if (XDown && X != 0)
				{
					rb.AddTorque(new Vector3(0, 0, -rotatePower * X), ForceMode.Impulse);
				}
			}
		}

		if (string.IsNullOrEmpty(manager.clientManager.playerFinished))
		{
			indicator.winner.gameObject.SetActive(false);
			indicator.loser.gameObject.SetActive(false);
		}
		else
		{
			indicator.winner.gameObject.SetActive(name == manager.clientManager.playerFinished);
			indicator.loser.gameObject.SetActive(name != manager.clientManager.playerFinished);
		}
	}

	void FixedUpdate()
	{
		if (isLocalPlayer)
		{
			manager.uiSpeedo.text = (speed.ToString("F1")) + " m/s";
			CalcVelocity();
		}
	}

	private void OnCollisionEnter(Collision other)
	{
		if (other.gameObject.layer == 0 && isJumping)
		{
			isJumping = false;
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("finish-zone"))
		{
			manager.playerEnteredFinishZone(this);
		}
	}

	[ClientRpc]
	public void ResetPlayer()
	{
		inputEnabled = false;
		transform.position = spawnPoint.position;
		transform.rotation = new Quaternion();

		rb.velocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;
	}


	private void CalcVelocity()
	{
		// Calculate velocity: Velocity = DeltaPosition / DeltaTime
		speed = Mathf.Abs(((transform.position - prevPos) / Time.deltaTime).x);

		// Position at frame start
		prevPos = transform.position;
	}
}