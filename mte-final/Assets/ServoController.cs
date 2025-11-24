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

        private void Awake() {
            //get references to all the virtual servos
            _aServo = GameObject.Find("A").GetComponent<Servo>();
            _bServo = GameObject.Find("B").GetComponent<Servo>();
            _cServo = GameObject.Find("C").GetComponent<Servo>();
            _dServo = FindFirstObjectByType<DualServo>();
            _fakeTransformPoint = GameObject.Find("Fake Transform Point").transform;
        }

        //another virtual only function, in the real world we would just input
        //the pickup objects position, but since we have the simulator
        //we can just find the closest pickup object like so:
        public Rigidbody GetClosestPickup() {
            Rigidbody closest = null;
            var objects = FindObjectsByType<Rigidbody>(0);

            foreach (Rigidbody o in objects) {
                if (!closest || Vector3.Distance(_fakeTransformPoint.position, o.transform.position) <
                    Vector3.Distance(_fakeTransformPoint.position, closest.transform.position))
                    closest = o;
            }

            return closest;
        }

        //physics sims wont be good enough to do a real pickup, we have to fake it
        public void FakePickup() {
            //if there something already picked up, drop it
            FakeDrop();

            //find the closest rigidbody, and attach it to the arm
            _pickedUpObject = GetClosestPickup();

            //disable rigidbody, parent it to the robot arm
            _pickedUpObject.isKinematic = true;
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

        //NON SIMULATOR CODE:
        public enum RobotTestType {
            SimpleMove,
            ComplexMove,
            ObstacleAvoidance
        }

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

            public Obstacle(Vector3 position, float obstacleHeight, float obstacleWidth) {
                this.position = position;
                this.obstacleHeight = obstacleHeight;
                this.obstacleWidth = obstacleWidth;
            }

            public float To360Angle(float angle) {
                if (angle < 0)
                    angle = 360 + angle;

                if (angle > 360)
                    angle -= 360;

                return angle;
            }

            public bool IsNearObstacle(float angle) {
                angle = To360Angle(angle);

                float angleThreshold = 10;
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
        }

        public List<Obstacle> obstacles = new List<Obstacle>();
        public RobotTestType robotTestType;

        //control functions
        //get closest obstacle based on normalized delta angles from the obstacle to the a servo's current angle
        public Obstacle GetClosestObstacle() {
            Obstacle closestObstacle = null;

            foreach (Obstacle obstacle in obstacles) {
                if (closestObstacle == null || AngleDelta(_aServo.currentAngle, obstacle.angleCenter) <
                    AngleDelta(_aServo.currentAngle, closestObstacle.angleCenter))
                    closestObstacle = obstacle;
            }

            return closestObstacle;
        }

        //get the necessary avoidance angle based on the nearest obstacles distance from the arm, and the obstacles height + a offset
        public float GetObstacleXAvoidanceAngle() {
            Obstacle closestObstacle = GetClosestObstacle();

            //want to be above the obstacle by a bit
            float offsetHeight = closestObstacle.avoidanceHeight + 0.1f;

            float avoidanceAngle = Mathf.Atan(closestObstacle.distanceFromArm / offsetHeight) * Mathf.Rad2Deg;

            return avoidanceAngle;
        }

        //return the delta between two angles, but make sure it's normalized, so 0 - 360 does not return 360, but returns 0 instead
        public float AngleDelta(float a, float b) {
            float diff = Mathf.Abs(a - b) % 360f;
            return diff > 180f ? 360f - diff : diff;
        }

        //helper functions to find necessary angles for the arm to point at certain positions (ex. to point at a pickup cube)
        public float GetXAngleToPosition(Vector3 position) {
            float distanceFromArm = Mathf.Sqrt(Mathf.Pow(position.x, 2) + Mathf.Pow(position.z, 2));

            float armGroundOffset = 1f;

            return Mathf.Atan2(distanceFromArm, position.y - armGroundOffset) * Mathf.Rad2Deg;
        }

        public float GetYAngleToPosition(Vector3 position) {
            return Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;
        }

        //robot tests
        private void Start() {
            switch (robotTestType) {
                case RobotTestType.SimpleMove:
                    StartCoroutine(SimpleMove());
                    break;
                case RobotTestType.ComplexMove:
                    StartCoroutine(ComplexMove());
                    break;
                case RobotTestType.ObstacleAvoidance:
                    StartCoroutine(ObstacleAvoidance());
                    break;
            }
        }

        IEnumerator SimpleMove() {
            while (true) {
                yield return new WaitForSeconds(1);

                //use hard coded angles to move towards the pickup object
                _aServo.SetAngle(270);

                _bServo.SetAngle(90);

                //perform a pickup,
                //using real servos it'd be able to pickup a paper cube,
                //but virtually we have to fake pickup the object
                yield return new WaitUntil(() => _aServo.isAtRotation);

                _dServo.SetAngle(25);

                yield return new WaitUntil(() => _dServo.isAtRotation);

                FakePickup();

                yield return new WaitForSeconds(1);

                _aServo.SetAngle(90);

                yield return new WaitUntil(() => _aServo.isAtRotation);

                //fake drop the pickup object
                _dServo.SetAngle(0);

                FakeDrop();

                yield return new WaitUntil(() => _dServo.isAtRotation);

                _bServo.SetAngle(0);

                //break out of the sim
                break;
            }
        }

        IEnumerator ComplexMove() {
            while (true) {
                yield return new WaitForSeconds(1);
                //use test function to get the closest virtual pickup object
                //using real servos, we'd manually put in the objects position,
                //virtually we can just read it like so:
                GameObject closestPickup = GetClosestPickup().gameObject;

                Vector3 pickupPosition = closestPickup.transform.position;

                //set the main servos to go towards the pickup object
                //calculate on the fly the necessary angles to go to the pickup object
                float yAngle = GetYAngleToPosition(pickupPosition);

                float xAngle = GetXAngleToPosition(pickupPosition);

                _aServo.SetAngle(yAngle);

                _bServo.SetAngle(xAngle);

                //perform a pickup
                yield return new WaitUntil(() => _aServo.isAtRotation);

                _dServo.SetAngle(25);

                yield return new WaitUntil(() => _dServo.isAtRotation);

                FakePickup();

                yield return new WaitForSeconds(1);

                //calculate on the fly the drop off angles,
                //going to be to the right, and up a bit
                Vector3 dropOffPosition = new(4, 4, 0);

                float dropOffYAngle = GetYAngleToPosition(dropOffPosition);

                float dropOffXAngle = GetXAngleToPosition(dropOffPosition);

                _aServo.SetAngle(dropOffYAngle);

                _bServo.SetAngle(dropOffXAngle);

                yield return new WaitUntil(() => _aServo.isAtRotation && _bServo.isAtRotation);

                //fake drop the pickup object
                _dServo.SetAngle(0);

                FakeDrop();

                yield return new WaitUntil(() => _dServo.isAtRotation);

                _bServo.SetAngle(0);

                //break out of the sim
                break;
            }
        }

        IEnumerator ObstacleAvoidance() {
            while (true) {
                yield return new WaitForSeconds(1);
                //virtual pickup position
                GameObject closestPickup = GetClosestPickup().gameObject;

                Vector3 pickupPosition = closestPickup.transform.position;

                //set the main servos to go towards the pickup object
                float yAngle = GetYAngleToPosition(pickupPosition);

                float xAngle = GetXAngleToPosition(pickupPosition);

                _aServo.SetAngle(yAngle);

                _bServo.SetAngle(xAngle);

                //perform a pickup
                yield return new WaitUntil(() => _aServo.isAtRotation);

                _dServo.SetAngle(25);

                yield return new WaitUntil(() => _dServo.isAtRotation);

                FakePickup();

                yield return new WaitForSeconds(1);

                //move towards the drop off point (180 deg to the right)
                //using a loop here allows the obstacle avoidance checks
                for (int i = -90; i < 90; i++) {
                    _aServo.SetAngle(i);

                    //if near a obstacle, set the x-axis servo to a angle that avoids the obstacle
                    if (GetClosestObstacle().IsNearObstacle(_aServo.currentAngle))
                        _bServo.SetAngle(GetObstacleXAvoidanceAngle());
                    else //otherwise just set it flat
                        _bServo.SetAngle(90);

                    //this just waits until the x-axis servo is avoiding the obstacle
                    yield return new WaitUntil(() => _bServo.isAtRotation);

                    //increment forward by a small amount
                    yield return new WaitForSeconds(0.01f);
                }

                yield return new WaitUntil(() => _aServo.isAtRotation);

                //fake drop the pickup object
                _dServo.SetAngle(0);

                FakeDrop();

                yield return new WaitUntil(() => _dServo.isAtRotation);

                _bServo.SetAngle(0);

                //leave this
                break;
            }
        }

        //some simple debugging
        //draw the obstacles using their angle start / end and their height
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