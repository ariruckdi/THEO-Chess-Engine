using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ChessBoard;

public struct UndoMoveData
{
    public int start;
    public int end;
    public int piece;
    public int captured;
    public int castlingIndex;
    public bool[] castlingBefore;
    public int epSpaceBefore;
    public bool promotion;

    public UndoMoveData(int start, int end, int movedPiece, int takenPiece, int castlingIndex, bool[] castlingBefore, int epSpaceBefore, bool wasPromotion)
    {
        this.start = start;
        this.end = end;
        this.piece = movedPiece;
        this.captured = takenPiece;
        this.castlingIndex = castlingIndex;
        this.castlingBefore = castlingBefore;
        this.epSpaceBefore = epSpaceBefore;
        this.promotion = wasPromotion;
    }
}