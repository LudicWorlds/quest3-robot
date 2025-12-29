#include <WiFi.h>
#include <WiFiUdp.h>

// Network configuration - Access Point mode
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";

WiFiUDP udp;
const int udpPort = 3310;

// Motor control pins using GPIO 16-19 as requested
const int motorPins[] = {16, 17, 18, 19};  // GPIO16-19 for motor direction
const int ENA = 25;  // Shared motor speed (PWM) - connected to both ENA and ENB on L298N
const int ledPin = 4;

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

// Motor speed value (0-255)
int sharedMotorSpeed = 180; // Single speed for both motors

// Timing variables
unsigned long lastCommandTime = 0;
const unsigned long COMMAND_TIMEOUT = 1000; // Stop if no command for 1 second


void setup() {

    Serial.begin(115200);
    while (!Serial) { delay(10); } // Wait for serial
    Serial.println("\n=== ESP32 Turtle Robot Starting ===");
    Serial.println("Single PWM pin configuration (GPIO25 -> both ENA and ENB)");

    pinMode(ledPin, OUTPUT);
    digitalWrite(ledPin, LOW); 
    
    // Set all motor pins as outputs
    for (int i = 0; i < 4; i++) {
        pinMode(motorPins[i], OUTPUT);
        digitalWrite(motorPins[i], LOW);  // Ensure motors are off at startup
    }
    
    // Configure PWM for motor speed control - NEW API for ESP32 Core 3.0+
    ledcAttach(ENA, pwmFreq, pwmResolution);
    
    // Set initial PWM to 0 (Motors Off)
    ledcWrite(ENA, sharedMotorSpeed);

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


void setupWiFiClient() {
  // Connect to existing network
  WiFi.mode(WIFI_STA);
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
  } else {
    Serial.println("\nFailed to connect to WiFi!");
    Serial.println("Restarting in 5 seconds...");
    delay(5000);
    ESP.restart();
  }
}


void loop() {

    // Check WiFi connection (client mode only)
    if (WiFi.getMode() == WIFI_STA && WiFi.status() != WL_CONNECTED) {
        Serial.println("WiFi connection lost! Attempting reconnect...");
        WiFi.reconnect();
        digitalWrite(ledPin, LOW);
        delay(1000);
        return;
    }

    // Check for UDP packets
    int packetSize = udp.parsePacket();
    if (packetSize) {
        // Read the command byte
        byte command;
        udp.read(&command, 1);
        
        // Get sender info for response
        IPAddress senderIP = udp.remoteIP();
        int senderPort = udp.remotePort();
        
        Serial.printf("Received command: 0x%02X (", command);
        Serial.print(command, BIN);
        Serial.printf(") from %s:%d\n", senderIP.toString().c_str(), senderPort);
        
        // Apply the command
        ApplyCommand(command);
        lastCommandTime = millis();
        
        // Send acknowledgment
        udp.beginPacket(senderIP, senderPort);
        udp.print("OK");
        udp.endPacket();

        digitalWrite(ledPin, HIGH);
    }
      
    // Optional: Check for serial commands for debugging
    if (Serial.available() > 0) {
        byte command = Serial.read();
        Serial.printf("Serial command: 0x%02X\n", command);
        ApplyCommand(command);
    }
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
    
    // Turn everything off at the end
    ApplyCommand(0x00);
    
    Serial.println("Motor test complete!\n");
}


void ApplyCommand(byte command) {
    // Apply each bit to its corresponding motor pin
    for (int i = 0; i < 4; i++) {
        bool pinState = (command >> i) & 0x01;
        digitalWrite(motorPins[i], pinState ? HIGH : LOW);
    }
    
    // Apply PWM speeds
    ApplyPWM(command);
}


void ApplyPWM(byte command) {
    // If any motor command is active, apply the shared speed
    if (command != 0b00000000) {  // If any motor is supposed to move
        ledcWrite(ENA, sharedMotorSpeed);
        Serial.printf("PWM set to: %d\n", sharedMotorSpeed);
    } else {
        ledcWrite(ENA, 0);  // Both motors off if no command
        Serial.println("PWM set to: 0 (stopped)");
    }
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
