using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Utils;
using ClusterLab.UseCase.Render;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClusterLab.UseCase
{
    public interface INodeRenderer
    {
        void UpdateNodes(NodeViewModel[] nodes);

        void ResetNodes();
    }

    [Serializable]
    public struct NodeRendererContext
    {
        public int MaxDepth;
        public string ActiveNodeId;
        public float MinScore;
        public float MaxScore;
        public float ScoreColoringRange;
        public float ScoreThreshold;
        public bool IsShowingHOOArrow;
        public bool IsShowingGridSearchArrow;
        public bool IsShowingHOOBound;
        public bool IsShowingFrustum;
        public GameObject ArrowPrefab;
        public ArrowObjectPool ArrowObjectPool;
        [MaybeNull] public TileCameraRenderer TileRenderer;
    }

    struct NodePhotoScoring
    {
        public string NodeId;
        public PhotoScoring PhotoScoring;
    }

    public class NodeRendererImpl : MonoBehaviour, INodeRenderer
    {
        [SerializeField] ReactiveProperty<NodeRendererContext> Context;
        [SerializeField] GameObject TreeRoot;
        [SerializeField] GameObject ArrowPoolRoot;
        static ArrowObjectPool arrowObjectPool;
        List<NodePhotoScoring> photoScoringsShown = new();
        int selectedPhotoIndex = -1;
        Vector2 scrollViewVector = Vector2.zero;
        HOONode lastSelectedNode;
        FloatReactiveProperty lastNodeUpdatedAt = new();


        readonly Dictionary<string, HOONode> nodes = new();


        void Awake()
        {
            ResetContext();
            lastNodeUpdatedAt.Throttle(TimeSpan.FromSeconds(5)).Subscribe(_ =>
            {
                var ctx = Context.Value;
                ctx.ActiveNodeId = "";
                Context.Value = ctx;
            });

            InitArrowObjectPool(Context.Value.ArrowPrefab);
#if UNITY_EDITOR
            Selection.selectionChanged += () =>
            {
                if (Selection.activeGameObject == null)
                {
                    OnNodeDeselected();
                    return;
                }
                ;
                var node = Selection.activeGameObject.GetComponent<HOONode>();
                if (node == null)
                {
                    OnNodeDeselected();
                    return;
                }
                OnNodeSelected(node);
            };
#endif
        }


        void InitArrowObjectPool(GameObject arrowPrefab)
        {
            if (arrowObjectPool != null)
                return;
            arrowObjectPool = new ArrowObjectPool(ArrowPoolRoot.transform, arrowPrefab.GetComponent<ArrowObject>());
        }

        void OnNodeSelected(HOONode node)
        {
            var ctx = Context.Value;
            var tileRenderer = ctx.TileRenderer;

            if (tileRenderer == null) return;

            var scoringsInChild = GetPhotoScoringsRecursively(node.gameObject)
                .OrderByDescending(ps => ps.PhotoScoring.Score)
                .Take(tileRenderer.NumCameras)
                .ToList();
            // scoringsInChild = node.PhotoScorings.Select(n => new NodePhotoScoring
            // {
                // NodeId = node.Id,
                // PhotoScoring = n
            // }).ToList();

            photoScoringsShown = scoringsInChild;
            var cameraParameters = scoringsInChild
                .Select(ps => ps.PhotoScoring.CameraParameter)
                .ToList();
            if (lastSelectedNode != null)
                lastSelectedNode.IsNodeSelected.Value = false;
            lastSelectedNode = node;
            node.IsNodeSelected.Value = true;
            ctx.TileRenderer.PrepareCameras(cameraParameters);
            // ctx.TileRenderer.Render(cameraParams);
        }

        void OnNodeDeselected()
        {
            var tileRenderer = Context.Value.TileRenderer;
            if (tileRenderer != null)
                tileRenderer.ResetCameras();
            if (lastSelectedNode != null)
            {
                lastSelectedNode.IsNodeSelected.Value = false;
                lastSelectedNode = null;
            }

            photoScoringsShown.Clear();
            selectedPhotoIndex = -1;
        }

        List<NodePhotoScoring> GetPhotoScoringsRecursively(GameObject go)
        {
            var photoScorings = new List<NodePhotoScoring>();
            foreach (var child in go.GetComponentsInChildren<HOONode>())
            {
                if (child.PhotoScorings.Count == 0) continue;
                var max = child.PhotoScorings.MaxOf(ps => ps.Score);

                photoScorings.Add(new()
                {
                    NodeId = child.Id,
                    PhotoScoring = max
                });
            }
            return photoScorings;
        }

        void OnGUI()
        {
            var ctx = Context.Value;
            var tileRenderer = ctx.TileRenderer;
            var photoScorings = photoScoringsShown;
            if (tileRenderer == null) return;
            var renderTextures = tileRenderer.RenderTextures;
            var boxWidth = 336;
            var boxHeight = 320;
            var textureSize = 64;
            var left = 0;
            var top = 0;
            GUI.Box(new Rect(left, top, boxWidth, boxHeight), "Node Renderer");
            var scrollHeight = ((photoScorings.Count * textureSize) / boxWidth) * textureSize + 256;
            top += 40;
            {
                // toggle hoo node arrow button
                var label = ctx.IsShowingHOOArrow ? "Hide HOO" : "Show HOO";
                if (GUI.Button(new Rect(0, top, 100, 20), label))
                    ctx.IsShowingHOOArrow = !ctx.IsShowingHOOArrow;
            }
            {
                // toggle hoo node bound button
                var label = ctx.IsShowingHOOBound ? "Hide Bound" : "Show Bound";
                if (GUI.Button(new Rect(100, top, 100, 20), label))
                    ctx.IsShowingHOOBound = !ctx.IsShowingHOOBound;
            }
            {
                // toggle grid node arrow button
                var label = ctx.IsShowingFrustum ? "Hide Frustum" : "Show Frustum";
                if (GUI.Button(new Rect(200, top, 100, 20), label))
                    ctx.IsShowingFrustum = !ctx.IsShowingFrustum;
            }
            top += 20;


            Context.Value = ctx;

            scrollViewVector = GUI.BeginScrollView(new Rect(left, top, boxWidth, boxHeight - top), scrollViewVector, new Rect(0, 0, boxWidth, scrollHeight));


            var i = 0;

            for (float y = 0; y < scrollHeight; y += textureSize)
            {
                for (float x = left; x + textureSize < boxWidth; x += textureSize)
                {
                    if (i > photoScorings.Count - 1)
                        goto EndOfElement;

                    var ps = photoScorings[i];
                    var tex = renderTextures[i];

                    var texRect = new Rect(x, y, textureSize, textureSize);
                    if (texRect.Contains(Event.current.mousePosition))
                    {
                        selectedPhotoIndex = i;
#if UNITY_EDITOR
                        if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                        {
                            var selectedNodeId = photoScoringsShown[i].NodeId;
                            var hooNode = nodes[selectedNodeId];
                            SceneView.lastActiveSceneView.Frame(new Bounds(hooNode.Center, hooNode.Size));
                        }
#endif
                    }
                    GUI.DrawTexture(texRect, tex);
                    GUI.Label(new Rect(x, y, textureSize, 20), $"{i}: {ps.PhotoScoring.Score:F3}");
                    i++;
                }
                continue;
                EndOfElement:
                break;
            }
            GUI.EndScrollView();
            if (0 <= selectedPhotoIndex && selectedPhotoIndex < photoScoringsShown.Count)
            {
                var selectedPhotoScoring = photoScoringsShown[selectedPhotoIndex];
                GUI.Label(new Rect(left + 5, 20, boxWidth, 30), $"Index: {selectedPhotoIndex} Score: {selectedPhotoScoring.PhotoScoring.Score}");
            }
        }

        void Update()
        {
            if (selectedPhotoIndex >= 0 && selectedPhotoIndex < photoScoringsShown.Count)
            {
                var cp = photoScoringsShown[selectedPhotoIndex].PhotoScoring.CameraParameter;
                if (Camera.main != null)
                    Camera.main.transform.SetPositionAndRotation(cp.Position, cp.Rotation);
            }
        }

        void OnDrawGizmos()
        {
            if (!Context.Value.IsShowingFrustum)
                return;
            for (var i = 0; i < photoScoringsShown.Count; i++)
            {
                var ps = photoScoringsShown[i];
                var cp = ps.PhotoScoring.CameraParameter;
                var mat = Gizmos.matrix;
                var color = Gizmos.color;
                Gizmos.color = Color.gray;
                Gizmos.matrix = Matrix4x4.TRS(cp.Position, cp.Rotation, Vector3.one);
                if (selectedPhotoIndex == i)
                {
                    Gizmos.color = Color.red;
                }
                var aspect = cp.Aspect == 0 ? 1.6666667f : cp.Aspect;
                Gizmos.DrawFrustum(Vector3.zero, cp.FieldOfView, 10f, 1f, aspect);
                Gizmos.color = color;
                Gizmos.matrix = mat;


#if UNITY_EDITOR
                // Handles.color = Gizmos.color;
                // Handles.Label(cp.Position, $"{i} {ps.Score:F3}");
#endif
            }
        }

        public void UpdateNodes(NodeViewModel[] nodeVMs)
        {
            Loan.RunOnMainthread(() =>
            {
                lastNodeUpdatedAt.Value = Time.time;
                foreach (var vm in nodeVMs)
                {
                    HOONode parent;
                    nodes.TryGetValue(vm.ParentId, out parent);
                    if (nodes.TryGetValue(vm.Id, out var existingNode))
                    {
                        existingNode.UpdateNode(parent, vm, Context);
                        continue;
                    }

                    InstantiateNode(vm, Context.Value.ArrowPrefab, parent);
                }
                var tCtx = Context.Value;
                tCtx.MaxScore = Math.Max(tCtx.MaxScore, nodeVMs.Max(vm => vm.Score));
                tCtx.MinScore = Math.Min(tCtx.MinScore, nodeVMs.Min(vm => vm.Score));
                tCtx.MaxDepth = Math.Max(tCtx.MaxDepth, nodeVMs.Max(vm => vm.Depth));
                tCtx.ScoreColoringRange = Math.Max(0, tCtx.MaxScore - 0.3f);
                tCtx.ActiveNodeId = nodeVMs.Last().Id;
                Context.Value = tCtx;
            });
        }

        void InstantiateNode(NodeViewModel vm, GameObject arrowPrefab, [MaybeNull] HOONode parent)
        {
            var go = Instantiate(arrowPrefab, parent?.transform);
            go.name = vm.Id;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var node = go.AddComponent<HOONode>();
            node.UpdateNode(parent, vm, Context);
            nodes.Add(vm.Id, node);

            go.transform.parent = parent == null ? TreeRoot.transform : parent.gameObject.transform;
            if (parent != null)
                parent.HasChild = parent != null;
        }


        public void ResetNodes()
        {
            Loan.RunOnMainthreadSynchronized(() =>
            {
                ResetContext();

                Enumerable.Range(0, TreeRoot.transform.childCount)
                    .Select(TreeRoot.transform.GetChild)
                    .ForEach(tf => Object.Destroy(tf.gameObject));
                nodes.Clear();
            });
        }

        void ResetContext()
        {
            var tCtx = Context.Value;
            tCtx.MaxScore = 0;
            tCtx.MinScore = 1;
            tCtx.MaxDepth = 0;
            tCtx.IsShowingHOOArrow = true;
            tCtx.IsShowingGridSearchArrow = false;
            tCtx.IsShowingHOOBound = true;
            tCtx.IsShowingFrustum = true;
            tCtx.ActiveNodeId = "";
            tCtx.ArrowObjectPool = arrowObjectPool;
            Context.Value = tCtx;
        }
    }
}
