using UnityEngine;

namespace Ashfold
{
    /// <summary>Gem крутится вокруг мира-Y и слегка парит. Вешается на корень кристалла.</summary>
    public sealed class CrystalGemMotion : MonoBehaviour
    {
        public Transform Gem;
        public float SpinDegPerSec = 32f;
        public float BobAmplitude = 0.16f;
        public float BobHz = 0.42f;

        Vector3 _baseLocal;
        float _t;

        void Awake()
        {
            if (Gem == null)
                Gem = FindGem(transform);
            if (Gem != null)
                _baseLocal = Gem.localPosition;
        }

        void Update()
        {
            if (Gem == null)
                return;
            _t += Time.deltaTime;
            Gem.Rotate(Vector3.up, SpinDegPerSec * Time.deltaTime, Space.World);
            Gem.localPosition = _baseLocal + Vector3.up * (Mathf.Sin(_t * Mathf.PI * 2f * BobHz) * BobAmplitude);
        }

        static Transform FindGem(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == "Gem")
                    return t;
            }
            return null;
        }
    }
}
