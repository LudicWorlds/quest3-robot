'Quest 3 Robot' Project
=======================

This Unity project demonstrates how a 'Meta Quest 3' VR headset can be used as the "brain" of a robot. The Quest 3 acts as both the control system and sensor array, providing spatial mapping, positional tracking, pathfinding and on-device AI speech recognition.

The robot responds to voice commands like "move to the fridge" and autonomously navigates to locations marked by spatial anchors.


Key Features
------------

- **On-Device Speech Recognition** - Local Whisper AI model (no cloud required)
- **Navigation/Pathfinding** - NavMesh-based route planning
- **Spatial Anchor System** - Persistent location markers that survive across sessions
- **Manual Override** - Direct joystick control via Quest Touch controllers


Included Assets
---------------

This project includes the following third-party components:

- **Whisper Tiny Model** - Speech recognition model from OpenAI, obtained from:
  https://huggingface.co/unity/inference-engine-whisper-tiny

- **Meta MR Utility Kit** - For spatial mapping and anchor management:
  https://assetstore.unity.com/packages/tools/integration/meta-mr-utility-kit-272450

- **Unity Inference Engine** - inference engine for running AI models on-device (formerly Sentis):
  https://docs.unity3d.com/Packages/com.unity.ai.inference@latest



Hardware Requirements
---------------------

