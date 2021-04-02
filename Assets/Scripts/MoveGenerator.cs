using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using UnityEngine;
using System;

using static ChessBoard;

public struct ChessGameData
{
    public int playerOnTurn;
    public int epSpace;
    public bool[] castling;

    public ChessGameData(int nextPlayer, int enPassantSpace, bool[] castlingData)
    {
        playerOnTurn = nextPlayer;
        epSpace = enPassantSpace;
        castling = castlingData;
    }
}

public class MoveGenerator
{
    public ChessBoard board;

    public const int shortCastlingWhite = 0, longCastlingWhite = 1, shortCastlingBlack = 2, longCastlingBlack = 3;

    static readonly int[] slideDirections = new int[] { 8, -8, 1, -1, 9, 7, -7, -9 };
    //up, down, right, left, ur, ul, dr, dl
    static readonly int[][] pawnDirs = new int[][] { new int[] { 8, 9, 7 }, new int[] { -8, -7, -9} };
    static readonly int[][] knightDirs = new int[][] { new int[] { 15, 17 }, new int[] { -15, -17 },new int[] { -6, 10 },new int[] { 6, -10 } };
    //up, down, right, left
    static int[][] SpacesToEdge;

    public ChessGameData gameData;

    public BitBoard isSpaceAttackedByWhite, isSpaceAttackedByBlack;

    public int whiteKingPosition;
    public int blackKingPosition;


    public MoveGenerator()
    {
        board = new ChessBoard();
        gameData = new ChessGameData(white, 0, new bool[] { true, true, true, true });
        GenerateSpacesToEdgeData();
    }

    //getters and initalisation
    static void GenerateSpacesToEdgeData()
    {
        SpacesToEdge = new int[64][];
        for (int i = 0; i < 64; i++)
        {
            int x = SpaceX(i);
            int y = SpaceY(i);
            var edgeData = new int[] { 7 - y, y, 7 - x, x, Mathf.Min(7 - y, 7 - x), Mathf.Min(7 - y, x), Mathf.Min(y, 7 - x), Mathf.Min(y, x) };
            SpacesToEdge[i] = edgeData;
        }
    }

    public string MoveName(int startSpace, int endSpace, bool longNotation = false)
    {
        string output;
        if (board[startSpace] == 0) return "Tried to move from empty space";
        int piece = board[startSpace];
        int pieceType = PieceType(piece);
        UndoMoveData testMoveForCheck = MovePiece(startSpace, endSpace);
        bool leadsToCheck = IsPlayerInCheck(gameData.playerOnTurn ^ 1);
        UndoMovePiece(testMoveForCheck);
        if (pieceType == 6)
        {
            if (startSpace - endSpace == -2) return "0-0";
            if (startSpace - endSpace == 2) return "0-0-0";
        }
        if (board[endSpace] == 0)
        {
            output = PieceLetter(piece).ToString() + SpaceName(endSpace);
        }
        else
        {
            output = PieceLetter(piece).ToString() + "x" + SpaceName(endSpace);
        }
        if (leadsToCheck)
        {
            output += "+";
        }
        if (longNotation)
        {
            output = SpaceName(startSpace) + SpaceName(endSpace);
        }
        return output;
    }

    public void LoadFEN(string fenNotation)
    {
        board = FENHandler.ReadFEN(fenNotation);
        gameData = FENHandler.GameDataFromFEN(fenNotation);
        SetAttackedSpaceData();
        SetKingPositions();
    }

    //pseudo legal movegen
    private BitBoard GetSlideSpaces(int space, int pieceType, int range = 8, bool capturesOnly = false)
    {
        int pieceOnNewSpaceColor, newSpace;
        int movingPieceColor = PieceColor(board[space]); 
        int dirStart = (pieceType == bishop) ? 4 : 0;
        int dirEnd = (pieceType == rook) ? 4 : 8;
        BitBoard output = new BitBoard(0);
        for (int dirIndex = dirStart; dirIndex < dirEnd; dirIndex++)
        {
            for (int i = 0; i < SpacesToEdge[space][dirIndex]; i++)
            {
                newSpace = space + slideDirections[dirIndex] * (i + 1);
                pieceOnNewSpaceColor = PieceColor(board[newSpace]);
                if (pieceOnNewSpaceColor == movingPieceColor || (i+1) > range)
                {
                    break;
                } else
                { 
                    output[newSpace] = true && !capturesOnly; 
                    if (pieceOnNewSpaceColor != -1)
                    {
                        output[newSpace] = true;
                        break;
                    }
                } 
            }
        }
        return output;
    }


