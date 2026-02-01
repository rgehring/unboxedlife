public sealed class PlayerLink : Component
{
	public GameObject Player { get; private set; }
	public GameObject State { get; private set; }

	public void SetPlayer( GameObject player ) => Player = player;
	public void SetState( GameObject state ) => State = state;
}

