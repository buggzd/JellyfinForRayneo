using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    [RequireComponent(typeof(Graphic))]
    public sealed class UiGradient : BaseMeshEffect
    {
        public Color StartColor = Color.white;
        public Color EndColor = Color.white;
        public bool Horizontal;

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper.currentVertCount == 0)
            {
                return;
            }

            UIVertex vertex = default(UIVertex);
            float minimum = float.MaxValue;
            float maximum = float.MinValue;
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);
                float coordinate = Horizontal ? vertex.position.x : vertex.position.y;
                minimum = Mathf.Min(minimum, coordinate);
                maximum = Mathf.Max(maximum, coordinate);
            }

            float range = Mathf.Max(0.0001f, maximum - minimum);
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);
                float coordinate = Horizontal ? vertex.position.x : vertex.position.y;
                float amount = Mathf.Clamp01((coordinate - minimum) / range);
                Color source = vertex.color;
                vertex.color = source * Color.Lerp(StartColor, EndColor, amount);
                vertexHelper.SetUIVertex(vertex, index);
            }
        }
    }
}
