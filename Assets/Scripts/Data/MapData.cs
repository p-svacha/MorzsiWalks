using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapData
{
    public string Name;
    public List<NodeData> Nodes { get; set; }
    public List<PathData> Paths { get; set; }
}
