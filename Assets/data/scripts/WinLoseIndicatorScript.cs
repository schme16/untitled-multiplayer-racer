using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinLoseIndicatorScript : MonoBehaviour
{
	public Transform winner;
	public Transform loser;
	public FollowTarget followTarget;

	// Start is called before the first frame update
	void Start()
	{
		followTarget = gameObject.GetComponent<FollowTarget>();
	}

	// Update is called once per frame
	void Update()
	{
		if (followTarget.target == null)
		{
			winner.gameObject.SetActive(false);
			loser.gameObject.SetActive(false);
		}
	}
}