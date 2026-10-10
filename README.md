# Puff — Quest 3 mixed reality

Puff is a native Unity 6 mixed reality pet with an optional tabletop fight. Place her on a scanned desk or floor in your real room. She walks, naps, plays, eats, drinks, sings when she catches the microphone, and reacts to touch. Fight Mode brings in the creepy Bean Revenant through a warp gate.

## Install and play

Build with **Puff → Configure Quest 3 → Build Quest APK**, or install `Builds/PuffQuest.apk` using SideQuest. The Android package ID remains `com.tabletopmayhem.quest`, so this updates the existing app and keeps its saved pets. Allow Spatial Data and complete Quest Space Setup with the surfaces you want to use. At the title, press any button to open five pet slots. Choose with the left stick and press A to play or create a pet.

For first placement, aim at a scanned horizontal surface. A gold marker shows the target. Hold right trigger to see the faint landing ray, then release to place Puff. A also places her at a valid marker. After you have placed a slot once, a saved spatial anchor attempts to restore her home on the next launch; if relocalization fails, use the normal placement marker. Tap Y to choose a new surface, hold Y to return to the pet slots, or press B to reset Puff to her home.

Reach behind either shoulder and hold grip to grab the backpack. Pull it in front of you; its inventory opens as it comes into view. Release grip to leave it where you want, or grip its handle to move it again. Use right grip to take and throw food, water, or the microphone; the microphone starts her song. Hold the right glove fingertip briefly near Puff's head while pressing trigger to pet her. Left grip picks her up. Press A in pet mode for the optional controls card.

Press **left X** after placement to enter or leave Fight Mode. The left stick moves Puff. **Right A punches**, **right B kicks**, **left trigger jumps**, and **right trigger shields**; the blue shield meter drains and refills between blocks. Press **left grip** close to the rival to grab it, then release grip to throw it. Punches chain into a three-hit combo, kicks launch the rival, and a throw over the tabletop edge scores a ring-out. Pull the same backpack forward from behind either shoulder: throw a bomb and it explodes after five seconds, or hold and release trigger with the rocket launcher to charge a slingshot shot. A trajectory arc previews the launch. The Bean Revenant telegraphs slime shots and fights back. Fight health is separate from Puff's pet life.

The small HUD tracks hunger, thirst, boredom, energy, health, daily care, and age. The care loop asks for food, water, song, and a cuddle; each completed round raises bond. Each UTC day has one rotating care goal, and finishing it awards a star and a small health and mood boost. Pet needs progress in real time, including while the app is closed. Neglect can end a life in a few days; consistent care can extend it well past fourteen days. Each of the five slots saves independently.

## Development

Open the project in Unity **6000.0.66f2**. In the editor, Tab opens the pet menu, P pets Puff, M plays her song, H toggles help, 1–5 give food or water, and F toggles Fight Mode. In Fight Mode, WASD moves, J punches, K kicks, left Shift jumps, Space shields, holding G grabs and releasing G throws, 6 throws a bomb, and 7 launches a rocket.

Run `./Tests/run.sh` for the engine-independent lifecycle checks. `TabletopMayhem.Editor.PetVisualCheck.Capture` exercises the title, pet care, items, stable placement, and fight setup in Unity. See [Quest device testing](Documentation/DEVICE_TESTING.md) for the headset checks. The editable character source is `Art/Blender/Puff.blend`; the runtime model is `Assets/TabletopMayhem/Resources/Fighters/Puff.fbx`. The supplied song remains in `Assets/TabletopMayhem/Resources/Audio/PuffSong.mp3`.

The game uses Quest's saved spatial scan and spatial anchors. It does not access raw passthrough camera frames. Surface collisions depend on the accuracy of the saved room scan.
