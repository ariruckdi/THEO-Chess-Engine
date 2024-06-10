using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq.Expressions;
using UnityEngine;
using static ChessBoard;

public struct Move
{
    public int Start;
    public int End;
    public int Eval;
    public Move(int start, int end, int eval = Engine.NEG_INFTY)
    {
        Start = start;
        End = end;
        Eval = eval;
    }

    public void SetEval(Evaluation eval)
    {
        Eval = eval.EvaluateMove(this);
    }

    public static int CompareByEval(Move move1, Move move2)
    {
        return -move1.Eval.CompareTo(move2.Eval); // that minus makes sure better moves come first in the search
    }
}

public class MoveGenerator
{
    //core
    private ChessBoard board;
    public ChessBoard Board {get => board;}

    //constants
    public const int SHORT_CASTLING_WHITE = 0, LONG_CASTLING_WHITE = 1, SHORT_CASTLING_BLACK = 2, LONG_CASTLING_BLACK = 3;
    static readonly int[] SLIDE_DIRECTIONS = new int[] { 8, -8, 1, -1, 9, 7, -7, -9 };
    //up, down, right, left, ur, ul, dr, dl
    static readonly int[][] KNIGHT_DIRECTIONS = new int[][] { new int[] { 15, 17 }, new int[] { -15, -17 }, new int[] { -6, 10 }, new int[] { 6, -10 } };
    //up, down, right, left
    static int[][] SpacesToEdge;
    static BitBoard[] PrecomputedKnightSpaces, PrecomputedWhitePawnAttacks, PrecomputedBlackPawnAttacks, PrecomputedKingSpaces;

    static readonly int[] CASTLING_SPACES_WHITE = new int[] { 5, 6, 2, 3, 1};
    static readonly int[] CASTLING_SPACES_BLACK = new int[] {5 + 8 * 7, 6 + 8 * 7, 2 + 8 * 7, 3 + 8 * 7, 1 + 8 * 7};

    static readonly int[][] CASTLING_SPACES = {CASTLING_SPACES_WHITE, CASTLING_SPACES_BLACK};

    //variables

    //hashing

    public MoveGenerator(ChessBoard board)
    {
        this.board = board;
        SpacesToEdge = GenerateSpacesToEdgeData();
        PrecomputedKnightSpaces = PrecomputeKnightSpaces();
        PrecomputedWhitePawnAttacks = PrecomputeWhitePawnAttacks();	
        PrecomputedBlackPawnAttacks = PrecomputeBlackPawnAttacks();
        PrecomputedKingSpaces = PrecomputeKingSpaces();
    }

    public MoveGenerator()
    {
        this.board = new ChessBoard();
        SpacesToEdge = GenerateSpacesToEdgeData();
        PrecomputedKnightSpaces = PrecomputeKnightSpaces();
        PrecomputedWhitePawnAttacks = PrecomputeWhitePawnAttacks();	
        PrecomputedBlackPawnAttacks = PrecomputeBlackPawnAttacks();
        PrecomputedKingSpaces = PrecomputeKingSpaces();
    }

    //getters and initalisation
    static int[][] GenerateSpacesToEdgeData()
    {
        var output = new int[64][];
        for (int i = 0; i < 64; i++)
        {
            int x = SpaceX(i);
            int y = SpaceY(i);
            var edgeData = new int[] { 7 - y, y, 7 - x, x, System.Math.Min(7 - y, 7 - x), System.Math.Min(7 - y, x), System.Math.Min(y, 7 - x), System.Math.Min(y, x) };
            output[i] = edgeData;
        }
        return output;
    }