    private BitBoard GetPawnSpaces(int space, bool capturesOnly = false)
    {
        int newSpace, newSpacePieceColor, doublePushRow;
        var output = new BitBoard();
        int pieceColor = PieceColor(board[space]);
        newSpace = space + pawnDirs[pieceColor][0];
        newSpacePieceColor = PieceColor(board[newSpace]);
        if(newSpacePieceColor == -1 && !capturesOnly)
        {
            output[newSpace] = true;
            doublePushRow = (pieceColor == white) ? 1 : 6;
            if (SpaceY(space) == doublePushRow)
            {
                newSpace += pawnDirs[pieceColor][0];
                newSpacePieceColor = PieceColor(board[newSpace]);
                if (newSpacePieceColor == -1)
                {
                    output[newSpace] = true;
                }
            }
        }
        foreach (int possibleCapture in GetSpacesAttackedByPawn(space).GetActive())
        {
            if(PieceColor((board[possibleCapture])) == (pieceColor ^ 0b1) || possibleCapture == gameData.epSpace)
            {
                output[possibleCapture] = true;
            }
        }
        return output;
    }

    //useful for finding attacked spaces
    private BitBoard GetSpacesAttackedByPawn(int space)
    {
        int newSpace, newSpacePieceColor;
        var output = new BitBoard();
        int pieceColor = PieceColor(board[space]);
        for (int dirIndex = 1; dirIndex < 3; dirIndex++)
        {
            if (SpacesToEdge[space][dirIndex + 1] >= 1)
            {
                newSpace = space + pawnDirs[pieceColor][dirIndex];
                newSpacePieceColor = PieceColor(board[newSpace]);
                if (newSpacePieceColor != pieceColor)
                {
                    output[newSpace] = true;
                }
            }
        }
        return output;
    }

