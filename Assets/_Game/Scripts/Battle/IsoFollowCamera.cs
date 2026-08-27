using UnityEngine;

namespace Ashfold
{
    public sealed class IsoFollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 24f, -20f);
        public float Damp = 6f;

        void LateUpdate()
        {
            if (Target == null)
                return;
            var desired = Target.position + Offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-Damp * Time.deltaTime));
            transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        }
    }
}
