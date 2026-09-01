using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ashfold
{
    public static class AppUi
    {
        public static void EnsureEventSystem()
        {
            var found = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem keep = null;
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] == null)
                    continue;
                if (keep == null)
                    keep = found[i];
                else
                    Object.Destroy(found[i].gameObject);
            }

            if (keep == null)
            {
                var go = new GameObject("EventSystem");
                keep = go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
            }
            else if (keep.GetComponent<InputSystemUIInputModule>() == null)
                keep.gameObject.AddComponent<InputSystemUIInputModule>();

            keep.SetSelectedGameObject(null);
        }

        public static void PurgeBattleLeftovers()
        {
            EnsureEventSystem();
            DamagePopup.ClearWorld();
            CombatUnit.PurgeDead();

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null)
                    continue;
                if (GameSession.I != null && canvas.transform.IsChildOf(GameSession.I.transform))
                    continue;
                Object.Destroy(canvas.gameObject);
            }

            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;
                var n = t.name;
                if (n == "AimMark" || n == "MapPing" || n.StartsWith("RangeRing_"))
                    Object.Destroy(t.gameObject);
            }
        }

        public static Canvas OverlayCanvas(string name, int sort = 30)
        {
            var canvas = UiFactory.CreateCanvas(name);
            canvas.sortingOrder = sort;
            return canvas;
        }

        public static void DisableWorldRaycasts(GameObject go)
        {
            if (go == null)
                return;
            var gr = go.GetComponent<GraphicRaycaster>();
            if (gr == null)
                return;
            gr.enabled = false;
            Object.Destroy(gr);
        }
    }
}
