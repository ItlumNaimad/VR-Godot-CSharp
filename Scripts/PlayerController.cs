using Godot;
using System;

public partial class PlayerController : XROrigin3D
{
	[Export]
	public float WalkSpeed { get; set; } = 3.0f;

	[Export]
	public float SprintSpeed { get; set; } = 6.0f;

	// Pozwala ręcznie przypisać węzeł w inspektorze
	[Export]
	public NodePath MovementDirectPath { get; set; }

	private Node _movementDirectNode;

	public override void _Ready()
	{
		// 1. Próba pobrania węzła ze ścieżki w inspektorze
		if (MovementDirectPath != null && !MovementDirectPath.IsEmpty)
		{
			_movementDirectNode = GetNodeOrNull(MovementDirectPath);
		}

		// 2. Jeśli nie przypisano w inspektorze, szukamy automatycznie w "LeftController/MovementDirect"
		if (_movementDirectNode == null)
		{
			var leftController = GetNodeOrNull("LeftController");
			if (leftController != null)
			{
				_movementDirectNode = leftController.GetNodeOrNull("MovementDirect");
			}
		}

		// 3. Obsługa błędu
		if (_movementDirectNode == null)
		{
			GD.PrintErr("PlayerController: Nie znaleziono węzła 'MovementDirect'. Przypisz go w inspektorze lub upewnij się, że znajduje się w 'LeftController/MovementDirect'.");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Zabezpieczenie przed nullem
		if (_movementDirectNode == null) return;

		// 4. Obsługa inputu "sprint"
		// Jeśli wciśnięty -> SprintSpeed, jeśli nie -> WalkSpeed
		float targetSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;

		// 5. Ustawienie wartości w skrypcie GDScript
		// Używamy Set, ponieważ C# nie widzi właściwości GDScript bezpośrednio
		_movementDirectNode.Set("max_speed", targetSpeed);
	}
}
