using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
    private Mode Mode;
    private string CurrentMap;


    [Header("UI Elements")]
    public TextMeshProUGUI InstructionsText;
    public TextMeshProUGUI RouteText;

    public MenuButton ViewButton;
    public MenuButton AddNodeButton;
    public MenuButton AddPathButton;
    public MenuButton ChangePathPriorityButton;
    public MenuButton ChangePathDirectionButton;
    public MenuButton RemoveNodeButton;
    public MenuButton RemovePathButton;

    public TMP_InputField RouteLengthInput;
    public MenuButton GenerateRouteButton;
    public MenuButton SimulateRouteButton;
    public MenuButton UnhighlightRouteButton;

    public MenuButton SaveMapButton;

    [Header("Colors")]
    public Color LowestPrioColor;
    public Color LowPrioColor;
    public Color MediumPrioColor;
    public Color HighPrioColor;
    public Color HighestPrioColor;

    public Color DefaultColor;
    public Color HighlightColor;

    [Header("Prefabs")]
    public Node NodePrefab;
    public Path PathPrefab;

    private int NextNodeId;
    public Dictionary<int, Node> Nodes;

    private int NextPathId;
    public Dictionary<int, Path> Paths;

    private Route HighlightedRoute;
    private Route SimulatedRoute;

    private Path CurrentPath;
    private Node CurrentRouteStart;

    private const float DESIRED_ROUTE_LENGTH_MAX_DEVIATION = 0.3f; // % value how much the length of a generated route may deviate from the desired length (i.e. 30% on 20 minutes would accept 14-26 minutes)

    public static Main Singleton;

    private void Start()
    {
        Singleton = GameObject.Find("Main").GetComponent<Main>();

        ViewButton.Button.onClick.AddListener(() => SelectMode(Mode.View));
        AddNodeButton.Button.onClick.AddListener(() => SelectMode(Mode.AddNode));
        AddPathButton.Button.onClick.AddListener(() => SelectMode(Mode.AddPathStart));
        ChangePathPriorityButton.Button.onClick.AddListener(() => SelectMode(Mode.ChangePathPriority));
        ChangePathDirectionButton.Button.onClick.AddListener(() => SelectMode(Mode.ChangePathDirection));
        RemoveNodeButton.Button.onClick.AddListener(() => SelectMode(Mode.RemoveNode));
        RemovePathButton.Button.onClick.AddListener(() => SelectMode(Mode.RemovePath));

        GenerateRouteButton.Button.onClick.AddListener(() => SelectMode(Mode.GenerateRouteStart));
        SimulateRouteButton.Button.onClick.AddListener(() => SelectMode(Mode.SimulateRouteStart));
        UnhighlightRouteButton.Button.onClick.AddListener(UnhighlightEverything);

        SaveMapButton.Button.onClick.AddListener(() => SaveMap(CurrentMap));
        

        Nodes = new Dictionary<int, Node>();
        Paths = new Dictionary<int, Path>();

        LoadMap(JsonUtilities.LoadGame("reding"));

        SelectMode(Mode.View);
    }

    #region Route Generation

    private void GenerateAndHighlightRoute(Node start, Node end, int targetLength)
    {
        // Unhighlight old route
        UnhighlightEverything();

        // Generate route
        Route route = GenerateRoute(start, end, targetLength);

        // Highlight route
        HighlightRoute(route);
    }

    private Route GenerateRoute(Node start, Node end, int targetLength)
    {
        float maxLengthDeviation = targetLength * DESIRED_ROUTE_LENGTH_MAX_DEVIATION;
        float minLength = targetLength - maxLengthDeviation;
        float maxLength = targetLength + maxLengthDeviation;

        // The first x paths and nodes are allowed to be visited twice
        int numFirstPathsAllowedToRevisit = 2 + (targetLength / 30);

        // Calculate shortest route to start for every node
        foreach (Node node in Nodes.Values) node.MinLengthToEnd = node.ShortestPaths[end];

        // Generate route
        Route route = null;

        while (route == null || (start.MinLengthToEnd < maxLength && (route.Length < minLength || route.Length > maxLength)))
        {
            List<Node> routeNodes = new List<Node>();
            List<Path> routePaths = new List<Path>();
            float remainingLength = targetLength;
            Node currentNode = start;
            routeNodes.Add(currentNode);
            int counter = 0;
            bool routeIsInvalid = false;

            List<KeyValuePair<Path, Node>> chosenPaths = new List<KeyValuePair<Path, Node>>();

            while ((routeNodes.Last() != end || routeNodes.Count <= 1) && counter < 200)
            {
                counter++;

                // Identify candidates and their probabilities for the next path
                Dictionary<KeyValuePair<Path, Node>, float> nextPathCandidates = new Dictionary<KeyValuePair<Path, Node>, float>();
                foreach (var path in currentNode.ConnectedPaths)
                {
                    if (routePaths.Count > 0 && path.Key == routePaths.Last())// Can't return the exact path we came from
                    {
                        Debug.Log("Excluding path " + path.Key.Id + " because it's the path we came from.");
                        continue;
                    }
                    if(chosenPaths.Where(x => x.Key == path.Key && x.Value == path.Value).Count() > 0) // Can't take the same path twice in the same direction
                    {
                        Debug.Log("Excluding path " + path.Key.Id + " because we already walked that path in the same direction.");
                        continue;
                    }
                    if(chosenPaths.Where(x => x.Key == path.Key && (chosenPaths.IndexOf(x) >= numFirstPathsAllowedToRevisit || remainingLength > maxLengthDeviation + 3f)).Count() > 0) // Except for the first few, can't take a path twice (no matter what direction)
                    {
                        Debug.Log("Excluding path " + path.Key.Id + " because we already walked that path (and it's not one of the first " + numFirstPathsAllowedToRevisit + " paths).");
                        continue;
                    }
                    if (chosenPaths.Where(x => x.Value == path.Value && chosenPaths.IndexOf(x) >= numFirstPathsAllowedToRevisit).Count() > 0) // Except for the first few, can't visit the same node twice
                    {
                        Debug.Log("Excluding path " + path.Key.Id + " because it would lead to a node that we already visited (and it's not one of the first " + (numFirstPathsAllowedToRevisit + 1) + " nodes).");
                        continue;
                    }
                    if (path.Value.MinLengthToEnd > remainingLength - path.Key.Length) // Can't get back in time
                    {
                        Debug.Log("Excluding path " + path.Key.Id + " because we couldn't get home in time.");
                        continue;
                    }
                    if (remainingLength - path.Key.Length > maxLengthDeviation && path.Value == end) // Too early to end
                    {
                        Debug.Log("Excluding path " + path.Key.Id + " because then the path would end while too short.");
                        continue;
                    }

                    float probability = GetPriorityValue(path.Key.Priority);

                    nextPathCandidates.Add(path, probability); // add to candidates
                }

                // Chose next path
                KeyValuePair<Path, Node> chosenNextPath;

                // Take shortest path that we didn't come from if all are too long
                if (nextPathCandidates.Count == 0)
                {
                    Debug.Log("######## OVERRIDE ######## Taking shortest way home from here.");
                    List<KeyValuePair<Path, Node>> validPaths = new List<KeyValuePair<Path, Node>>();
                    foreach (var path in currentNode.ConnectedPaths)
                    {
                        if (routePaths.Count > 0 && path.Key == routePaths.Last())
                        {
                            Debug.Log("Excluding path " + path.Key.Id + " because it's the path we came from.");
                            continue;
                        }
                        if (chosenPaths.Where(x => x.Key == path.Key && x.Value == path.Value).Count() > 0) // Can't take the same path twice in the same direction
                        {
                            Debug.Log("Excluding path " + path.Key.Id + " because we already walked that path in the same direction.");
                            continue;
                        }
                        if (chosenPaths.Where(x => x.Key == path.Key && (chosenPaths.IndexOf(x) >= numFirstPathsAllowedToRevisit || remainingLength > maxLengthDeviation + 5f)).Count() > 0) // Except for the first few, can't take a path twice (no matter what direction)
                        {
                            Debug.Log("Excluding path " + path.Key.Id + " because we already walked that path and it's not one of the first paths.");
                            continue;
                        }
                        if (chosenPaths.Where(x => x.Value == path.Value && chosenPaths.IndexOf(x) >= numFirstPathsAllowedToRevisit).Count() > 0) // Except for the first few, can't visit the same node twice
                        {
                            Debug.Log("Excluding path " + path.Key.Id + " because it would lead to a node that we already visited (and it's not one of the first " + (numFirstPathsAllowedToRevisit + 1) + " nodes).");
                            continue;
                        }

                        validPaths.Add(path);
                    }

                    if (validPaths.Count == 0) // We are in a pickle => Abort
                    {
                        routeIsInvalid = true;
                        break;
                    }

                    chosenNextPath = validPaths.First(x => x.Value.MinLengthToEnd == validPaths.Min(y => y.Value.MinLengthToEnd));
                }

                // Else chose random one
                else chosenNextPath = GetWeightedRandomElement(nextPathCandidates);

                chosenPaths.Add(chosenNextPath);

                // Apply path
                routeNodes.Add(chosenNextPath.Value);
                routePaths.Add(chosenNextPath.Key);

                currentNode = chosenNextPath.Value;
                remainingLength -= chosenNextPath.Key.Length;

                Debug.Log(">>> " + counter + ": Added path " + chosenNextPath.Key.Id + " to route. Remaining length = " + remainingLength + ". We are at node " + currentNode.Id);
            }

            if (routeIsInvalid) continue; // Try again

            route = new Route(routeNodes, routePaths);

            Debug.Log("Generated route with length " + route.Length + ".");
        }

        return route;
    }

    private float GetPriorityValue(PathPriority prio)
    {
        return prio switch
        {
            PathPriority.VeryLow => 0.5f,
            PathPriority.Low => 0.75f,
            PathPriority.Medium => 1f,
            PathPriority.High => 1.25f,
            PathPriority.VeryHigh => 1.5f,
            _ => throw new System.Exception("prio not handled")
        };
    }

    private T GetWeightedRandomElement<T>(Dictionary<T, float> weightDictionary)
    {
        if (weightDictionary.Any(x => x.Value < 0)) throw new System.Exception("Negative probability found for " + weightDictionary.First(x => x.Value < 0).Key.ToString());
        float probabilitySum = weightDictionary.Sum(x => x.Value);
        float rng = Random.Range(0, probabilitySum);
        float tmpSum = 0;
        T chosenValue = default(T);
        bool resultFound = false;
        foreach (KeyValuePair<T, float> kvp in weightDictionary)
        {
            tmpSum += kvp.Value;
            if (rng < tmpSum)
            {
                chosenValue = kvp.Key;
                resultFound = true;
                break;
            }
        }

        if (resultFound) return chosenValue;

        if (probabilitySum == 0) throw new System.Exception("Can't return anything of " + typeof(T).FullName + " because all probabilities are 0");
        throw new System.Exception();
    }

    /// <summary>
    /// Calculates the shortest path from every node to every other node.
    /// </summary>
    public void CalculateShortestPaths()
    {
        int numNodes = Nodes.Count;

        // Initialize the shortest paths dictionary for each node
        foreach (Node node in Nodes.Values)
        {
            node.ShortestPaths = new Dictionary<Node, float>();
            node.ShortestPaths[node] = 0; // The distance to itself is 0
        }

        // Initialize the shortest paths dictionary for each connected node
        foreach (Path path in Paths.Values)
        {
            Node startNode = path.StartNode;
            Node endNode = path.EndNode;
            float length = path.Length;

            startNode.ShortestPaths[endNode] = length;
            endNode.ShortestPaths[startNode] = length;
        }

        // Perform the Floyd-Warshall algorithm
        for (int k = 0; k < numNodes; k++)
        {
            for (int i = 0; i < numNodes; i++)
            {
                for (int j = 0; j < numNodes; j++)
                {
                    float distanceIK, distanceKJ;
                    Node nodeI = Nodes.Values.ToList()[i];
                    Node nodeJ = Nodes.Values.ToList()[j];
                    Node nodeK = Nodes.Values.ToList()[k];

                    // Check if a path exists from i to k
                    if (nodeI.ShortestPaths.TryGetValue(nodeK, out distanceIK))
                    {
                        // Check if a path exists from k to j
                        if (nodeK.ShortestPaths.TryGetValue(nodeJ, out distanceKJ))
                        {
                            float currentDistance;
                            // Calculate the distance from i to j via k
                            if (!nodeI.ShortestPaths.TryGetValue(nodeJ, out currentDistance))
                            {
                                currentDistance = float.PositiveInfinity;
                            }

                            float newDistance = distanceIK + distanceKJ;
                            if (newDistance < currentDistance)
                            {
                                nodeI.ShortestPaths[nodeJ] = newDistance;
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Display

    private void HighlightRoute(Route route)
    {
        HighlightedRoute = route;
        HighlightedRoute.Highlight();

        RouteText.text = "Highlighted route has an estimated length of " + HighlightedRoute.GetLengthString() + ".";
    }

    private void UnhighlightEverything()
    {
        HighlightedRoute = null;
        SimulatedRoute = null;
        RouteText.text = "";

        foreach (Node node in Nodes.Values) node.SetColor(DefaultColor);
        foreach (Path path in Paths.Values) path.ResetDisplay();
    }

    #endregion

    #region Modes

    private void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;

        Node hoveredNode = null;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D[] hit = Physics2D.RaycastAll(mouseWorldPos, Vector2.zero);
        if (hit.Any(x => x.collider.GetComponent<Node>() != null))
        {
            hoveredNode = hit.First(x => x.collider.GetComponent<Node>() != null).collider.GetComponent<Node>();
        }

        Path hoveredPath = null;
        if (hit.Any(x => x.collider.GetComponent<Path>() != null))
        {
            hoveredPath = hit.First(x => x.collider.GetComponent<Path>() != null).collider.GetComponent<Path>();
        }

        switch (Mode)
        {
            case Mode.AddNode:
                if (Input.GetMouseButtonDown(0)) AddNode();
                break;

            case Mode.AddPathStart:
                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredNode != null)
                    {
                        CurrentPath = Instantiate(PathPrefab);
                        CurrentPath.Init(NextPathId++, hoveredNode);
                        SelectMode(Mode.AddPath);
                    }
                }
                break;

            case Mode.AddPath:
                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredNode != null && hoveredNode != CurrentPath.StartNode)
                    {
                        CurrentPath.End(hoveredNode);
                        Paths.Add(CurrentPath.Id, CurrentPath);
                        CurrentPath = null;
                        SelectMode(Mode.AddPathStart);
                    }
                    else
                    {
                        CurrentPath.AddPoint(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                    }

                    UnhighlightEverything();
                }
                if (Input.GetMouseButtonDown(1))
                {
                    RemoveCurrentPath();
                    SelectMode(Mode.AddPathStart);
                }
                break;

            case Mode.RemoveNode:
                if (Input.GetMouseButtonDown(0))
                    if (hoveredNode != null && hoveredNode.ConnectedPaths.Count == 0)
                        RemoveNode(hoveredNode);
                break;

            case Mode.RemovePath:
                if (Input.GetMouseButtonDown(0))
                    if (hoveredPath != null)
                        RemovePath(hoveredPath);
                break;

            case Mode.GenerateRouteStart:
                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredNode != null)
                    {
                        CurrentRouteStart = hoveredNode;
                        HighlightRoute(new Route(new List<Node>() { CurrentRouteStart }, new List<Path>()));
                        SelectMode(Mode.GenerateRouteEnd);
                    }
                }
                if (Input.GetMouseButtonDown(1)) UnhighlightEverything();
                break;

            case Mode.GenerateRouteEnd:
                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredNode != null)
                    {
                        GenerateAndHighlightRoute(CurrentRouteStart, hoveredNode, int.Parse(RouteLengthInput.text));
                        CurrentRouteStart = null;
                        SelectMode(Mode.GenerateRouteStart);
                    }
                }
                if(Input.GetMouseButtonDown(1))
                {
                    UnhighlightEverything();
                    SelectMode(Mode.GenerateRouteStart);
                }
                break;

            case Mode.SimulateRouteStart:
                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredNode != null)
                    {
                        SimulatedRoute = new Route(new List<Node>() { hoveredNode }, new List<Path>());
                        HighlightRoute(SimulatedRoute);
                        SelectMode(Mode.SimulateRoute);
                    }
                }
                break;

            case Mode.SimulateRoute:
                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredNode != null && hoveredNode != SimulatedRoute.Nodes.Last())
                    {
                        Path path = SimulatedRoute.Nodes.Last().ConnectedPaths.FirstOrDefault(x => x.Value == hoveredNode).Key;
                        if (path != null)
                        {
                            SimulatedRoute.AddPath(path, hoveredNode);
                            HighlightRoute(SimulatedRoute);
                        }
                    }
                }
                if(Input.GetMouseButtonDown(1))
                {
                    SelectMode(Mode.SimulateRouteStart);
                }
                break;

            case Mode.ChangePathDirection:
                if (Input.GetMouseButtonDown(0))
                    if (hoveredPath != null)
                        hoveredPath.CycleDirection();
                break;

            case Mode.ChangePathPriority:
                if (Input.GetMouseButtonDown(0))
                    if (hoveredPath != null)
                        hoveredPath.IncreasePriority();

                if (Input.GetMouseButtonDown(1))
                    if (hoveredPath != null)
                        hoveredPath.DecreasePriority();
                break;
        }
    }

    private void AddNode()
    {
        Node newNode = Instantiate(NodePrefab);
        newNode.Init(NextNodeId++, Camera.main.ScreenToWorldPoint(Input.mousePosition));
        Nodes.Add(newNode.Id, newNode);
        UnhighlightEverything();
    }

    private void RemoveNode(Node node)
    {
        Destroy(node.gameObject);
        Nodes.Remove(node.Id);
        UnhighlightEverything();
    }

    /// <summary>
    /// Removes the path that is currently being made
    /// </summary>
    private void RemoveCurrentPath()
    {
        Destroy(CurrentPath.gameObject);
        CurrentPath = null;
    }
    private void RemovePath(Path path)
    {
        Destroy(path.gameObject);
        Paths.Remove(path.Id);

        if (path.StartNode.ConnectedPaths.ContainsKey(path)) path.StartNode.ConnectedPaths.Remove(path);
        if (path.EndNode.ConnectedPaths.ContainsKey(path)) path.EndNode.ConnectedPaths.Remove(path);

        UnhighlightEverything();
    }

    public void SelectMode(Mode mode)
    {
        if (CurrentPath != null && mode != Mode.AddPath) RemoveCurrentPath();
        if (SimulatedRoute != null && mode != Mode.SimulateRoute) UnhighlightEverything();

        GetButtonForMode(Mode).Unselect();
        Mode = mode;
        GetButtonForMode(Mode).Select();
        InstructionsText.text = GetModeInstructions(Mode);
    }

    private MenuButton GetButtonForMode(Mode mode)
    {
        return mode switch
        {
            Mode.View => ViewButton,
            Mode.AddNode => AddNodeButton,
            Mode.AddPath => AddPathButton,
            Mode.AddPathStart => AddPathButton,
            Mode.ChangePathPriority => ChangePathPriorityButton,
            Mode.ChangePathDirection => ChangePathDirectionButton,
            Mode.RemoveNode => RemoveNodeButton,
            Mode.RemovePath => RemovePathButton,
            Mode.GenerateRouteStart => GenerateRouteButton,
            Mode.GenerateRouteEnd => GenerateRouteButton,
            Mode.SimulateRouteStart => SimulateRouteButton,
            Mode.SimulateRoute => SimulateRouteButton,
            _ => throw new System.Exception("Mode " + mode.ToString() + " not handled.")
        };
    }

    private string GetModeInstructions(Mode mode)
    {
        return mode switch
        {
            Mode.View => "",
            Mode.AddNode => "Click anywhere to place a node.",
            Mode.AddPathStart => "Click on a node where the new path should start.",
            Mode.AddPath => "Click anywhere to continue the path.\nClick on a node other than the start node to end the path.\nRight click to stop.",
            Mode.ChangePathPriority => "Leftclick on a path to increase its priority.\nRightclick on a path to decrease its priority.",
            Mode.ChangePathDirection => "Click on a path to change which directions you are allowed to traverse it.",
            Mode.RemoveNode => "Click on a node with no connections to remove it.",
            Mode.RemovePath => "Click on a path to remove it.",
            Mode.GenerateRouteStart => "Click on a node that will be the start point of the route.\nRightclick to remove previous route.",
            Mode.GenerateRouteEnd => "Click on a node that will be the end point of the route.\nRightclick to abort.",
            Mode.SimulateRouteStart => "Click on a node where the route starts.",
            Mode.SimulateRoute => "Click on a node connected to the previous node to continue the route.\nRightclick to remove current route.",
            _ => throw new System.Exception("Mode " + mode.ToString() + " not handled.")
        };
    }

    #endregion

    #region Save / Load

    private void Clear()
    {
        foreach (Node n in Nodes.Values) Destroy(n.gameObject);
        foreach (Path p in Paths.Values) Destroy(p.gameObject);

        Nodes.Clear();
        Paths.Clear();
    }

    private void LoadMap(MapData mapData)
    {
        Clear();

        // Load nodes
        foreach(NodeData data in mapData.Nodes)
        {
            Node newNode = Instantiate(NodePrefab);
            newNode.Init(data);
            Nodes.Add(newNode.Id, newNode);
        }

        // Load paths
        foreach(PathData data in mapData.Paths)
        {
            // Discard empty paths
            if (data.StartNodeId == data.EndNodeId && data.PointsX.Count == 0) continue;

            Path newPath = Instantiate(PathPrefab);
            newPath.Init(this, data);
            Paths.Add(newPath.Id, newPath);
        }

        NextNodeId = Nodes.Keys.Count == 0 ? 1 : Nodes.Keys.Max() + 1;
        NextPathId = Paths.Keys.Count == 0 ? 1 : Paths.Keys.Max() + 1;

        CurrentMap = mapData.Name;

        UnhighlightEverything();
        CalculateShortestPaths();
    }

    private void SaveMap(string saveName)
    {
        saveName = saveName == "" ? "reding" : saveName;

        // Create save instance
        MapData mapData = new MapData();
        mapData.Name = saveName;

        // Nodes
        mapData.Nodes = Nodes.Select(x => x.Value.ToData()).ToList();

        // Paths
        mapData.Paths = Paths.Select(x => x.Value.ToData()).ToList();

        // Save
        JsonUtilities.SaveMap(mapData);

        // Reload newly created save
        LoadMap(JsonUtilities.LoadGame(mapData.Name));
    }

    #endregion
}
