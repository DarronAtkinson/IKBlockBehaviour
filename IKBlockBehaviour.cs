using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;

namespace Combat.IK
{
    /// <summary>
    /// IKBlockBehaviour works alongside FinalIK 
    /// to provided fluid blocking motions for sword based combat.
    /// 
    /// The concept uses a Rail to define an arc around the player
    /// restricting the hand position to a location on this arc.
    /// 
    /// A Rotation of the hand blends bewteen the rotations set by the
    /// two nodes of the rail.
    /// 
    /// The Rail object uses two Transform objects as the nodes denoting
    /// the start and end point of arc. This may change in future to a more
    /// abstract representation as on the position and rotation are needed.
    /// 
    /// Attacks are assigned a rail index and ideal delta position required
    /// to block the attack.
    /// </summary>
    [RequireComponent(typeof(FullBodyBipedIK))]
    public class IKBlockBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Represents an arc around the character that the hand can follow using IK.
        /// </summary>
        [System.Serializable]
        public class Rail
        {
            // The starting point of the arc.
            public Transform startNode;

            // The end point of the arc.
            public Transform endNode;

            // The origin on the arc in local space.
            public Vector3 origin;

            // The radius of the arc from the origin.
            public float radius;
        }

        #region Editor Variables

        // A list of rails to represent arm movements for the character.
        [SerializeField] private List<Rail> m_rails = new List<Rail>();

        // A multipier to control the speed of the movements.
        [SerializeField] private float m_lerpMultiplier = 1;

        #endregion

        #region Private Variables

        // A reference to the IK component on this character.
        private FullBodyBipedIK ik;

        // The target transform for the hand to follow.
        private Transform target;

        // The index of the current active rail.
        private int currentRail = -1;

        // The current delta value for the active rail.
        private float delta = 0;

        // The current target position based on the active rail and delta value.
        private Vector3 targetPosition;

        // The current target rotation based on the active rail and delta value.
        private Quaternion targetRotation;

        // The state of the component
        private bool active = false;

        #endregion

        #region Initialisation and Cleanup

        /// <summary>
        /// Initialisation.
        /// </summary>
        private void Start()
        {
            CreateTargetObject();
            SetupFullBodyBipedIK();
        }

        /// <summary>
        /// Creates the ik target for the solver to follow.
        /// </summary>
        private void CreateTargetObject()
        {
            // Create the target gameobject.
            var targetObject = new GameObject("IKTarget");

            // Parent the object to the character.
            targetObject.transform.parent = gameObject.transform;

            // Set the target to this object.
            target = targetObject.transform;
        }

        /// <summary>
        /// Gets the Biped IK and prepares the right hand to be used by this behaviour.
        /// </summary>
        private void SetupFullBodyBipedIK()
        {
            // Get the reference to the ik component on this character and set the target.
            ik = GetComponent<FullBodyBipedIK>();
            ik.solver.rightHandEffector.target = target;

            // Set the ik weight of the position and rotation intially to full.
            SetIKWeight(1);
        }

        /// <summary>
        /// Cleanup references.
        /// </summary>
        private void OnDestroy()
        {
            ik = null;
        }

        #endregion

        #region Update

        /// <summary>
        /// Update the target to the desired position and rotation.
        /// </summary>
        private void Update()
        {
            if (active)
            {
                LerpToTarget(Time.deltaTime);
            }
        }

        /// <summary>
        /// Interpolates the position and rotation of the ik target to the desired target position and rotation.
        /// </summary>
        /// <param name="deltaTime"></param>
        private void LerpToTarget(float deltaTime)
        {
            var t = deltaTime * 60 * m_lerpMultiplier;
            target.position = Vector3.Lerp(target.position, targetPosition, t);
            target.rotation = Quaternion.Slerp(target.rotation, targetRotation, t);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the component to the active state.
        /// </summary>
        public void Activate()
        {
            active = true;
            SetIKWeight(1);
        }

        /// <summary>
        /// Sets the component to the deactive state.
        /// </summary>
        public void Deactivate()
        {
            ative = false;
            SetIKWeight(0);
        }

        /// <summary>
        /// Set the weight of the IK solver.
        /// </summary>
        /// <param name="value"></param>
        public void SetIKWeight(float value)
        {
            var weight = Mathf.Clamp(value, 0, 1);
            ik.solver.rightHandEffector.positionWeight = weight;
            ik.solver.rightHandEffector.rotationWeight = weight;
        }

        /// <summary>
        /// Sets the target position and rotation without any smoothing from the current position.
        /// Can be used to set the target to the hand position when beginning a lerp to 
        /// the target position on the current active rail.
        /// </summary>
        /// <param name="position">The desired position in world space.</param>
        /// <param name="rotation">The desired rotation in world space.</param>
        public void SetTarget(Vector3 position, Quaternion rotation)
        {
            target.position = position;
            target.rotation = rotation;
        }

        /// <summary>
        /// Sets the active rail and the delta position on that rail.
        /// </summary>
        /// <param name="currentRail">?The index on the desired rail.</param>
        /// <param name="delta">The desired delta position on the rail.</param>
        public void SetDeltaOnRail(int railIndex, float deltaValue)
        {
            // Only set the current rail and delta if the index is valid.
            if (railIndex < m_rails.Count && railIndex >= 0)
            {
                currentRail = railIndex;
                delta = deltaValue;

                SetTargetToCurrentRailDelta();
            }
            else
            {
                Debug.Log("Rail index is not valid: " + railIndex);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates the target position and rotation based on the current active rail and delta.
        /// </summary>
        private void SetTargetToCurrentRailDelta()
        {
            // Guard to ensure the current rail has been set.
            if (currentRail == -1) return;

            var rail = rails[currentRail];

            targetPosition = GetDeltaPosition(rail);
            targetRotation = GetDeltaRotation(rail);
        }

        /// <summary>
        /// Returns the world position for the current delta from a rail.
        /// </summary>
        /// <param name="rail">The rail to be probed.</param>
        /// <returns>The world position on the given rail for the current delta</returns>
        private Vector3 GetDeltaPosition(Rail rail)
        {
            // Get the world space of the rail origin.
            var origin = transform.TransformPoint(rail.origin);

            // Get the position of the target along the line from the start to the end node of the rail.
            var position = Vector3.Lerp(rail.startNode.position, rail.endNode.position, delta);

            // Get the normalized direction from the orign to the desired position.
            var directionToPosition = (position - origin).normalized;

            // Project the desired position away from the origin by the radius on the rail.
            var positionOnRail = origin + (directionToPosition * rail.radius);

            return positionOnRail;
        }

        /// <summary>
        /// Returns the rotation value of the rail at the curretn delta.
        /// </summary>
        /// <param name="rail">The rail to probe.</param>
        /// <returns>The world space rotation of the given rail for the current delta.</returns>
        private Quaternion GetDeltaRotation(Rail rail)
        {
            return Quaternion.Slerp(rail.startNode.rotation, rail.endNode.rotation, delta);
        }

        #endregion 
    }
}
