namespace UnboxedLife;

/// <summary>
/// Shared trace helpers (use for interaction + combat).
/// </summary>
public static class TraceUtil
{
	// Global trace debug toggle (runtime): ul_trace_debug 0/1
	[ConVar( "ul_trace_debug" )]
	public static bool DebugEnabled { get; set; } = false;

	// Debug duration: ul_trace_debug_time 0.05 etc
	[ConVar( "ul_trace_debug_time" )]
	public static float DebugDuration { get; set; } = 0.05f;

	/// <summary>
	/// Convenience: traces from pawn "eyes" forward.
	/// Uses PlayerController.EyePosition/EyeAngles when available, otherwise falls back.
	/// </summary>
	public static SceneTraceResult TraceFromEyes(
		GameObject pawn,
		Sandbox.PlayerController pc,
		float fallbackEyeHeight,
		float distance,
		bool ignorePawnHierarchy = true
	)
	{
		var start = pc?.EyePosition ?? (pawn.WorldPosition + Vector3.Up * fallbackEyeHeight);
		var forward = pc?.EyeAngles.Forward ?? pawn.WorldRotation.Forward;
		var end = start + forward * distance;

		var builder = pawn.Scene.Trace
			.FromTo( start, end );

		if ( ignorePawnHierarchy )
			builder = builder.IgnoreGameObjectHierarchy( pawn.Root );

		var tr = builder.Run();

		if ( DebugEnabled )
		{
			pawn.Scene.DebugOverlay.Trace( tr, DebugDuration, true );
		}

		return tr;
	}
}
