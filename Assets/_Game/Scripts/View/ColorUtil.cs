using UnityEngine;

namespace RotateRings.Gameplay.View
{
    public static class ColorUtil
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock _block;

        public static void SetColor(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        public static Color Shade(Color color, float factor) =>
            new(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}
