using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;

using static ChessBoard;

public struct EngineMove
{
    public int Piece;
    public int Start;
    public int End;
    public int Eval;
    public EngineMove(int piece, int start, int end, int eval = 0)
    {
        Piece = piece;
        Start = start;
        End = end;
        Eval = eval;
    }

    public static int CompareByEval(EngineMove move1, EngineMove move2)
    {
        return -move1.Eval.CompareTo(move2.Eval); // that minus makes sure better moves come first in the search
    }
}

public struct SearchData
{
    public int currentSearchCount;
    public int currentBestEval;
    public int currentMoveScore;
    public string currentBestMoveName;

    public int currentDepth;
    public bool valuesChanged;
    public bool searchStarted;

    public SearchData(int currentSearchCount, int currentBestEval, string currentBestMove, int currentMoveScore, int currentDepth)
    {
        this.currentSearchCount = currentSearchCount;
        this.currentBestEval = currentBestEval;
        this.currentBestMoveName = currentBestMove;
        this.currentMoveScore = currentMoveScore;
        this.currentDepth = currentDepth;
        valuesChanged = false;
        searchStarted = false;
    }
}

public class Engine
{
    //consts
    const int positiveInfinity = 9999999;
    const int negativeInfinity = -positiveInfinity;
    const int mateScore = 100000;

    //linking
    GameMngr manager;
    MoveGenerator moveGenerator;
    ConsoleBehaviour console;
    Evaluation evaluator;
    public Evaluation Evaluation { get => evaluator; }

    //search data
    int possibleMoveCount;
    public int originalDepth;
    public int searchCount;
    EngineMove currentBestMove;
    public bool abortSearch;

    //output data
    public bool moveReady = false;
    public bool evalReady = false;
    public EngineMove nextFoundMove;
    public SearchData currentSearch;

    //transpotable
    public TranspositionTable transpositionTable;

    //init
    public Engine(MoveGenerator moveGenToUse)
    {
        moveGenerator = moveGenToUse;
        manager = GameObject.FindGameObjectWithTag("Manager").GetComponent<GameMngr>();
        console = manager.console;
        currentSearch = new SearchData(0, 0, "", 0, 0);
        evaluator = new Evaluation(moveGenerator);
        transpositionTable = new TranspositionTable(64000, moveGenerator);
    }

    //moveset generation
    public List<EngineMove> GetMoveset(int player) //ATTN: no repetition avoidance
    {
        var output = new List<EngineMove>();
        for (int space = 0; space < 64; space++)
        {
            int currentPiece = moveGenerator.board[space];
            if (ChessBoard.PieceColor(currentPiece) == player)
            {
                foreach (int endSpace in moveGenerator.GetLegalMovesForPiece(space).GetActive())
                {
                    if (endSpace == -1) break;
                    output.Add(new EngineMove(currentPiece, space, endSpace));
                }
            }
        }
        return output;
    }

    public List<EngineMove> GetOrderedMoveset(int player) // gets an ordered (by move eval) list of possible list
    {
        var moves = new List<EngineMove>();
        for (int space = 0; space < 64; space++)
        {
            int currentPiece = moveGenerator.board[space];
            if (ChessBoard.PieceColor(currentPiece) == player)
            {
                BitBoard pieceMoveset = moveGenerator.GetLegalMovesForPiece(space);
                if ((ulong)pieceMoveset == 0ul) continue; //skip pieces without moves
                foreach (int endSpace in pieceMoveset.GetActive())
                {
                    if (endSpace == -1) break;
                    EngineMove newMove = new EngineMove(currentPiece, space, endSpace);
                    newMove.Eval = evaluator.EvaluateMove(newMove);
                    moves.Add(newMove);
                }
            }
        }
        moves.Sort(EngineMove.CompareByEval);
        return moves;
    }

