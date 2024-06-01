using System.Collections.Generic;
using System;
using System.Runtime.Serialization;
using System.ComponentModel;
using System.Xml;
using Unity.Collections;
using System.Diagnostics;

public struct BitBoard
{
    static readonly int[] LSB_TABLE = {
        63, 30,  3, 32, 59, 14, 11, 33,
        60, 24, 50,  9, 55, 19, 21, 34,
        61, 29,  2, 53, 51, 23, 41, 18,
        56, 28,  1, 43, 46, 27,  0, 35,
        62, 31, 58,  4,  5, 49, 54,  6,
        15, 52, 12, 40,  7, 42, 45, 16,
        25, 57, 48, 13, 10, 39,  8, 44,
        20, 47, 38, 22, 17, 37, 36, 26
    };

    public static readonly BitBoard[] FILES = {(BitBoard)0x0101010101010101, (BitBoard)0x0202020202020202, (BitBoard)0x0404040404040404, (BitBoard)0x0808080808080808, 
                                        (BitBoard)0x1010101010101010, (BitBoard)0x2020202020202020, (BitBoard)0x4040404040404040, (BitBoard)0x8080808080808080};

    public static readonly BitBoard[] RANKS = {(BitBoard)0x00000000000000ff, (BitBoard)0x000000000000ff00, (BitBoard)0x0000000000ff0000, (BitBoard)0x00000000ff000000, 
                                        (BitBoard)0x000000ff00000000, (BitBoard)0x0000ff0000000000, (BitBoard)0x00ff000000000000, (BitBoard)0xff00000000000000};

    //core
    ulong boardInt;

    //init
    public BitBoard(ulong newBoardInt = 0)
    {
        boardInt = newBoardInt;
    }

    //operators
    public static BitBoard operator +(BitBoard left, BitBoard right) // combines 2 boards
    {
        return new BitBoard(left.boardInt | right.boardInt);
    }

    public static BitBoard operator &(BitBoard left, BitBoard right) // usable for filtering
    {
        return new BitBoard(left.boardInt & right.boardInt);
    }

    public static BitBoard operator ~(BitBoard input) // inverts board
    {
        return new BitBoard(~input.boardInt);
    }

    public static BitBoard operator >>(BitBoard input, int shift)
    {
        return new BitBoard(input.boardInt >> shift);
    }

    public static BitBoard operator <<(BitBoard input, int shift)
    {
        return new BitBoard(input.boardInt << shift);
    }

    public static explicit operator BitBoard(ulong input)
    {
        return new BitBoard(input);
    }

    public static explicit operator ulong(BitBoard input)
    {
        return input.boardInt;
    }

    public static bool operator ==(BitBoard left, BitBoard right)
    {
        if (left.boardInt == right.boardInt) return true;
        else return false;
    }

    public static bool operator !=(BitBoard left, BitBoard right)
    {
        if (left.boardInt == right.boardInt) return false;
        else return true;
    }

    public override bool Equals(object other)
    {
        return other is BitBoard && this.Equals(other);
    }

    public bool Equals(BitBoard other)
    {
        return this.boardInt == other.boardInt;
    }

    public override int GetHashCode()
    {
        return boardInt.GetHashCode();
    }

    //getting and setting
    public bool this[int index]
    {
        get => GetBoolAtSpace(index);
        set => SetBoolAtSpace(index, value);
    }

    bool GetBoolAtSpace(int index)
    {
        return ((boardInt >> index) & 1) == 1;
    }

    void SetBoolAtSpace(int index, bool input)
    {
        if (input)
        {
            boardInt |= (ulong)1 << index;
        }
        else
        {
            boardInt &= ~((ulong)1 << index);
        }
    }

    public static BitBoard FromIndex(int index)
    {
        return (BitBoard)1 << index;
    }

    public bool IsEmpty()
    {
        return boardInt == 0;
    }

    //shifting, useful for movegen
    public static BitBoard ShiftSouth(BitBoard input)
    {
        return input >> 8;
    }
    
    public static BitBoard ShiftNorth(BitBoard input)
    {
        return input << 8;
    }

