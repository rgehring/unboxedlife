public sealed class NetworkIdentification : Component
{
	private TimeSince _lastLog;

	protected override void OnUpdate()
	{
		// Client-only truth
		if ( !Networking.IsClient )
			return;

		// Throttle (every 10 seconds)
		if ( _lastLog < 10f )
			return;

		_lastLog = 0f;

		Log.Info(
			$"[CLIENT][ROLESTAMP] go={GameObject.Id} name={GameObject.Name} " +
			$"netActive={GameObject.Network.Active} " +
			$"mode={GameObject.NetworkMode} " +
			$"owner={GameObject.Network.OwnerId} " +
			$"isOwner={GameObject.Network.IsOwner}"
		);
	}
}