    public List<EngineMove> GetOrderedCaptures(int player)
    {
        var output = new List<EngineMove>();
        for (int space = 0; space < 64; space++)
        {
            int currentPiece = moveGenerator.board[space];
            if (ChessBoard.PieceColor(currentPiece) == player)
            {
                BitBoard pieceCaptures = moveGenerator.GetLegalCapturesForPiece(space);
                if ((ulong)pieceCaptures == 0ul) continue; // skips pieces without availble captures
                foreach (int endSpace in pieceCaptures.GetActive())
                {
                    if (endSpace == -1) break;
                    EngineMove nextMove = new EngineMove(currentPiece, space, endSpace);
                    nextMove.Eval = evaluator.EvaluateMove(nextMove);
                    output.Add(new EngineMove(currentPiece, space, endSpace));
                }
            }
        }
        output.Sort(EngineMove.CompareByEval);
        return output;
    }

    //move searching
    public int Search(int player, int depth, int alpha, int beta, int captureDepth = -1)
    {
        if (abortSearch) return 0;
        bool newBestMove = false;
        searchCount++;
        int plyFromRoot = originalDepth - depth;

        if (plyFromRoot > 1)
        {
            alpha = System.Math.Max(alpha, -mateScore + plyFromRoot); //stop if we have got a mate
            beta = System.Math.Min(beta, mateScore - plyFromRoot);
            if (alpha >= beta)
            {
                return alpha;
            }
        }

        if (depth == 0) return CaptureSearch(player, captureDepth, alpha, beta);

        int storedEval = transpositionTable.LookupEval(depth, alpha, beta);

        if (storedEval != TranspositionTable.lookupFailed && depth != originalDepth)
        {
            return storedEval;
        }

        List<EngineMove> moves = GetOrderedMoveset(player);
        if (moves.Count == 0)
        {
            if (moveGenerator.IsPlayerInCheck(player)) return -mateScore + plyFromRoot; //Checkmate in (originalDepth - depth) ply, favors earlier checkmate 
            return 0; // draw
        }

        int evalType = TranspositionTable.upperBound;
        EngineMove bestMoveInThisPos = new EngineMove(0, 0, 0);

        foreach (EngineMove move in moves)
        {
            UndoMoveData madeMove = moveGenerator.MovePiece(move.Start, move.End);
            int eval = -Search(player ^ 1, depth - 1, -beta, -alpha, captureDepth);
            if (manager.positionHistory.Count(x => x == moveGenerator.ZobristHash()) >= 2) return 0; //draw after 3 fold repetion

            moveGenerator.UndoMovePiece(madeMove);

            if (eval >= beta)//prune the branch if move would be too good
            {
                transpositionTable.StoreEval(depth, beta, TranspositionTable.lowerBound, move);
                return eval;
            }

            if (eval > alpha)
            { //found new best move
                alpha = eval;
                evalType = TranspositionTable.exact;
                bestMoveInThisPos = move;
                newBestMove = true;
            }

            if (depth == originalDepth && newBestMove)//dont forget to set this in wrapper
            {
                currentBestMove = move;

                currentSearch.currentBestMoveName = moveGenerator.MoveName(move.Start, move.End); // SLOW: i'm literally building a string in the most performance critical place... usable for displaying in console
                currentSearch.currentSearchCount = searchCount;
                currentSearch.currentBestEval = alpha;
                currentSearch.currentDepth = originalDepth;
                currentSearch.valuesChanged = true;
            }

            newBestMove = false;
        }
        if (IsMate(alpha))
        { //excludes mate sequences from transpo table
            transpositionTable.StoreEval(depth, alpha, evalType, bestMoveInThisPos);
        }
        return alpha;
    }

    public int CaptureSearch(int player, int captureDepth, int alpha, int beta)
    {
        searchCount++;
        int eval = evaluator.EvaluatePosition(player);
        if (captureDepth == 0) return eval; //when captureDepth is negative this never happens
        if (eval >= beta)
        {
            return beta;
        }

        if (eval > alpha)
        { //in case the pos is good but all captures are bad
            alpha = eval;
        }

        List<EngineMove> moves = GetOrderedCaptures(player);

        for (int i = 0; i < moves.Count; i++)
        {
            UndoMoveData madeMove = moveGenerator.MovePiece(moves[i].Start, moves[i].End);
            eval = -CaptureSearch(player ^ 1, captureDepth - 1, -beta, -alpha);
            moveGenerator.UndoMovePiece(madeMove);
            if (eval > alpha)
            { //found new best move
                alpha = eval;
            }
            if (alpha >= beta) //prune the branch, move too good
            {
                return beta;
            }

        }
        return alpha;
    }

