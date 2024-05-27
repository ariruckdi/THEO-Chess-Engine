using static ChessBoard;

public class Evaluation
{
    MoveGenerator moveGenerator;


    static readonly int[] corners = new int[] { 0, 7, 56, 63 };
    static readonly int[] pieceValues = new int[] { 0, 100, 300, 320, 500, 900, 0 };
    const int checkBonus = 40;
    const int endgameKingDistanceBonusMultiplier = 3;
    const int endgameKingCornerBonusMultiplier = 15; // cornering the king in endgames is very important
    const int controlBonusMultiplier = 3;
    const int endgameThreshold = 2 * 500 + 2 * 300 + 2 * 100; //two rooks, two pieces, two pawns or similar

    public Evaluation(MoveGenerator movegen)
    {
        moveGenerator = movegen;
    }

    //static eval and prerequisites
    public int MaterialValue()
    {
        int output = 0;
        for (int i = 0; i < 64; i++)
        {
            int currentPiece = moveGenerator.board[i];
            int valueSign = (ChessBoard.PieceColor(currentPiece) == ChessBoard.white) ? 1 : -1;
            int currentValue = pieceValues[ChessBoard.PieceType(currentPiece)] * valueSign;
            output += currentValue;
        }
        return output;
    }

    public int MaterialSum()
    {
        int output = 0;
        for (int i = 0; i < 64; i++)
        {
            int currentPiece = moveGenerator.board[i];
            int currentValue = pieceValues[ChessBoard.PieceType(currentPiece)];
            output += currentValue;
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
        foreach (int piece in ChessBoard.possiblePieces)
        {
            foreach (int space in moveGenerator.board.FindPieces(piece))
            {
                if (space == -1) break;
                int colorSign = ChessBoard.PieceColor(piece) == ChessBoard.black ? -1 : 1;
                int spaceValue = PieceBonusTable.Read(piece, space, endgame);
                output += spaceValue * colorSign;
            }
        }
        return output;
    }

    int EndgameKingCornerBonus(int player)
    {
        int smallestCornerDistance = 32;
        int otherKingPos = (player == white) ? moveGenerator.blackKingPosition : moveGenerator.whiteKingPosition;
        foreach (int corner in corners)
        {
            int distance = Distance(otherKingPos, corner);
            if (distance < smallestCornerDistance) smallestCornerDistance = distance;
        }
        return ((8 - smallestCornerDistance) + (8 - ChessBoard.Distance(moveGenerator.blackKingPosition, moveGenerator.whiteKingPosition))) * endgameKingCornerBonusMultiplier;
    }

    int BoardControlBonus()
    {
        BitBoard whiteSpaces = moveGenerator.isSpaceAttackedByWhite;
        BitBoard blackSpaces = moveGenerator.isSpaceAttackedByBlack;
        return (whiteSpaces.CountActive() - blackSpaces.CountActive()) * controlBonusMultiplier;
    }

    public int EvaluatePosition(int player) //static eval of given position from the players perspective
    {
        int eval = MaterialValue();
        bool endgame = IsEndgame();
        if (endgame && System.Math.Abs(eval) >= 300)
        {
            if (System.Math.Sign(eval) == 1)
            {
                eval += EndgameKingCornerBonus(white);
            }
            else if (System.Math.Sign(eval) == -1)
            {
                eval -= EndgameKingCornerBonus(black);
            }
        }
        if (moveGenerator.IsPlayerInCheck(player)) eval -= checkBonus;
        if (moveGenerator.IsPlayerInCheck(player ^ 1)) eval += checkBonus; //SLOW: maybe test if this is worth it
        eval += BonusValue();
        eval += BoardControlBonus();
        //if (endgame) eval += EndgameKingDistanceBonus();
        return (player == ChessBoard.white) ? eval : -eval;
    }

    //move evaluation, used for moveordering
    //move eval assumes the move has not been made yet
    public int CaptureDelta(EngineMove move)
    {
        int startValue = pieceValues[PieceType(move.Piece)];
        int capturedPiece = moveGenerator.board[move.End];
        int endValue = pieceValues[PieceType(capturedPiece)];
        if (endValue == 0) return -400; // base penalty for non capture moves, we first look at captures where we lose less than 4 pawns, then at non captures, then at the rest
        return endValue - startValue; // high values for taking good pieces with bad ones, negative for the reverse
    }

    public int PositionDelta(EngineMove move, bool endgame)
    {
        int before = PieceBonusTable.Read(move.Piece, move.Start, endgame);
        int after = PieceBonusTable.Read(move.Piece, move.End, endgame);
        return after - before; // high values for positioning pieces better, negative for worse positions
    }

    public int EvaluateMove(EngineMove move)
    {
        int eval = 0;
        bool endgame = IsEndgame();
        eval += CaptureDelta(move);
        eval += PositionDelta(move, endgame);
        return eval;
    }
}
