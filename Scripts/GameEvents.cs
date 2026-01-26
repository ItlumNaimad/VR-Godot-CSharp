using Godot;
using System;

public partial class GameEvents : Node
{
	// Singleton instance
	public static GameEvents Instance { get; private set; }

	// Event delegate
	public delegate void NoiseMadeEventHandler(Vector3 noisePosition, float volume);
	
	// Event to subscribe to
	public static event NoiseMadeEventHandler OnNoiseMade;

	public override void _Ready()
	{
		Instance = this;
	}

	public static void EmitNoiseMade(Vector3 noisePosition, float volume)
	{
		OnNoiseMade?.Invoke(noisePosition, volume);
	}
}