    bool IsMate(int eval)
    {
        return (System.Math.Abs(eval) > (mateScore - 100));
    }

    // I cant get negascout to not blunder tons of pieces, so it has to go... RIP nice effient algorithm

    //search wrappers
    public EngineMove ChooseMove(int player, int depth, int captureDepth = -1)
    {
        originalDepth = depth; //important
        searchCount = 0;
        Search(player, depth, negativeInfinity, positiveInfinity, captureDepth);
        currentSearch.currentSearchCount = searchCount;
        currentSearch.valuesChanged = true;
        return currentBestMove;
    }

    public EngineMove IterDepthChooseMove(int player, int maxDepth, bool depthLimit, bool timeLimit = false, float maxTime = 100)
    {
        int lastEval = 0, lastDepth = 0;
        manager.searching = true;
        abortSearch = false;
        if (!depthLimit) maxDepth = 100;
        EngineMove bestMoveThisIter = new EngineMove(0, 0, 0);
        searchCount = 0;
        for (int searchDepth = 1; searchDepth <= maxDepth; searchDepth++)
        {
            //deltaTime = manager.currentTime - startTime;
            //if (deltaTime > maxTime) abortSearch = true;
            originalDepth = searchDepth;

            int currentEval = Search(player, searchDepth, negativeInfinity, positiveInfinity, 2 + 4 * (searchDepth - 1));

            if (abortSearch)
            {
                currentSearch.currentBestMoveName = moveGenerator.MoveName(bestMoveThisIter.Start, bestMoveThisIter.End);
                currentSearch.currentSearchCount = searchCount;
                currentSearch.currentBestEval = lastEval;
                currentSearch.currentDepth = lastDepth;
                currentSearch.valuesChanged = true;
                return bestMoveThisIter;
            }

            lastEval = currentEval; // we didnt abort so values are fine
            lastDepth = searchDepth;
            bestMoveThisIter = currentBestMove;
            if (IsMate(lastEval))
            {
                break;
            }
        }

        currentSearch.currentBestMoveName = moveGenerator.MoveName(bestMoveThisIter.Start, bestMoveThisIter.End);
        currentSearch.currentSearchCount = searchCount;
        currentSearch.currentBestEval = lastEval;
        currentSearch.currentDepth = lastDepth;
        currentSearch.valuesChanged = true;

        return bestMoveThisIter;
    }

    public int SearchEval(int player, int depth, int captureDepth = -1)
    {
        originalDepth = depth; //important
        searchCount = 0;
        int eval = Search(player, depth, negativeInfinity, positiveInfinity, captureDepth);
        return eval;
    }

    public void ChooseMove()
    {
        nextFoundMove = IterDepthChooseMove(manager.playerOnTurn, manager.engineDepth, true, true);
        moveReady = true;
    }

    public void SearchEval()
    {
        currentSearch.currentBestEval = SearchEval(manager.playerOnTurn, 4, -1); //hardcoded for now since engine depth can get very high now
        evalReady = true;
    }

    //running searches inside other threads
    public void ThreadedMove()
    {
        Thread thread = new Thread(ChooseMove) { IsBackground = true };
        currentSearch.searchStarted = true;
        thread.Start();
    }

    public void ThreadedSearch()
    {
        Thread thread = new Thread(SearchEval) { IsBackground = true };
        thread.Start();
    }

    //perft testing
    public int MoveGenCountTest(int depth, int playerToStart, ConsoleBehaviour console = null)
    {
        if (depth == 0) return 1;
        List<EngineMove> moves = GetMoveset(playerToStart);
        int output = 0, lastOutput = 0;
        foreach (EngineMove move in moves)
        {
            UndoMoveData thisMove = moveGenerator.MovePiece(move.Start, move.End);
            lastOutput = output;
            output += MoveGenCountTest(depth - 1, playerToStart ^ 1);
            moveGenerator.UndoMovePiece(thisMove);
            if (depth == originalDepth && console != null) console.Line(moveGenerator.MoveName(move.Start, move.End, true).PadRight(7) + (output - lastOutput).ToString("N0"));
        }

        return output;
    }

}
