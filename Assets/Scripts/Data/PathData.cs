using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PathData
{
    public int Id { get; set; }
    public int StartNodeId { get; set; }
    public int EndNodeId { get; set; }
    public List<float> PointsX { get; set; }
    public List<float> PointsY { get; set; }
    public PathDirection Direction { get; set; }
    public PathPriority Priority { get; set; }
}
