#include <WiFi.h>
#include <WiFiUdp.h>
#include <LiquidCrystal.h>
#include "D:/Misc/wifi_credentials.h"


// Network configuration - Access Point mode
//const char* ssid = "YOUR_WIFI_SSID";
//const char* password = "YOUR_WIFI_PASSWORD";

WiFiUDP udp;
const int udpPort = 3310;

// Motor control pins using GPIO 16-19 as requested
const int motorPins[] = {16, 17, 18, 19};  // GPIO16-19 for motor direction
const int ENA = 26;  // Left motor speed (PWM) - GPIO26 to ENA on L298N
const int ENB = 25;  // Right motor speed (PWM) - GPIO25 to ENB on L298N
const int ledPin = 4;

// LCD pin assignments (4-bit mode)
const int LCD_RS = 13;
const int LCD_E  = 14;
const int LCD_D4 = 27;
const int LCD_D5 = 32;
const int LCD_D6 = 33;
const int LCD_D7 = 23;

// Initialise the LiquidCrystal library
// Parameters: RS, E, D4, D5, D6, D7
LiquidCrystal lcd(LCD_RS, LCD_E, LCD_D4, LCD_D5, LCD_D6, LCD_D7);

// Test patterns for motor testing
const byte testValues[] = {
    0b00000000,   // All off
    0b00000001,   // Bit 0 → GPIO16 (left forward)
    0b00000010,   // Bit 1 → GPIO17 (left reverse)
    0b00000100,   // Bit 2 → GPIO18 (right forward)
    0b00001000    // Bit 3 → GPIO19 (right reverse)
};

// PWM configuration for ESP32
const int pwmFreq = 30000;     // 30kHz PWM frequency
const int pwmResolution = 8;   // 8-bit resolution (0-255)

// Motor speed values (0-255) - adjust leftMotorSpeed to compensate for weaker left motor
int leftMotorSpeed = 220;   // Left motor (ENA) - slightly higher to correct leftward veer
int rightMotorSpeed = 220;  // Right motor (ENB)

// MOTOR-FIX 3 (active braking): PWM value written to a stopped motor's enable pin.
// Enable HIGH with both direction inputs LOW is the L298N's "fast motor stop": the motor's
// terminals are shorted through the driver, so the wheels stop at once instead of coasting.
// Previously a stopped motor got 0 here (enable LOW = "free running" stop, i.e. coasting).
const int BRAKE_DUTY = 255;

// Timing variables
unsigned long lastCommandTime = 0;
// MOTOR-FIX 2 (failsafe): was 1000 ms and never checked. Now enforced by CheckCommandTimeout():
// if the motors are running and no packet has arrived for this long, the robot brakes.
// Unity resends the current command every 100 ms while moving (HEARTBEAT_INTERVAL in
// ESP32_Communicator.cs), so 500 ms tolerates about 4 lost packets. Keep the two in step.
const unsigned long COMMAND_TIMEOUT = 500; // Stop if no command for 0.5 seconds

// MOTOR-FIX 2 (failsafe): the command currently applied to the motors. Lets loop() tell a
// heartbeat (the same command again) from a new command, and tells the failsafe whether the
// motors are running.
byte currentCommand = 0;

// Packet loss tracking
byte expectedSequence = 0;
unsigned long totalReceived = 0;
unsigned long totalDropped = 0;
unsigned long lastLcdUpdateTime = 0;
const unsigned long LCD_UPDATE_INTERVAL = 1000; // Update LCD every 1 second


void setup() {

    Serial.begin(115200);
    while (!Serial) { delay(10); } // Wait for serial
    Serial.println("\n=== ESP32 Turtle Robot Starting ===");
    Serial.println("Dual PWM pin configuration (GPIO26 -> ENA left, GPIO25 -> ENB right)");

    initLCD();

    
    pinMode(ledPin, OUTPUT);
    digitalWrite(ledPin, LOW); 
    
    // Set all motor pins as outputs
    for (int i = 0; i < 4; i++) {
        pinMode(motorPins[i], OUTPUT);
        digitalWrite(motorPins[i], LOW);  // Ensure motors are off at startup
    }
    
    // Configure PWM for motor speed control - NEW API for ESP32 Core 3.0+
    ledcAttach(ENA, pwmFreq, pwmResolution);
    ledcAttach(ENB, pwmFreq, pwmResolution);

    // Set initial PWM to 0 (Motors Off)
    ledcWrite(ENA, 0);
    ledcWrite(ENB, 0);

    setupWiFiClient();

    // Start UDP
    udp.begin(udpPort);
    Serial.printf("UDP server started on port %d\n", udpPort);

    //Flash the Green LED 3 times
    for (int i = 0; i < 3; i++)
    {
        digitalWrite(ledPin, HIGH);
        delay(200);
        digitalWrite(ledPin, LOW);
        delay(100);
    }
    
    // Test motors on startup
    TestMotors();
    
    Serial.println("\nRobot ready! Send commands via UDP.");
    Serial.println("Command format: Single byte with bits controlling motors");
    Serial.println("Bit 0: Left Forward, Bit 1: Left Reverse");
    Serial.println("Bit 2: Right Forward, Bit 3: Right Reverse");
}


