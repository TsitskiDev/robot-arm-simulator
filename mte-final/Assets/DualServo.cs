using System;
using UnityEngine;

namespace DefaultNamespace {
    public class DualServo : MonoBehaviour {
        public Servo left;
        public Servo right;

        public bool isAtRotation => left.isAtRotation;

        public void SetAngle(float angle) {
            left.SetAngle(angle);
            right.SetAngle(-angle);
        }
    }
}