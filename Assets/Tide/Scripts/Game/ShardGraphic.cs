// 一张图的"一块"：只画 poly 围出来的那部分（0~1 的图内坐标），用来做锁沿锯齿裂缝裂成两半。
// poly 要能从 fan（扇形中心）看到每条边，裂成左右两半时扇心放在各自那半的中间就行。
using UnityEngine;
using UnityEngine.UI;

namespace Tide
{
    public class ShardGraphic : MaskableGraphic
    {
        public Texture tex; public Rect uv = new Rect(0, 0, 1, 1);
        public Vector2[] poly; public Vector2 fan = new Vector2(0.5f, 0.5f);
        public override Texture mainTexture => tex ? tex : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (poly == null || poly.Length < 3) return;
            var r = rectTransform.rect;
            void V(Vector2 q) => vh.AddVert(new Vector3(r.xMin + q.x * r.width, r.yMin + q.y * r.height), color, new Vector2(uv.x + q.x * uv.width, uv.y + q.y * uv.height));
            V(fan); foreach (var q in poly) V(q);
            for (int i = 0; i < poly.Length; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % poly.Length);
        }
    }
}
