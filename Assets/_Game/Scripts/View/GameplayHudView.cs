using TMPro;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    public class GameplayHudView : MonoBehaviour
    {
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text scoreLabel;

        public void SetLevel(int levelNumber) => levelLabel.text = $"Level\n{levelNumber}";

        public void SetScore(int score) => scoreLabel.text = score.ToString();
    }
}
