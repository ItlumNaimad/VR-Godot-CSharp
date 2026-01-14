using Godot;
using System;

public partial class Player : XROrigin3D
{
	// Kontrolery i Kamera
	public XRCamera3D XRCamera { get; private set; }
	public XRController3D LeftController { get; private set; }
	public XRController3D RightController { get; private set; }

	// Komponenty z pluginu godot-xr-tools (GDScript)
	// Przechowujemy je jako Node, ponieważ są napisane w GDScript i nie mają typów C#
	public Node PlayerBody { get; private set; }
	
	// Funkcje lewej ręki
	public Node LeftFunctionPickup { get; private set; }

	// Funkcje prawej ręki
	public Node RightFunctionPickup { get; private set; }
	public Node RightMovementTurn { get; private set; }
	public Node RightMovementSprint { get; private set; }

	public override void _Ready()
	{
		// 1. Inicjalizacja podstawowych węzłów VR
		XRCamera = GetNode<XRCamera3D>("XRCamera3D");
		LeftController = GetNode<XRController3D>("LeftController");
		RightController = GetNode<XRController3D>("RightController");
		
		// 2. Inicjalizacja PlayerBody
		PlayerBody = GetNode("PlayerBody");

		// 3. Pobieranie funkcji (GDScript) dla Lewego Kontrolera
		// Używamy GetNodeOrNull na wypadek gdybyś zmienił strukturę w edytorze
		LeftFunctionPickup = LeftController.GetNodeOrNull("FunctionPickup");

		// 4. Pobieranie funkcji (GDScript) dla Prawego Kontrolera
		RightFunctionPickup = RightController.GetNodeOrNull("FunctionPickup");
		RightMovementTurn = RightController.GetNodeOrNull("MovementTurn");
		RightMovementSprint = RightController.GetNodeOrNull("MovementSprint");

		// Opcjonalnie: Podłączanie sygnałów (przykład)
		ConnectSignals();
		
		GD.Print("Player.cs: Zainicjalizowano komponenty XR Tools.");
	}

	private void ConnectSignals()
	{
		// Przykład: Reagowanie na podniesienie przedmiotu lewą ręką
		if (LeftFunctionPickup != null)
		{
			// "has_picked_up" to sygnał z function_pickup.gd
			LeftFunctionPickup.Connect("has_picked_up", Callable.From<Node3D>(OnLeftHandPickedUp));
			LeftFunctionPickup.Connect("has_dropped", Callable.From(OnLeftHandDropped));
		}
	}

	private void OnLeftHandPickedUp(Node3D pickedObject)
	{
		GD.Print($"Lewa ręka podniosła: {pickedObject.Name}");
	}

	private void OnLeftHandDropped()
	{
		GD.Print("Lewa ręka upuściła przedmiot.");
	}
}
