using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Lean.Touch;

using UnityEngine;
using UnityEngine.TextCore.Text;
using Random = UnityEngine.Random;

//<summary>
//This class handles maninly handles A* pathfinding and characters movement for the bus line slots.
//<summary>

public class GridManager : MonoBehaviour
{
    private const float STRAIGHT_COST = 10f;
    private const float DIAGONAL_COST = 14f;

    private const float SPEED_MOVE_DOPATH = 2f;

    [SerializeField] private LayerMask _gridPartLayerMask;

    private GridCreator _gridCreator;
    public GridCreator GridCreator => _gridCreator;

    private List<Grid> _openList;
    private List<Grid> _closedList;

    private Camera _mainCamera;
    public bool PreventSelection;
    public Grid SelectedGridPart;

    public List<GameObject> waitGridParts;

    public BusManager busManager;

    public GameObject failPanel;

    public AudioSource popSoundAudio;

    public bool allSlotsAreFull;



    public ColorDataSO colorDataSO;
    private Dictionary<ColorType, (Material normalMaterial, Material outlineMaterial)> materialDictionary;

    //<summary>
    //This function initializes the material dictionary required
    //for characters material change when there is a available path for the bus.
    //<summary>

    private void InitializeMaterialDictionary()
    {
        materialDictionary = new Dictionary<ColorType, (Material, Material)>();
        foreach (var colorData in colorDataSO.ColorDatas)
        {
            if (!materialDictionary.ContainsKey(colorData.ColorType))
            {
                materialDictionary.Add(colorData.ColorType, (colorData.Material, colorData.outLineMaterial));
            }
        }
    }


    public List<CharacterCountData> CharacterCountDatas = new();

    public void Awake()
    {

        _gridCreator = GetComponent<GridCreator>();
        _mainCamera = Camera.main;


    }


    private void Start()
    {
        InitializeMaterialDictionary();



        _gridCreator.GridParts = new Grid[_gridCreator.RowCount, _gridCreator.ColumnCount];
        for (int i = 0; i < _gridCreator.StartTransform.childCount; i++)
        {
            var gridPart = _gridCreator.StartTransform.GetChild(i).GetComponent<Grid>();
            _gridCreator.GridParts[gridPart.NoRow, gridPart.NoColumn] = gridPart;
        }

        var characterAis = FindObjectsOfType<Character>().ToList();
        for (int i = 0; i < characterAis.Count; i++)
        {
            var characterAi = characterAis[i];
            var characterAiColorType = characterAi.ColorType;
            if (CharacterCountDatas.Any(x => x.ColorType == characterAiColorType))
            {
                var data = CharacterCountDatas.First(x => x.ColorType == characterAiColorType);
                data.CharacterCount++;
            }
            else
            {
                CharacterCountData characterCountData = new CharacterCountData();
                characterCountData.ColorType = characterAiColorType;
                characterCountData.CharacterCount++;

                CharacterCountDatas.Add(characterCountData);
            }
        }


    }


    void Update()
    {
        CheckAllSlotsFull();
        CheckCharacterMaterials();






    }

    //<summary>
    //This function checks if each character can be moved.
    //<summary>


    void CheckCharacterMaterials()
    {
        foreach (Grid gridp in FindObjectsOfType(typeof(Grid)))
        {
            if (IsTopRow(gridp))
            {

                UpdateCharacterMaterial(gridp.InsideCharacter, true);
            }
            else
            {
                Grid nearestTopGrid = FindNearestTopRowGridPart(gridp);
                if (nearestTopGrid != null)
                {
                    if (gridp.InsideCharacter != null)
                    {
                        UpdateCharacterMaterial(gridp.InsideCharacter, true);

                    }

                }
            }

        }
    }

    //<summary>
    //This function updates the character materials if can they be moved.
    //<summary>

    public void UpdateCharacterMaterial(Character character, bool isReachable)
    {
        if (character != null)
        {
            if (materialDictionary.TryGetValue(character.ColorType, out var materials))
            {
                Renderer characterRenderer = character.GetComponentInChildren<Renderer>();
                if (characterRenderer != null)
                {
                    characterRenderer.material = isReachable ? materials.outlineMaterial : materials.normalMaterial;
                }
            }
        }


    }

