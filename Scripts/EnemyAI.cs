using Godot;
using System;
using System.Threading.Tasks; // Używane do zadań asynchronicznych (Task), choć tutaj głównie korzystamy z Timerów.

// "public partial class" - W C# w Godot 4 klasy dziedziczące po Node muszą być "partial" (częściowe),
// ponieważ Godot generuje drugą część klasy w tle.
// ": CharacterBody3D" oznacza dziedziczenie (extends CharacterBody3D w GDScript).
public partial class EnemyAI : CharacterBody3D
{
	// [Export] to odpowiednik @export w GDScript.
	// [ExportCategory] grupuje zmienne w inspektorze.
	// "public float" - zmienna publiczna typu zmiennoprzecinkowego. W C# musimy podawać typy zmiennych.
	// "15.0f" - literka 'f' na końcu oznacza, że to typ float (a nie double).
	[ExportCategory("Behavior")]
	[Export] public float HearingRange = 15.0f;
	[Export] public float PatrolSpeed = 2.0f;
	[Export] public float ChaseSpeed = 4.5f;
	[Export] public float PatrolWaitTime = 1.0f;

	[ExportCategory("Navigation")]
	// Tablica węzłów Node3D (odpowiednik Array[Node3D] w GDScript).
	[Export] public Node3D[] PatrolPoints;
	[Export] public float TargetReachedThreshold = 1.5f;

	// Definicja Enuma (wyliczenia) - w GDScript też istnieje (enum State { ... }).
	// Służy do zarządzania stanami maszyny stanów (State Machine).
	public enum State
	{
		Patrol,
		Investigate, // Badanie hałasu
		Chase        // Pościg
	}

	// Zmienne prywatne (zaczynające się od _ to konwencja, nie wymóg języka, ale dobra praktyka).
	// W C# jeśli nie napiszesz "public", zmienna jest domyślnie "private".
	private State _currentState = State.Patrol;
	private int _currentPatrolIndex = 0; // int - liczba całkowita
	
	// Deklaracja zmiennych dla komponentów. W C# nie ma "onready var",
	// przypisujemy je w metodzie _Ready().
	private NavigationAgent3D _navigationAgent;
	private AudioStreamPlayer3D _audioPlayer;
	
	// Zmienne stanu
	private bool _isWaiting = false; // bool - prawda/fałsz
	private Vector3 _lastSeenPlayerPosition; // Vector3 to struktura (struct), nie klasa.
	private Node3D _playerTarget; // Referencja do gracza (może być null).

	// override void _Ready() - odpowiednik func _ready():
	// "override" oznacza, że nadpisujemy metodę z klasy bazowej (Node).
	// "void" oznacza, że funkcja nic nie zwraca.
	public override void _Ready()
	{
		// Pobieranie węzłów. W C# używamy metody generycznej GetNode<Typ>("Ścieżka").
		// Dzięki temu _navigationAgent od razu jest typu NavigationAgent3D i mamy podpowiadanie kodu.
		_navigationAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		_audioPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");

		// Ustawienia nawigacji
		_navigationAgent.PathDesiredDistance = 1.0f;
		_navigationAgent.TargetDesiredDistance = 1.0f;
		
		// Obsługa audio
		// W C# sprawdzamy czy obiekt nie jest null (null to odpowiednik nil w GDScript).
		if (_audioPlayer.Stream != null)
		{
			_audioPlayer.Play();
		}

		// Podłączanie zdarzeń (Events/Signals).
		// W C# zdarzenia C# (Events) obsługuje się operatorem +=.
		// Zakładamy, że GameEvents to statyczna klasa pomocnicza (Singleton/Autoload).
		// To odpowiednik: GameEvents.connect("OnNoiseMade", HearNoise) w GDScript, ale bezpieczniejszy typowo.
		GameEvents.OnNoiseMade += HearNoise;

		// CallDeferred to odpowiednik call_deferred("SetupPatrol").
		// nameof(SetupPatrol) zwraca nazwę funkcji jako string - chroni przed literówkami.
		CallDeferred(nameof(SetupPatrol));
	}