    private BitBoard GetKnightSpaces(int space, bool capturesOnly = false)
    {
        int newSpacePieceColor, newSpace, deltaX;
        var output = new BitBoard();
        int pieceColor = PieceColor(board[space]);
        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            if (SpacesToEdge[space][directionIndex] >= 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    newSpace = space + knightDirs[directionIndex][i];
                    if (newSpace >= 0 && newSpace < 64)
                    {
                        newSpacePieceColor = PieceColor(board[newSpace]);
                        deltaX = Mathf.Abs(SpaceX(newSpace) - SpaceX(space));
                        if (newSpacePieceColor != pieceColor && (deltaX == 1 || deltaX == 2))
                        {
                            output[newSpace] = ((newSpacePieceColor == -1) && (!capturesOnly)) || (newSpacePieceColor == (pieceColor ^ 1));
                        }
                    }
                }
            }
        }
        return output;
    }


    //completly useless wrappers, kept for completeness
    private BitBoard GetBishopSpaces(int space)
    {
        return GetSlideSpaces(space, 3);
    }

    private BitBoard GetRookSpaces(int space)
    {
        return GetSlideSpaces(space, 4);
    }

    private BitBoard GetQueenSpaces(int space)
    {
        return GetSlideSpaces(space, 5);
    }

    private BitBoard GetKingSpaces(int space)
    {
        return GetSlideSpaces(space, 6, 1);
    }

    public BitBoard GetPossibleSpacesForPiece(int space, bool possibleCapturesOnly = false)
    {
        int pieceType = PieceType(board[space]);
        if (pieceType == pawn) return GetPawnSpaces(space, capturesOnly: possibleCapturesOnly);
        else if (bishop <= pieceType && pieceType <= queen) return GetSlideSpaces(space, pieceType, capturesOnly: possibleCapturesOnly);
        else if (pieceType == knight) return GetKnightSpaces(space, capturesOnly: possibleCapturesOnly);
        else if (pieceType == king) return GetSlideSpaces(space, king, range: 1, capturesOnly: possibleCapturesOnly);
        else return new BitBoard(0);
    }

    //legal movegen and prerequisites

    public BitBoard GenerateAttackedSpaceBitboard(int player)
    {
        int currentPiece;
        var output = new BitBoard();
        foreach (int space in board.fullSpaces.GetActive())
        {
            currentPiece = board[space];
            if (PieceColor(currentPiece) == player)
            {
                if (PieceType(currentPiece) == pawn)
                {
                    output += GetSpacesAttackedByPawn(space);
                }
                else
                {
                    output += GetPossibleSpacesForPiece(space);
                }
            }
        }
        return output;
    }

    void SetAttackedSpaceData()
    {
        isSpaceAttackedByBlack = GenerateAttackedSpaceBitboard(black);
        isSpaceAttackedByWhite = GenerateAttackedSpaceBitboard(white);
    }

    void SetKingPositions()
    {
        for (int i = 0; i < 64; i++)
        {
            if (board.Contains(i, blackPiece | king))
            {
                blackKingPosition = i;
                break;
            }
        }
        for (int i = 0; i < 64; i++) // looks bad, but breaking the loop after finding the kings should be faster
        {
            if (board.Contains(i, whitePiece | king))
            {
                whiteKingPosition = i;
                break;
            }
        }
    }

    int KingPosition(int player)
    {
        return (player == white) ? whiteKingPosition : blackKingPosition;
    }

    
    public bool IsPlayerInCheck(int player)
    {
        var bitboardToCheck = (player == white) ? isSpaceAttackedByBlack : isSpaceAttackedByWhite;
        return bitboardToCheck[KingPosition(player)];
    }

    public BitBoard GetLegalMovesForPiece(int space)
    {
        //variables
        int piece = board[space];
        int player = PieceColor(piece);
        int pieceType = PieceType(piece);
        var output = GetPossibleSpacesForPiece(space);

        //castling
        int castlingRow = 7 * player;
        bool shortCastlingValid, longCastlingValid;
        if (pieceType == king && space == castlingRow * 8 + 4)
        {
            BitBoard attackedSpaces = GenerateAttackedSpaceBitboard(player ^ 1);
            var castlingSpaces = new int[] { 5 + 8 * castlingRow, 6 + 8 * castlingRow, 2 + 8 * castlingRow, 3 + 8 * castlingRow, 1 + 8 * castlingRow };
            bool castlingPossible = !IsPlayerInCheck(player);
            if (gameData.castling[2 * player] && castlingPossible)
            {
                shortCastlingValid = true;
                for (int i = 0; i < 2; i++) // short castling
                {
                    if (board.fullSpaces[castlingSpaces[i]]) shortCastlingValid = false; //cant castle is pieces are in the way
                    if (attackedSpaces[castlingSpaces[i]]) shortCastlingValid = false; //cant castle through check
                    if (!shortCastlingValid) break;
                }
                if (shortCastlingValid) output[space + 2] = true;
            }
            if (gameData.castling[2 * player + 1] && castlingPossible)
            {
                longCastlingValid = true;
                if (board.fullSpaces[castlingSpaces[4]]) longCastlingValid = false;
                for (int i = 2; i < 4; i++) // long castling
                {
                    if (!longCastlingValid) break;
                    if (board.fullSpaces[castlingSpaces[i]]) longCastlingValid = false;
                    if (attackedSpaces[castlingSpaces[i]]) longCastlingValid = false;
                }
                if (longCastlingValid) output[space - 2] = true;
            }
        }

        //checking legality
        foreach (int newSpace in output.GetActive())
        {
            bool invalidMove = false;
            UndoMoveData move = MovePiece(space, newSpace);
            if (IsPlayerInCheck(player))
            {
                invalidMove = true;
            }
            UndoMovePiece(move);
            if (invalidMove)
            {
                output[newSpace] = false;
            }
        }
        //Debug.Log("There were " + output.Count.ToString() + " moves.");
        return output;
    }

    public BitBoard GetLegalCapturesForPiece(int space)
    {
        BitBoard output = GetPossibleSpacesForPiece(space, true);
        int piece = board[space];
        int player = PieceColor(piece);
        //no need for castling, thats not a capture :D
        foreach (int newSpace in output.GetActive())
        {
            bool invalidMove = false;
            UndoMoveData move = MovePiece(space, newSpace);
            if (IsPlayerInCheck(player))
            {
                invalidMove = true;
            }
            UndoMovePiece(move);
            if (invalidMove)
            {
                output[newSpace] = false;
            }
        }
        return output;
    }

    //moving pieces and undoing moves

    public UndoMoveData MovePiece(int start, int end)
    {
        //variables
        UndoMoveData undoData = new UndoMoveData(start, end, this);
        int piece = undoData.movedPiece;
        int color = PieceColor(piece);
        int type = PieceType(piece);
        int capture = undoData.takenPiece;
        bool isCapture = (capture != 0);
        int shortCastlingIndex = color * 2; // 0 for white, 2 for black
        int longCastlingIndex = color * 2 + 1; // 1 for white, 3 for black

        //castling + castling prevention when king moved
        if (type == king)
        { 
            int deltaX = end - start;
            if (!isCapture && (deltaX == 2 || deltaX == -2)) //keeps castling code from running all the time, castling is never a capture
            {
                int castlingRook = PieceInt(rook, color);
                if (deltaX == 2)
                { // short castling
                    board.MovePieceToEmptySpace(rooksBefore[shortCastlingIndex], rooksAfter[shortCastlingIndex], castlingRook);
                }
                else if (deltaX == -2)
                { // long castling
                    board.MovePieceToEmptySpace(rooksBefore[longCastlingIndex], rooksAfter[longCastlingIndex], castlingRook);
                }
            }
            // no castling after moving kings
            gameData.castling[shortCastlingIndex] = false;
            gameData.castling[longCastlingIndex] = false;
            /*foreach (int index in new int[] { shortCastlingIndex, longCastlingIndex }) //no castling after moving kings
            {
                if (gameData.castling[index]) previousPositions[0][index] = 1;
                gameData.castling[index] = false;
            }*/
            if (color == black) blackKingPosition = end;
            else if (color == white) whiteKingPosition = end;
        }

        //castling prevention after rook moved
        else if (type == rook)
        { 
            if (SpaceX(start) == 7 && gameData.castling[shortCastlingIndex])
            {
                gameData.castling[shortCastlingIndex] = false;
            }
            else if (SpaceX(start) == 0 && gameData.castling[longCastlingIndex])
            {
                gameData.castling[longCastlingIndex] = false;
            }
        }

        //taking the pawn in case of e.p.
        else if (type == pawn)
        { 
            if (end == gameData.epSpace && gameData.epSpace != 0)
            {
                int spaceToTake = board.TakeEPPawn(end, color);
            }
        }

        //prventing castling when rook needed for it was taken
        if (isCapture && (end == 7 || end == 0 || end == 63 || end == 56))
        {
            if (board.Contains(end, whitePiece | rook)) 
            {
                gameData.castling[shortCastlingIndex] = (!(end == 7)) && gameData.castling[shortCastlingIndex];
                gameData.castling[longCastlingIndex] = (!(end == 0)) && gameData.castling[longCastlingIndex];
            }
            else if (board.Contains(end, blackPiece | rook)) // ugly repetition
            {
                gameData.castling[shortCastlingIndex] = (!(end == 63)) && gameData.castling[shortCastlingIndex];
                gameData.castling[longCastlingIndex] = (!(end == 56)) && gameData.castling[longCastlingIndex];
            }
        }

        //leave that like this! dont put it in the if clause up there. IT WILL BREAK
        
        //setting the e.p space for the next move
        if (!isCapture && Mathf.Abs(start / 8 - end / 8) == 2 && type == pawn)
        {
            gameData.epSpace = (color == black) ? end + 8 : end - 8;
        }
        else
        {
            gameData.epSpace = 0;
        }

        // making the actual move
        if (!isCapture) 
        {
            board.MovePieceToEmptySpace(start, end, piece);
        }
        else
        {
            board.MovePieceToFullSpace(start, end, piece, capture);
        }

        // turning pawns to queens on promotion
        if ((SpaceY(end) == 7 || SpaceY(end) == 0) && type == pawn) // pawn of own color never will go backwards
        {
            board.TurnPawnToQueen(end, color);
            //board.UpdateFullSpaces(); //for safety, bugs out when i dont
        }

        //ultra mega super slow
        SetAttackedSpaceData();
        //SetKingPositions();
        //board.UpdateFullSpaces();
        return undoData;
    }

    public void UndoMovePiece(UndoMoveData undoData){
        board.MovePieceToEmptySpace(undoData.end, undoData.start, undoData.movedPiece);
        gameData.castling = undoData.castlingBefore;
        gameData.epSpace = undoData.epSpaceBefore;
        if(undoData.takenPiece != 0){
            board.CreatePiece(undoData.end, undoData.takenPiece);
        }
        if (undoData.wasPromotion){
            board.TurnQueenToPawn(undoData.start, PieceColor(undoData.movedPiece));
        }
        if(undoData.end == undoData.epSpaceBefore && undoData.end != 0){ // move was en passant
            int pawnColor = (PieceColor(undoData.movedPiece) == white) ? blackPiece : whitePiece;
            int epOffset = (pawnColor == blackPiece) ? -8 : 8;
            board.CreatePiece(undoData.end + epOffset, pawn|pawnColor);
            return; // ep cant be castling
        }
        if(undoData.castlingIndex != -1){ // move was castling
            int rookColor = (PieceColor(undoData.movedPiece) == white) ? whitePiece : blackPiece;
            board.MovePieceToEmptySpace(rooksAfter[undoData.castlingIndex], rooksBefore[undoData.castlingIndex], rook | rookColor);
        }
        SetKingPositions();
    }
}