- Meta Quest 3
- [ESP32 Development Board](https://dratek.cz/arduino-platforma/51547-esp32-devkitc-development-board-38pin.html) (DevKit C variant with WiFi)
- [L298N Motor Driver](https://dratek.cz/arduino-platforma/877-arduino-h-mustek-pro-krokovy-motor-l298n-dual-h-most-dc.html) (H-bridge module)
- [2x DC Geared Motors + wheels](https://dratek.cz/arduino-platforma/972-kolo-s-prevodovanym-motorem.html) (6V with gear reduction)
- [6x AA Battery Pack](https://dratek.cz/arduino-platforma/7497-bateriovy-box-6xaa-1.5v.html) (9V total)
- Wireless Lavalier Microphone (e.g. [Soundeus Wireless Lavalier USB-C](https://www.alza.cz/EN/soundeus-wireless-lavalier-usb-c-d9494970.htm))
- Chassis (LEGO or custom build)


License
-------

This project is licensed under the MIT License (see `LICENSE.txt`). However, it includes components that are licensed under different terms:

- **Whisper Model** - Apache License 2.0:
  https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md

- **Meta MR Utility Kit** - Oculus SDK License (see `META_SDK_LICENSE.txt`):
  https://developer.oculus.com/licenses/oculussdk/

- **Unity Inference Engine** - Unity Companion License:
  https://unity.com/legal/licenses/unity-companion-license



Software Requirements
---------------------

- **Arduino IDE** - v2.3.6 (For programming the ESP32 microcontroller)
- **Unity Editor** - Version 6000.2.14f1 (tested version)

**KNOWN ISSUE:** Currently, the Unity app will crash on startup if built using Unity version 6000.3. 

**Unity Packages:**
- **Meta MR Utility Kit** - v81.0.0
- **Inference Engine** - v2.3.0
- **Oculus XR Plugin** - v4.5.2
- **Unity AI Navigation** - For NavMesh support


Downloading This Project
------------------------

**Option A: Download Release (Easiest)**

Download the complete project ZIP from the [Releases page](https://github.com/LudicWorlds/quest3-robot/releases) - no Git LFS required.


**Option B: Clone with Git LFS**
1. Install Git LFS: https://git-lfs.github.com/
2. Clone the repository:
```bash
git lfs install
git clone https://github.com/LudicWorlds/quest3-robot.git
```



Project Setup
-------------

### 1. Hardware Setup

**Wire the Electronics:**

For complete wiring instructions, see the circuit diagram: `Documentation/quest3-robot-circuit-diagram.png`

**Program the ESP32:**
1. Open `ESP32_Code/sketch_esp32_Quest3_Robot.ino` in Arduino IDE
2. Update WiFi credentials in the code:
   ```cpp
   const char* ssid = "YOUR_WIFI_SSID";
   const char* password = "YOUR_WIFI_PASSWORD";
   ```
3. Select board: "ESP32 Dev Module"
4. Upload to ESP32 and note the IP address from Serial Monitor

**Prepare the Quest 3:**
1. Cover the proximity sensor with electrical tape (prevents auto-sleep when mounted on robot)
2. Cover the lenses (sun protection)
3. Plug in wireless microphone USB receiver


### 2. Unity Project Setup

**Open the Project:**
1. Launch Unity Hub
2. Add project from disk
3. Open with Unity 6000.2.14f1
4. Wait for package import to complete (may take several minutes)

**Configure Build Settings:**
1. Go to **File > Build Settings**
2. Select **Android** from the platform list
3. Set **Texture Compression** to **ASTC**
4. Click **Switch Platform** to make **Meta Quest** the active build target

**Configure ESP32 Connection:**
1. Open scene: `Assets/Scenes/Robot.unity`
2. In Hierarchy, select **Robot Controller** GameObject
3. In Inspector, find **ESP32_Communicator** component
4. Set **IP Address** to your ESP32's IP (from Serial Monitor)
5. Set **Port** to **3310** (default)

**Build and Deploy:**
Note: Developer Mode must be enabled on Quest 3 (via Meta Quest mobile app)

1. Connect Quest 3 to PC via USB cable
2. In Unity Build Settings, ensure **Run Device** shows "Oculus Quest 3"
4. Click **Build and Run**
5. Unity will build and automatically deploy to your headset


How to Use the App
------------------

### First-Time Setup in VR

1. Put on the Quest 3 headset and launch the application
2. Grant camera and microphone permissions when prompted
3. Use the Touch controllers to place spatial anchors around your room:
   - Point at a location (e.g., in front of your fridge)
   - Press the **Right Grip Trigger** to create an anchor
   - Use the **B button** to cycle through labels (Fridge, Sofa, Table, etc.)
   - Repeat for other locations
4. Press **Left Grip Trigger** to save all anchors

### Operating the Robot

**Voice Commands:**
1. Mount the Quest 3 on the robot chassis
2. When the robot is at rest: say "Fridge" (or other labeled location)
3. The robot will automatically navigate to the destination

**Manual Control:**
- Use the **Right Thumbstick** for direct control:
  - Push up: Move forward
  - Push down: Move backward
  - Push left: Turn left
  - Push right: Turn right
- Click **Right Thumbstick** for emergency stop

**Touch Controller Reference:**

Place destination Marker at the Selected Anchor
- **Right Index Trigger** - Set destination marker at ray position
- **Left Index Trigger** - Set selected anchor as destination
- **Right Grip Trigger** - Place spatial anchor (when pointing)
- **Left Grip Trigger** - Save all anchors 
- **A button** - Cycle through anchors
- **B button** - Cycle through labels (Fridge, Sofa, Table)
- **Y Button** - Toggle Debug Panel
- **Right Thumbstick** - Manual robot control
- **Right Thumbstick Click** - Emergency stop
- **Left Thumbstick Click** - Destroy all anchors


How It Works
------------

### System Architecture

The Quest 3 handles all high-level processing:
1. **Speech Recognition** - MicRecorder captures audio, Whisper AI transcribes to text
3. **Anchor Lookup** - Finds spatial anchor matching the location
4. **Pathfinding** - Calculates NavMesh path around obstacles
5. **Navigation** - State machine executes turning and movement actions
6. **Motor Commands** - ESP32_Communicator sends UDP packets to the ESP32

The ESP32 handles low-level motor control:
1. Listens for UDP packets on port 3310
2. Decodes 4-bit motor commands
3. Controls L298N motor driver via GPIO pins
4. Executes forward, backward, left, right, and stop actions


Known Issues
------------

### Hardware Limitations

1. **Proximity Sensor Workaround** - Covering the sensor with tape is not ideal. Future versions may implement software-based disabling.

2. **External Microphone Strongly Recommended** - Built-in Quest mic has poor range when headset is mounted on robot. Voice commands may not be detected without external wireless mic.

3. **Lens Sun Damage Risk** - Quest 3 lenses can be damaged by direct sunlight acting as a magnifying glass. Always cover the lenses when Quest 3 is mounted on the robot chassis.


### Software Limitations

1. **Whisper Processing Lag** - On-device Tiny model takes a few seconds to transcribe speech, especially on first run. This is expected behavior due to Quest 3's computational limits.

2. **Limited Vocabulary** - Currently recognizes only pre-defined location keywords. Custom locations can be added by editing the voice recognition code.

3. **Static Obstacle Detection** - Robot uses pre-calculated NavMesh paths. I may add real-time obstacle avoidance using the Quest's depth sensors in a future version.


### Navigation Quirks

1. **Settling Delays** - Robot pauses briefly after each turn/move to allow physical settling. This ensures accurate heading for next action but adds time.

2. **Turn Accuracy** - Currently the robot can overshoot when turning to align itself with it's next destination.


Not for Commercial Use
----------------------

Given the limitations listed above, this project is currently intended for:
- **Experimental / Educational purposes** - Learning robotics, VR, and AI integration
- **Maker projects** - Hobbyist robotics exploration

This is NOT recommended for:
- Commercial deployment
- Mission-critical applications
- Environments requiring safety certifications
- Production robotics systems


Disclaimer
----------

This Unity project and associated hardware instructions are provided "as is" without warranty of any kind. Use at your own risk. The authors are not responsible for any damage to equipment, property, or personal injury resulting from the use of this project.

**Important Safety Notes:**
- This project involves electronics and moving robotics
- Quest 3 lenses can be permanently damaged by direct sunlight
- Moving robots can cause injury or property damage if not properly controlled
- Always supervise the robot during operation
- Keep away from stairs, water, and fragile objects


Credits & Attribution
---------------------

- **Meta** - Quest 3 hardware and MR Utility Kit
- **OpenAI** - Whisper speech recognition model
- **Unity Technologies** - Game Engine + Unity Inference Engine
- **Arduino/Espressif** - ESP32 platform and libraries