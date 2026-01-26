# Dokumentacja Projektu VR (Godot 4 + C#)

## Spis Treści
1. [Wstęp](#wstęp)
2. [Architektura Hybrydowa (C# i GDScript)](#architektura-hybrydowa-c-i-gdscript)
3. [System Gracza VR](#system-gracza-vr)
4. [Sztuczna Inteligencja (Maszyna Stanów)](#sztuczna-inteligencja-maszyna-stanów)
5. [System Zdarzeń Globalnych](#system-zdarzeń-globalnych)
6. [Różnice: Sygnały (Godot) vs Zdarzenia (C#)](#różnice-sygnały-godot-vs-zdarzenia-c)
7. [Konfiguracja OpenXR (HTC Vive)](#konfiguracja-openxr-htc-vive)

---

## Wstęp
Projekt jest aplikacją VR tworzoną w silniku **Godot 4.4** przy użyciu środowiska **.NET (C#)**. Celem projektu jest implementacja mechanik interakcji w wirtualnej rzeczywistości oraz stworzenie inteligentnego przeciwnika reagującego na otoczenie.

Projekt wykorzystuje wtyczkę **Godot XR Tools** do obsługi podstawowych mechanik VR (chwytanie, poruszanie się), jednak **cała logika gry, sztuczna inteligencja i zarządzanie stanem rozgrywki są autorskimi rozwiązaniami napisanymi w języku C#**.

---

## Architektura Hybrydowa (C# i GDScript)

Jednym z kluczowych wyzwań projektu była integracja skryptów C# z gotowymi komponentami wtyczki *Godot XR Tools*, które są napisane w natywnym języku Godota – GDScript.

### Problem
C# (język statycznie typowany) nie widzi bezpośrednio klas napisanych w GDScript (język dynamicznie typowany). Nie możemy napisać `MovementDirect movement = GetNode<MovementDirect>(...)`, ponieważ typ `MovementDirect` nie istnieje w przestrzeni nazw C#.

### Rozwiązanie
Zastosowano podejście "Reflection-like", gdzie C# traktuje obiekty GDScript jako ogólny typ `Node` lub `GodotObject`, a metody i właściwości wywołuje za pomocą nazw tekstowych.

**Przykład z `Player.cs`:**
```csharp
// Pobieramy węzeł jako ogólny typ 'Node', bo C# nie zna typu 'MovementDirect' z GDScript
public Node LeftMovementDirect { get; private set; }

public override void _Ready()
{
    // Pobranie referencji do węzła GDScript
    LeftMovementDirect = LeftController.GetNodeOrNull("MovementDirect");
    
    // Wyłączenie komponentu poprzez ustawienie właściwości "enabled" na false
    // Używamy metody Set(), podając nazwę zmiennej jako string
    if (LeftMovementSprint != null)
    {
        LeftMovementSprint.Set("enabled", false);
    }
}

public override void _PhysicsProcess(double delta)
{
    // Modyfikacja prędkości w skrypcie GDScript w czasie rzeczywistym
    if (LeftMovementDirect != null)
    {
        float targetSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
        
        // Komunikacja C# -> GDScript: Ustawienie zmiennej "max_speed" wewnątrz skryptu .gd
        LeftMovementDirect.Set("max_speed", targetSpeed);
    }
}
```

> **Dla studenta:** Wyobraź sobie, że C# to Polak, a GDScript to Japończyk. Nie mogą rozmawiać bezpośrednio swoją gramatyką. C# używa więc "karteczek" z napisami (stringami) takimi jak "max_speed", aby przekazać instrukcje do GDScript.

---

## System Gracza VR

Skrypt `Player.cs` pełni rolę zarządcy (Controllera) dla fizycznego ciała gracza w VR.

**Kluczowe funkcjonalności:**
*   Inicjalizacja kontrolerów `XRController3D` (lewa i prawa ręka).
*   Mapowanie sygnałów (Events) z systemu XR Tools na logikę C#.
*   Obsługa sprintu poprzez modyfikację parametrów ruchu w locie.

---

## Sztuczna Inteligencja (Maszyna Stanów)

Logika przeciwnika (`EnemyAI.cs`) została oparta na wzorcu projektowym **Finite State Machine (Skończona Maszyna Stanów)**. Pozwala to na uporządkowanie zachowań bota w logiczne, odseparowane bloki.

### Stany Przeciwnika
1.  **Patrol:** Przeciwnik chodzi między wyznaczonymi punktami.
2.  **Investigate (Dochodzenie):** Przeciwnik usłyszał hałas i idzie sprawdzić to miejsce.
3.  **Chase (Pościg):** Przeciwnik widzi gracza i biegnie w jego stronę.

### Implementacja w C#
```csharp
// Definicja możliwych stanów (Enum)
public enum State
{
    Patrol,
    Investigate,
    Chase
}

// Główna pętla decyzyjna (uruchamiana co klatkę fizyki)
public override void _PhysicsProcess(double delta)
{
    // Wywołanie odpowiedniej logiki w zależności od aktualnego stanu
    switch (_currentState)
    {
        case State.Patrol:
            ProcessPatrol(); // Logika chodzenia od punktu A do B
            break;
        case State.Chase:
            ProcessChase();  // Logika biegania za graczem
            break;
        case State.Investigate:
            ProcessInvestigate(); // Logika szukania źródła hałasu
            break;
    }
    
    // Fizyczne przesunięcie postaci (korzysta z NavigationAgent3D do omijania ścian)
    MoveAlongPath((float)delta);
    MoveAndSlide();
}
```

### Nawigacja
Wykorzystano węzeł `NavigationAgent3D`. W kodzie C# wyznaczamy cel (`TargetPosition`), a silnik Godot oblicza najkrótszą ścieżkę omijającą przeszkody (NavMesh).

---

## System Zdarzeń Globalnych

Aby przeciwnik mógł "usłyszeć" upadający przedmiot lub strzał, nie powinien być na sztywno połączony z każdym obiektem w grze. Zastosowano wzorzec **Observer (Obserwator)** poprzez statyczną klasę `GameEvents`.

**Skrypt `GameEvents.cs`:**
```csharp
public partial class GameEvents : Node
{
    // Definicja typu zdarzenia (delegat): Ktoś zrobił hałas w punkcie X o głośności Y
    public delegate void NoiseMadeEventHandler(Vector3 noisePosition, float volume);
    
    // Publiczne zdarzenie, do którego każdy może się "zapisać"
    public static event NoiseMadeEventHandler OnNoiseMade;

    // Metoda wywoływana przez obiekt generujący hałas (np. upadającą wazę)
    public static void EmitNoiseMade(Vector3 noisePosition, float volume)
    {
        // Powiadom wszystkich nasłuchujących (jeśli tacy są)
        OnNoiseMade?.Invoke(noisePosition, volume);
    }
}
```

**Odbiór w `EnemyAI.cs`:**
```csharp
public override void _Ready()
{
    // Przeciwnik "zapisuje się" na nasłuchiwanie hałasu
    GameEvents.OnNoiseMade += HearNoise;
}

// Metoda wywoływana automatycznie, gdy zdarzy się OnNoiseMade
public void HearNoise(Vector3 noisePosition, float volume)
{
    // Jeśli hałas jest blisko, przełącz stan na Investigate
    if (GlobalPosition.DistanceTo(noisePosition) <= HearingRange)
    {
        SwitchState(State.Investigate);
    }
}
```

> **Dla studenta:** Działa to jak radio. Obiekt upadający jest stacją radiową – nadaje sygnał "Hałas!". Przeciwnik jest odbiornikiem radiowym nastrojonym na tę stację. Obiekt nie wie, kto go słucha, po prostu nadaje sygnał.

---

## Różnice: Sygnały (Godot) vs Zdarzenia (C#)

Dla studenta ważne jest zrozumienie, dlaczego w kodzie C# używamy słowa kluczowego `event` zamiast funkcji `EmitSignal`, którą widać w tutorialach do GDScript. Są to dwa różne mechanizmy realizujące ten sam cel (komunikację).

### 1. Podejście Godot (Sygnały / Signals)
W GDScript i edytorze Godot sygnały są dynamiczne. Często łączy się je "wyklikując" w inspektorze lub kodem, używając nazw tekstowych (string).
```gdscript
# Przykład w GDScript
signal noise_made(position, volume)

func make_noise():
    # Emitowanie sygnału przez podanie jego nazwy
    emit_signal("noise_made", Vector3.ZERO, 1.0)
```
*   **Zaleta:** Bardzo elastyczne, łatwe do podpięcia w edytorze wizualnym.
*   **Wada:** Podatne na literówki (np. napisanie "nois_made" nie spowoduje błędu kompilacji, gra po prostu nie zadziała).

### 2. Podejście C# (Zdarzenia / Events)
W C# używamy natywnego, "sztywnego" mechanizmu **Events** i **Delegates** (Delegatów).
```csharp
// Przykład w C# (z GameEvents.cs)
// 1. Deklaracja kształtu funkcji (Delegat) - "podpis"
public delegate void NoiseHandler(Vector3 pos, float vol);

// 2. Deklaracja zdarzenia
public static event NoiseHandler OnNoiseMade;

// 3. Wywołanie (zamiast EmitSignal)
public void MakeNoise()
{
    // Znak '?' sprawdza, czy ktoś w ogóle słucha (chroni przed błędem null)
    OnNoiseMade?.Invoke(Vector3.Zero, 1.0f);
}
```

**Dlaczego w tym projekcie wybrano C# Events?**
*   **Bezpieczeństwo Typów:** Kompilator sprawdzi, czy wysyłamy dobre dane (Vector3 i float). W przypadku sygnałów Godota błędy często wychodzą dopiero w trakcie gry.
*   **Intellisense:** Edytor kodu podpowiada dostępne zdarzenia po wpisaniu kropki.
*   **Szybkość:** Zdarzenia C# są bezpośrednimi odwołaniami do funkcji w pamięci, co jest nieco szybsze niż mechanizm wyszukiwania sygnałów po nazwie w silniku.

---

## Konfiguracja OpenXR (HTC Vive)

Projekt został skonfigurowany pod gogle **HTC Vive**. 
Kluczowa poprawka techniczna obejmowała przypisanie mapy akcji (`Action Map`) w pliku `project.godot`.

*   **Action Map:** Definiuje, co robią przyciski (np. "Trigger" to strzał, "Trackpad" to ruch).
*   **Problem:** Domyślnie Godot nie ładował konfiguracji przycisków dla Vive.
*   **Rozwiązanie:** Wymuszono ładowanie pliku `res://openxr_action_map.tres` w ustawieniach startowych OpenXR.

---
*Dokumentacja wygenerowana automatycznie na podstawie stanu projektu z dnia 26.01.2026.*