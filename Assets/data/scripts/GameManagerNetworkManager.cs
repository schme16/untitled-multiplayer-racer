using UnityEngine;

public class GameManagerNetworkManager : MonoBehaviour
{
	// Start is called before the first frame update
	public GameManager manager;

	/*public struct CreatePlayerJoinedMessage : NetworkMessage
	{
		public string name;
	}

	public struct CreatePlayerLeftMessage : NetworkMessage
	{
		public string name;
	}*/


	/*public override void OnStartServer()
	{
		//Register the player joined event
		NetworkServer.RegisterHandler<CreatePlayerJoinedMessage>(manager.OnClientConnect);

		//Start the server
		base.OnStartServer();
	}*/

	/*public override void OnClientConnect()
	{
		base.OnClientConnect();

		CreatePlayerJoinedMessage characterMessage = new CreatePlayerJoinedMessage();
		characterMessage.name = "test";
		NetworkClient.Send(characterMessage);
	}*/

	/*public override void OnServerDisconnect(NetworkConnectionToClient conn)
	{
		base.OnServerDisconnect(conn);
		Debug.Log(2222);

		manager.OnClientDisconnect(conn);
	}*/
}