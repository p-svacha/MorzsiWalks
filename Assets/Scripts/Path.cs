using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Path : MonoBehaviour
{
    public int Id;

    public int StartNodeId { get; private set; }
    public Node StartNode;

    public int EndNodeId { get; private set; }
    public Node EndNode;

    public List<Vector3> Points { get; private set; }

    public PathDirection Direction;
    public PathPriority Priority;

    public float Length;
    private const float WORLD_DISTANCE_TO_LENGTH_FACTOR = 0.142f;

    public LineRenderer Line;
    private float PathVisualizationWidth = 0.3f;

    public SpriteRenderer ArrowheadPrefab;
    private const float ARROWHEAD_POSITION_OFFSET = 0.6f;
    public SpriteRenderer ArrowheadToEnd;
    public SpriteRenderer ArrowheadToStart;

    public PolygonCollider2D Collider;

    public void Init(int id, Node startNode)
    {
        Id = id;
        StartNode = startNode;
        StartNodeId = startNode.Id;
        Points = new List<Vector3>();
        Priority = PathPriority.Medium;
        Direction = PathDirection.Bidirectional;
    }

    public void AddPoint(Vector3 pos)
    {
        Points.Add(new Vector3(pos.x, pos.y, 0f));
        UpdateLineRenderer();
        UpdateCollider();
    }
    public void End(Node endNode)
    {
        EndNode = endNode;
        EndNodeId = endNode.Id;

        UpdatedConnectedPaths();
        CalculateLength();

        UpdateLineRenderer();
        UpdateCollider();
        CreateArrowheads();

        ResetDisplay();
    }

    public void IncreasePriority()
    {
        int currentIndex = (int)Priority;
        int totalValues = Enum.GetValues(typeof(PathPriority)).Length;
        int nextIndex = Math.Min(currentIndex + 1, totalValues - 1);
        Priority = (PathPriority)nextIndex;

        ResetDisplay();
    }
    public void DecreasePriority()
    {
        int currentIndex = (int)Priority;
        int previousIndex = Math.Max(currentIndex - 1, 0);
        Priority = (PathPriority)previousIndex;

        ResetDisplay();
    }

    public void CycleDirection()
    {
        int currentIndex = (int)Direction;
        int nextIndex = (currentIndex + 1) % Enum.GetValues(typeof(PathDirection)).Length;
        Direction = (PathDirection)nextIndex;

        UpdatedConnectedPaths();

        ResetDisplay();
    }

    private void UpdatedConnectedPaths()
    {
        if (StartNode.ConnectedPaths.ContainsKey(this)) StartNode.ConnectedPaths.Remove(this);
        if (EndNode.ConnectedPaths.ContainsKey(this)) EndNode.ConnectedPaths.Remove(this);

        if (Direction == PathDirection.Bidirectional || Direction == PathDirection.UnidirectionalToStart) EndNode.ConnectedPaths.Add(this, StartNode);
        if (Direction == PathDirection.Bidirectional || Direction == PathDirection.UnidirectionalToEnd) StartNode.ConnectedPaths.Add(this, EndNode);
    }

    #region Display

    private void UpdateLineRenderer()
    {
        List<Vector3> points = GetAllPathPoints();

        if (points.Count >= 2)
        {
            Line.startWidth = PathVisualizationWidth;
            Line.endWidth = PathVisualizationWidth;
            Line.startColor = Main.Singleton.HighlightColor;
            Line.endColor = Main.Singleton.HighlightColor;
            Line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                Line.SetPosition(i, points[i]);
            }
        }
    }
    public void SetColor(Color c)
    {
        Line.startColor = c;
        Line.endColor = c;
    }

    public void ResetDisplay()
    {
        // Color
        Line.startColor = GetColor(Priority);
        Line.endColor = GetColor(Priority);

        // Arrowhead
        ResetArrowheads();
    }

    public Color GetColor(PathPriority prio)
    {
        return prio switch
        {
            PathPriority.VeryLow => Main.Singleton.LowestPrioColor,
            PathPriority.Low => Main.Singleton.LowPrioColor,
            PathPriority.Medium => Main.Singleton.MediumPrioColor,
            PathPriority.High => Main.Singleton.HighPrioColor,
            PathPriority.VeryHigh => Main.Singleton.HighestPrioColor,
            _ => throw new System.Exception("prio not handled")
        };
    }

    private void CreateArrowheads()
    {
        ArrowheadToEnd = DrawArrowhead(EndNode);
        ArrowheadToStart = DrawArrowhead(StartNode);

        ResetArrowheads();
    }

    public SpriteRenderer DrawArrowhead(Node target)
    {
        if (target != StartNode && target != EndNode) throw new System.Exception("Can't draw an arrowhead to a node that is not connected to this path.");

        Vector3 toPosition = target.Position;
        Vector3 fromPosition = Points.Count == 0 ? GetOtherNode(target).Position : (target == EndNode ? Points.Last() : Points.First());
        Vector3 directionVector = toPosition - fromPosition;
        float targetAngle = Vector2.SignedAngle(directionVector, Vector2.up);

        float relativePosition = 1f - (ARROWHEAD_POSITION_OFFSET / directionVector.magnitude);
        Vector3 targetPosition = fromPosition + (relativePosition * directionVector);

        SpriteRenderer arrowhead = GameObject.Instantiate(ArrowheadPrefab);
        arrowhead.transform.position = targetPosition;
        arrowhead.transform.rotation = Quaternion.Euler(0f, 0f, -targetAngle);

        arrowhead.gameObject.SetActive(false);

        return arrowhead;
    }

    public void ShowArrowhead(Node target, Color c)
    {
        if(target == StartNode)
        {
            ArrowheadToStart.gameObject.SetActive(true);
            ArrowheadToStart.color = c;
        }
        else if(target == EndNode)
        {
            ArrowheadToEnd.gameObject.SetActive(true);
            ArrowheadToEnd.color = c;
        }
        else throw new System.Exception("Can't show an arrowhead to a node that is not connected to this path.");
    }

    private void ResetArrowheads()
    {
        ArrowheadToStart.color = GetColor(Priority);
        ArrowheadToEnd.color = GetColor(Priority);

        switch (Direction)
        {
            case PathDirection.Bidirectional:
                ArrowheadToEnd.gameObject.SetActive(false);
                ArrowheadToStart.gameObject.SetActive(false);
                break;

            case PathDirection.UnidirectionalToEnd:
                ArrowheadToEnd.gameObject.SetActive(true);
                ArrowheadToStart.gameObject.SetActive(false);
                break;

            case PathDirection.UnidirectionalToStart:
                ArrowheadToEnd.gameObject.SetActive(false);
                ArrowheadToStart.gameObject.SetActive(true);
                break;
        }
    }

    #endregion

    #region Collider

    private void UpdateCollider()
    {
        Mesh mesh = new Mesh();
        Line.BakeMesh(mesh);
        MeshToPolygon2D(mesh);
    }

    private void MeshToPolygon2D(Mesh mesh)
    {
        // Get triangles and vertices from mesh
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        //vertices = vertices.Select(x => Camera.main.WorldToScreenPoint(x)).ToArray();

        // Get just the outer edges from the mesh's triangles (ignore or remove any shared edges)
        Dictionary<string, KeyValuePair<int, int>> edges = new Dictionary<string, KeyValuePair<int, int>>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int vert1 = triangles[i + e];
                int vert2 = triangles[i + e + 1 > i + 2 ? i : i + e + 1];
                string edge = Mathf.Min(vert1, vert2) + ":" + Mathf.Max(vert1, vert2);
                if (edges.ContainsKey(edge))
                {
                    edges.Remove(edge);
                }
                else
                {
                    edges.Add(edge, new KeyValuePair<int, int>(vert1, vert2));
                }
            }
        }

        // Create edge lookup (Key is first vertex, Value is second vertex, of each edge)
        Dictionary<int, int> lookup = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> edge in edges.Values)
        {
            if (lookup.ContainsKey(edge.Key) == false)
            {
                lookup.Add(edge.Key, edge.Value);
            }
        }

        // Create empty polygon collider
        Collider.pathCount = 0;

        // Loop through edge vertices in order
        int startVert = 0;
        int nextVert = startVert;
        int highestVert = startVert;
        List<Vector2> colliderPath = new List<Vector2>();
        while (true)
        {

            // Add vertex to collider path
            colliderPath.Add(vertices[nextVert]);

            // Get next vertex
            nextVert = lookup[nextVert];

            // Store highest vertex (to know what shape to move to next)
            if (nextVert > highestVert)
            {
                highestVert = nextVert;
            }

            // Shape complete
            if (nextVert == startVert)
            {

                // Add path to polygon collider
                Collider.pathCount++;
                Collider.SetPath(Collider.pathCount - 1, colliderPath.ToArray());
                colliderPath.Clear();

                // Go to next shape if one exists
                if (lookup.ContainsKey(highestVert + 1))
                {

                    // Set starting and next vertices
                    startVert = highestVert + 1;
                    nextVert = startVert;

                    // Continue to next loop
                    continue;
                }

                // No more verts
                break;
            }
        }
    }

    #endregion

    #region Getters

    private void CalculateLength()
    {
        List<Vector3> points = GetAllPathPoints();
        Length = 0f;
        for (int i = 1; i < points.Count; i++) Length += Vector3.Distance(points[i - 1], points[i]) * WORLD_DISTANCE_TO_LENGTH_FACTOR;
    }

    private List<Vector3> GetAllPathPoints()
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(StartNode.Position);
        foreach (Vector3 pos in Points) points.Add(pos);
        if (EndNode != null) points.Add(EndNode.Position);
        return points;
    }

    public Node GetOtherNode(Node node)
    {
        if (node == StartNode) return EndNode;
        if (node == EndNode) return StartNode;
        throw new System.Exception("node is not connected to path.");
    }

    #endregion

    #region Load / Save

    public void Init(Main main, PathData data)
    {
        Id = data.Id;
        StartNodeId = data.StartNodeId;
        StartNode = main.Nodes[data.StartNodeId];
        EndNodeId = data.EndNodeId;
        EndNode = main.Nodes[data.EndNodeId];
        Direction = data.Direction;
        Priority = data.Priority;
        Points = new List<Vector3>();
        for(int i = 0; i < data.PointsX.Count; i++) Points.Add(new Vector3(data.PointsX[i], data.PointsY[i], 0f));

        UpdatedConnectedPaths();
        CalculateLength();
        UpdateLineRenderer();
        UpdateCollider();
        CreateArrowheads();

        ResetDisplay();
    }

    public PathData ToData()
    {
        PathData data = new PathData();
        data.Id = Id;
        data.StartNodeId = StartNodeId;
        data.EndNodeId = EndNodeId;
        data.PointsX = Points.Select(x => x.x).ToList();
        data.PointsY = Points.Select(x => x.y).ToList();
        data.Direction = Direction;
        data.Priority = Priority;
        return data;
    }

    #endregion
}
