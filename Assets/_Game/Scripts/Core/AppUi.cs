using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ashfold
{
    public static class AppUi
    {
        static readonly string[] LeftoverCanvasNames =
        {
            "ShopBattle", "TutorialBattle", "TutorialHall", "ModeOverlay", "DraftOverlay",
            "LoadingOverlay", "QueueOverlay", "PartyHint", "AimMark", "DamagePopups"
        };

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

            keep.SetSelectedGameObject(null);
        }

        public static void PurgeBattleLeftovers()
        {
            EnsureEventSystem();
            DamagePopup.ClearWorld();
            CombatUnit.PurgeDead();
            foreach (var name in LeftoverCanvasNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                    Object.Destroy(go);
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
            var canvas = go.GetComponent<Canvas>();
            if (canvas != null)
            {
                var gr = go.GetComponent<GraphicRaycaster>();
                if (gr != null)
                    Object.Destroy(gr);
            }
        }
    }
}
