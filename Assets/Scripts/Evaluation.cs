using System.Data;
using UnityEngine;
using static ChessBoard;

public class Evaluation
{
    MoveGenerator moveGenerator;


    static readonly int[] corners = new int[] { 0, 7, 56, 63 };
    static readonly int[] pieceValues = new int[] { 0, 100, 300, 320, 500, 900, 0 };
    const int checkBonus = 50;
    const int endgameKingDistanceBonusMultiplier = 3;
    const int endgameKingCornerBonusMultiplier = 15; // cornering the king in endgames is very important
    const int controlBonusMultiplier = 4;
    const int endgameThreshold = 2 * 500 + 2 * 300 + 2 * 100; //two rooks, two pieces, two pawns or similar
    const int doublePawnPenalty = 60;

    const int pawnsNearKingBonus = 30;

    public Evaluation(MoveGenerator movegen)
    {
        moveGenerator = movegen;
    }

    //static eval and prerequisites
    public int MaterialValue() //always white sided
    {
        int output = 0;
        foreach (int space in moveGenerator.Board.WhiteOccupied.GetActive())
        {
            output += pieceValues[PieceType(moveGenerator.Board[space])];
        }
        foreach (int space in moveGenerator.Board.BlackOccupied.GetActive())
        {
            output += pieceValues[PieceType(moveGenerator.Board[space])] * -1;
        }
        return output;
    }

    public int MaterialSum()
    {
        int output = 0;
        foreach (int space in moveGenerator.Board.Occupied.GetActive())
        {
            output += pieceValues[PieceType(moveGenerator.Board[space])];
        }
        return output;
    }

    bool IsEndgame()
    {
        return MaterialSum() <= endgameThreshold;
    }

    int BonusValue()
    {
        int output = 0;
        bool endgame = IsEndgame();
        foreach (int piece in POSSIBLE_PIECES)
        {
            foreach (int space in moveGenerator.Board.FindPieces(piece))
            {
                int colorSign = PieceColor(piece) == BLACK ? -1 : 1;
                int spaceValue = PieceBonusTable.Read(piece, space, endgame);
                output += spaceValue * colorSign;
            }
        }
        return output;
    }

    int EndgameKingCornerBonus(int color)
    {
        int smallestCornerDistance = 32;
        int otherKingPos = moveGenerator.Board.KingPosition(color ^ 1);
        foreach (int corner in corners)
        {
            int distance = Distance(otherKingPos, corner);
            if (distance < smallestCornerDistance) smallestCornerDistance = distance;
        }
        return ((8 - smallestCornerDistance) + (8 - ChessBoard.Distance(moveGenerator.Board.BlackKingPosition, moveGenerator.Board.WhiteKingPosition))) * endgameKingCornerBonusMultiplier;
    }

    int BoardControlBonus()
    {
        BitBoard whiteSpaces = moveGenerator.GenerateCoveredSpaceBitboard(WHITE);
        BitBoard blackSpaces = moveGenerator.GenerateCoveredSpaceBitboard(BLACK);
        return (whiteSpaces.CountActive() - blackSpaces.CountActive()) * controlBonusMultiplier;
    }

    int KingSafetyBonus(int color)
    {
        if (moveGenerator.Board.KingPosition(color) > 63 | moveGenerator.Board.KingPosition(color) < 0){
            Debug.Log(moveGenerator.Board.ToString());	
        } 
        BitBoard pawnsNearKing = moveGenerator.Board.GetPieceBitBoard(PAWN, color) & moveGenerator.GetSpacesCoveredByKing(moveGenerator.Board.KingPosition(color));
        pawnsNearKing &= ~(BitBoard.FILES[3] + BitBoard.FILES[4]); //exclude center pawns
        return pawnsNearKingBonus * pawnsNearKing.CountActive();
    }

    int DoublePawnPenalty()
    {
        return (moveGenerator.Board.CountDoublePawns(BLACK) * doublePawnPenalty) - (moveGenerator.Board.CountDoublePawns(WHITE) * doublePawnPenalty);
    }

    public int EvaluatePosition(int color) //static eval of given position from the players perspective
    {
        int eval = MaterialValue();
        bool endgame = IsEndgame();
        if (endgame && System.Math.Abs(eval) >= 300)
        {
            if (System.Math.Sign(eval) == 1)
            {
                eval += EndgameKingCornerBonus(WHITE);
            }
            else if (System.Math.Sign(eval) == -1)
            {
                eval -= EndgameKingCornerBonus(BLACK);
            }
        }
        if (moveGenerator.IsPlayerInCheck(color)) eval -= checkBonus;
        if (moveGenerator.IsPlayerInCheck(color ^ 1)) eval += checkBonus; //SLOW: maybe test if this is worth it
        eval += BonusValue();
        eval += BoardControlBonus();
        eval += DoublePawnPenalty();
        eval += KingSafetyBonus(color);
        //if (endgame) eval += EndgameKingDistanceBonus();
        return (color == WHITE) ? eval : -eval;
    }

    //move evaluation, used for moveordering
    //move eval assumes the move has not been made yet
    public int CaptureDelta(Move move)
    {
        int movedPiece = moveGenerator.Board[move.Start];
        int startValue = pieceValues[PieceType(movedPiece)];
        int capturedPiece = moveGenerator.Board[move.End];
        int endValue = pieceValues[PieceType(capturedPiece)];
        if (endValue == 0) return -400; // base penalty for non capture moves, we first look at captures where we lose less than 4 pawns, then at non captures, then at the rest
        return endValue - startValue; // high values for taking good pieces with bad ones, negative for the reverse
    }

    public int PositionDelta(Move move, bool endgame)
    {
        int movedPiece = moveGenerator.Board[move.Start];
        int before = PieceBonusTable.Read(movedPiece, move.Start, endgame);
        int after = PieceBonusTable.Read(movedPiece, move.End, endgame);
        return after - before; // high values for positioning pieces better, negative for worse positions
    }

    public int EvaluateMove(Move move)
    {
        int eval = 0;
        bool endgame = IsEndgame();
        eval += CaptureDelta(move);
        eval += PositionDelta(move, endgame);
        return eval;
    }
}
