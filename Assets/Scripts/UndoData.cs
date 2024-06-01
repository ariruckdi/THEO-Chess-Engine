using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ChessBoard;

public struct UndoMoveData
{
    public int start;
    public int end;
    public int movedPiece;
    public int takenPiece;
    public int castlingIndex;
    public bool[] castlingBefore;
    public int epSpaceBefore;
    public bool wasPromotion;

    public UndoMoveData(int start, int end, int movedPiece, int takenPiece, int castlingIndex, bool[] castlingBefore, int epSpaceBefore, bool wasPromotion)
    {
        this.start = start;
        this.end = end;
        this.movedPiece = movedPiece;
        this.takenPiece = takenPiece;
        this.castlingIndex = castlingIndex;
        this.castlingBefore = castlingBefore;
        this.epSpaceBefore = epSpaceBefore;
        this.wasPromotion = wasPromotion;
    }

    public UndoMoveData(int start, int end, MoveGenerator moveGenerator)
    { //do this before actually making the move
        this.start = start;
        this.movedPiece = moveGenerator.board[start];
        this.end = end;
        this.takenPiece = moveGenerator.board[end];
        this.castlingBefore = (bool[])moveGenerator.gameData.castling.Clone();
        this.epSpaceBefore = moveGenerator.gameData.epSpace;
        castlingIndex = -1;
        if ((SpaceY(end) == 0 || SpaceY(end) == 7) && PieceType(movedPiece) == PAWN)
        { // kinda slow but gets mostly skipped
            wasPromotion = true;
            movedPiece = PieceInt(QUEEN, PieceColor(movedPiece));
        }
        else
        {
            wasPromotion = false;
        }
        if (PieceType(movedPiece) == KING && Mathf.Abs(start - end) == 2)
        { // castling
            for (int i = 0; i < 4; i++)
            {
                if (KINGS_AFTER_CASTLING[i] == end)
                {
                    castlingIndex = i;
                }
            }
        }
    }
}