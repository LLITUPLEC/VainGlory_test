using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Поднимает панель над системной клавиатурой на телефоне.</summary>
    public sealed class KeyboardLift : MonoBehaviour
    {
        RectTransform _rt;
        Transform _scope;
        Vector2 _baseMin;
        Vector2 _baseMax;
        float _applied;

        public static KeyboardLift Attach(RectTransform target, Transform scope = null)
        {
            if (target == null)
                return null;
            var lift = target.GetComponent<KeyboardLift>() ?? target.gameObject.AddComponent<KeyboardLift>();
            lift._rt = target;
            lift._scope = scope != null ? scope : target;
            lift._baseMin = target.offsetMin;
            lift._baseMax = target.offsetMax;
            lift._applied = 0f;
            TouchScreenKeyboard.hideInput = true;
            return lift;
        }

        void LateUpdate()
        {
            if (_rt == null)
                return;

            var kb = KeyboardHeightPx();
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponent<InputField>() : null;
            if (field == null || kb < 8f || _scope == null || !field.transform.IsChildOf(_scope))
            {
                Apply(0f);
                return;
            }

            var corners = new Vector3[4];
            field.GetComponent<RectTransform>().GetWorldCorners(corners);
            var fieldBottom = corners[0].y;
            const float pad = 28f;
            var overlap = kb + pad - fieldBottom;
            if (overlap <= 0f)
            {
                Apply(0f);
                return;
            }

            var canvas = _rt.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            Apply(overlap / scale);
        }

        void Apply(float lift)
        {
            if (Mathf.Abs(lift - _applied) < 0.5f)
                return;
            _applied = lift;
            _rt.offsetMin = new Vector2(_baseMin.x, _baseMin.y + lift);
            _rt.offsetMax = new Vector2(_baseMax.x, _baseMax.y + lift);
        }

        static float KeyboardHeightPx()
        {
            var h = 0f;
            if (TouchScreenKeyboard.visible)
            {
                var area = TouchScreenKeyboard.area;
                if (area.height > 1f)
                    h = area.height;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            var androidH = AndroidVisibleGap();
            if (androidH > h)
                h = androidH;
#endif
            return h;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static float AndroidVisibleGap()
        {
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                using (var view = window.Call<AndroidJavaObject>("getDecorView"))
                using (var rect = new AndroidJavaObject("android.graphics.Rect"))
                {
                    view.Call("getWindowVisibleDisplayFrame", rect);
                    var visible = rect.Get<int>("bottom") - rect.Get<int>("top");
                    return Mathf.Max(0, Screen.height - visible);
                }
            }
            catch
            {
                return 0f;
            }
        }
#endif
    }
}
