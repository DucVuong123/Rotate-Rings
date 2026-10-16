using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RotateRings.Gameplay.View
{
    public class WinPanelView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text scoreLabel;
        [SerializeField] Button replayButton;
        [SerializeField] Button nextButton;

        public Button ReplayButton => replayButton;
        public Button NextButton => nextButton;

        void Awake() => root.SetActive(false);

        public void Show(int score)
        {
            scoreLabel.text = score.ToString();
            root.SetActive(true);
            Tween.RunAsync(0.35f, t =>
            {
                canvasGroup.alpha = t;
                card.localScale = Vector3.one * Tween.EaseOutBack(t);
            }, destroyCancellationToken).Forget();
        }

        public void Hide() => root.SetActive(false);
    }
}
