using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Node : MonoBehaviour
{
    public SpriteRenderer Image;

    public int Id;
    public Vector3 Position;
    public Dictionary<Path, Node> ConnectedPaths; // keys are paths, nodes are where the path leads to
    public Dictionary<Node, float>  ShortestPaths;
    public float MinLengthToEnd;

    public void Init(int id, Vector3 pos)
    {
        Id = id;
        Position = new Vector3(pos.x, pos.y, 0f);
        transform.position = Position;
        ConnectedPaths = new Dictionary<Path, Node>();
    }

    public void Init(NodeData data)
    {
        Id = data.Id;
        Position = new Vector3(data.PositionX, data.PositionY, 0f);
        transform.position = Position;
        ConnectedPaths = new Dictionary<Path, Node>();
    }

    public void SetColor(Color c)
    {
        Image.color = c;
    }

    public NodeData ToData()
    {
        NodeData data = new NodeData();
        data.Id = Id;
        data.PositionX = Position.x;
        data.PositionY = Position.y;
        return data;
    }
}
