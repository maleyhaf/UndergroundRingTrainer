using FightTrainer.Core;
using UnityEngine;

namespace FightTrainer.Game
{
    /// <summary>
    /// Draws one fighter as coloured boxes. The colour tells you its state:
    ///   yellow tint = winding up (startup) · red fist = active hit · dark = recovery (punishable)
    ///   guard bar = blocking · see-through = dodging (invulnerable) · white = hit · pink = guard broken
    /// </summary>
    public class FighterView
    {
        private readonly Transform root;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer fist;
        private readonly SpriteRenderer guard;
        private readonly Color baseColor;

        public FighterView(Transform parent, string name, Color baseColor)
        {
            this.baseColor = baseColor;
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            body = GreyboxGame.Box(root, "Body", Vector2.zero, Vector2.one, baseColor, 0);
            fist = GreyboxGame.Box(root, "Fist", Vector2.zero, Vector2.one, Color.red, 1);
            guard = GreyboxGame.Box(root, "Guard", Vector2.zero, Vector2.one, Color.white, 2);
        }

        public void Sync(Fighter f, FightConfig c)
        {
            float w = c.fighterWidth;
            float h = GreyboxGame.BodyHeight;
            float ground = GreyboxGame.GroundY;

            Color col = baseColor;
            float bodyH = h;
            float alpha = 1f;
            float rot = 0f;
            AttackPhase phase = f.Phase(c);

            switch (f.mode)
            {
                case FighterMode.Attack:
                    if (phase == AttackPhase.Startup) col = Color.Lerp(baseColor, new Color(1f, 0.9f, 0.2f), 0.55f);
                    else if (phase == AttackPhase.Recovery) col = baseColor * 0.45f;
                    break;
                case FighterMode.Dodge:
                    bodyH = h * 0.75f;
                    alpha = f.IsInvulnerable(c) ? 0.3f : 0.7f;
                    break;
                case FighterMode.Hitstun:
                    col = Color.Lerp(baseColor, Color.white, 0.7f);
                    break;
                case FighterMode.Blockstun:
                    col = Color.Lerp(baseColor, Color.white, 0.3f);
                    break;
                case FighterMode.GuardBroken:
                    col = (f.modeFrame / 4) % 2 == 0 ? new Color(1f, 0.35f, 0.85f) : baseColor;
                    break;
                case FighterMode.KO:
                    col = baseColor * 0.4f;
                    rot = 90f;
                    break;
                case FighterMode.Dash:
                    col = Color.Lerp(baseColor, Color.white, 0.15f);
                    break;
            }
            if (f.counterWindow > 0 && f.mode != FighterMode.Attack)
                col = Color.Lerp(col, new Color(1f, 0.85f, 0.2f), 0.35f); // glowing: punish now!
            col.a = alpha;

            body.color = col;
            if (f.mode == FighterMode.KO)
            {
                body.transform.localPosition = new Vector3(f.x, ground + w * 0.5f, 0f);
                body.transform.localScale = new Vector3(w, h, 1f);
            }
            else
            {
                body.transform.localPosition = new Vector3(f.x, ground + bodyH * 0.5f, 0f);
                body.transform.localScale = new Vector3(w, bodyH, 1f);
            }
            body.transform.localRotation = Quaternion.Euler(0f, 0f, rot);

            // Fist: shows the attack's reach and phase.
            if (f.mode == FighterMode.Attack)
            {
                MoveData m = c.Move(f.currentAttack);
                bool heavy = f.currentAttack == FighterAction.Heavy;
                float len;
                Color fc;
                if (phase == AttackPhase.Startup) { len = 0.2f; fc = new Color(1f, 0.85f, 0.2f); }
                else if (phase == AttackPhase.Active) { len = m.reach; fc = new Color(1f, 0.2f, 0.15f); }
                else { len = m.reach * 0.5f; fc = new Color(0.45f, 0.2f, 0.2f); }
                fist.enabled = true;
                fist.color = fc;
                fist.transform.localPosition = new Vector3(f.x + f.facing * (w * 0.5f + len * 0.5f), ground + h * 0.75f, 0f);
                fist.transform.localScale = new Vector3(len, heavy ? 0.3f : 0.16f, 1f);
            }
            else fist.enabled = false;

            // Guard bar while blocking.
            bool blocking = f.mode == FighterMode.Block || f.mode == FighterMode.Blockstun;
            guard.enabled = blocking;
            if (blocking)
            {
                guard.color = f.mode == FighterMode.Blockstun ? Color.white : new Color(0.8f, 0.9f, 1f, 0.85f);
                guard.transform.localPosition = new Vector3(f.x + f.facing * (w * 0.5f + 0.08f), ground + h * 0.65f, 0f);
                guard.transform.localScale = new Vector3(0.12f, 0.8f, 1f);
            }
        }
    }
}