	// _ExitTree() - odpowiednik func _exit_tree():
	// Ważne w C#: Zawsze odłączaj zdarzenia statyczne (C# events), aby uniknąć wycieków pamięci!
	// W GDScript signals często czyszczą się same przy usunięciu obiektu, ale statyczne eventy C# nie.
	public override void _ExitTree()
	{
		GameEvents.OnNoiseMade -= HearNoise; // -= odłącza funkcję.
	}

	private void SetupPatrol()
	{
		// Sprawdzamy czy tablica punktów nie jest pusta.
		// .Length to długość tablicy (w GDScript .size()).
		if (PatrolPoints != null && PatrolPoints.Length > 0)
		{
			SetMovementTarget(PatrolPoints[_currentPatrolIndex].GlobalPosition);
		}
	}

	// _PhysicsProcess - odpowiednik func _physics_process(delta):
	// Uwaga: w C# delta jest typu double (podwójna precyzja), a nie float.
	public override void _PhysicsProcess(double delta)
	{
		if (_isWaiting) return; // return przerywa funkcję (jak w GDScript).

		// switch - instrukcja wyboru, czytelniejsza niż wiele if/else if.
		// Odpowiednik match w GDScript.
		switch (_currentState)
		{
			case State.Patrol:
				ProcessPatrol();
				break; // break jest wymagany w C# w switchu.
			case State.Chase:
				ProcessChase();
				break;
			case State.Investigate:
				ProcessInvestigate();
				break;
		}

		// Logika poruszania się
		if (_navigationAgent.IsNavigationFinished())
		{
			Velocity = Vector3.Zero;
		}
		else
		{
			// Rzutowanie (cast): (float)delta zamienia double na float,
			// bo większość funkcji Godot API używa float.
			MoveAlongPath((float)delta);
		}
		
		// MoveAndSlide() działa tak samo jak w GDScript.
		MoveAndSlide();
	}

	private void MoveAlongPath(float delta)
	{
		Vector3 currentAgentPosition = GlobalTransform.Origin; // Globalna pozycja
		Vector3 nextPathPosition = _navigationAgent.GetNextPathPosition(); // Następny punkt ścieżki

		// Operator trójargumentowy (ternary operator): warunek ? wartość_jeśli_prawda : wartość_jeśli_fałsz
		// To skrócony if/else.
		float currentSpeed = _currentState == State.Chase ? ChaseSpeed : PatrolSpeed;

		Vector3 newVelocity = (nextPathPosition - currentAgentPosition).Normalized() * currentSpeed;

		// Przypisanie prędkości
		Velocity = newVelocity;
		
		// Obracanie w stronę ruchu
		// LengthSquared() jest szybsze niż Length() (nie liczy pierwiastka), dobre do porównań.
		if (Velocity.LengthSquared() > 0.1f)
		{
			float lookSpeed = 5.0f * delta;
			// Tworzymy wektor kierunku ignorując oś Y (żeby wróg się nie przechylał góra/dół).
			Vector3 lookDir = new Vector3(Velocity.X, 0, Velocity.Z).Normalized();
			
			// Mathf to klasa z funkcjami matematycznymi (jak globalne funkcje w GDScript).
			float targetAngle = Mathf.Atan2(lookDir.X, lookDir.Z);
			
			Vector3 currentRotation = Rotation;
			// LerpAngle - płynna interpolacja kąta.
			currentRotation.Y = Mathf.LerpAngle(currentRotation.Y, targetAngle, lookSpeed);
			Rotation = currentRotation;
		}
	}

	private void SetMovementTarget(Vector3 targetPosition)
	{
		_navigationAgent.TargetPosition = targetPosition;
	}

	// #region to dyrektywa preprocesora pozwalająca zwijać fragmenty kodu w edytorze (dla porządku).
	#region State Logic

