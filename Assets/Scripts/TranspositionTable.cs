using System.Collections;
using System.Collections.Generic;
using UnityEngine;


struct Entry
{
    public readonly ulong key;
    public readonly int value;
    public readonly Move move;
    public readonly byte depth;
    public readonly byte nodeType;

    public Entry(ulong key, int value, Move move, byte depth, byte nodeType)
    {
        this.key = key;
        this.value = value;
        this.depth = depth;
        this.move = move;
        this.nodeType = nodeType;
    }
}

public class TranspositionTable
{
    public const int exact = 0, upperBound = 2, lowerBound = 1, lookupFailed = int.MinValue;
    ulong size;
    Entry[] table;
    MoveGenerator moveGenerator;

    public TranspositionTable(ulong size, MoveGenerator moveGenerator)
    {
        table = new Entry[size];
        this.size = size;
        this.moveGenerator = moveGenerator;
    }

    ulong Index(ulong zobristHash)
    {
        return zobristHash % size;
    }

    public void StoreEval(int depth, int eval, int evalType, Move bestMove)
    {
        ulong currentHash = moveGenerator.Board.ZobristHash();
        Entry newEntry = new Entry(currentHash, eval, bestMove, (byte)depth, (byte)evalType);
        table[Index(currentHash)] = newEntry;
    }

    public int LookupEval(int depth, int alpha, int beta)
    {
        ulong currentHash = moveGenerator.Board.ZobristHash();
        Entry entry = table[Index(currentHash)];

        if (entry.key == currentHash)
        {
            if (entry.depth >= depth)
            {
                if (entry.nodeType == exact)
                {
                    return entry.value;
                }
                if (entry.nodeType == upperBound && entry.value <= alpha)
                {
                    return entry.value;
                }
                if (entry.nodeType == lowerBound && entry.value >= beta)
                {
                    return entry.value;
                }
            }
        }

        return lookupFailed;
    }

    public void Clear()
    {
        for (uint i = 0; i < size; i++)
        {
            table[i] = new Entry();
        }
    }

    public Move LookUpMove()
    {
        ulong currentHash = moveGenerator.Board.ZobristHash();
        Entry entry = table[Index(currentHash)];
        return entry.move;
    }

}
