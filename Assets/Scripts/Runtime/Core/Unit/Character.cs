using System;
using UnityEngine;

public class Character : MonoBehaviour
{
    public ColorType ColorType;
    public MaterialCharacterController materialCharacterController;
    public GridManager.BusLineSlot currentSlot;
    public event Action OnMoveComplete;



    private void Awake()
    {

        materialCharacterController = GetComponent<MaterialCharacterController>();

    }


    public void MoveTo(Grid targetGrid)
    {
        transform.position = targetGrid.transform.position;
        OnMoveComplete?.Invoke();
    }
}