    public static BitBoard ShiftSouthDouble(BitBoard input)
    {
        return input >> 16;
    }

    public static BitBoard ShiftNorthDouble(BitBoard input)
    {
        return input << 16;
    }

    public static BitBoard ShiftSouthEast(BitBoard input)
    {
        return (input & ~FILES[7]) >> 7;
    }

    public static BitBoard ShiftSouthWest(BitBoard input)
    {
        return (input & ~FILES[0]) >> 9;
    }

    public static BitBoard ShiftNorthEast(BitBoard input)
    {
        return (input & ~FILES[7]) << 9;
    }

    public static BitBoard ShiftNorthWest(BitBoard input)
    {
        return (input & ~FILES[0]) << 7;
    }

    //converters
    public override string ToString()
    {
        var fullString = Convert.ToString((long)boardInt, 2).PadLeft(64, '0');
        var partArray = new string[8];
        for (int i = 0; i < 8; i++)
        {
            var currentPart = fullString.Substring(i * 8, 8);
            partArray[i] = currentPart;
        }
        return string.Join("\n", partArray);
    }

    //methods, higher level stuff
    public int CountActive()
    {
        ulong currentInt = boardInt;
        int count = 0;
        while (currentInt != 0) {
            currentInt ^= (ulong)1 << TrailingZeroCount(currentInt);
            count++;
        }
        return count;
    }

    public static int TrailingZeroCount(ulong bitBoardInt)
    {
        uint folded;
        if (bitBoardInt == 0) return 64;
        ulong xorDecrement = bitBoardInt ^ (bitBoardInt - 1);
        folded = (uint)(xorDecrement ^ (xorDecrement >> 32));
        return LSB_TABLE[(folded * 0x78291ACF) >> 26];
    }

    public int TrailingZeroCount()
    {
        uint folded;
        if (boardInt == 0) return 64;
        ulong xorDecrement = boardInt ^ (boardInt - 1);
        folded = (uint)(xorDecrement ^ (xorDecrement >> 32));
        return LSB_TABLE[(folded * 0x78291ACF) >> 26];
    }

    public int[] GetActive()
    {
        var output = new int[64] {-1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1};
        ulong currentInt = boardInt;
        int arrayIndex = 0;
        int nextActiveSpace;
        while (currentInt != 0) {
            nextActiveSpace = TrailingZeroCount(currentInt);
            output[arrayIndex] = nextActiveSpace;
            arrayIndex++;
            currentInt ^= (ulong)1 << nextActiveSpace;
        }
        return output;
    }

