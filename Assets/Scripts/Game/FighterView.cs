using FightTrainer.Core;
using UnityEngine;

namespace FightTrainer.Game
{
    /// <summary>
    /// Draws one fighter as coloured boxes in the side view. The ring is shown with depth:
    /// fighters further back in the ring stand higher on screen and are drawn behind.
    /// The colour tells you its state:
    ///   yellow tint = winding up (startup) · red fist = active hit · dark = recovery (punishable)
    ///   guard bar = blocking · see-through = dodging (invulnerable) · white = hit · pink = guard broken
    /// </summary>
    public class FighterView
    {
        private readonly Transform root;
        private readonly SpriteRenderer shadow;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer fist;
        private readonly SpriteRenderer guard;
        private readonly Color baseColor;

        public FighterView(Transform parent, string name, Color baseColor)
        {
            this.baseColor = baseColor;
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            shadow = GreyboxGame.Box(root, "Shadow", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.45f), -5);
            body = GreyboxGame.Box(root, "Body", Vector2.zero, Vector2.one, baseColor, 0);
            fist = GreyboxGame.Box(root, "Fist", Vector2.zero, Vector2.one, Color.red, 1);
            guard = GreyboxGame.Box(root, "Guard", Vector2.zero, Vector2.one, Color.white, 2);
        }

        public void Sync(Fighter f, FightConfig c)
        {
            float w = c.fighterWidth;
            float h = GreyboxGame.BodyHeight;
            Vector3 feet = GreyboxGame.RingToWorld(f.pos, 0f);

            // Nearer fighters (lower y in the ring) draw on top.
            int order = 100 - Mathf.RoundToInt(f.pos.y * 10f);
            body.sortingOrder = order;
            fist.sortingOrder = order + 1;
            guard.sortingOrder = order + 2;

            shadow.transform.localPosition = feet;
            shadow.transform.localScale = new Vector3(w * 1.2f, w * GreyboxGame.DepthScale * 1.2f, 1f);

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
                body.transform.localPosition = feet + new Vector3(0f, w * 0.5f, 0f);
                body.transform.localScale = new Vector3(w, h, 1f);
            }
            else
            {
                body.transform.localPosition = feet + new Vector3(0f, bodyH * 0.5f, 0f);
                body.transform.localScale = new Vector3(w, bodyH, 1f);
            }
            body.transform.localRotation = Quaternion.Euler(0f, 0f, rot);

            // Facing, as seen on screen (depth is squashed).
            Vector2 screenDir = new Vector2(f.facing.x, f.facing.y * GreyboxGame.DepthScale);
            float shoulder = h * 0.75f;

            // Fist: shows the attack's reach, direction and phase.
            if (f.mode == FighterMode.Attack)
            {
                MoveData m = c.Move(f.currentAttack);
                bool heavy = f.currentAttack == FighterAction.Heavy;
                float len;
                Color fc;
                if (phase == AttackPhase.Startup) { len = 0.2f; fc = new Color(1f, 0.85f, 0.2f); }
                else if (phase == AttackPhase.Active) { len = m.reach; fc = new Color(1f, 0.2f, 0.15f); }
                else { len = m.reach * 0.5f; fc = new Color(0.45f, 0.2f, 0.2f); }
                Vector2 start = screenDir * (w * 0.5f);
                Vector2 seg = screenDir * len;
                fist.enabled = true;
                fist.color = fc;
                fist.transform.localPosition = feet + new Vector3(start.x + seg.x * 0.5f, shoulder + start.y + seg.y * 0.5f, 0f);
                fist.transform.localScale = new Vector3(Mathf.Max(0.05f, seg.magnitude), heavy ? 0.3f : 0.16f, 1f);
                fist.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg);
            }
            else fist.enabled = false;

            // Guard bar while blocking, held in the direction the fighter is facing.
            bool blocking = f.mode == FighterMode.Block || f.mode == FighterMode.Blockstun;
            guard.enabled = blocking;
            if (blocking)
            {
                Vector2 off = screenDir * (w * 0.5f + 0.08f);
                guard.color = f.mode == FighterMode.Blockstun ? Color.white : new Color(0.8f, 0.9f, 1f, 0.85f);
                guard.transform.localPosition = feet + new Vector3(off.x, h * 0.65f + off.y, 0f);
                guard.transform.localScale = new Vector3(0.12f, 0.8f, 1f);
            }
        }
    }
}
