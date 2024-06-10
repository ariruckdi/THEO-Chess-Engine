using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Captures Displaying Command", menuName = "Commands/Capture Displaying")]
public class DisplayCapturesCommand : ConsoleCommand
{
    GameMngr manager;
    ConsoleBehaviour console;
    public override void Init()
    {
        manager = GameObject.FindGameObjectWithTag("Manager").GetComponent<GameMngr>();
        console = GameObject.FindGameObjectWithTag("Console").GetComponent<ConsoleBehaviour>();
    }
    public override bool Action(string[] args)
    {
        if (manager.searching) return false;
        if (manager.currentState != GameState.Running) return false;
        
        if (!args[0].Equals("white", System.StringComparison.OrdinalIgnoreCase) && !args[0].Equals("black", System.StringComparison.OrdinalIgnoreCase))
        { // sorting out wrong inputs
            console.Print("Can't highlight possible captures for color " + args[0] + ", please use white or black.");
            return false;
        }
        manager.spaceHandler.UnHighlightAll();
        int player = (args[0] == "white") ? ChessBoard.WHITE : ChessBoard.BLACK;
        List<Move> possibleMoves = manager.engine.GetOrderedMoveset(player, capturesOnly: true);
        List<int> alreadyHighlightedSpaces = new List<int>();
        List<int> alreadyHighlightedPieces = new List<int>();
        foreach (Move move in possibleMoves)
        {
            if (!alreadyHighlightedSpaces.Contains(move.End))
            {
                alreadyHighlightedSpaces.Add(move.End);
                manager.spaceHandler.HighlightSpace(move.End, Color.green, 0.5f);
            }
            if (!alreadyHighlightedPieces.Contains(move.Start))
            {
                alreadyHighlightedPieces.Add(move.Start);
                manager.spaceHandler.HighlightSpace(move.Start, Color.yellow, 0.5f);
            }
        }
        console.Print("Showing possible captures for color " + args[0] + ".");

        return true;
    }
}
