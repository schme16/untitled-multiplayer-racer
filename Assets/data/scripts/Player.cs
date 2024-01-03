using System;
using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
	public GameManager manager;

	public WinLoseIndicatorScript indicator;

	public Rigidbody rb;

	public Color colour;

	public float jumpPower = 5;

	public float rotatePower = 50;

	private Vector3 prevPos;

	private string uuid;

	private bool isJumping = true;
	
	public bool unsynced = true;

	public float speed;
	public Transform spawnPoint;
	public bool inputEnabled = false;
	public string playerObjectID;

	//This needs to be set properly, as it indicates which prefab can actually run it's logic
	public bool isLocalPlayer;
	
	bool playerChanged;


	public SpawnPointScript spawnPointScript;
	public Camera cam;

	void Start()
	{
	}

	// Update is called once per frame
	void Update()
	{
		//Set the players name
		gameObject.name = playerObjectID;


		//Only the local player can run this
		if (isLocalPlayer)
		{
			float X = Input.GetAxis("Horizontal");
			float Y = Input.GetAxis("Vertical");
			bool XDown = Input.GetButtonDown("Horizontal");
			bool YDown = Input.GetButtonDown("Vertical");

			//Make the camera follow the player
			manager.virtualCamera.Follow = transform;

			//Vertical action triggered?
			if (YDown && Y > 0 && !isJumping)
			{
				//Flag that the player changed
				playerChanged = true;
				
				//Add some force to bump the player up
				rb.AddForce(cam.transform.up * jumpPower, ForceMode.Impulse);
				
				//Mark the player as jumping
				isJumping = true;
			}

			//If the inputs are enabled
			if (manager.room.playersInputEnabled && inputEnabled)
			{
				//Horizontal action triggered?
				if (XDown && X != 0)
				{
					//Flag that the player changed
					playerChanged = true;
					
					rb.AddTorque(new Vector3(0, 0, -rotatePower * X), ForceMode.Impulse);
				}
			}
		}

		if (string.IsNullOrEmpty(manager.room.playerFinished))
		{
			indicator.winner.gameObject.SetActive(false);
			indicator.loser.gameObject.SetActive(false);
		}
		else
		{
			indicator.winner.gameObject.SetActive(playerObjectID == manager.room.playerFinished);
			indicator.loser.gameObject.SetActive(playerObjectID != manager.room.playerFinished);
		}
	}

	void FixedUpdate()
	{
		if (isLocalPlayer)
		{
			manager.uiSpeedo.text = (speed.ToString("F1")) + " m/s";
			CalcVelocity();

			if (true || playerChanged)
			{
				manager.SyncLocalPlayer(transform, rb, true);
				playerChanged = false;
			}
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
			manager.PlayerEnteredFinishZone(this);
		}
	}

	
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