    //<summary>
    //This function checks if the all bus line waiting slots are full in order to initialize the call the level fail function;
    //<summary>

    void CheckAllSlotsFull()
    {
        foreach (BusLineSlot slot in busLineSlots)
        {
            GameObject currentBus = busManager.currentBus;
            if (!slot.isOccupied || (slot.isOccupied && slot.insideCharacterAi.gameObject.GetComponentInChildren<SkinnedMeshRenderer>().material.color == currentBus.GetComponent<Renderer>().material.color))
                return;

        }

        ShowFailPanel();
    }
    //<summary>
    //A basic function for showing the player level failed screen.
    //<summary>


    void ShowFailPanel()
    {
        if (failPanel != null)
            failPanel.SetActive(true);

    }




    private void OnEnable()
    {
        LeanTouch.OnFingerDown += TouchAction;
    }


    public void OnDisable()
    {
        LeanTouch.OnFingerDown -= TouchAction;


    }

    //<summary>
    //This function controls the player touch actions.
    //<summary>

    private void TouchAction(LeanFinger finger)
    {
        if (PreventSelection)
        {
            Debug.Log("Selection prevented.");
            return;
        }

        Grid touchedGridPart = GetTouchedGridPart();

        if (touchedGridPart == null)
        {
            Debug.Log("No Grid touched.");
            return;
        }

        if (touchedGridPart.InsideCharacter == null)
        {
            Debug.Log("No Character in touched Grid.");
            return;
        }

        Character character = touchedGridPart.InsideCharacter;

        //<summary>
        //Checks if the touched grid part is in top row, also checks if the character in the grid is the same color with the cuurent bus
        //If it is sends the character in the touched grid to directly to the bus. If it's not sends the character to the available bus line slot.
        //<summary>

        if (IsTopRow(touchedGridPart))
        {
            if (character.gameObject.GetComponentInChildren<SkinnedMeshRenderer>().material.color == busManager.currentBus.GetComponent<Renderer>().material.color && !busManager.currentBus.GetComponent<Bus>().IsFull)
            {
                if (busManager.iscurrentBusReachedDestination)
                {

                    character.GetComponent<Animator>().SetBool("isRunning", true);
                    UpdateCharacterMaterial(character, false);

                    character.transform.DOLookAt(busManager.currentBus.transform.position, 0.5f, AxisConstraint.Y);

                    character.transform.DOMove(new Vector3(busManager.currentBus.transform.position.x, character.transform.position.y, busManager.currentBus.transform.position.z), 1).SetEase(Ease.InOutQuad).OnComplete(() =>
                    {
                        //Checks if the bus is full and manages the next one.


                        popSoundAudio.Play();
                        Debug.Log("Bus reached the destination.");
                        touchedGridPart.SetInsideCharacter(null);

                        character.GetComponent<Animator>().SetBool("isRunning", true);
                        character.transform.localScale = Vector3.zero;
                        Transform seat = busManager.currentBus.GetComponent<Bus>().GetNextAvailableSeat();
                        if (busManager.currentBus.GetComponent<Bus>().IsFull)
                        {
                            busManager.CheckBusCapacityAndManageNextBus(busManager.currentBus);
                        }
                        if (seat != null)
                        {

                            character.transform.SetParent(seat);
                            character.transform.DOScale(new Vector3(15, 15, 15), 0.5f);
                            character.GetComponent<Animator>().SetTrigger("isSitting");
                            character.GetComponent<Character>().currentSlot.isOccupied = false;
                            character.GetComponent<Character>().currentSlot = null;
                            character.transform.position = seat.position;


                        }
                    });
                }


            }
            else
            {
                MoveCharacterToAvailableBusLineSlot(character, touchedGridPart);
                //touchedGridPart.SetInsideCharacter(null);
                UpdateCharacterMaterial(character, false);
                popSoundAudio.Play();

            }




        }
        else
        {
            //Sends the character to the nearest grid part in the top row. When the movement ends sends the character to an available
            //line waiting slot.

            Grid nearestTopRowGridPart = FindNearestTopRowGridPart(touchedGridPart);
            if (nearestTopRowGridPart != null)
            {
                MoveCharacterToTarget(character, touchedGridPart, nearestTopRowGridPart);
                touchedGridPart.SetInsideCharacter(null);
                popSoundAudio.Play();
            }
            else
            {
                //Triggers when there is no available path. 

                Debug.Log("No reachable top row Grid found.");
                character.GetComponentInChildren<ParticleSystem>().Play();
            }
        }
    }

