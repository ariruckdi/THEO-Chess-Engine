using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using TMPro;

class ZobristHashing
{
    ulong[] keys = new ulong[781];

    public ulong[] Keys { get => keys; }

    public ZobristHashing(ulong randomSeed)
    {
        XORShiftRandom rng = new XORShiftRandom(randomSeed);
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = rng.Next();
        }
    }

    ulong LookUpPiece(int space, int piece)
    {
        int index = ((piece & 0b111) - 1 + (6 * (piece >> 4))) * space; //piece type and piece color create an index from 0 to 11, which is then multiplied with space
        return keys[index];
    }

    ulong LookUpColor(int color)
    {
        return keys[768] * (ulong)color;
    }

    ulong LookUpCastling(int castlingIndex)
    {
        return keys[769 + castlingIndex];
    }

    ulong LookUpEP(int epSpace)
    {
        if (epSpace == 0) return 0;
        return keys[773 + ChessBoard.SpaceY(epSpace)];
    }

    public ulong Hash(ChessBoard input, int player, bool[] castling, int epSpace)
    {
        ulong result = 0;
        foreach (int space in input.Occupied.GetActive())
        {
            if (space == -1) break;
            result ^= LookUpPiece(space, input[space]);
        }
        result ^= LookUpColor(player);
        for (int castlingIndex = 0; castlingIndex < 4; castlingIndex++)
        {
            if (castling[castlingIndex]) result ^= LookUpCastling(castlingIndex);
        }
        result ^= LookUpEP(epSpace);
        return result;
    }
}

public struct ChessGameData
{
    public int OnTurn;
    public int EPSpace;
    public bool[] Castling;

    public ChessGameData(int nextPlayer, int enPassantSpace, bool[] castlingData)
    {
        OnTurn = nextPlayer;
        EPSpace = enPassantSpace;
        Castling = castlingData;
    }
}

public class ChessBoard
{
    //constants
    public const int PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6;
    public const int WHITE_PIECE = 8, BLACK_PIECE = 16;
    public const int WHITE = 0, BLACK = 1;
    public static readonly int[] POSSIBLE_PIECES = new int[]
    {
        PAWN | WHITE_PIECE, KNIGHT | WHITE_PIECE, BISHOP | WHITE_PIECE, ROOK | WHITE_PIECE, QUEEN | WHITE_PIECE, KING | WHITE_PIECE,
        PAWN | BLACK_PIECE, KNIGHT | BLACK_PIECE, BISHOP | BLACK_PIECE, ROOK | BLACK_PIECE, QUEEN | BLACK_PIECE, KING | BLACK_PIECE
    };
    public static readonly char[] PIECE_NAMES = new char[] { 'P', 'N', 'B', 'R', 'Q', 'K' };
    public static readonly char[] FILE_LETTERS = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
    public static readonly char[] RANK_NUMBERS = new char[] { '1', '2', '3', '4', '5', '6', '7', '8' };

    public static readonly BitBoard WHITE_SPACES = (BitBoard)0b10101010_01010101_10101010_01010101_10101010_01010101_10101010_01010101;

    
    //castling data 
    //short white, long white, short black, short white
    public static readonly int[] ROOKS_BEFORE_CASTLING = new int[] { 7, 0, 63, 56 };
    public static readonly int[] KINGS_BEFORE_CASTLING = new int[] { 4, 4, 60, 60 };
    public static readonly int[] ROOKS_AFTER_CASTLING = new int[] { 5, 3, 61, 59 };
    public static readonly int[] KINGS_AFTER_CASTLING = new int[] { 6, 2, 62, 58 };

    //core

    public BitBoard Occupied {get => occupied;} // Every PiecePosBoard added together
    public BitBoard WhiteOccupied {get => occupied & ~colorMask;}
    public BitBoard BlackOccupied {get => occupied & colorMask;}

    public int WhiteKingPosition {get =>  piecePositionBoards[5].TrailingZeroCount();}
    public int BlackKingPosition {get =>  piecePositionBoards[11].TrailingZeroCount();}

    private BitBoard occupied;
    private BitBoard[] piecePositionBoards; //white pawns, white knights, white bishops, white rooks, white queens, white kings, after that same for black
    private BitBoard colorMask; // All black pieces

    public ChessGameData GameData;
    
    readonly ZobristHashing hashing;

    //init
    public ChessBoard()
    {
        piecePositionBoards = new BitBoard[12];
        hashing = new ZobristHashing((ulong)WHITE_SPACES);
        occupied = new BitBoard();
        colorMask = new BitBoard();
        GameData = new ChessGameData(WHITE, 0, new bool[] { true, true, true, true });
        for (int i = 0; i < 12; i++)
        {
            piecePositionBoards[i] = new BitBoard();
        }
    }

