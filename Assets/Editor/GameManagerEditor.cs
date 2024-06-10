using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameMngr))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GameMngr self = (GameMngr)target;
        DrawDefaultInspector();
        if (GUILayout.Button("Generate Board") && !self.boardExists)
        {
            MoveGenerator testBoard = new MoveGenerator();
            self.boardCreation.GenerateBoard();
            self.boardExists = true;
        }
        if (GUILayout.Button("Destroy Board") && self.boardExists)
        {
            self.boardCreation.RemoveBoard();
            self.boardExists = false;
        }
        if (GUILayout.Button("Starting Position") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition(GameMngr.startingPosString);
        }
        if (GUILayout.Button("Empty Board") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition("8/8/8/8/8/8/8/8  w KQkq - 0 1");
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Test Pawn Move Generation") && self.boardExists)
        {
            self.MoveGenerationTest(1);
        }
        if (GUILayout.Button("Test Knight Move Generation") && self.boardExists)
        {
            self.MoveGenerationTest(2);
        }
        if (GUILayout.Button("Test Bishop Move Generation") && self.boardExists)
        {
            self.MoveGenerationTest(3);
        }
        if (GUILayout.Button("Test Rook Move Generation") && self.boardExists)
        {
            self.MoveGenerationTest(4);
        }
        if (GUILayout.Button("Test Queen Move Generation") && self.boardExists)
        {
            self.MoveGenerationTest(5);
        }
        if (GUILayout.Button("Test King Move Generation") && self.boardExists)
        {
            self.MoveGenerationTest(6);
        }
        if (GUILayout.Button("Test Attacked Space Generation") && self.boardExists)
        {
            self.AttackedSpaceGenerationTest();
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Show Attacked Spaces") && self.boardExists)
        {
            self.ShowAttackedSpaces();
        }
        GUILayout.Space(20);
        if (GUILayout.Button("Test Engine Move Counts") && self.boardExists)
        {
            self.EngineMoveCountTest();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Test 1") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition(GameMngr.perftTest1);
        }
        if (GUILayout.Button("Test 2") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition(GameMngr.perftTest2);
        }
        if (GUILayout.Button("Test 3") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition(GameMngr.perftTest3);
        }
        if (GUILayout.Button("Test 4") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition(GameMngr.perftTest4);
        }
        if (GUILayout.Button("Test 5") && self.boardExists)
        {
            self.spaceHandler.UnHighlightAll();
            self.LoadPosition(GameMngr.perftTest5);
        }
        GUILayout.EndHorizontal();

    }


}
