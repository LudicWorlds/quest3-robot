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
    [SerializeField] private string _esp32IP = "192.168.0.18"; // Update with your ESP32's IP
    [SerializeField] private int _udpPort = 3310;
    [SerializeField] private GameObject _connectionIndicator; // Optional: 3D object to show connection status

    private UdpClient _udpClient;
    private IPEndPoint _esp32EndPoint;

    // Thread-safe response handling
    private string _pendingResponse = "";
    private bool _hasResponse = false;
    private readonly object _responseLock = new object();

    // Command repetition for reliability
    private float _lastCommandSentTime = 0f;
    private int _commandRepeatCount = 0;
    private const float COMMAND_REPEAT_INTERVAL = 0.1f; // 100ms between repeats
    private const int MAX_COMMAND_REPEATS = 3; // Send command up to 3 times

    private byte _lastValue = 255; // Use 255 so first comparison always sends - equals 0b11111111
    private byte _command = 0;

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
        SendCommandViaWiFi(GetCommandGivenAction((int)RobotAction.Stop));
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
                command |= 1 << 1; // Left motor reverse (Bit 1)
                command |= 1 << 2; // Right motor forward (Bit 2)
                break;

            case 3: // Right turn: Left motor clockwise, Right motor anticlockwise
                command |= 1 << 0; // Left motor forward (Bit 0)
                command |= 1 << 3; // Right motor reverse (Bit 3)
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

    private void SendCommandViaWiFi(byte command)
    {
        try
        {
            byte[] data = { command };
            _udpClient.Send(data, data.Length, _esp32EndPoint);

            Debug.Log($"- Sent command: {command}");
            _txComCount++;
            DebugPanel.TxComCount(_txComCount);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send command: {e.Message}");
            UpdateConnectionIndicator(false);
        }
    }

    private void ProcessCommand()
    {
        // Send command if it changed or repeat current command for reliability
        if (_command != _lastValue)
        {
            // New command - send immediately and reset repeat counter
            SendCommandViaWiFi(_command);
            _lastCommandSentTime = Time.time;
            _commandRepeatCount = 1;
        }
        else if (_commandRepeatCount < MAX_COMMAND_REPEATS &&
                 Time.time - _lastCommandSentTime >= COMMAND_REPEAT_INTERVAL)
        {
            // Repeat current command for reliability (including stop commands)
            SendCommandViaWiFi(_command);
            _lastCommandSentTime = Time.time;
            _commandRepeatCount++;
        }

        _lastValue = _command;
    }


    private void ProcessResponse(string response)
    {
        UpdateConnectionIndicator(true);

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
            lock (_responseLock)
            {
                response = _pendingResponse;
                _hasResponse = false;
            }

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

            Debug.Log($"Received response: {response}");
            _rxComCount++;
            DebugPanel.RxComCount(_rxComCount);

            // Thread-safe storage of response
            lock (_responseLock)
            {
                _pendingResponse = response;
                _hasResponse = true;
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
