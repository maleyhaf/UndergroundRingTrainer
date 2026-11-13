using UnityEngine;

namespace FightTrainer.Game
{
    /// <summary>Placeholder art: a white square sprite that everything is drawn with, tinted per use.</summary>
    public static class GreyboxArt
    {
        private static Sprite white;
        private static Texture2D whiteTex;
        private static Material spriteMaterial;
        private static bool materialSearched;

        public static Texture2D WhiteTexture
        {
            get
            {
                if (whiteTex == null)
                {
                    whiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    Color[] px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    whiteTex.SetPixels(px);
                    whiteTex.filterMode = FilterMode.Point;
                    whiteTex.Apply();
                }
                return whiteTex;
            }
        }

        /// <summary>1×1 world-unit white square, pivot in the centre.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (white == null)
                    white = Sprite.Create(WhiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                return white;
            }
        }

        /// <summary>Unlit sprite material so the greybox doesn't depend on scene lights.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (!materialSearched)
                {
                    materialSearched = true;
                    Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (s == null) s = Shader.Find("Sprites/Default");
                    if (s != null) spriteMaterial = new Material(s);
                }
                return spriteMaterial;
            }
        }
    }
}
