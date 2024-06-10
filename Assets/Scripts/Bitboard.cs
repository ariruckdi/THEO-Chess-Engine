using System;
using System.Collections.Generic;

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

    private static int TrailingZeroCount(ulong bitBoardInt)
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

    public List<int> GetActive()
    {
        var result = new List<int>(64);
        ulong currentInt = boardInt;
        int nextActive;
        while (currentInt != 0) {
            nextActive = TrailingZeroCount(currentInt);
            result.Add(nextActive);
            currentInt ^= (ulong)1 << nextActive;
        }
        return result;
    }

    public List<Move> ToMoveList(int startSpace)
    {
        var result = new List<Move>(32);
        foreach (int active in this.GetActive())
        {
            result.Add(new Move(startSpace, active));
        }
        return result;
    }
}