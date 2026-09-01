using UnityEngine;
using UnityEngine.EventSystems;

namespace Ashfold
{
    /// <summary>Полноэкранный приём кликов по карте, ниже кнопок HUD. Без второго канала Mouse.wasPressed.</summary>
    public sealed class WorldClickCatcher : MonoBehaviour, IPointerDownHandler
    {
        public PlayerCommander Commander;

        public static void Attach(Transform canvasRoot, PlayerCommander commander)
        {
            if (canvasRoot == null)
                return;
            var img = UiFactory.Panel(canvasRoot, Color.clear, "WorldClick");
            img.raycastTarget = true;
            img.transform.SetAsFirstSibling();
            var catcher = img.gameObject.AddComponent<WorldClickCatcher>();
            catcher.Commander = commander;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Commander == null || eventData == null)
                return;
            Commander.OnWorldPointer(eventData.position);
        }
    }
}