void initLCD() {
     // Initialise the LCD (16 columns, 2 rows)
    lcd.begin(16, 2);
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("Robot Starting...");
}

void setupWiFiClient() {
  // Connect to existing network
  WiFi.mode(WIFI_STA);
  // MOTOR-FIX 1 (WiFi sleep off): the Arduino core turns on WiFi "modem sleep" by default.
  // The radio then sleeps between router beacons, and the router holds incoming packets until
  // it wakes, so motor commands arrived late and in bunches (a quick turn + stop could arrive
  // together, and a stop could arrive well after it was sent). Keeping the radio awake makes
  // commands arrive when they are sent, at the cost of slightly higher current draw.
  WiFi.setSleep(false);
  WiFi.begin(ssid, password);
  
  Serial.print("Connecting to WiFi");
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    attempts++;
  }
  
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\nWiFi connected!");
    Serial.print("IP address: ");
    Serial.println(WiFi.localIP());
    Serial.print("Signal strength (RSSI): ");
    Serial.print(WiFi.RSSI());
    Serial.println(" dBm");
    // MOTOR-FIX 1: confirm in the Serial Monitor that power saving is really off
    Serial.printf("WiFi power saving: %s\n", WiFi.getSleep() == WIFI_PS_NONE ? "OFF" : "ON");

    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("WiFi Connected!");
    lcd.setCursor(0, 1);
    lcd.print(WiFi.localIP());
  } else {
    Serial.println("\nFailed to connect to WiFi!");
    Serial.println("Restarting in 5 seconds...");

    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("WiFi FAILED!");
    lcd.setCursor(0, 1);
    lcd.print("Restarting...");

    delay(5000);
    ESP.restart();
  }
}


void loop() {

    // MOTOR-FIX 2 (failsafe): checked first, before anything below that can return early or
    // wait, so the motors stop even when packets have stopped arriving.
    CheckCommandTimeout();

    // Check WiFi connection (client mode only)
    if (WiFi.getMode() == WIFI_STA && WiFi.status() != WL_CONNECTED) {
        PrintTimestamp(); // MOTOR-FIX 4
        Serial.println("WiFi connection lost! Attempting reconnect...");
        // MOTOR-FIX 2 (failsafe): stop the motors straight away. This branch returns early and
        // waits 1 s per reconnect attempt, so without this the motors kept running while offline.
        if (currentCommand != 0) {
            ApplyCommand(0);
        }
        WiFi.reconnect();
        digitalWrite(ledPin, LOW);
        delay(1000);
        return;
    }

    // Check for UDP packets (expecting 2 bytes: [sequence, command])
    int packetSize = udp.parsePacket();
    if (packetSize >= 2) {
        byte packet[2];
        udp.read(packet, 2);
        byte sequence = packet[0];
        byte command = packet[1];

        // Get sender info for response
        IPAddress senderIP = udp.remoteIP();
        int senderPort = udp.remotePort();

        // Track packet loss
        if (totalReceived > 0 && sequence != expectedSequence) {
            // Calculate gap (handles wrapping)
            byte gap = sequence - expectedSequence; // unsigned wrapping handles 255->0
            totalDropped += gap;
            PrintTimestamp(); // MOTOR-FIX 4
            Serial.printf("*** DROPPED %d packet(s)! Expected seq:%d, got seq:%d ***\n",
                gap, expectedSequence, sequence);
        }
        expectedSequence = sequence + 1; // Will wrap naturally
        totalReceived++;

        // MOTOR-FIX 2 (heartbeat): while the robot moves, Unity resends the current command every
        // 100 ms. A packet that repeats the command already applied is a heartbeat: it only resets
        // the failsafe timer below, and the motor pins and PWM are left alone.
        bool isNewCommand = (command != currentCommand);

        // MOTOR-FIX 4 (timestamps): each packet line now starts with the time since boot and ends
        // with "new" or "repeat", so the gap between a turn and a stop can be read off the log.
        PrintTimestamp();
        Serial.printf("Seq:%d Cmd:0x%02X (", sequence, command);
        Serial.print(command, BIN);
        Serial.printf(") from %s:%d %s\n", senderIP.toString().c_str(), senderPort,
            isNewCommand ? "new" : "repeat");

        // Apply the command (MOTOR-FIX 2: only when it differs from what the motors are doing)
        if (isNewCommand) {
            ApplyCommand(command);
        }
        // MOTOR-FIX 2 (failsafe): every packet, new or repeat, keeps the failsafe from firing
        lastCommandTime = millis();

        // Send acknowledgment
        udp.beginPacket(senderIP, senderPort);
        udp.print("OK");
        udp.endPacket();

        digitalWrite(ledPin, HIGH);
    }

    // Periodically update LCD with packet stats
    if (millis() - lastLcdUpdateTime >= LCD_UPDATE_INTERVAL && totalReceived > 0) {
        updateLcdStats();
        lastLcdUpdateTime = millis();
    }
      
    // Optional: Check for serial commands for debugging
    if (Serial.available() > 0) {
        byte command = Serial.read();
        PrintTimestamp(); // MOTOR-FIX 4
        Serial.printf("Serial command: 0x%02X\n", command);
        ApplyCommand(command);
        // MOTOR-FIX 2 (failsafe): a serial command counts as a command too, so it runs for
        // COMMAND_TIMEOUT and then the failsafe brakes the motors.
        lastCommandTime = millis();
    }
}


