#include <ESP32Servo.h>

Servo Aservo;
Servo Bservo;
Servo Cservo;
Servo Dservo;

int AservoPin = 4;
int BservoPin = 5;
int CservoPin = 6;
int DservoPin = 7;

float pickupX = 3;
float pickupY = 2;
float pickupZ = 4;

struct Obstacle {
    float x;
    float y;
    float z;
    float height;
    float width;
};

Obstacle obstacles[3] = {
    {2, 1, 3, 1.0, 1.0},
    {4, 1, 1, 1.2, 1.0},
    {-2, 1, 2, 1.0, 0.8}
};

int obstacleCount = 3;

bool picked = false;

int state = 0;
unsigned long t = 0;
int sweepI = -90;

float deg(float r) { return r * 180.0 / 3.14159265; }

float AngleCenter(float x, float z) {
    return deg(atan2(x, z));
}

float DistanceFromArm(float x, float z) {
    return sqrt(x*x + z*z);
}

float AvoidanceHeight(float y, float h) {
    return y + h * 0.5;
}

float AngleOffset(float width, float dist) {
    if (dist <= 0) return 0;
    return deg((width * 0.5) / dist);
}

float To360(float a) {
    if (a < 0) a += 360;
    if (a > 360) a -= 360;
    return a;
}

bool IsNear(float angle, float start, float end) {
    angle = To360(angle);
    float s = To360(start - 10);
    float e = To360(end + 10);
    if (s <= e) return angle >= s && angle <= e;
    return angle >= s || angle <= e;
}

int GetClosestObstacleIndex(float currentAngle) {
    int idx = 0;
    float best = 9999;
    for (int i = 0; i < obstacleCount; i++) {
        float c = AngleCenter(obstacles[i].x, obstacles[i].z);
        float d = abs(currentAngle - c);
        if (d > 180) d = 360 - d;
        if (d < best) { best = d; idx = i; }
    }
    return idx;
}

float GetObstacleXAvoidanceAngle(int i) {
    float dist = DistanceFromArm(obstacles[i].x, obstacles[i].z);
    float ah = AvoidanceHeight(obstacles[i].y, obstacles[i].height) + 0.1;
    return deg(atan(dist / ah));
}

float GetYAngleToPosition(float x, float z) {
    return deg(atan2(x, z));
}

float GetXAngleToPosition(float x, float y) {
    float d = sqrt(x*x + y*y);
    float offset = 1.0;
    return deg(atan2(d, y - offset));
}

void FakePickup() { picked = true; }
void FakeDrop() { picked = false; }

void setup() {
    Aservo.attach(AservoPin);
    Bservo.attach(BservoPin);
    Cservo.attach(CservoPin);
    Dservo.attach(DservoPin);
    t = millis();
}

void loop() {
    if (state == 0) {
        Aservo.write(GetYAngleToPosition(pickupX, pickupZ));
        Bservo.write(GetXAngleToPosition(pickupX, pickupY));
        if (millis() - t > 1000) { state = 1; t = millis(); }
    }

    else if (state == 1) {
        Dservo.write(25);
        if (millis() - t > 600) { FakePickup(); state = 2; t = millis(); }
    }

    else if (state == 2) {
        sweepI = -90;
        state = 3;
        t = millis();
    }

    else if (state == 3) {
        if (sweepI > 90) { state = 4; t = millis(); return; }

        Aservo.write(sweepI);

        int idx = GetClosestObstacleIndex(sweepI);
        float c = AngleCenter(obstacles[idx].x, obstacles[idx].z);
        float off = AngleOffset(obstacles[idx].width, DistanceFromArm(obstacles[idx].x, obstacles[idx].z));
        float start = c - off;
        float end = c + off;

        if (IsNear(sweepI, start, end))
            Bservo.write(GetObstacleXAvoidanceAngle(idx));
        else
            Bservo.write(90);

        sweepI++;
        delay(10);
    }

    else if (state == 4) {
        Dservo.write(0);
        FakeDrop();
        if (millis() - t > 600) { Bservo.write(0); state = 5; }
    }
}
