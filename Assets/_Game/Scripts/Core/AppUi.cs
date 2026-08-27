using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Ashfold
{
    public static class AppUi
    {
        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static Canvas OverlayCanvas(string name, int sort = 30)
        {
            var canvas = UiFactory.CreateCanvas(name);
            canvas.sortingOrder = sort;
            return canvas;
        }
    }
}
