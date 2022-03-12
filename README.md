# HKMP Tag
The Tag game-mode implemented as an addon for the HKMP API.

## How it works
When the game is started a random player will be selected as the initial "infected".
They will get a different skin and be placed on a different team.
The task of this player is to infect all other non-infected players.

The infected players will be equipped with Nailmaster's Glory, while the non-infected players
will be equipped with Wayward Compass and Kingsoul. The non-infected players will also have access
to the Vengeful Spirit ability to keep the infected players at bay.

Once a non-infected player is hit (either by another player or by a hazard) they will automatically
join the opposing team, change skin and get the corresponding equipment.

The game ends whenever there is only one player left uninfected, which is deemed the winner.

## Install
The addon can be installed by dropping the `HKMPTag.dll` (which can be found on the
[releases page](https://github.com/Extremelyd1/HKMP-Tag/releases)) in the `HKMP` folder in your mods folder.
The mods folder can be found in your steam installation (Beware that these are the default locations.
Your install may be in a different location):
- **Windows**: `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Mods\HKMP`
- **Mac**: `~/Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app/`,
then click "open package contents" and `content -> resources -> data -> managed -> mods -> HKMP`
- **Linux**: `~/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Mods/HKMP`

## Usage
The following commands can be used to control the game-mode. Note that these commands require the player
executing them to be authorized.
- `/tag start [number of infected]`: Start the game with the given number of initial infected.
- `/tag stop`: Will forcefully stop the game if it is in progress.

Alternatively, authorized players can use the mod menu to start the game, select how many initial
infected should be chosen and stop the game.