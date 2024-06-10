using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;

using static ChessBoard;

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
    public const int POS_INFTY = 9999999;
    public const int NEG_INFTY = -POS_INFTY;
    public const int mateScore = 100000;

    //linking
    private readonly GameMngr manager;
    private readonly MoveGenerator moveGenerator;
    private readonly ConsoleBehaviour console;
    private readonly Evaluation eval;
    public Evaluation Evaluation { get => eval; }

    //search data
    public int originalDepth;
    public int searchCount;
    Move currentBestMove;
    public bool abortSearch;

    //output data
    public bool moveReady = false;
    public bool evalReady = false;
    public Move nextFoundMove;
    public SearchData currentSearch;

    //transpotable
    public TranspositionTable transpositionTable;
    int transpoHits;

    //init
    public Engine(MoveGenerator moveGenerator)
    {
        this.moveGenerator = moveGenerator;
        manager = GameObject.FindGameObjectWithTag("Manager").GetComponent<GameMngr>();
        console = manager.console;
        currentSearch = new SearchData(0, 0, "", 0, 0);
        eval = new Evaluation(this.moveGenerator);
        transpositionTable = new TranspositionTable(512000, this.moveGenerator);
    }

    //moveset generation
    public List<Move> GetMoveset(int player) //ATTN: no repetition avoidance
    {
        var moves = new List<Move>();
        foreach (int space in moveGenerator.Board.ColorOccupied(player).GetActive())
        {
            foreach (int targetSpace in moveGenerator.GetLegalMovesForPiece(player, space).GetActive())
            {
                moves.Add(new Move(space, targetSpace));
            }
        }
        return moves;
    }

    public List<Move> GetOrderedMoveset(int player, bool capturesOnly = false) // gets an ordered (by move eval) list of possible list
    {
        var moves = new List<Move>();
        foreach (int space in moveGenerator.Board.ColorOccupied(player).GetActive())
        {
            foreach (int targetSpace in moveGenerator.GetLegalMovesForPiece(player, space, capturesOnly).GetActive())
            {
                Move move = new Move(space, targetSpace);
                move.SetEval(eval);
                moves.Add(move);
            }
        }
        moves.Sort(Move.CompareByEval);
        return moves;
    }

    //move searching
    public int Search(int player, int depth, int alpha, int beta)
    {
        if (abortSearch) return CaptureSearch(player, alpha, beta);
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

        if (depth == 0) return CaptureSearch(player, alpha, beta);

        int storedEval = transpositionTable.LookupEval(depth, alpha, beta);

        if (storedEval != TranspositionTable.lookupFailed && depth != originalDepth)
        {
            return storedEval;
        }

        List<Move> moves = GetOrderedMoveset(player);
        if (moves.Count == 0)
        {
            if (moveGenerator.IsPlayerInCheck(player)) return -mateScore + plyFromRoot; //Checkmate in (originalDepth - depth) ply, favors earlier checkmate 
            return 0; // draw
        }

        int evalType = TranspositionTable.upperBound;
        Move bestMoveInThisPos = new Move();

        foreach (Move move in moves)
        {
            UndoMoveData madeMove = moveGenerator.Board.MovePiece(move.Start, move.End);
            int eval = -Search(player ^ 1, depth - 1, -beta, -alpha);
            if (manager.positionHistory.Count(x => x == moveGenerator.Board.ZobristHash()) >= 2) return 0; //draw after 3 fold repetion

            moveGenerator.Board.UndoMovePiece(madeMove);

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

                currentSearch.currentBestMoveName = moveGenerator.SimpleMoveName(move.Start, move.End);
                currentSearch.currentSearchCount = searchCount;
                currentSearch.currentBestEval = alpha;
                currentSearch.currentDepth = originalDepth;
                currentSearch.valuesChanged = true;
            }

            newBestMove = false;
        }
        if (!IsMate(alpha))
        { //excludes mate sequences from transpo table
            transpositionTable.StoreEval(depth, alpha, evalType, bestMoveInThisPos);
        }
        return alpha;
    }

    public int CaptureSearch(int player, int alpha, int beta)
    {
        searchCount++;
        int eval = this.eval.EvaluatePosition(player);

        if (eval >= beta)
        {
            return beta;
        }

        if (eval > alpha)
        { //in case the pos is good but all captures are bad
            alpha = eval;
        }
        List<Move> moves = GetOrderedMoveset(player, true);

        for (int i = 0; i < moves.Count; i++)
        {
            UndoMoveData madeMove = moveGenerator.Board.MovePiece(moves[i].Start, moves[i].End);
            eval = -CaptureSearch(player ^ 1, -beta, -alpha);
            moveGenerator.Board.UndoMovePiece(madeMove);
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
    public Move ChooseMove(int player, int depth)
    {
        originalDepth = depth; //important
        searchCount = 0;
        Search(player, depth, NEG_INFTY, POS_INFTY);
        currentSearch.currentSearchCount = searchCount;
        currentSearch.valuesChanged = true;
        return currentBestMove;
    }

    public Move IterDepthChooseMove(int player, int maxDepth, bool depthLimit, bool timeLimit = false, float maxTime = 100)
    {
        int lastEval = 0, lastDepth = 0;
        manager.searching = true;
        abortSearch = false;
        if (!depthLimit) maxDepth = 100;
        Move bestMoveThisIter = new Move();
        searchCount = 0;
        for (int searchDepth = 1; searchDepth <= maxDepth; searchDepth++)
        {
            //deltaTime = manager.currentTime - startTime;
            //if (deltaTime > maxTime) abortSearch = true;
            originalDepth = searchDepth;

            int currentEval = Search(player, searchDepth, NEG_INFTY, POS_INFTY);

            if (abortSearch)
            {
                currentSearch.currentBestMoveName = moveGenerator.SimpleMoveName(bestMoveThisIter.Start, bestMoveThisIter.End);
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

        currentSearch.currentBestMoveName = moveGenerator.SimpleMoveName(bestMoveThisIter.Start, bestMoveThisIter.End);
        currentSearch.currentSearchCount = searchCount;
        currentSearch.currentBestEval = lastEval;
        currentSearch.currentDepth = lastDepth;
        currentSearch.valuesChanged = true;

        return bestMoveThisIter;
    }

    public int SearchEval(int player, int depth)
    {
        originalDepth = depth; //important
        searchCount = 0;
        int eval = Search(player, depth, NEG_INFTY, POS_INFTY);
        return eval;
    }

    public void ChooseMove()
    {
        nextFoundMove = IterDepthChooseMove(manager.playerOnTurn, manager.engineDepth, true, true);
        moveReady = true;
    }

    public void SearchEval()
    {
        currentSearch.currentBestEval = SearchEval(manager.playerOnTurn, 8); //hardcoded for now since engine depth can get very high now
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
        List<Move> moves = GetMoveset(playerToStart);
        int output = 0, lastOutput = 0;
        foreach (Move move in moves)
        {
            UndoMoveData thisMove = moveGenerator.Board.MovePiece(move.Start, move.End);
            lastOutput = output;
            output += MoveGenCountTest(depth - 1, playerToStart ^ 1);
            moveGenerator.Board.UndoMovePiece(thisMove);
            if (depth == originalDepth && console != null) console.Line(moveGenerator.MoveName(move.Start, move.End, true).PadRight(7) + (output - lastOutput).ToString("N0"));
        }

        return output;
    }

}
