using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using LudicWorlds;


public enum RobotAction
{
    Stop = 0,
    Forward = 1,
    TurnLeft = 2,
    TurnRight = 3,
    Backward = 4
}


public class ESP32_Communicator : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private string _esp32IP = "192.168.0.164"; //"192.168.0.17"; // Update with your ESP32's IP
    [SerializeField] private int _udpPort = 3310;
    [SerializeField] private GameObject _connectionIndicator; // Optional: 3D object to show connection status

    [Header("Chassis")]
    [Tooltip("Tick for a chassis with the drive wheels at the front (swaps the Left/Right turn bits). Untick for the original build with the drive wheels at the back.")]
    [SerializeField] private bool _driveWheelsAtFront = true;

    private UdpClient _udpClient;
    private IPEndPoint _esp32EndPoint;

    // Thread-safe response handling
    private string _pendingResponse = "";
    private bool _hasResponse = false;
    private readonly object _responseLock = new object();

    // MOTOR-FIX 2 (heartbeat): replaces the optional command repetition (the _repeatCommands
    // toggle, which was off in the scene, plus _commandRepeatCount, COMMAND_REPEAT_INTERVAL and
    // MAX_COMMAND_REPEATS). The ESP32 now brakes if it hears nothing for COMMAND_TIMEOUT
    // (500 ms, in Quest3Robot.ino), so while the robot is moving the current command is resent
    // every HEARTBEAT_INTERVAL. A Stop is resent STOP_REPEATS more times in case a packet is
    // lost, then the link goes quiet. Keep HEARTBEAT_INTERVAL well below COMMAND_TIMEOUT.
    // The repetition is no longer optional: with it off, the failsafe would stop the robot
    // every 0.5 s.
    private const float HEARTBEAT_INTERVAL = 0.1f; // 100 ms between resends
    private const int STOP_REPEATS = 2; // Extra Stop sends after the first one

    private float _lastCommandSentTime = 0f;
    private int _stopRepeatsLeft = 0; // MOTOR-FIX 2: Stop resends still to do

    private byte _lastValue = 255; // Use 255 so first comparison always sends - equals 0b11111111
    private byte _command = 0;
    private byte _sequenceNumber = 0; // Wraps 0-255 for packet loss detection

    public byte Command
    {
        get { return _command; }
    }


    private uint _rxComCount = 0;
    private uint _txComCount = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        try
        {
            // Initialize UDP client
            _udpClient = new UdpClient();
            _esp32EndPoint = new IPEndPoint(IPAddress.Parse(_esp32IP), _udpPort);

            // Initial visual feedback
            UpdateConnectionIndicator(false);

            // Start listening ONCE - continuous loop from here
            _udpClient.BeginReceive(OnReceiveResponse, null);

            // Test connection
            SendCommandViaWiFi(0);

            Debug.Log($"ESP32 LED Controller initialized. Target: {_esp32IP}:{_udpPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize UDP client: {e.Message}");
        }
    }

    private void Update()
    {
        CheckForPendingResponses();
    }

    // Update is called once per frame
    private void LateUpdate()
    {
        ProcessCommand();
    }

    public void EmergencyStop()
    {
        // MOTOR-FIX 2 (heartbeat): also make Stop the current command. This used to send a Stop
        // packet and leave _command unchanged, which was harmless while commands were only sent
        // when they changed. With the heartbeat, ProcessCommand would resend the old command
        // (e.g. Forward from Moving_NavState) on the next frame and restart the robot.
        _command = GetCommandGivenAction((int)RobotAction.Stop);
        SendCommandViaWiFi(_command);
        _lastValue = _command;
        _lastCommandSentTime = Time.time;
        _stopRepeatsLeft = STOP_REPEATS; // Resend the Stop like any other Stop
    }

    public void SetCommandGivenAction(RobotAction action)
    {
        _command = GetCommandGivenAction((int)action);
    }

    private byte GetCommandGivenAction(int action)
    {
        byte command = 0;

        switch (action)
        {
            case 0: // Stop
                command = 0; // No bits set = stop
                break;

            case 1: // Forward: Both motors clockwise
                command |= 1 << 0; // Left motor forward (Bit 0)
                command |= 1 << 2; // Right motor forward (Bit 2)
                break;

            case 2: // Left turn: Left motor anticlockwise, Right motor clockwise

                if (_driveWheelsAtFront)
                {
                    // *** Drive motors at front, hence switching direction bits ***
                    command |= 1 << 0; // Left motor forward (Bit 0)
                    command |= 1 << 3; // Right motor reverse (Bit 3)
                }
                else
                {
                    command |= 1 << 1; // Left motor reverse (Bit 1)
                    command |= 1 << 2; // Right motor forward (Bit 2)
                }
                break;

            case 3: // Right turn: Left motor clockwise, Right motor anticlockwise

                if (_driveWheelsAtFront)
                {
                    // *** Drive motors at front, hence switching direction bits ***
                    command |= 1 << 1; // Left motor reverse (Bit 1)
                    command |= 1 << 2; // Right motor forward (Bit 2)
                }
                else
                {
                    command |= 1 << 0; // Left motor forward (Bit 0)
                    command |= 1 << 3; // Right motor reverse (Bit 3)
                }
                break;

            case 4: // Backward: Both motors anticlockwise
                command |= 1 << 1; // Left motor reverse (Bit 1)
                command |= 1 << 3; // Right motor reverse (Bit 3)
                break;

            default:
                Debug.LogWarning($"Unknown action: {action}, sending stop command");
                command = 0;
                break;
        }

        return command;
    }



    private void UpdateConnectionIndicator(bool connected)
    {
        if (_connectionIndicator != null)
        {
            // Change color based on connection status
            Renderer renderer = _connectionIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = connected ? Color.green : Color.red;
            }
        }
    }

    // MOTOR-FIX 4: isRepeat marks heartbeat and Stop resends, which are not logged
    private void SendCommandViaWiFi(byte command, bool isRepeat = false)
    {
        try
        {
            byte[] data = { _sequenceNumber, command };
            _udpClient.Send(data, data.Length, _esp32EndPoint);

            // MOTOR-FIX 4 (timestamps): new commands are logged with the time since app start, so
            // they can be lined up against the ESP32's Serial log. Repeats are not logged:
            // DebugPanel mirrors every log line, and 10 lines a second while driving would bury it.
            // (Was: Debug.Log($"- Sent seq:{_sequenceNumber} command:{command}") on every send.)
            if (!isRepeat)
            {
                Debug.Log($"[{Time.realtimeSinceStartup:F3} s] Sent seq:{_sequenceNumber} command:{command}");
            }
            _sequenceNumber++; // Wraps naturally at 255->0
            _txComCount++;
            DebugPanel.TxComCount(_txComCount);
        }
        catch (Exception e)
        {
            // MOTOR-FIX 4: only logged for new commands, so a lost link can't flood the log 10x a second
            if (!isRepeat)
            {
                Debug.LogError($"Failed to send command: {e.Message}");
            }
            UpdateConnectionIndicator(false);
        }
    }

    private void ProcessCommand()
    {
        if (_command != _lastValue)
        {
            // New command - send immediately
            SendCommandViaWiFi(_command);
            _lastCommandSentTime = Time.time;
            // MOTOR-FIX 2: a new Stop (command 0) is followed by STOP_REPEATS resends
            _stopRepeatsLeft = (_command == 0) ? STOP_REPEATS : 0;
        }
        else if (Time.time - _lastCommandSentTime >= HEARTBEAT_INTERVAL)
        {
            // MOTOR-FIX 2 (heartbeat): the command hasn't changed since the last send
            if (_command != 0)
            {
                // Moving: resend so the ESP32's failsafe knows the link is still alive
                SendCommandViaWiFi(_command, true);
                _lastCommandSentTime = Time.time;
            }
            else if (_stopRepeatsLeft > 0)
            {
                // Stopped: resend Stop a couple of times in case a packet was lost, then go quiet
                SendCommandViaWiFi(_command, true);
                _lastCommandSentTime = Time.time;
                _stopRepeatsLeft--;
            }
        }

        _lastValue = _command;
    }


    private void ProcessResponse(string response)
    {
        UpdateConnectionIndicator(true);

        // MOTOR-FIX 4: the ESP32 acknowledges every packet with "OK". With the heartbeat that is
        // about 10 a second while driving, so acks are only counted (RX on the DebugPanel) and no
        // longer logged as "Response: OK".
        if (response == "OK")
        {
            return;
        }

        // Provide different haptic feedback based on LED state
        if (response == "LED_ON")
        {
            Debug.Log("LED is now ON");
            // Strong pulse for ON
            //ProvideHapticFeedback(OVRInput.Controller.RTouch, 0.8f);
        }
        else if (response == "LED_OFF")
        {
            Debug.Log("LED is now OFF");
            // Weak pulse for OFF
            //ProvideHapticFeedback(OVRInput.Controller.RTouch, 0.2f);
        }
        else
        {
            Debug.Log($"Response: {response}");
        }
    }

    private void CheckForPendingResponses()
    {
        // Check for pending responses from background thread
        if (_hasResponse)
        {
            string response;
            uint rxCount; // MOTOR-FIX 4
            lock (_responseLock)
            {
                response = _pendingResponse;
                rxCount = _rxComCount; // MOTOR-FIX 4
                _hasResponse = false;
            }

            // MOTOR-FIX 4: the RX counter is now shown here, on the main thread. It used to be set
            // from the background receive thread, but DebugPanel.RxComCount updates a UI Text, and
            // Unity UI should only be touched from the main thread.
            DebugPanel.RxComCount(rxCount);

            // Process response on main thread
            ProcessResponse(response);
        }
    }


    protected void OnReceiveResponse(IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = _udpClient.EndReceive(result, ref remoteEP);
            string response = Encoding.UTF8.GetString(data);

            // MOTOR-FIX 4: removed from this background thread:
            //   Debug.Log($"Received response: {response}");  - logged every ack, ~10/s with the heartbeat
            //   DebugPanel.RxComCount(_rxComCount);            - UI update, now done in CheckForPendingResponses
            // The counter is now incremented inside the lock below.

            // Thread-safe storage of response
            lock (_responseLock)
            {
                _pendingResponse = response;
                _hasResponse = true;
                _rxComCount++; // MOTOR-FIX 4: read on the main thread under the same lock
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error receiving response: {e.Message}");
        }
        finally
        {
            // Only re-arm if not disposed
            if (_udpClient != null)
            {
                try
                {
                    // RE-ARM: Start listening for the NEXT packet
                    _udpClient.BeginReceive(OnReceiveResponse, null);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to re-arm UDP listener: {e.Message}");
                }
            }
        }
    }

    private void OnDestroy()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
    }
}
