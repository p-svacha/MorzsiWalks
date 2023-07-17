using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Route
{
    public List<Node> Nodes;
    public List<Path> Paths;

    private const float ARROWHEAD_POSITION_OFFSET = 0.6f;
    private List<GameObject> DirectionArrows = new List<GameObject>();

    public float Length { get; private set; }

    public Route(List<Node> nodes, List<Path> paths)
    {
        Nodes = nodes;
        Paths = paths;
        Length = paths.Sum(x => x.Length);
    }

    public void Highlight(Main main)
    {
        // Colors
        foreach (Node node in Nodes) node.SetColor(main.HighlightColor);
        foreach (Path path in Paths) path.SetColor(main.HighlightColor);

        // Create direction arrows
        for (int i = 1; i < Nodes.Count; i++)
        {
            Vector3 toPosition = Nodes[i].Position;
            Vector3 fromPosition = Paths[i - 1].Points.Count == 0 ? Nodes[i - 1].Position : (Nodes[i] == Paths[i -1].EndNode ? Paths[i - 1].Points.Last() : Paths[i - 1].Points.First());
            Vector3 directionVector = toPosition - fromPosition;
            float targetAngle = Vector2.SignedAngle(directionVector, Vector2.up);

            float relativePosition = 1f - (ARROWHEAD_POSITION_OFFSET / directionVector.magnitude);
            Vector3 targetPosition = fromPosition + (relativePosition * directionVector);


            SpriteRenderer arrowhead = GameObject.Instantiate(main.ArrowheadPrefab);
            arrowhead.transform.position = targetPosition;
            arrowhead.transform.rotation = Quaternion.Euler(0f, 0f, -targetAngle);

            arrowhead.color = main.HighlightColor;

            DirectionArrows.Add(arrowhead.gameObject);
        }
    }

    public void Unhighlight(Main main)
    {
        // Colors
        foreach (Node node in Nodes) node.SetColor(main.DefaultColor);
        foreach (Path path in Paths) path.SetColor(main.DefaultColor);

        // Destroy direction arrows
        foreach (GameObject arrow in DirectionArrows) GameObject.Destroy(arrow);
        DirectionArrows.Clear();
    }
}
