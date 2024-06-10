using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceHandler : MonoBehaviour
{
    public BoardCreation boardCreation;
    public GameMngr manager;
    float thisX;
    float thisY;

    public void Awake()
    {
        thisX = transform.position.x;
        thisY = transform.position.y;
    }

    public static int FlipIndex(int index)
    {
        return 63 - index;
    }

    public Vector3 ChessSpaceToWorldSpace(int space)
    {
        if (manager.boardFlipped) space = FlipIndex(space);
        int x = ChessBoard.SpaceX(space);
        int y = ChessBoard.SpaceY(space);
        return new Vector3(-3.5f + x + thisX, -3.5f + y + thisY, 0);
    }

    public int WorldSpaceToChessSpace(Vector2 koords)
    {
        int space = -1;
        int x = Mathf.RoundToInt(koords.x + 3.5f - thisX);
        int y = Mathf.RoundToInt(koords.y + 3.5f - thisY);
        if (0 <= x && x < 8 && 0 <= y && y < 8) space = x + 8 * y;
        if (manager.boardFlipped) space = FlipIndex(space);
        return space;
    }

    GameObject GetSpaceObjectAtPosition(int pos)
    {
        if (manager.boardFlipped) pos = FlipIndex(pos);
        return boardCreation.spaces[pos];
    }

    public void HighlightSpace(int space, Color color, float lerpValue = 1)
    {
        GameObject spaceToHighlight = GetSpaceObjectAtPosition(space);
        spaceToHighlight.GetComponent<SpriteRenderer>().color = Color.Lerp(spaceToHighlight.GetComponent<SpriteRenderer>().color, color, lerpValue);
    }

    public void UnHighlightSpace(int space)
    {
        GameObject spaceToUnHighlight = GetSpaceObjectAtPosition(space);
        Color color;
        if (ChessBoard.SpaceColor(space) == 0) { color = boardCreation.blackColor; }
        else { color = boardCreation.whiteColor; }
        spaceToUnHighlight.GetComponent<SpriteRenderer>().color = color;
    }

    public void HighlightSpaceList(List<int> spaces, Color color, float lerpValue = 1)
    {
        foreach (int space in spaces)
        { 
            if (space == -1) break;
            HighlightSpace(space, color, lerpValue);
        }
    }

    public void UnHighlightAll()
    {
        for (int i = 0; i < 64; i++)
        {
            UnHighlightSpace(i);
        }
    }
}
