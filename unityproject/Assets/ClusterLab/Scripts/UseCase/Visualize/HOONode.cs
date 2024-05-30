using System;
using System.Collections.Generic;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Utils;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClusterLab.UseCase
{
    public class HOONode : MonoBehaviour, IArrowRenderer
    {
        public string Id;
        public string BranchId;
        public string ParentId;
        public int Depth;
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 Center;
        public Vector3 Size;
        public float Score;
        public bool Active = false;
        public float Alpha;
        public bool HasChild = false;
        public bool IsShowingHOOBound = true;
        public BoolReactiveProperty IsShowingHOOArrow = new(true);
        public BoolReactiveProperty IsShowingGridArrow = new(true);
        public BoolReactiveProperty IsNodeSelected = new(false);
        public FloatReactiveProperty ArrowScale = new(1.0f);
        public List<PhotoScoring> PhotoScorings;
        public List<LeafGridNode> LeafGridNodes;
        public List<ArrowObject> LeafGridArrows = new();
        public float AncestorsMaxScore;
        public HOONode Parent;
        public Color GizmoColor = Color.blue;

        static readonly int SEmissionColorPropId = Shader.PropertyToID("_EmissionColor");
        static readonly int SColorPropId = Shader.PropertyToID("_Color");

        #region IArrowRenderer

        public Transform ArrowTransform => transform;
        public Material ArrowMaterial { get; set; }
        public MeshRenderer ArrowRenderer { get; set; }
        public int EmissionColorPropId => SEmissionColorPropId;
        public int ColorPropertyPropId => SColorPropId;

        ReactiveProperty<NodeRendererContext> ctx;

        #endregion

        readonly List<IDisposable> rxListeners = new();

        IDisposable contextListener;

        void Awake()
        {
            this.InitializeArrowRenderer(gameObject);
            rxListeners.Add(IsShowingHOOArrow.Subscribe(this.SetArrowVisible));
            rxListeners.Add(IsShowingGridArrow.Subscribe(_ => UpdateGridArrows()));
            rxListeners.Add(IsNodeSelected.Subscribe(selected =>
            {
                if (ctx == null) return;
                IsShowingGridArrow.Value = selected && ctx.Value.IsShowingGridSearchArrow;
            }));
            rxListeners.Add(ArrowScale.Subscribe(scale => LeafGridArrows.ForEach(a => a.SetArrowScale(scale))));
        }

        public void UpdateNode(HOONode parent, NodeViewModel vm, ReactiveProperty<NodeRendererContext> context)
        {
            Parent = parent;
            Id = vm.Id;
            BranchId = vm.BranchId;
            ParentId = vm.ParentId;
            Depth = vm.Depth;
            Min = vm.Min;
            Max = vm.Max;
            Score = vm.Score;
            Center = (Min + Max) / 2;
            Size = Max - Min;
            PhotoScorings = vm.PhotoScorings;
            LeafGridNodes = vm.LeafGridNodes;
            // Place and rotate the node according to argmax of the photo scorings
            var maxPS = vm.PhotoScorings.MaxOf(ps => ps.Score);
            this.SetArrowTransform(maxPS.CameraParameter);

            AncestorsMaxScore = Parent != null ? Math.Max(Parent.Score, Score) : Score;

            contextListener?.Dispose();
            contextListener = context.Subscribe(_ => UpdateVisualElements());
            ArrowMaterial.name = vm.Id + " Material";
            ctx = context;
            GizmoColor = CalculateColor(ctx.Value, Score);
            UpdateVisualElements();
        }

        void UpdateVisualElements()
        {
            IsShowingGridArrow.Value = IsNodeSelected.Value && ctx.Value.IsShowingGridSearchArrow;
            UpdateHooVisualElements();
            UpdateGridArrows();
        }

        void UpdateHooVisualElements()
        {
            if (ctx == null)
                return;
            var c = ctx.Value;

            Alpha = 0.33f * Math.Max(0.05f, Depth - (c.MaxDepth - 3));
            Active = c.ActiveNodeId == Id;
            var isLeafAndHasEnoughScore = (!HasChild && Score >= c.ScoreThreshold) || Active;
            IsShowingHOOArrow.Value = isLeafAndHasEnoughScore && c.IsShowingHOOArrow;
            IsShowingHOOBound = isLeafAndHasEnoughScore && c.IsShowingHOOBound;
            this.SetArrowColor(GizmoColor);
        }

        void UpdateGridArrows()
        {
            if (ctx == null)
                return;
            if (IsShowingGridArrow.Value && IsNodeSelected.Value)
                InstantiateGridArrows(ctx.Value);
            else
                ReleaseGridArrows(ctx.Value);
        }

        void InstantiateGridArrows(NodeRendererContext c)
        {
            ReleaseGridArrows(c);
            if (LeafGridNodes.Count == 0)
                return;
            foreach (var lgn in LeafGridNodes)
            {
                var arrowObject = c.ArrowObjectPool.Rent();
                LeafGridArrows.Add(arrowObject);
                if (lgn.PhotoScorings.Count == 0)
                    continue;
                var maxPS = lgn.PhotoScorings.MaxOf(ps => ps.Score);
                arrowObject.SetArrowTransform(maxPS.CameraParameter);
                arrowObject.SetArrowVisible(true);
                arrowObject.SetArrowColor(CalculateColor(c, maxPS.Score));
            }
            ArrowScale.Value = Size.magnitude * 0.05f;
        }

        void ReleaseGridArrows(NodeRendererContext c)
        {
            foreach (var arrowObject in LeafGridArrows)
            {
                c.ArrowObjectPool.Return(arrowObject);
            }
            LeafGridArrows.Clear();
        }

        Color CalculateColor(NodeRendererContext c, float score)
        {
            var value = Mathf.Clamp((score - Mathf.Max(0, c.MaxScore - c.ScoreColoringRange)) / (c.ScoreColoringRange), 0f, 1f);
            // Debug.Log(value);
            var color = value > 0.5
                ? HSV.LerpHSV(Color.green, Color.red, (value - 0.5f) * 2f)
                : HSV.LerpHSV(Color.blue, Color.green, value * 2f);
            return color;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Active ? Color.magenta : GizmoColor;
            DrawGizmos();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Active ? Color.magenta : Color.red;
#if UNITY_EDITOR
            Handles.color = Gizmos.color;
            // Handles.Label(transform.position, $"{Score:F3}");
#endif

            // var center = transform.position;
            // Gizmos.DrawSphere(center, 0.5f);
            DrawBoundingBox();
        }

        void DrawGizmos()
        {
            DrawBoundingBox(!Active);
        }

        void DrawBoundingBox(bool wire = true)
        {
            if (!IsShowingHOOBound)
                return;

            Gizmos.color = GizmoColor;
            var dx = Mathf.Abs(Max.x - Min.x);
            var dy = Mathf.Abs(Max.y - Min.y);
            var dz = Mathf.Abs(Max.z - Min.z);

            var size = new Vector3(dx, dy, dz);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, Alpha);
            Gizmos.color = new Color(1f, 0.92156863f, 0.015686275f, 0.5f);
            if (wire)
                Gizmos.DrawWireCube(Center, size);
            else
                Gizmos.DrawCube(Center, size);
        }

        void OnDestroy()
        {
            contextListener?.Dispose();
            foreach (var listener in rxListeners)
            {
                listener.Dispose();
            }
        }
    }
}