    public int[] GetActiveSlow() // SLOW: There are way faster algos for that
    {
        var output = new int[64] {-1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1,
                                  -1, -1, -1, -1, -1, -1, -1, -1};
        ulong currentInt = boardInt;
        int arrayIndex = 0;
        for (int i = 0; i < 64; i++)
        {

            if (currentInt == 0) break;
            if ((currentInt & 1) == 1)
            {
                output[arrayIndex] = i;
                arrayIndex++;
            }
            currentInt >>= 1;
        }
        return output;
    }
}

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
        foreach (int space in input.occupied.GetActive())
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
    public static readonly char[] PIECE_NAMES = new char[] { ' ', 'N', 'B', 'R', 'Q', 'K' };
    public static readonly char[] FILE_LETTERS = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
    public static readonly char[] RANK_NUMBERS = new char[] { '1', '2', '3', '4', '5', '6', '7', '8' };

    public static readonly BitBoard WHITE_SPACES = (BitBoard)0b10101010_01010101_10101010_01010101_10101010_01010101_10101010_01010101;

    //core
    public BitBoard[] piecePositionBoards; //white pawns, white knights, white bishops, white rooks, white queens, white kings, after that same for black
    public BitBoard occupied; // Every PiecePosBoard added together
    public BitBoard colorMask; // All black pieces

    //castling data 
    //short white, long white, short black, short white
    public static readonly int[] ROOKS_BEFORE_CASTLING = new int[] { 7, 0, 63, 56 };
    public static readonly int[] KINGS_BEFORE_CASTLING = new int[] { 4, 4, 60, 60 };
    public static readonly int[] ROOKS_AFTER_CASTLING = new int[] { 5, 3, 61, 59 };
    public static readonly int[] KINGS_AFTER_CASTLING = new int[] { 6, 2, 62, 58 };



    //init
    public ChessBoard()
    {
        piecePositionBoards = new BitBoard[12];
        occupied = new BitBoard();
        colorMask = new BitBoard();
        for (int i = 0; i < 12; i++)
        {
            piecePositionBoards[i] = new BitBoard();
        }
    }

    //operators and nice stuff
    public bool Equals(ChessBoard otherBoard)
    {
        for (int i = 0; i < 12; i++)
        {
            if (piecePositionBoards[i] != otherBoard.piecePositionBoards[i])
            {
                return false;
            }
        }
        return true;
    }

    public int this[int index]
    {
        get => GetPieceAtPos(index);
        set => SetPieceAtPos(index, value);
    }

    int GetPieceAtPos(int position)
    {
        if (!occupied[position])
        {
            return 0;
        }
        for (int i = 0; i < 6; i++)
        {
            if (piecePositionBoards[i][position])
            {
                return i + 1 + WHITE_PIECE;
            }
            else if (piecePositionBoards[i + 6][position])
            {
                return i + 1 + BLACK_PIECE;
            }
        }
        throw new IndexOutOfRangeException("Full spaces are not updated correctly or there is a beer bottle on the board.");
    }

    void SetPieceAtPos(int position, int piece)
    {
        int prevPiece = GetPieceAtPos(position);
        if (prevPiece != 0) piecePositionBoards[BitBoardIndex(prevPiece)][position] = false;
        if (piece != 0) piecePositionBoards[BitBoardIndex(piece)][position] = true;

        occupied[position] = (piece != 0);
        colorMask[position] = (piece >> 4) == 1;
    }

    //hashing, used for position history
    public ulong QuickHash()
    {
        ulong cutoffSum = 0, bitboardSum = 0;
        for (int i = 0; i < 12; i++)
        {
            ulong currentBoardInt = (ulong)piecePositionBoards[i];
            ulong currentCutoff = currentBoardInt & (ulong)0b11;
            bitboardSum += currentBoardInt >> 2;
            cutoffSum &= currentCutoff << (2 * i);
        }
        return bitboardSum + cutoffSum;
    }

    //some static getters
    public static int PieceColor(int piece)
    {
        if (piece == 0) return -1;
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
    public BitBoard WhiteOccupied() 
    {
        return occupied & ~colorMask;
    }

    public BitBoard BlackOccupied()
    {
        return occupied & colorMask;
    }

    public BitBoard ColorOccupied(int color)
    {
        return (color == WHITE) ? WhiteOccupied() : BlackOccupied();
    }

    public int PieceCount(int piece)
    {
        int index = BitBoardIndex(piece);
        return piecePositionBoards[index].CountActive();
    }

    public int WhitePieceCount()
    {
        BitBoard whitePieces = new BitBoard();
        for (int i = 0; i < 6; i++)
        {
            whitePieces += piecePositionBoards[i];
        }
        return whitePieces.CountActive();
    }

    public int BlackPieceCount()
    {
        BitBoard blackPieces = new BitBoard();
        for (int i = 6; i < 12; i++)
        {
            blackPieces += piecePositionBoards[i];
        }
        return blackPieces.CountActive();
    }

    //faster methods for board acessing
    public bool Contains(int space, int piece)
    {
        return piecePositionBoards[BitBoardIndex(piece)][space];
    }

    public int[] FindPieces(int piece) //returns list of spaces where the piece is
    {
        int index = BitBoardIndex(piece);
        return piecePositionBoards[index].GetActive();
    }

    public int WhiteKingPosition() {
        return piecePositionBoards[BitBoardIndex(WHITE_PIECE | KING)].TrailingZeroCount();
    }
    public int BlackKingPosition() {
        return piecePositionBoards[BitBoardIndex(BLACK_PIECE | KING)].TrailingZeroCount();
    }

    public int[] FindPiecesOfColor(int color)
    {
        return ColorOccupied(color).GetActive();
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
}
