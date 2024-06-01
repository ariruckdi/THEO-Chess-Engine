using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIEnginePlayerButton : MonoBehaviour
{
    bool active;
    public bool IsActivated { get => active; set => Activate(value); }

    [SerializeField] int player; //white = 0, black = 1
    [SerializeField] UIEnginePlayerButton otherButton;
    Image thisImage;
    Color[] colors;
    Color defaultColor;
    GameMngr manager;

    private void Start()
    {
        manager = GameObject.FindGameObjectWithTag("Manager").GetComponent<GameMngr>();
        thisImage = GetComponent<Image>();
        defaultColor = thisImage.color;
        colors = new Color[] { manager.boardCreation.whiteColor, manager.boardCreation.blackColor };
    }

    public void ButtonPressed() 
    {
        bool otherButtonActive = otherButton.IsActivated;
        EngineState ourState = (player == ChessBoard.WHITE) ? EngineState.White : EngineState.Black;
        EngineState theirState = (player == ChessBoard.WHITE) ? EngineState.Black : EngineState.White;

        IsActivated = !IsActivated;

        if (otherButtonActive && IsActivated)
        {
            manager.engineState = EngineState.Both;
        }
        else if (IsActivated)
        {
            manager.engineState = ourState;
        }
        else if (otherButtonActive)
        {
            manager.engineState = theirState;
        } 
        else
        {
            manager.engineState = EngineState.Off;
        }
    }

    void Activate(bool value) 
    {
        if (value) 
        {
            active = value;
            thisImage.color = colors[player];
        } 
        else 
        {
            active = value;
            thisImage.color = defaultColor;
        }
    }
}