// MOTOR-FIX 2 (failsafe): new function. Brakes the motors if they are running and no command
// has arrived for COMMAND_TIMEOUT ms. This covers a lost Stop packet, WiFi dropping out, and
// the headset app pausing or crashing (its heartbeat stops). Does nothing while stopped.
void CheckCommandTimeout() {
    bool motorsRunning = (currentCommand & 0b00001111) != 0;   // any direction bit set
    unsigned long silence = millis() - lastCommandTime;

    if (motorsRunning && silence > COMMAND_TIMEOUT) {
        PrintTimestamp(); // MOTOR-FIX 4
        Serial.printf("FAILSAFE: no command for %lu ms - braking\n", silence);
        ApplyCommand(0);
    }
}


// MOTOR-FIX 4 (timestamps): new function. Prints "[    1234 ms] " (milliseconds since boot) at
// the start of a Serial log line, so the timing of commands can be read off the Serial Monitor.
void PrintTimestamp() {
    Serial.printf("[%8lu ms] ", millis());
}


void TestMotors() {
    Serial.println("\n=== Testing Motors ===");
    
    for (int i = 0; i < 5; i++) {
        byte value = testValues[i];
        Serial.print("Testing motor control with value: 0b");
        Serial.println(value, BIN);
        
        // Apply the command
        ApplyCommand(value);
        
        delay(500); // Wait 1 second before next step
    }
    
    // Turn everything off at the end (MOTOR-FIX 3: this now leaves both motors braked)
    ApplyCommand(0x00);
    
    Serial.println("Motor test complete!\n");
}


void ApplyCommand(byte command) {
    // MOTOR-FIX 2 (failsafe): remember what the motors are doing (see CheckCommandTimeout)
    currentCommand = command;

    // Apply each bit to its corresponding motor pin
    for (int i = 0; i < 4; i++) {
        bool pinState = (command >> i) & 0x01;
        digitalWrite(motorPins[i], pinState ? HIGH : LOW);
    }
    
    // Apply PWM speeds
    ApplyPWM(command);
}


void ApplyPWM(byte command) {
    // Check if each motor is active (any of its direction bits set)
    bool leftActive = (command & 0b00000011);   // Bits 0-1: left motor
    bool rightActive = (command & 0b00001100);   // Bits 2-3: right motor

    // MOTOR-FIX 3 (active braking): a stopped motor now gets BRAKE_DUTY (enable HIGH) instead of
    // 0 (enable LOW). Its direction inputs are both LOW (set in ApplyCommand), so the L298N brakes
    // the motor instead of letting it coast to a stop.
    ledcWrite(ENA, leftActive ? leftMotorSpeed : BRAKE_DUTY);
    ledcWrite(ENB, rightActive ? rightMotorSpeed : BRAKE_DUTY);

    // MOTOR-FIX 4: timestamped. MOTOR-FIX 3: a braked motor is logged as BRAKE instead of 0.
    PrintTimestamp();
    Serial.print("PWM set to: L=");
    if (leftActive) Serial.print(leftMotorSpeed); else Serial.print("BRAKE");
    Serial.print(" R=");
    if (rightActive) Serial.println(rightMotorSpeed); else Serial.println("BRAKE");
}

void updateLcdStats() {
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("Rx:");
    lcd.print(totalReceived);
    lcd.print(" Drop:");
    lcd.print(totalDropped);
    lcd.setCursor(0, 1);
    lcd.print("Loss:");
    float lossPercent = (totalDropped * 100.0) / (totalReceived + totalDropped);
    lcd.print(lossPercent, 1);
    lcd.print("%");
}

// Additional helper functions for different movement commands
void moveForward() {
    ApplyCommand(0b00000101);  // Left forward + Right forward
}

void moveBackward() {
    ApplyCommand(0b00001010);  // Left reverse + Right reverse
}

void turnLeft() {
    ApplyCommand(0b00001001);  // Left reverse + Right forward
}

void turnRight() {
    ApplyCommand(0b00000110);  // Left forward + Right reverse
}

void stopMotors() {
    ApplyCommand(0b00000000);  // All stop
}
