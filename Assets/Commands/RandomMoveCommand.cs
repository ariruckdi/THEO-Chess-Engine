using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Random Move Command", menuName = "Commands/Random Move")]
public class RandomMoveCommand : ConsoleCommand
{
    GameMngr manager;
    ConsoleBehaviour console;
    //run by console object in start code
    public override void Init()
    {
        manager = GameObject.FindGameObjectWithTag("Manager").GetComponent<GameMngr>();
        console = GameObject.FindGameObjectWithTag("Console").GetComponent<ConsoleBehaviour>();
    }
    //runs on command execution
    public override bool Action(string[] args)
    {
        if (manager.searching) return false;
        if (manager.currentState != GameState.Running) return false;
        EngineState prevState = manager.engineState;
        manager.engineState = (manager.playerOnTurn == ChessBoard.WHITE) ? EngineState.White : EngineState.Black;
        manager.engine.ThreadedMove();
        manager.engineState = prevState;
        return true;
    }
}

