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
- `/tag set <setting name> [value]`: Read or change the value of a setting. See a list of settings below.
- `/tag preset <preset name>`: Warp all players to the given preset that is defined in the preset file. See below on details about presets.
- `/tag auto`: Toggle whether the game will be handled automatically. See below on details about game automation.

### Settings
These are the server settings that can be changed with the 'set' sub-command described above.
- `countdown_time`: The time in seconds the countdown will take before starting the game.
- `warp_time`: The time after warping players before starting the automatic game in seconds.
- `post_game_time`: The time after an automatic game has ended to wait before starting a new one in seconds.
- `max_game_time`: The maximum time an automatic game can last before ending it in seconds.
- `max_games_on_preset`: The number of games that will be played on one preset before switching.
- `auto`: Whether game automation is enabled.

### Automation
The Tag games can be played fully automatic by setting the server setting `auto` to True.
Automatic games will be started once at least 3 players are online on the server.
Playable areas are defined by presets, which are configurations that denote where to warp players to and which transitions are then restricted.
This will effectively lock players in a certain area in which the game can be played.
These presets can be defined in a file next to the `HKMPTag.dll` file, named `transition_presets.json`.
For an example of the format these presets should have, see the example file [`transition_presets_example.json`](https://github.com/Extremelyd1/HKMP-Tag/blob/master/transition_presets_example.json).
The fields have the following meaning:
- `name`: The name of the preset used for logging and for manually warping players to.
- `warp_scene`: The name of the scene to warp players to (see [the transition JSON](https://github.com/Extremelyd1/HKMP-Tag/blob/master/Resource/transitions.json) for a full list of possible options).
- `warp_transition`: The name of the transition in the scene to warp players to.
- `min_players`: The minimum (inclusive) number of players for this preset to be used. This field is optional.
- `max_players`: The maximum (inclusive) number of players for this preset to be used. This field is optional.
- `transitions`: A list of scene names that have transition restrictions associated with them.
Each entry in this list (denoting a scene name) contains a list of strings that denote a transition name that should be inaccessible for players.