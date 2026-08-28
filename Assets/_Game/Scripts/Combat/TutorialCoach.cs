using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Этап 6.5: подсказки ~60 с на первый бой. Пропуск сохраняется на устройство.</summary>
    public sealed class TutorialCoach : MonoBehaviour
    {
        public const string PrefsKey = "ashfold.tutorial.done";

        static readonly string[] BattleKeys =
        {
            "tut.move", "tut.attack", "tut.skill", "tut.shop", "tut.brush", "tut.ping"
        };

        const float StepSeconds = 10f;

        public static bool Done => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

        public static void MarkDone()
        {
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
        }

        public static void TryStartBattle()
        {
            if (Done)
                return;
            var canvas = AppUi.OverlayCanvas("TutorialBattle", 40);
            canvas.gameObject.AddComponent<TutorialCoach>().BuildBattle(canvas.transform);
        }

        public static void TryShowHall()
        {
            if (Done)
                return;
            var canvas = AppUi.OverlayCanvas("TutorialHall", 32);
            canvas.gameObject.AddComponent<TutorialCoach>().BuildHall(canvas.transform);
        }

        Text _body;
        float _t;
        int _step = -1;
        bool _battle;

        void BuildHall(Transform root)
        {
            BuildCard(root, Loc.T("tut.hall"));
            _t = 12f;
        }

        void BuildBattle(Transform root)
        {
            _battle = true;
            BuildCard(root, Loc.T(BattleKeys[0]));
            _step = 0;
            _t = 0f;
        }

        void BuildCard(Transform root, string text)
        {
            UiFactory.Box(root, new Vector2(0.18f, 0.78f), new Vector2(0.82f, 0.96f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x000000, 0.72f), "Card");
            var box = UiFactory.Box(root, new Vector2(0.20f, 0.80f), new Vector2(0.68f, 0.94f), Vector2.zero, Vector2.zero, Color.clear, "Txt");
            _body = UiFactory.Label(box.transform, text, 20, GameTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, true);
            var skip = UiFactory.Button(root, Loc.T("tut.skip"), Finish, GameTheme.BgPanelSoft, GameTheme.Gold);
            UiFactory.SetAnchors(skip.GetComponent<RectTransform>(), new Vector2(0.70f, 0.81f), new Vector2(0.80f, 0.93f), new Vector2(6, 6), new Vector2(-6, -6));
            skip.GetComponentInChildren<Text>().fontSize = 16;
        }

        void Update()
        {
            if (!_battle)
            {
                _t -= Time.unscaledDeltaTime;
                if (_t <= 0f)
                    Destroy(gameObject);
                return;
            }

            _t += Time.unscaledDeltaTime;
            var next = Mathf.Min(BattleKeys.Length - 1, Mathf.FloorToInt(_t / StepSeconds));
            if (next != _step && next < BattleKeys.Length)
            {
                _step = next;
                if (_body != null)
                    _body.text = Loc.T(BattleKeys[_step]);
            }

            if (_t >= BattleKeys.Length * StepSeconds)
                Finish();
        }

        void Finish()
        {
            if (_battle)
                MarkDone();
            Destroy(gameObject);
        }
    }
}
