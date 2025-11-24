using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DefaultNamespace {
    public class ServoController : MonoBehaviour {
        //references to virtual objects
        private Servo _aServo;
        private Servo _bServo;
        private Servo _cServo;
        private DualServo _dServo;
        private Transform _fakeTransformPoint;
        private Rigidbody _pickedUpObject;

        //test variables;
        public float aAngle;
        public float bAngle;
        public float cAngle;
        public float dAngle;
        public bool testPickup;
        public bool reset;

        private void Awake() {
            //get references to all the virtual servos
            _aServo = GameObject.Find("A").GetComponent<Servo>();
            _bServo = GameObject.Find("B").GetComponent<Servo>();
            _cServo = GameObject.Find("C").GetComponent<Servo>();
            _dServo = FindFirstObjectByType<DualServo>();
            _fakeTransformPoint = GameObject.Find("Fake Transform Point").transform;
        }

        private void Update() {
            /*
            _a.SetAngle(aAngle);
            _b.SetAngle(bAngle);
            _c.SetAngle(cAngle);
            _d.SetAngle(dAngle);
            */

            //test functions, dont do anything for actual run

            if (testPickup) {
                if (_pickedUpObject)
                    FakeDrop();
                else
                    FakePickup();

                testPickup = false;
            }

            if (reset)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        //physics sims wont be good enough to do a real pickup, we have to fake it
        public void FakePickup() {
            //if there something already picked up, drop it
            FakeDrop();

            //find the closest rigidbody, and attach it to the arm
            var objects = FindObjectsByType<Rigidbody>(0);

            Rigidbody closest = null;

            foreach (Rigidbody o in objects) {
                if (!closest || Vector3.Distance(_fakeTransformPoint.position, o.transform.position) <
                    Vector3.Distance(_fakeTransformPoint.position, closest.transform.position))
                    closest = o;
            }

            _pickedUpObject = closest;

            //disable rigidbody, parent it to the robot arm
            _pickedUpObject.isKinematic = true;
            _pickedUpObject.transform.position = _fakeTransformPoint.position;
            _pickedUpObject.transform.rotation = _fakeTransformPoint.rotation;
            _pickedUpObject.transform.parent = _fakeTransformPoint;
        }

        public void FakeDrop() {
            if (!_pickedUpObject)
                return;

            //enable rigidbody, unparent it, clear it
            _pickedUpObject.isKinematic = false;
            _pickedUpObject.transform.parent = null;
            _pickedUpObject = null;
        }

        private void Start() {
            StartCoroutine(RobotRoutine());
        }

        //YOUR CODE HERE
        [Serializable]
        public class Obstacle {
            public Vector3 position;
            public float obstacleHeight;
            public float obstacleWidth;

            public float avoidanceHeight => GetAvoidanceHeight();
            public float distanceFromArm => GetDistanceFromArm();
            public float angleStart => angleCenter - GetAngleOffset(); //might not be from 0-360
            public float angleEnd => angleCenter + GetAngleOffset();
            public float angleCenter => To360Angle(GetAngleCenter());

            public float To360Angle(float angle) {
                if (angle < 0)
                    angle = 360 + angle;

                if (angle > 360)
                    angle -= 360;

                return angle;
            }

            public bool IsNearObstacle(float angle) {
                angle = To360Angle(angle);

                float angleThreshold = 5;
                float start = To360Angle(angleStart - angleThreshold);
                float end = To360Angle(angleEnd + angleThreshold);

                if (start <= end) {
                    return angle >= start && angle <= end;
                }

                return angle >= start || angle <= end;
            }

            public float GetDistanceFromArm() {
                return Mathf.Sqrt(Mathf.Pow(position.x, 2) + Mathf.Pow(position.z, 2));
            }

            public float GetAvoidanceHeight() {
                return position.y + obstacleHeight / 2;
            }

            public float GetAngleCenter() {
                return Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;
            }

            public float GetAngleOffset() {
                float halfWidth = obstacleWidth / 2;
                float angleOffset = distanceFromArm <= 0f ? 0f : halfWidth / distanceFromArm * Mathf.Rad2Deg;

                return angleOffset;
            }

            public Obstacle(Vector3 position, float obstacleHeight, float obstacleWidth) {
                this.position = position;
                this.obstacleHeight = obstacleHeight;
                this.obstacleWidth = obstacleWidth;
            }
        }

        public List<Obstacle> obstacles = new List<Obstacle>();

        public float GetObstacleAvoidanceAngle() {
            Obstacle closestObstacle = GetClosestObstacle();

            //want to be above the obstacle by a bit
            float offsetHeight = closestObstacle.avoidanceHeight + 0.1f;

            float avoidanceAngle = Mathf.Atan(closestObstacle.distanceFromArm / offsetHeight) * Mathf.Rad2Deg;

            return avoidanceAngle;
        }
        
        public float AngleDelta(float a, float b)
        {
            float diff = Mathf.Abs(a - b) % 360f;
            return diff > 180f ? 360f - diff : diff;
        }

        public Obstacle GetClosestObstacle() {
            Obstacle closestObstacle = null;

            foreach (Obstacle obstacle in obstacles) {
                if (closestObstacle == null || AngleDelta(_aServo.currentAngle, obstacle.angleCenter) < AngleDelta(_aServo.currentAngle, closestObstacle.angleCenter))
                    closestObstacle = obstacle;
            }

            return closestObstacle;
        }

        IEnumerator RobotRoutine() {
            while (true) {
                //put your code here:
                yield return new WaitForSeconds(1);

                _aServo.SetAngle(-90);

                _bServo.SetAngle(90);

                _cServo.SetAngle(30);

                yield return new WaitUntil(() => _aServo.isAtRotation);

                _dServo.SetAngle(25);

                yield return new WaitUntil(() => _dServo.isAtRotation);

                FakePickup();

                yield return new WaitForSeconds(1);

                for (int i = -90; i < 90; i++) {
                    _aServo.SetAngle(i);

                    if (GetClosestObstacle().IsNearObstacle(_aServo.currentAngle))
                        _bServo.SetAngle(GetObstacleAvoidanceAngle());
                    else
                        _bServo.SetAngle(90);

                    yield return new WaitUntil(() => _bServo.isAtRotation);

                    yield return new WaitForSeconds(0.01f);
                }

                yield return new WaitUntil(() => _aServo.isAtRotation);

                _dServo.SetAngle(0);

                FakeDrop();

                yield return new WaitUntil(() => _dServo.isAtRotation);

                _bServo.SetAngle(0);

                //leave this
                break;
            }
        }

        private Vector3 AngleToDirection(float angleDeg) {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        private void OnDrawGizmos() {
            foreach (Obstacle o in obstacles) {
                float r = o.distanceFromArm;

                Vector3 startDir = AngleToDirection(o.angleStart);
                Vector3 endDir = AngleToDirection(o.angleEnd);

                Vector3 pivot = transform.position;
                Vector3 startPoint = Vector3.up * o.avoidanceHeight + pivot + startDir * r;
                Vector3 endPoint = Vector3.up * o.avoidanceHeight + pivot + endDir * r;

                Gizmos.color = Color.red;
                Gizmos.DrawLine(startPoint, endPoint);
            }
        }
    }
}