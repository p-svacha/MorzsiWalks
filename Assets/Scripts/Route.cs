using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Route
{
    public List<Node> Nodes;
    public List<Path> Paths;

    public float Length { get; private set; }

    public Route(List<Node> nodes, List<Path> paths)
    {
        Nodes = nodes;
        Paths = paths;
        Length = Paths.Sum(x => x.Length);
    }

    public void AddPath(Path path, Node targetNode)
    {
        Nodes.Add(targetNode);
        Paths.Add(path);
        Length = Paths.Sum(x => x.Length);
    }

    public void Highlight()
    {
        // Colors
        foreach (Node node in Nodes) node.SetColor(Main.Singleton.HighlightColor);
        foreach (Path path in Paths) path.SetColor(Main.Singleton.HighlightColor);

        // Create direction arrows
        for (int i = 1; i < Nodes.Count; i++) Paths[i - 1].ShowArrowhead(Nodes[i], Main.Singleton.HighlightColor);
    }

    public void Unhighlight()
    {
        // Colors
        foreach (Node node in Nodes) node.SetColor(Main.Singleton.DefaultColor);
        foreach (Path path in Paths) path.ResetDisplay();
    }

    public string GetLengthString()
    {
        int totalSeconds = (int)(Length * 60);  // Convert minutes to seconds
        int minutesPart = totalSeconds / 60;     // Extract the minutes part
        int secondsPart = totalSeconds % 60;     // Extract the seconds part

        string formattedTime = $"{minutesPart:00}:{secondsPart:00}";  // Format the time as "mm:ss"

        return formattedTime;
    }
}
