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
    public MenuButton RemoveNodeButton;
    public MenuButton RemovePathButton;

    public TMP_InputField RouteLengthInput;
    public MenuButton GenerateRouteButton;
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
    public SpriteRenderer ArrowheadPrefab;

    private int NextNodeId;
    public Dictionary<int, Node> Nodes;

    private int NextPathId;
    public Dictionary<int, Path> Paths;

    private Route HighlightedRoute;

    private Path CurrentPath;
    private Node CurrentRouteStart;

    private void Start()
    {
        ViewButton.Button.onClick.AddListener(() => SelectMode(Mode.View));
        AddNodeButton.Button.onClick.AddListener(() => SelectMode(Mode.AddNode));
        AddPathButton.Button.onClick.AddListener(() => SelectMode(Mode.AddPathStart));
        RemoveNodeButton.Button.onClick.AddListener(() => SelectMode(Mode.RemoveNode));
        RemovePathButton.Button.onClick.AddListener(() => SelectMode(Mode.RemovePath));
        GenerateRouteButton.Button.onClick.AddListener(() => SelectMode(Mode.GenerateRouteStart));
        SaveMapButton.Button.onClick.AddListener(() => SaveMap(CurrentMap));
        UnhighlightRouteButton.Button.onClick.AddListener(UnhighlightRoute);

        Nodes = new Dictionary<int, Node>();
        Paths = new Dictionary<int, Path>();

        LoadMap(JsonUtilities.LoadGame("reding"));

        SelectMode(Mode.View);
    }

    #region Route Generation

    private void GenerateAndHighlightRoute(Node start, Node end, int targetLength)
    {
        // Unhighlight old route
        UnhighlightRoute();

        // Generate route
        Route route = GenerateRoute(start, end, targetLength);

        // Highlight route
        HighlightRoute(route);
    }

    private Route GenerateRoute(Node start, Node end, int targetLength)
    {
        // Calculate shortest route to start for every node
        foreach (Node node in Nodes.Values) node.MinLengthToEnd = node.ShortestPaths[end];

        // Generate route
        List<Node> routeNodes = new List<Node>();
        List<Path> routePaths = new List<Path>();
        float remainingLength = targetLength;
        Node currentNode = start;
        routeNodes.Add(currentNode);
        int counter = 0;


        while((routeNodes.Last() != end || routeNodes.Count <= 1) && counter < 200)
        {
            counter++;

            // Identify candidates and their probabilities for the next path
            Dictionary<KeyValuePair<Path, Node>, float> nextPathCandidates = new Dictionary<KeyValuePair<Path, Node>, float>();
            foreach(var path in currentNode.ConnectedPaths)
            {
                if (routePaths.Count > 0 && path.Key == routePaths.Last())// Can't return the exact path we came from
                {
                    Debug.Log("Excluding path " + path.Key.Id + " because it's the path we came from.");
                    continue;
                }
                if (path.Value.MinLengthToEnd > remainingLength - path.Key.Length) // Can't get back in time
                {
                    Debug.Log("Excluding path " + path.Key.Id + " because we couldn't get home in time.");
                    continue;
                }
                if (remainingLength > 2f && path.Value == end) // Too early to end
                {
                    Debug.Log("Excluding path " + path.Key.Id + " because then the path would end while too short.");
                    continue;
                }

                float probability = 1f; // default probability
                if (routeNodes.Contains(path.Value)) probability = 0.1f; // low probability for nodes we already visited

                nextPathCandidates.Add(path, probability); // add to candidates
            }

            // Chose next path
            KeyValuePair<Path, Node> chosenNextPath;

            // Take shortest path that we didn't come from if all are too long
            if (nextPathCandidates.Count == 0)
            {
                Debug.Log("######## OVERRIDE ######## Taking shortest way home from here.");
                List<KeyValuePair<Path, Node>> validPaths = currentNode.ConnectedPaths.Where(x => routePaths.Count == 0 || x.Key != routePaths.Last()).ToList(); // all paths are valid here except the one we came from
                chosenNextPath = validPaths.First(x => x.Value.MinLengthToEnd == validPaths.Min(y => y.Value.MinLengthToEnd));
            }

            // Else chose random one
            else chosenNextPath = GetWeightedRandomElement(nextPathCandidates);

            // Apply path
            routeNodes.Add(chosenNextPath.Value);
            routePaths.Add(chosenNextPath.Key);

            currentNode = chosenNextPath.Value;
            remainingLength -= chosenNextPath.Key.Length;

            Debug.Log(">>> " + counter + ": Added path " + chosenNextPath.Key.Id + " to route. Remaining length = " + remainingLength + ". We are at node " + currentNode.Id);
        }

        Route route = new Route(routeNodes, routePaths);

        Debug.Log("Generated route with length " + route.Length + ".");

        return route;
    }

    private void HighlightRoute(Route route)
    {
        HighlightedRoute = route;
        HighlightedRoute.Highlight(this);

        RouteText.text = "Highlighted route has an estimated length of " + (int)Mathf.Round(HighlightedRoute.Length) + " minutes.";
    }

    private void UnhighlightRoute()
    {
        if (HighlightedRoute == null) return;

        HighlightedRoute.Unhighlight(this);
        HighlightedRoute = null;

        RouteText.text = "";
    }

    private void UnhighlightEverything()
    {
        UnhighlightRoute();

        foreach (Node node in Nodes.Values) node.SetColor(DefaultColor);
        foreach (Path path in Paths.Values) path.SetColor(DefaultColor);
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

    #region Edit Map

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
                if(Input.GetMouseButtonDown(0)) AddNode();
                break;

            case Mode.AddPathStart:
                if(Input.GetMouseButtonDown(0))
                {
                    if(hoveredNode != null)
                    {
                        CurrentPath = Instantiate(PathPrefab);
                        CurrentPath.Init(NextPathId++, hoveredNode);
                        SelectMode(Mode.AddPath);
                    }
                }
                break;

            case Mode.AddPath:
                if(Input.GetMouseButtonDown(0))
                {
                    if(hoveredNode != null && hoveredNode != CurrentPath.StartNode)
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
                if(Input.GetMouseButtonDown(1))
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
                        SelectMode(Mode.GenerateRouteEnd);
                    }
                }
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

        path.StartNode.ConnectedPaths.Remove(path);
        path.EndNode.ConnectedPaths.Remove(path);

        UnhighlightEverything();
    }

    public void SelectMode(Mode mode)
    {
        if (CurrentPath != null && mode != Mode.AddPath) RemoveCurrentPath();

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
            Mode.RemoveNode => RemoveNodeButton,
            Mode.RemovePath => RemovePathButton,
            Mode.GenerateRouteStart => GenerateRouteButton,
            Mode.GenerateRouteEnd => GenerateRouteButton,
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
            Mode.RemoveNode => "Click on a node with no connections to remove it.",
            Mode.RemovePath => "Click on a path to remove it.",
            Mode.GenerateRouteStart => "Click on a node that will be the start point of the route.",
            Mode.GenerateRouteEnd => "Click on a node that will be the end point of the route.",
            _ => throw new System.Exception("Mode " + mode.ToString() + " not handled.")
        };
    }

    #endregion

    private void ShowMapUrls()
    {
        int mapSizeX = 2;
        int mapSizeY = 2;
        for (int y = -mapSizeY; y <= mapSizeY; y++)
        {
            for (int x = -mapSizeX; x <= mapSizeX; x++)
            {
                Debug.Log(x + "/" + y + ":                           " + GoogleMapUrlCreator.GetUrlForSector(x, y));
            }
        }
    }

    private void Clear()
    {
        foreach (Node n in Nodes.Values) Destroy(n.gameObject);
        foreach (Path p in Paths.Values) Destroy(p.gameObject);

        Nodes.Clear();
        Paths.Clear();
    }

    #region Save / Load

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

            newPath.StartNode.ConnectedPaths.Add(newPath, newPath.EndNode);
            if(newPath.StartNode != newPath.EndNode) newPath.EndNode.ConnectedPaths.Add(newPath, newPath.StartNode);
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
}