	private void ProcessPatrol()
	{
		if (_navigationAgent.IsNavigationFinished())
		{
			// Przejście do następnego punktu (modulo % zapętla indeks).
			_currentPatrolIndex = (_currentPatrolIndex + 1) % PatrolPoints.Length;
			_isWaiting = true;
			
			// Tworzenie timera w kodzie (zamiast węzła Timer w scenie).
			// Callable.From(() => { ... }) to wyrażenie lambda (funkcja anonimowa).
			// To nowoczesny sposób na podłączanie sygnałów w C# bez tworzenia osobnych metod.
			GetTree().CreateTimer(PatrolWaitTime).Connect("timeout", Callable.From(() => 
			{
				_isWaiting = false;
				if (_currentState == State.Patrol)
					SetMovementTarget(PatrolPoints[_currentPatrolIndex].GlobalPosition);
			}), (uint)ConnectFlags.OneShot); // OneShot - timer wykona się tylko raz.
		}
		
		// Upewnij się, że dźwięk gra podczas patrolu
		if (!_audioPlayer.Playing) _audioPlayer.Play();
	}

	private void ProcessChase()
	{
		if (_playerTarget != null)
		{
			SetMovementTarget(_playerTarget.GlobalPosition);
			_lastSeenPlayerPosition = _playerTarget.GlobalPosition;
		}
		
		// Tutaj można dodać logikę sprawdzania dystansu, by przerwać pościg.
	}

	private void ProcessInvestigate()
	{
		if (_navigationAgent.IsNavigationFinished())
		{
			// GD.Print to odpowiednik print() w GDScript.
			GD.Print("Enemy: Reached investigation point. Searching...");
			_isWaiting = true;
			
			// Zatrzymaj się
			Velocity = Vector3.Zero;

			// Czekaj 3 sekundy, potem wróć do patrolu.
			GetTree().CreateTimer(3.0f).Connect("timeout", Callable.From(() =>
			{
				if (_currentState == State.Investigate)
				{
					GD.Print("Enemy: Nothing found. Returning to patrol.");
					SwitchState(State.Patrol);
				}
				_isWaiting = false;
			}), (uint)ConnectFlags.OneShot);
		}
	}

	private void SwitchState(State newState)
	{
		if (_currentState == newState) return;

		// Interpolacja stringów ($"...") pozwala wstawiać zmienne bezpośrednio w tekst (jak f-stringi w Pythonie).
		GD.Print($"Enemy: Switching State from {_currentState} to {newState}");
		_currentState = newState;
		_isWaiting = false;

		switch (_currentState)
		{
			case State.Patrol:
				// Wróć do patrolu
				SetupPatrol();
				break;
			case State.Investigate:
				// Cel jest już ustawiony przez HearNoise
				break;
			case State.Chase:
				_audioPlayer.Stop(); // Zazwyczaj w pościgu gra inna muzyka, wyciszamy nucenie
				break;
		}
	}

	#endregion

	#region Public Methods & Events

	// Metoda publiczna - może być wywołana przez inne skrypty.
	public void HearNoise(Vector3 noisePosition, float volume)
	{
		if (_currentState == State.Chase) return; // Jeśli już goni, ignoruj hałas

		float distance = GlobalPosition.DistanceTo(noisePosition);
		if (distance <= HearingRange)
		{
			GD.Print("Enemy: Heard noise!");
			SetMovementTarget(noisePosition);
			SwitchState(State.Investigate);
		}
	}

	// Metoda wywoływana, gdy gracz wejdzie w Area3D (wykrywanie wzrokowe).
	public void OnPlayerDetected(Node3D player)
	{
		GD.Print("Enemy: Player detected!");
		_playerTarget = player;
		SwitchState(State.Chase);
	}

	// Metoda wywoływana, gdy gracz wyjdzie z Area3D.
	public void OnPlayerLost()
	{
		if (_currentState != State.Chase) return;

		GD.Print("Enemy: Player lost sight. Waiting before investigating...");
		
		// Lambda z logiką "zgubienia" gracza.
		// Czekaj 5 sekund, potem zbadaj ostatnią znaną pozycję.
		GetTree().CreateTimer(5.0f).Connect("timeout", Callable.From(() =>
		{
			if (_currentState == State.Chase) // Upewnij się, że nadal gonimy
			{
				SetMovementTarget(_lastSeenPlayerPosition);
				SwitchState(State.Investigate);
				_playerTarget = null;
			}
		}), (uint)ConnectFlags.OneShot);
	}

	#endregion
}
