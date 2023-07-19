using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NodeData
{
    public int Id { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public Dictionary<int, float> ShortestDistances { get; set; }
}