    //operators and nice stuff
    public int this[int index]
    {
        get => GetSpace(index);
        set => SetSpace(index, value);
    }

    int GetSpace(int space)
    {
        if (!occupied[space]) return 0;
        for (int i = 0; i < 6; i++)
        {
            if (piecePositionBoards[i][space])
            {
                return i + 1 + WHITE_PIECE;
            }
            else if (piecePositionBoards[i + 6][space])
            {
                return i + 1 + BLACK_PIECE;
            }
        }
        throw new IndexOutOfRangeException("The board is fucked!");
    }

    void SetSpace(int space, int value)
    {
        if (Occupied[space]) {
            int prevPiece = GetSpace(space);
            piecePositionBoards[BitBoardIndex(prevPiece)][space] = false;
        } 
        if (value != 0) piecePositionBoards[BitBoardIndex(value)][space] = true;

        occupied[space] = (value != 0);
        colorMask[space] = (value >> 4) == 1;
    }

    public override string ToString()
    {
        var result = "";
        for (int space = 0; space < 64; space++)
        {
            if (space % 8 == 0 && space != 0) result += "\n";
            int piece = this[space];
            if (piece == 0) result += "-";
            else result += (PieceColor(piece) == WHITE) ? PIECE_NAMES[PieceType(piece) - 1].ToString() : PIECE_NAMES[PieceType(piece) - 1].ToString().ToLower();
        }
        return result;
    }

    //some static getters
    public static int PieceColor(int piece)
    {
        if (piece == 0) throw new Exception();
        return (piece >> 4);
        //equal to: return ((piece & 0b11000) == whitePiece) ? white: black ;
    }

    public static int PieceType(int piece)
    {
        return piece & 0b111;
    }

    public static int PieceInt(int pieceType, int color)
    {
        return pieceType | ((color + 1) << 3);
    }

    public static int BitBoardIndex(int piece)
    {
        return (piece & 0b111) - 1 + 6 * (piece >> 4);
    }

    public static int BitBoardIndex(int pieceType, int color)
    {
        return pieceType - 1 + 6 * color;
    }

    public static int SpaceColor(int space)
    {
        return WHITE_SPACES[space] ? WHITE : BLACK;
    }

    public static int SpaceX(int space)
    {
        //return space % 8; but should be faster
        return space & 0b111;
    }

    public static int SpaceY(int space)
    {
        //return space / 8; but quicker
        return space >> 3;
    }

    public static int Distance(int start, int end)
    {
        int deltaX = Math.Abs(SpaceX(end) - SpaceX(start));
        int deltaY = Math.Abs(SpaceY(end) - SpaceY(start));
        return Math.Max(deltaY, deltaX);
    }

    public static string SpaceName(int space)
    {
        int x = SpaceX(space);
        int y = SpaceY(space);
        return FILE_LETTERS[x].ToString() + RANK_NUMBERS[y].ToString();
    }

    public static int SpaceNumberFromString(string spaceName)
    {
        int x = 0, y = 0;
        for (int i = 0; i < 8; i++)
        {
            if (FILE_LETTERS[i] == spaceName[0])
            {
                x = i;
            }
            if (RANK_NUMBERS[i] == spaceName[1])
            {
                y = i;
            }
        }
        return 8 * y + x;
    }

    public static char PieceLetter(int piece)
    {
        return PIECE_NAMES[PieceType(piece) - 1];
    }

    //non static getters, mostly used for eval
    public BitBoard ColorOccupied(int color)
    {
        return (color == WHITE) ? WhiteOccupied : BlackOccupied;
    }

    public int PieceCount(int piece)
    {
        return piecePositionBoards[BitBoardIndex(piece)].CountActive();
    }

    //faster methods for board acessing
    public bool Contains(int space, int piece)
    {
        return piecePositionBoards[BitBoardIndex(piece)][space];
    }

    public bool ContainsBitBoardIndex(int space, int index)
    {
        return piecePositionBoards[index][space];
    }

    public BitBoard GetPieceBitBoard(int piece)
    {
        return piecePositionBoards[BitBoardIndex(piece)];
    }

    public BitBoard GetPieceBitBoard(int pieceType, int color)
    {
        return piecePositionBoards[BitBoardIndex(pieceType, color)];
    }

    public List<int> FindPieces(int piece) //returns list of spaces where the piece is
    {
        int index = BitBoardIndex(piece);
        return piecePositionBoards[index].GetActive();
    }

    public List<int> FindPieces(int pieceType, int color) //returns list of spaces where the piece is
    {
        int index = BitBoardIndex(pieceType, color);
        return piecePositionBoards[index].GetActive();
    }

