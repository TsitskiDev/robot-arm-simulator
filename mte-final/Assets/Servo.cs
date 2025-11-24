using System;
using UnityEngine;

public class Servo : MonoBehaviour
{
    public float rotationSpeed = 12;
    public bool x, y, z;

    private Quaternion _baseRotation;
    private Quaternion _toRotation;

    public float currentAngle {
        get {
            if (x)
                return transform.localRotation.eulerAngles.x;
            if (y)
                return transform.localRotation.eulerAngles.y;
            
            return transform.localRotation.eulerAngles.z;
        }
    }
    public bool isAtRotation => transform.localRotation == _toRotation;

    private void Awake() {
        _baseRotation = transform.localRotation;
    }

    public void SetAngle(float angle) {
        if (x)
            _toRotation = Quaternion.Euler(angle, 0, 0);
        else if (y)
            _toRotation = Quaternion.Euler(0, angle, 0);
        else if (z)
            _toRotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update() {
        Quaternion offsetToRotation = _baseRotation * _toRotation;

        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, offsetToRotation, rotationSpeed * Time.deltaTime);
    }
}
