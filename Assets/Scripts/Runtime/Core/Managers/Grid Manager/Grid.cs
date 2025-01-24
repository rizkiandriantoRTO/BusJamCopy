using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//<summary>
//This class holds values for each grid parts.
//It also has functions the create and remove characters in each grid for level editing purposes.
//<summary>

public class Grid : MonoBehaviour
{

    public int NoRow, NoColumn;

    public float FromStartCost;
    public float ToEndCost;
    public float TotalCost;

    public Grid PreviousGirdPart;

    [SerializeField]
    private Character _insideCharacter;
    public Character InsideCharacter => _insideCharacter;

    [SerializeField] private Character _prefabCharacter;
    public ColorType ColorType;
    public ColorDataSO ColorDataSo;



    private void Start()
    {
        Character character = GetComponentInChildren<Character>();
        if (character)
        {
            _insideCharacter = character;
        }
    }

    public void SetInsideCharacter(Character character)
    {
        _insideCharacter = character;
    }

    public void SetTotalCost()
    {
        TotalCost = FromStartCost + ToEndCost; // Gets to total cost for the A* pathfinding.
    }

    //<summary>
    //This function creates a character in the selected grid. It gets called int the GridCreator class for random character creating.
    //<summary>


    public void CreateACharacter()
    {
        if (_insideCharacter != null) return;

        Character character = Instantiate(_prefabCharacter);
        character.transform.position = transform.position;
        character.transform.SetParent(transform);
        _insideCharacter = character;

        character.ColorType = ColorType;
        character.materialCharacterController.SetCharacterMaterial(GetMaterialAccordingToColorType(ColorType));
    }


    public void RemoveTheCharacter() //Removes the character in the selected grid.
    {
        if (_insideCharacter == null) return;

        DestroyImmediate(_insideCharacter.gameObject);
    }


    private Material GetMaterialAccordingToColorType(ColorType colorType)
    {
        return ColorDataSo.ColorDatas.First(x => x.ColorType == colorType).Material;
    }


}