    public int KingPosition(int color) {
        return (color == WHITE) ? WhiteKingPosition : BlackKingPosition;	
    }

    public List<int> FindPiecesOfColor(int color)
    {
        return ColorOccupied(color).GetActive();
    }

    public int CountDoublePawns(int color)
    {
        int result = 0;
        foreach (BitBoard file in BitBoard.FILES)
        {
            if ((piecePositionBoards[6 * color] & file).CountActive() > 1) result += 1;
        }
        return result;
    }

    //methods to make moving and undoing faster 
    public void CreatePiece(int position, int piece)
    {
        if (piece == 0) return;
        
        piecePositionBoards[BitBoardIndex(piece)][position] = true;

        occupied[position] = true;
        colorMask[position] = (piece >> 4) == 1;
    }

    public void MovePieceToEmptySpace(int start, int end, int piece)
    {
        piecePositionBoards[BitBoardIndex(piece)][start] = false;
        piecePositionBoards[BitBoardIndex(piece)][end] = true;

        occupied[start] = false;
        colorMask[start] = false;
        occupied[end] = true;
        colorMask[end] = (piece >> 4) == 1;
    }

    public void MovePieceToFullSpace(int start, int end, int piece, int takenPiece)
    {
        piecePositionBoards[BitBoardIndex(takenPiece)][end] = false;
        piecePositionBoards[BitBoardIndex(piece)][start] = false;
        piecePositionBoards[BitBoardIndex(piece)][end] = true;

        occupied[start] = false; // no need to update full spaces at end, there will still be a piece
        colorMask[start] = false;
        colorMask[end] = (piece >> 4) == 1;
    }

    public void TurnPawnToQueen(int pos, int color)
    {
        piecePositionBoards[QUEEN - 1 + (6 * color)][pos] = true; //queen = true
        piecePositionBoards[6 * color][pos] = false; //pawn = false
    }

    public void TurnQueenToPawn(int pos, int color) //for undoing promotions
    {
        piecePositionBoards[QUEEN - 1 + (6 * color)][pos] = false;
        piecePositionBoards[6 * color][pos] = true;
    }

    public int TakeEPPawn(int pos, int color)
    {
        piecePositionBoards[-6 * (color - 1)][(-8 + 16 * color) + pos] = false; // 6 for white (black pawn), 0 for black (white pawn) and -8 for white, 8 for black 
        occupied[(-8 + 16 * color) + pos] = false;
        colorMask[(-8 + 16 * color) + pos] = false;
        return (-8 + 16 * color) + pos;
    }

    public UndoMoveData MovePiece(int start, int end)
    {
        //variables
        int piece = this[start];
        int color = PieceColor(piece);
        int type = PieceType(piece);
        int captured = this[end];
        bool capture = captured != 0;
        int shortCastlingIndex = color * 2; // 0 for white, 2 for black
        int longCastlingIndex = color * 2 + 1; // 1 for white, 3 for black
        int nextEPSpace = 0;

        //for undoing
        int castlingIndex = -1;
        bool[] castlingBefore = (bool[])GameData.Castling.Clone();
        int epSpaceBefore = GameData.EPSpace;
        bool promotion = false;

        if (PieceType(captured) == KING) UnityEngine.Debug.LogWarning(this.ToString());


        //castling + castling prevention when king moved
        if (type == KING)
        {
            int deltaX = end - start;
            if (!capture && (deltaX == 2 || deltaX == -2)) //keeps castling code from running all the time, castling is never a capture
            {
                int castlingRook = PieceInt(ROOK, color);
                if (deltaX == 2) {
                    MovePieceToEmptySpace(ROOKS_BEFORE_CASTLING[shortCastlingIndex], ROOKS_AFTER_CASTLING[shortCastlingIndex], castlingRook);
                    castlingIndex = shortCastlingIndex;
                } //short
                else if (deltaX == -2) {
                    MovePieceToEmptySpace(ROOKS_BEFORE_CASTLING[longCastlingIndex], ROOKS_AFTER_CASTLING[longCastlingIndex], castlingRook);
                    castlingIndex = longCastlingIndex;
                } //long
            }
            // no castling after moving kings
            GameData.Castling[shortCastlingIndex] = false;
            GameData.Castling[longCastlingIndex] = false;
        }

        //castling prevention after rook moved
        else if (type == ROOK)
        {
            if (SpaceX(start) == 7) GameData.Castling[shortCastlingIndex] = false;
            else if (SpaceX(start) == 0) GameData.Castling[longCastlingIndex] = false;
        }

        //prventing castling when rook needed for it was taken
        if (PieceType(captured) == ROOK && (end == 7 || end == 0 || end == 63 || end == 56))
        {
            if (PieceColor(captured) == WHITE)
            {
                GameData.Castling[shortCastlingIndex] = (!(end == 7)) && GameData.Castling[shortCastlingIndex];
                GameData.Castling[longCastlingIndex] = (!(end == 0)) && GameData.Castling[longCastlingIndex];
            }
            else if (PieceColor(captured) == BLACK) // ugly repetition
            {
                GameData.Castling[shortCastlingIndex] = (!(end == 63)) && GameData.Castling[shortCastlingIndex];
                GameData.Castling[longCastlingIndex] = (!(end == 56)) && GameData.Castling[longCastlingIndex];
            }
        }

        //en passant execution and space setting
        if (type == PAWN)
        {
            if (end == GameData.EPSpace && GameData.EPSpace != 0) TakeEPPawn(end, color);
            if (!capture && Math.Abs(SpaceY(start) - SpaceY(end)) == 2) nextEPSpace = (color == BLACK) ? end + 8 : end - 8;
        }

        // making the actual move
        if (!capture) MovePieceToEmptySpace(start, end, piece);
        else MovePieceToFullSpace(start, end, piece, captured);

        // turning pawns to queens on promotion
        if ((SpaceY(end) == 7 || SpaceY(end) == 0) && type == PAWN) // pawn of own color never will go backwards
        {
            TurnPawnToQueen(end, color);
            promotion = true;
        }
        
        GameData.EPSpace = nextEPSpace;
        GameData.OnTurn ^= 1;

        return new UndoMoveData(start, end, piece, captured, castlingIndex, castlingBefore, epSpaceBefore, promotion);
    }

