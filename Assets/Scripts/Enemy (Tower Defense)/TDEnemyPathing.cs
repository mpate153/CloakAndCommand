using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemyPathing : MonoBehaviour
{
    //SET TO SAME AS TILEMAP DATA
    [SerializeField] private float cellHeight = 1f;
    [SerializeField] private float cellWidth = 1f;

    [SerializeField] private bool generatePath;
    [SerializeField] private bool visualizeGrid;
    [SerializeField] private bool visualizeFinalPath;
    [SerializeField] private bool ignoreDiagonals = false;

    private Vector2 startPos;
    private Vector2 endPos;
    [SerializeField] public GameObject spawnPos;
    [SerializeField] public GameObject targetPos;

    private bool pathGenerated;

    private Dictionary<Vector2, Cell> cells;

    [SerializeField] private List<Vector2> cellsToSearch;
    [SerializeField] private List<Vector2> searchedCells;
    [SerializeField] private List<Vector2> finalPath;
    
    public List<Transform> transformPoints = new List<Transform>(); //Make this private, and accessible via get functions

    [SerializeField] public Tilemap targetTileMap;
    [SerializeField] public Tile targetPathTile;

    private void Awake()
    {
        //Find tilemap
        if (targetTileMap != null){ Debug.Log("Found it"); }
        else { Debug.Log("Not Found"); }

        //Use enemyspawner spawnpoint data to determine start position
        startPos = spawnPos.transform.position;
        //Use towerOrigin (or whatever the endpoint is called) to determin the end position
        endPos = targetPos.transform.position;
    }

    private void Start()
    {
        if (generatePath && !pathGenerated)
        {
            GenerateGrid();
            FindPath(startPos, endPos);
            pathGenerated = true;
            if (visualizeFinalPath)
            {
                ShowPath();
            }
        }
        else if (!generatePath)
        {
            pathGenerated = false;
        }
    }

    void Update()
    {
        //use this to continually update path?
    }

    private void GenerateGrid()
    {
        cells = new Dictionary<Vector2, Cell>();

        //Get the bounds of the tile map
        targetTileMap.CompressBounds();
        
        //Create a grid to based on the information, adding empty cells
        for (float x = targetTileMap.cellBounds.min.x; x < targetTileMap.cellBounds.max.x; x += cellWidth)
        {
            for(float y = targetTileMap.cellBounds.min.y; y < targetTileMap.cellBounds.max.y; y += cellHeight)
            {
                Vector2 pos = new(x, y);
                cells.Add(pos, new Cell(pos));
            }
        }


        //"Dynamically" find level data that is considered a wall
        for(int x = targetTileMap.cellBounds.min.x; x < targetTileMap.cellBounds.max.x; x++)
        {
            for(int y = targetTileMap.cellBounds.min.y;  y < targetTileMap.cellBounds.max.y; y++)
            {
                //Using inspect, find coorespoding wall point
                Vector3Int inspect = new(x, y, 0);
                TileBase tile = targetTileMap.GetTile<TileBase>(inspect);
                
                //using check, make each wall considered a wall in the grid
                Vector2 check = new(x, y);
                if (tile != null)
                {
                    cells[check].isWall = true;
                }
                //tile == null means no tile was placed at that coordinate in the scene
            }
        }
    }

    private void FindPath(Vector2 startPos, Vector2 endPos)
    {
        searchedCells = new List<Vector2>();
        cellsToSearch = new List<Vector2> { startPos };
        finalPath = new List<Vector2>();

        Cell startCell = cells[startPos];
        startCell.gCost = 0;
        startCell.hCost = GetDist(startPos, endPos);
        startCell.fCost = GetDist(startPos, endPos);

        while (cellsToSearch.Count > 0)
        {
            Vector2 cellToSearch = cellsToSearch[0];

            foreach (Vector2 pos in cellsToSearch)
            {
                Cell c = cells[pos];
                if (c.fCost < cells[cellToSearch].fCost || c.fCost == cells[cellToSearch].fCost 
                    && c.hCost == cells[cellToSearch].hCost)
                {
                    cellToSearch = pos;
                }
            }

            cellsToSearch.Remove(cellToSearch);
            searchedCells.Add(cellToSearch);

            //Trace back fom destination to beginning
            if (cellToSearch == endPos)
            {
                //Offset used to place transform data point in the center of a tilemap cell
                Vector2 offset = new(0.5f, 0.5f);
                Cell pathCell = cells[endPos];

                while (pathCell.position != startPos)
                {
                    finalPath.Add(pathCell.position);

                    //Create gameobject that only holds transform data
                    GameObject wayPoint = new("WayPoint");
                    wayPoint.transform.position = pathCell.position + offset;
                    transformPoints.Add(wayPoint.transform);

                    pathCell = cells[pathCell.connection];
                }

                finalPath.Add(startPos);

                //Create last gameobject that holds transform data
                GameObject wayPoint2 = new("WayPoint");
                wayPoint2.transform.position = pathCell.position + offset;
                transformPoints.Add(wayPoint2.transform);

                return;
            }

            SearchCellNeighbors(cellToSearch, endPos);
        }
    }

    private void SearchCellNeighbors(Vector2 cellPos, Vector2 endPos)
    {
        for (float x = cellPos.x - cellWidth; x <= cellWidth + cellPos.x; x += cellWidth)
        {
            for (float y = cellPos.y - cellHeight; y <= cellHeight + cellPos.y; y += cellHeight)
            {
                //Ignore Diagonals via a series of checks and alterations to the iteration counter
                if (ignoreDiagonals)
                {
                    if (x == cellPos.x - cellWidth && y == cellPos.y - cellHeight || 
                        x == cellPos.x + cellWidth && y == cellPos.y - cellHeight || 
                        x == cellPos.x - cellWidth && y == cellPos.y + cellHeight || 
                        x == cellPos.x + cellWidth && y == cellPos.y + cellHeight)
                    {
                        continue;
                    }
                }

                Vector2 neighborPos = new(x, y);
                if (cells.TryGetValue(neighborPos, out Cell c) && !searchedCells.Contains(neighborPos) && !cells[neighborPos].isWall)
                {
                    int GcostToNeighbor = cells[cellPos].gCost + GetDist(cellPos, neighborPos);

                    if (GcostToNeighbor < cells[neighborPos].gCost)
                    {
                        Cell neighborNode = cells[neighborPos];

                        neighborNode.connection = cellPos;
                        neighborNode.gCost = GcostToNeighbor;
                        neighborNode.hCost = GetDist(neighborPos, endPos);
                        neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;

                        if (!cellsToSearch.Contains(neighborPos))
                        {
                            cellsToSearch.Add(neighborPos);
                        }
                    }
                }
            }
        }
    }

    private int GetDist(Vector2 pos1, Vector2 pos2)
    {
        Vector2Int dist = new(Mathf.Abs((int)pos1.x - (int)pos2.x), Mathf.Abs((int)pos1.y - (int)pos2.y));

        int lowest = Mathf.Min(dist.x, dist.y);
        int highest = Mathf.Max(dist.x, dist.y);

        int horizontalMoveRequired = highest - lowest;
        return lowest * 14 + horizontalMoveRequired * 10; //14 refers do diagonal movement required
        //I wonder if errors occur since 14 is being multiplied when ignoreDiagonals is checked on
    }

    private void ShowPath()
    {
        Vector3Int tileInfo;
        foreach(KeyValuePair<Vector2,Cell> kvp in cells)
        {
            if (finalPath.Contains(kvp.Key))
            {
                tileInfo = new(Mathf.RoundToInt(kvp.Key.x), Mathf.RoundToInt(kvp.Key.y), 0);
                targetTileMap.SetTile(tileInfo, targetPathTile);
                targetTileMap.RefreshTile(tileInfo);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if(!visualizeGrid || cells == null)
        {
            return;
        }

        foreach (KeyValuePair<Vector2, Cell> kvp in cells)
        {
            if (!kvp.Value.isWall)
            {
                Gizmos.color = Color.white;
            }
            else
            {
                Gizmos.color = Color.black;
            }

            if (finalPath.Contains(kvp.Key))
            {
                Gizmos.color = Color.magenta;
            }

            Gizmos.DrawCube(kvp.Key + (Vector2)transform.position, new Vector3(cellWidth, cellHeight));
        }
    }

    private class Cell
    {
        public Vector2 position;
        public int fCost = int.MaxValue;
        public int gCost = int.MaxValue;
        public int hCost = int.MaxValue;
        public Vector2 connection;
        public bool isWall;

        public Cell(Vector2 pos)
        {
            position = pos;
        }
    }
}