    private static BitBoard[] PrecomputeKnightSpaces()
    {
        BitBoard[] output = new BitBoard[64];
        int newSpace, deltaX;
        for (int space = 0; space < 64; space++)
        {
            output[space] = new BitBoard();
            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                if (SpacesToEdge[space][directionIndex] >= 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        newSpace = space + KNIGHT_DIRECTIONS[directionIndex][i];
                        deltaX = System.Math.Abs(SpaceX(newSpace) - SpaceX(space));
                        if (newSpace >= 0 && newSpace < 64 && (deltaX == 1 || deltaX == 2))
                        {
                            output[space] += BitBoard.FromIndex(newSpace);
                        }
                    }
                }
            }
        }
        return output;
    }

    private static BitBoard[] PrecomputeWhitePawnAttacks()
    {
        var result = new BitBoard[64];
        for (int space = 0; space < 64; space++)
        {
            result[space] = new BitBoard();
            BitBoard pawn = BitBoard.FromIndex(space);
            result[space] += BitBoard.ShiftNorthEast(pawn) + BitBoard.ShiftNorthWest(pawn);
        }
        return result;
    }

    private static BitBoard[] PrecomputeBlackPawnAttacks()
    {
        var result = new BitBoard[64];
        for (int space = 0; space < 64; space++)
        {
            result[space] = new BitBoard();
            BitBoard pawn = BitBoard.FromIndex(space);
            result[space] += BitBoard.ShiftSouthEast(pawn) + BitBoard.ShiftSouthWest(pawn);
        }
        return result;
    }

    private static BitBoard[] PrecomputeKingSpaces()
    {
        var result = new BitBoard[64];
        for (int space = 0; space < 64; space++)
        {
            result[space] = new BitBoard();
            for (int dirIndex = 0; dirIndex < 8; dirIndex++)
            {
                if (SpacesToEdge[space][dirIndex] == 0) continue;
                result[space][space + SLIDE_DIRECTIONS[dirIndex]] = true;
            }
        }
        return result;
    }

    public string SimpleMoveName(int startSpace, int endSpace)
    {
        return SpaceName(startSpace) + SpaceName(endSpace);
    }

    public string MoveName(int startSpace, int endSpace, bool longNotation = false)
    {
        string output;
        if (!Board.Occupied[startSpace]) return "Tried to move from empty space!";
        int piece = Board[startSpace];
        int pieceType = PieceType(piece);
        UndoMoveData testMoveForCheck = Board.MovePiece(startSpace, endSpace);
        bool leadsToCheck = IsPlayerInCheck(Board.GameData.OnTurn ^ 1);
        Board.UndoMovePiece(testMoveForCheck);
        if (pieceType == 6)
        {
            if (startSpace - endSpace == -2) return "0-0";
            if (startSpace - endSpace == 2) return "0-0-0";
        }
        if (Board[endSpace] == 0)
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

    //pseudo legal movegen
    private BitBoard GetSlideSpaces(int space, int color, int pieceType, bool capturesOnly = false)
    {
        int targetSpace;
        int dirStart = (pieceType == BISHOP) ? 4 : 0;
        int dirEnd = (pieceType == ROOK) ? 4 : 8;
        BitBoard result = new BitBoard();
        for (int dirIndex = dirStart; dirIndex < dirEnd; dirIndex++)
        {   for (int step = 0; step < SpacesToEdge[space][dirIndex]; step++)
            {
                targetSpace = space + SLIDE_DIRECTIONS[dirIndex] * (step + 1);
                if (Board.ColorOccupied(color)[targetSpace]) break;
                if (Board.ColorOccupied(color ^ 1)[targetSpace]) {result[targetSpace] = true; break;}
                result[targetSpace] = !capturesOnly;
            }
        }
        return result;
    }

    private BitBoard GetPawnPushes(int space, int color)
    {
        BitBoard pawn = BitBoard.FromIndex(space);
        var result = new BitBoard();
        if (color == WHITE) {
            result += BitBoard.ShiftNorth(pawn) & ~Board.Occupied;
            if (SpaceY(space) == 1 && !result.IsEmpty()) result += BitBoard.ShiftNorthDouble(pawn) & ~Board.Occupied;
        } else {
            result += BitBoard.ShiftSouth(pawn) & ~Board.Occupied;
            if (SpaceY(space) == 6 && !result.IsEmpty()) result += BitBoard.ShiftSouthDouble(pawn) & ~Board.Occupied;
        }
        return result;
    }

    private BitBoard GetPawnCaptures(int space, int color) 
    {
        if (color == WHITE) return PrecomputedWhitePawnAttacks[space] & (Board.BlackOccupied + BitBoard.FromIndex(Board.GameData.EPSpace));
        return PrecomputedBlackPawnAttacks[space] & (Board.WhiteOccupied + BitBoard.FromIndex(Board.GameData.EPSpace));
    }

    private BitBoard GetPawnSpaces(int space, int color, bool capturesOnly = false)
    {
        var output = new BitBoard();
        if (!capturesOnly) output += GetPawnPushes(space, color);
        output += GetPawnCaptures(space, color);
        return output;
    }

    private BitBoard GetSpacesCoveredByPawn(int space, int color) 
    {
        if (color == WHITE) return PrecomputedWhitePawnAttacks[space];
        return PrecomputedBlackPawnAttacks[space];
    }

    private BitBoard GetSpacesCoveredByKnight(int space) 
    {
        return PrecomputedKnightSpaces[space];
    }

    public BitBoard GetSpacesCoveredByKing(int space) 
    {
        return PrecomputedKingSpaces[space];
    }

    private BitBoard GetSpacesCoveredBySlider(int space, int type, int color)
    {
        int targetSpace;
        int dirStart = (type == BISHOP) ? 4 : 0;
        int dirEnd = (type == ROOK) ? 4 : 8;
        int theirKing = (color == WHITE) ? BLACK_PIECE | KING : WHITE_PIECE | KING;
        BitBoard result = new BitBoard();
        for (int dirIndex = dirStart; dirIndex < dirEnd; dirIndex++)
        {   for (int step = 0; step < SpacesToEdge[space][dirIndex]; step++)
            {
                targetSpace = space + SLIDE_DIRECTIONS[dirIndex] * (step + 1);
                if (Board.Occupied[targetSpace] && !Board.Contains(targetSpace, theirKing)) {result[targetSpace] = true; break;} //ignore kings as they have to move out the way
                result[targetSpace] = true;
            }
        }
        return result;
    }

    private BitBoard GetKnightSpaces(int space, int color, bool capturesOnly = false)
    {
        if (capturesOnly) return PrecomputedKnightSpaces[space] & Board.ColorOccupied(color ^ 1);
        return PrecomputedKnightSpaces[space] & ~Board.ColorOccupied(color);
    }

    private BitBoard GetKingSpaces(int space, int color, bool capturesOnly = false, bool includeCastling = true)
    {
        if (capturesOnly) return PrecomputedKingSpaces[space] & Board.ColorOccupied(color ^ 1);
        if (!includeCastling) return PrecomputedKingSpaces[space] & ~Board.ColorOccupied(color);
        var output = PrecomputedKingSpaces[space] & ~Board.ColorOccupied(color);
        int castlingRow = 7 * color;
        bool shortCastlingValid, longCastlingValid;
        if (space == castlingRow * 8 + 4 && !IsPlayerInCheck(color))
        {
            BitBoard attackedSpaces = GenerateCoveredSpaceBitboard(color ^ 1);
            if (Board.GameData.Castling[2 * color]) // short
            {
                shortCastlingValid = true;
                for (int i = 0; i < 2; i++)
                {
                    if (Board.Occupied[CASTLING_SPACES[color][i]]) {shortCastlingValid = false; break;} //cant castle is pieces are in the way
                    if (attackedSpaces[CASTLING_SPACES[color][i]]) {shortCastlingValid = false; break;} //cant castle through check
                }
                if (shortCastlingValid) output[space + 2] = true;
            }
            if (Board.GameData.Castling[2 * color + 1]) // long
            {
                longCastlingValid = true;
                for (int i = 2; i < 5; i++)
                {
                    if (Board.Occupied[CASTLING_SPACES[color][i]]) {longCastlingValid = false; break;}
                    if (attackedSpaces[CASTLING_SPACES[color][i]]) {longCastlingValid = false; break;}
                }
                if (longCastlingValid) output[space - 2] = true;
            }
        }
        return output;
    }

    public BitBoard GetPossibleSpacesForPiece(int space, int piece, bool capturesOnly = false, bool includeCastling = true)
    {
        int pieceType = PieceType(piece);
        int pieceColor = PieceColor(piece);
        if (pieceType == PAWN) return GetPawnSpaces(space, pieceColor, capturesOnly: capturesOnly);
        else if (BISHOP <= pieceType && pieceType <= QUEEN) return GetSlideSpaces(space, pieceColor, pieceType, capturesOnly: capturesOnly);
        else if (pieceType == KNIGHT) return GetKnightSpaces(space, pieceColor, capturesOnly: capturesOnly);
        else if (pieceType == KING) return GetKingSpaces(space, pieceColor, capturesOnly:capturesOnly, includeCastling);
        else return new BitBoard(0);
    }

    //legal movegen and prerequisites
    public BitBoard GenerateCoveredSpaceBitboard(int color)
    {
        var result = new BitBoard();
        foreach (int space in Board.ColorOccupied(color).GetActive())
        {
            int pieceType = PieceType(Board[space]);
            if (pieceType == PAWN) result += GetSpacesCoveredByPawn(space, color);
            else if (BISHOP <= pieceType && pieceType <= QUEEN) result += GetSpacesCoveredBySlider(space, pieceType, color);
            else if (pieceType == KNIGHT) result += GetSpacesCoveredByKnight(space);
            else if (pieceType == KING) result += GetSpacesCoveredByKing(space);
        }
        return result;
    }

    public bool IsPlayerInCheck(int color)
    {
        var bitboardToCheck = (color == WHITE) ? GenerateCoveredSpaceBitboard(BLACK) : GenerateCoveredSpaceBitboard(WHITE);
        return bitboardToCheck[Board.KingPosition(color)];
    }

    public BitBoard[] GetPinnedRays(int color)
    {
        var result = new BitBoard[9];
        result[8] = new BitBoard();
        for (int dirIndex = 0; dirIndex < 8; dirIndex++)
        {
            BitBoard rayMask = new BitBoard();
            bool diagonal = dirIndex > 3;
            int dirOffset = SLIDE_DIRECTIONS[dirIndex];
            if(Board.KingPosition(color) > 63 || Board.KingPosition(color) < 0) Debug.Log(Board.KingPosition(color).ToString());
            int toEdge = SpacesToEdge[Board.KingPosition(color)][dirIndex];
            bool friendlyAlongRay = false;
        	for (int rayPosIndex = 0; rayPosIndex < toEdge; rayPosIndex++)
            {
                int space = Board.KingPosition(color) + dirOffset * (rayPosIndex + 1);
                rayMask[space] = true;
                if (Board.ColorOccupied(color)[space]) {
                    if(!friendlyAlongRay) friendlyAlongRay = true; //this piece might be pinned
                    else break; //but only if its the first consecutive friendly along the way
                } else if (Board.ColorOccupied(color ^ 1)[space]) {
                    if ((diagonal && (PieceType(board[space]) == BISHOP || PieceType(board[space]) == QUEEN)) || (!diagonal && (PieceType(board[space]) == ROOK || PieceType(board[space]) == QUEEN))){
                        if (friendlyAlongRay) {result[dirIndex] = rayMask; result[8] += rayMask;} //pin found
                        break;
                    }
                    else break; //enemy blocks any pins
                }
            }
        }
        
        return result;
    }

    public BitBoard GetCheckMask(int color)
    {
        BitBoard result = new BitBoard();

        bool check = false;
        bool doubleCheck = false;
        //sliders
        for (int dirIndex = 0; dirIndex < 8; dirIndex++)
        {
            BitBoard rayMask = new BitBoard();
            bool diagonal = dirIndex > 3;
            int dirOffset = SLIDE_DIRECTIONS[dirIndex];
            if(Board.KingPosition(color) > 63 || Board.KingPosition(color) < 0) Debug.Log(Board.KingPosition(color).ToString());
            int toEdge = SpacesToEdge[Board.KingPosition(color)][dirIndex];
            bool friendlyAlongRay = false;

        	for (int rayPosIndex = 0; rayPosIndex < toEdge; rayPosIndex++)
            {
                int space = Board.KingPosition(color) + dirOffset * (rayPosIndex + 1);
                rayMask[space] = true;
                if (Board.ColorOccupied(color)[space]) {
                    if(!friendlyAlongRay) friendlyAlongRay = true; //this piece might be pinned
                    else break; //but only if its the first consecutive friendly along the way
                } else if (Board.ColorOccupied(color ^ 1)[space]) {
                    if ((diagonal && (PieceType(board[space]) == BISHOP || PieceType(board[space]) == QUEEN)) || (!diagonal && (PieceType(board[space]) == ROOK || PieceType(board[space]) == QUEEN))){
                        if (!friendlyAlongRay) {
                            result += rayMask;
                            doubleCheck = check; //if check is already true this is the second time we are here
                            check = true;
                            } //check ray found
                        break;
                    }
                    else break; //enemy blocks any pins
                }
            }
        }
        //knights
        foreach (int space in PrecomputedKnightSpaces[Board.KingPosition(color)].GetActive())
        {
            if (board.Contains(space, ((color == WHITE) ? BLACK_PIECE : WHITE_PIECE) | KNIGHT))
            {
                result += BitBoard.FromIndex(space);
                doubleCheck = check;
                check = true;
            }
        }

        //pawns
        var pawnMaskToCheck = (color == WHITE) ? PrecomputedWhitePawnAttacks[Board.KingPosition(color)] : PrecomputedBlackPawnAttacks[Board.KingPosition(color)];
        foreach (int space in pawnMaskToCheck.GetActive())
        {
            if (board.Contains(space, ((color == WHITE) ? BLACK_PIECE : WHITE_PIECE) | PAWN))
            {
                result += BitBoard.FromIndex(space);
                doubleCheck = check;
                check = true;
            }
        }
        if (doubleCheck) result = new BitBoard(ulong.MaxValue);
        return result;
    }

    public List<Move> GetAllLegalMoves(int color)
    {
        var result = new List<Move>(64);
        var occupiedWithoutKing = Board.ColorOccupied(color) & ~BitBoard.FromIndex(Board.KingPosition(color));
        foreach (int targetSpace in GetLegalKingMoves(color).GetActive())
        {
            result.Add(new Move(Board.KingPosition(color), targetSpace));
        }
        foreach (int startSpace in occupiedWithoutKing.GetActive())
        {
            result.AddRange(GetListOfLegalMovesForPiece(color, startSpace));
        }

        return result;
    }

    public BitBoard GetLegalKingMoves(int color)
    {
        return GetKingSpaces(Board.KingPosition(color), color) & ~GenerateCoveredSpaceBitboard(color ^ 1);
    }

    public BitBoard GetLegalMovesForPiece(int color, int space, bool capturesOnly = false)
    {
        //variables
        int piece = Board[space];
        var result = capturesOnly ?  GetPossibleSpacesForPiece(space, piece, true, false) : GetPossibleSpacesForPiece(space, piece);
        var checkMask = GetCheckMask(color);
        var pinMask = GetPinnedRays(color);
        bool pinned = pinMask[8][space];
        bool check = IsPlayerInCheck(color);

        if ((ulong)checkMask == ulong.MaxValue && (!(PieceType(piece) == KING))) return new BitBoard(); //double check means we only move the king

        if (PieceType(piece) == KING)
        {
            return result & ~GenerateCoveredSpaceBitboard(color ^ 1);
        }

        if (check)
        {
            if (pinned) return result & checkMask & PinnedRays(ref pinMask, space); //we are both in check and the piece we move is pinned
            return result & checkMask;
        }
        
        if (pinned)
        {
            return result & PinnedRays(ref pinMask, space); //piece can only move along pin ray
        }


        int epSpace = Board.GameData.EPSpace;
        if (PieceType(piece) == PAWN && result[epSpace]) //prevent en passant leading to check
        {
            UndoMoveData move = Board.MovePiece(space, epSpace);
            if (IsPlayerInCheck(color)) {
                result[epSpace] = false;
            }
            Board.UndoMovePiece(move);
        }

        return result;
    }

    public List<Move> GetListOfLegalMovesForPiece(int color, int space)
    {
        return GetLegalMovesForPiece(color, space).ToMoveList(space);
    }

    public BitBoard PinnedRays(ref BitBoard[] pinnedRays, int space)
    {
        if (!pinnedRays[8][space]) throw new ArgumentException("Piece ist not pinned!");
        var result = new BitBoard();
        for (int i = 0; i < 7; i++)
        {
            if(pinnedRays[i][space]) result += pinnedRays[i];
        }
        return result;
    }
}