    public void UndoMovePiece(UndoMoveData undoData)
    {
        int movedPieceColor = PieceColor(undoData.piece);
        if (undoData.promotion) TurnQueenToPawn(undoData.end, movedPieceColor);
        MovePieceToEmptySpace(undoData.end, undoData.start, undoData.piece);
        GameData.Castling = undoData.castlingBefore;
        GameData.EPSpace = undoData.epSpaceBefore;
        if (undoData.captured != 0) CreatePiece(undoData.end, undoData.captured);
        if (undoData.end == undoData.epSpaceBefore && undoData.end != 0)
        { // move was en passant
            int pawnColor = (movedPieceColor == WHITE) ? BLACK_PIECE : WHITE_PIECE;
            int epOffset = (pawnColor == BLACK_PIECE) ? -8 : 8;
            CreatePiece(undoData.end + epOffset, PAWN | pawnColor);
        }
        if (undoData.castlingIndex != -1)
        { // move was castling
            int rookColor = (movedPieceColor == WHITE) ? WHITE_PIECE : BLACK_PIECE;
            MovePieceToEmptySpace(ROOKS_AFTER_CASTLING[undoData.castlingIndex], ROOKS_BEFORE_CASTLING[undoData.castlingIndex], ROOK | rookColor);
        }
        GameData.OnTurn ^= 1;
    }

    public static ChessBoard LoadFEN(string fenNotation)
    {
        var board = FENHandler.ReadFEN(fenNotation);
        board.GameData = FENHandler.GameDataFromFEN(fenNotation);
        return board;
    }

    public ulong ZobristHash()
    {
        return hashing.Hash(this, GameData.OnTurn, GameData.Castling, GameData.EPSpace);
    }
}



/*
struct Move
{
    private uint moveInt;
    public int Start {get => GetBlock(0);}
    public int End {get => GetBlock(6);}
    public int MovedPiece {get => GetBlock(12);}
    public int TakenPiece {get => GetBlock(18);}

    public bool Castling {get => GetBool(25);}
    public bool Promotion {get => GetBool(26);}
    public bool EP {get => GetBool(27);}

    public Move(int start, int end, int movedPiece, int takenPiece, bool castling = false, bool promotion = false, bool ep = false)
    {
        moveInt = 0;
        moveInt |= (uint)start;
        moveInt |= (uint)end << 6;
        moveInt |= (uint)movedPiece << 12;
        moveInt |= (uint)takenPiece << 18;
        moveInt |= (uint)(castling ? (1 << 25) : 0);
        moveInt |= (uint)(promotion ? (1 << 26) : 0);
        moveInt |= (uint)(ep ? (1 << 27) : 0);
    } 

    private int GetBlock(int startIndex) 
    {
        return (int)((moveInt >> startIndex) & 0b111111);
    }

    private bool GetBool(int index)
    {
        if (((moveInt >> index) & 1) == 1) return true;
        return false;
    }
}
*/