    //<summary>
    //This function finds the nearest top row grid part.
    //<summary>

    private Grid FindNearestTopRowGridPart(Grid startGridPart)
    {
        Grid nearestTopRowGridPart = null;
        float minDistance = float.MaxValue;

        for (int column = 0; column < _gridCreator.ColumnCount; column++)
        {
            Grid candidateGridPart = _gridCreator.GridParts[0, column];
            var path = GetTheClosestPath(startGridPart, candidateGridPart);
            if (path != null && path.Count > 0 && path.Last().InsideCharacter == null)
            {
                float distance = path.Count;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestTopRowGridPart = candidateGridPart;
                }
            }
        }

        return nearestTopRowGridPart;
    }

    //<summary>
    //This function moves the target to nearest top row grid part. Only gets called when the player can go directly to the bus
    //or there is an available bus line waiting slot. At the end checks if the player can go to the current bus or to the available
    //bus line waiting slot.
    //<summary>

    private void MoveCharacterToTarget(Character characterAi, Grid startGridPart, Grid targetGridPart)
    {
        var path = GetTheClosestPath(startGridPart, targetGridPart);
        if (path != null && path.Count > 0 && !allSlotsAreFull)
        {
            Vector3[] waypoints = path.Select(x => x.transform.position).ToArray();
            characterAi.gameObject.GetComponent<Animator>().SetBool("isRunning", true);
            UpdateCharacterMaterial(characterAi, false);
            characterAi.transform.DOPath(waypoints, SPEED_MOVE_DOPATH, PathType.Linear)
                .SetLookAt(0.001f).SetEase(Ease.Linear)
                .SetSpeedBased()
                .OnComplete(() =>
                {
                    if (IsTopRow(targetGridPart))
                    {


                        if ((characterAi.gameObject.GetComponentInChildren<SkinnedMeshRenderer>().material.color == busManager.currentBus.GetComponent<Renderer>().material.color) && !busManager.currentBus.GetComponent<Bus>().IsFull)
                        {
                            if (busManager.iscurrentBusReachedDestination)
                            {
                                characterAi.transform.DOLookAt(busManager.currentBus.transform.position, 0.5f, AxisConstraint.Y);

                                characterAi.transform.DOMove(new Vector3(busManager.currentBus.transform.position.x, characterAi.transform.position.y, busManager.currentBus.transform.position.z), 1).SetEase(Ease.InOutQuad).OnComplete(() =>
                                {

                                    characterAi.GetComponent<Animator>().SetBool("isRunning", true);
                                    Debug.Log("Bus reached the destination.");

                                    characterAi.transform.localScale = Vector3.zero;
                                    Transform seat = busManager.currentBus.GetComponent<Bus>().GetNextAvailableSeat();
                                    if (busManager.currentBus.GetComponent<Bus>().IsFull)
                                    {
                                        busManager.CheckBusCapacityAndManageNextBus(busManager.currentBus);
                                    }
                                    if (seat != null)
                                    {

                                        characterAi.transform.SetParent(seat);
                                        characterAi.transform.DOScale(new Vector3(15, 15, 15), 0.5f);
                                        characterAi.GetComponent<Animator>().SetTrigger("isSitting");
                                        characterAi.GetComponent<Character>().currentSlot.isOccupied = false;
                                        characterAi.GetComponent<Character>().currentSlot = null;
                                        characterAi.transform.position = seat.position;


                                    }
                                });
                            }
                            else
                            {
                                MoveCharacterToAvailableBusLineSlot(characterAi, startGridPart);
                            }

                        }

                        else
                        {
                            MoveCharacterToAvailableBusLineSlot(characterAi, startGridPart);
                        }
                    }
                    else
                    {
                        targetGridPart.SetInsideCharacter(characterAi);
                        characterAi.gameObject.GetComponent<Animator>().SetBool("isRunning", false);
                    }
                    PreventSelection = false;
                });
        }
        else
        {
            startGridPart.SetInsideCharacter(characterAi);
        }
    }

    //<summary>
    //A classs for the bus line slots to hold the character inside and the boolean for checking if its occupied.
    //<summary>

    [System.Serializable]
    public class BusLineSlot
    {
        public Transform transform;
        public bool isOccupied;

        public Character insideCharacterAi;


    }



    public List<BusLineSlot> busLineSlots;

    //<summary>
    //This moves the character to the available bus waiting line slot. It gets called in the Move The Character To Target Method.
    //<summary>

    private void MoveCharacterToAvailableBusLineSlot(Character characterAi, Grid startGridPart)
    {
        BusLineSlot availableSlot = FindAvailableBusLineSlot();
        if (availableSlot != null)
        {
            startGridPart.SetInsideCharacter(null);
            UpdateCharacterMaterial(characterAi, false);
            characterAi.transform.DOLookAt(availableSlot.transform.position, 0.5f, AxisConstraint.Y);
            characterAi.gameObject.GetComponent<Animator>().SetBool("isRunning", true);
            UpdateCharacterMaterial(characterAi, false);
            availableSlot.isOccupied = true;
            availableSlot.insideCharacterAi = characterAi;
            characterAi.transform.DOMove(availableSlot.transform.position, 1.0f)
                .SetEase(Ease.InOutQuad)
                .OnComplete(() =>
                {
                    UpdateCharacterMaterial(characterAi, false);
                    characterAi.transform.DOLookAt(availableSlot.transform.position, 0.5f, AxisConstraint.Y);
                    characterAi.gameObject.GetComponent<Animator>().SetBool("isRunning", false);
                    GameObject.Find("BusManager").GetComponent<BusLineManager>().waitingCharacters.Add(characterAi.gameObject);
                    startGridPart.SetInsideCharacter(null);
                    characterAi.currentSlot = availableSlot;




                });
        }
        else
        {
            Debug.Log("No available bus line slots.");
            characterAi.gameObject.GetComponent<Animator>().SetBool("isRunning", false);
            //startGridPart.SetInsideCharacter(null);  
        }
    }


    //Finds the available bus slots by checking if its occupied.
    private BusLineSlot FindAvailableBusLineSlot()
    {
        foreach (var slot in busLineSlots)
        {
            if (!slot.isOccupied)
            {
                allSlotsAreFull = false;
                return slot;

            }
        }
        allSlotsAreFull = true;
        return null;

    }

    //<summary>
    //This function finds the grid part for each character.
    //<summary>

    private Grid FindGridPartByCharacter(Character characterAi)
    {

        for (int row = 0; row < _gridCreator.RowCount; row++)
        {
            for (int col = 0; col < _gridCreator.ColumnCount; col++)
            {
                Grid part = _gridCreator.GridParts[row, col];
                if (part != null && part.InsideCharacter == characterAi)
                {
                    return part;
                }
            }
        }
        return null;
    }

    //<summary>
    //This function checks if the grid part is in top row.
    //<summary>

    private bool IsTopRow(Grid gridPart)
    {
        return gridPart.NoRow == 0;
    }





    //<summary>
    //This function gets the touched grid part with raycasting.
    //<summary>


    private Grid GetTouchedGridPart()
    {
        RaycastHit hit;
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        bool isHit = Physics.Raycast(ray, out hit, 500, _gridPartLayerMask);
        if (isHit)
        {
            var hitObject = hit.collider.gameObject;
            var gridPart = hitObject.GetComponent<Grid>();
            return gridPart;
        }
        else if (!isHit)
        {

        }

        return null;
    }

    //<summary>
    //This function calculates the closest path from two grid parts, for pathfinding.
    //<summary>

    public List<Grid> GetTheClosestPath(Grid startGridPart, Grid endGridPart)
    {

        _openList = new List<Grid>();
        _openList.Add(startGridPart);

        _closedList = new List<Grid>();

        for (int i = 0; i < _gridCreator.RowCount; i++)
        {
            for (int j = 0; j < _gridCreator.ColumnCount; j++)
            {
                Grid gridPart = _gridCreator.GridParts[i, j];
                gridPart.FromStartCost = int.MaxValue;
                gridPart.SetTotalCost();
                gridPart.PreviousGirdPart = null;

            }
        }


        startGridPart.FromStartCost = 0;
        startGridPart.ToEndCost = GetDistance(startGridPart, endGridPart);
        startGridPart.SetTotalCost();

        while (_openList.Count > 0)
        {
            Grid currentGridPart = GetTheLowestTotalCostGridPart(_openList);

            if (currentGridPart == endGridPart)
            {
                return GetPath(endGridPart);
            }

            _openList.Remove(currentGridPart);
            _closedList.Add(currentGridPart);

            for (int i = 0; i < GetNeighbourGridParts(currentGridPart).Count; i++)
            {
                var neighbourGridPart = GetNeighbourGridParts(currentGridPart)[i];
                if (_closedList.Contains(neighbourGridPart)) continue;

                if (neighbourGridPart != endGridPart && neighbourGridPart.InsideCharacter != null)
                {
                    _closedList.Add(neighbourGridPart);
                    continue;
                }

                float tempCost = currentGridPart.FromStartCost + GetDistance(currentGridPart, neighbourGridPart);
                if (tempCost < neighbourGridPart.FromStartCost)
                {
                    neighbourGridPart.PreviousGirdPart = currentGridPart;
                    neighbourGridPart.FromStartCost = tempCost;
                    neighbourGridPart.ToEndCost = GetDistance(neighbourGridPart, endGridPart);
                    neighbourGridPart.SetTotalCost();

                    if (!_openList.Contains(neighbourGridPart))
                    {
                        _openList.Add(neighbourGridPart);
                    }
                }
            }
        }

        return null;
    }



    private float GetDistance(Grid firstGridPart, Grid lastGridPart)
    {
        float rowDistance = Mathf.Abs((lastGridPart.NoRow - firstGridPart.NoRow));
        float columnDistance = Mathf.Abs((lastGridPart.NoColumn - firstGridPart.NoColumn));
        float difference = Mathf.Abs(rowDistance - columnDistance);

        return DIAGONAL_COST * Mathf.Min(rowDistance, columnDistance) + STRAIGHT_COST * difference;

    }



    private Grid GetTheLowestTotalCostGridPart(List<Grid> gridParts)
    {
        Grid lowestTotalCost = gridParts[0];

        for (int i = 0; i < gridParts.Count; i++)
        {
            if (gridParts[i].TotalCost < lowestTotalCost.TotalCost)
                lowestTotalCost = gridParts[i];
        }

        return lowestTotalCost;
    }


    //Gets and returns the path for pathfinding.

    private List<Grid> GetPath(Grid endGridPart)
    {
        List<Grid> path = new();
        path.Add(endGridPart);

        Grid currentGridPart = endGridPart;
        while (currentGridPart.PreviousGirdPart != null)
        {
            path.Add(currentGridPart.PreviousGirdPart);
            currentGridPart = currentGridPart.PreviousGirdPart;
        }

        path.Reverse();
        return path;
    }

    //Gets the neighbour grid parts for each grids.

    private List<Grid> GetNeighbourGridParts(Grid gridPart)
    {
        List<Grid> neighbours = new();

        if (gridPart.NoColumn >= 1)
        {
            //Left
            neighbours.Add(_gridCreator.GridParts[gridPart.NoRow, gridPart.NoColumn - 1]);

        }

        if (gridPart.NoColumn < _gridCreator.ColumnCount - 1)
        {
            //Right
            neighbours.Add(_gridCreator.GridParts[gridPart.NoRow, gridPart.NoColumn + 1]);

        }

        //Down
        if (gridPart.NoRow >= 1)
        {
            neighbours.Add(_gridCreator.GridParts[gridPart.NoRow - 1, gridPart.NoColumn]);
        }

        //Up
        if (gridPart.NoRow < _gridCreator.RowCount - 1)
        {
            neighbours.Add(_gridCreator.GridParts[gridPart.NoRow + 1, gridPart.NoColumn]);
        }

        return neighbours;
    }


}

[Serializable]
public class CharacterCountData
{
    public ColorType ColorType;
    public int CharacterCount;

}