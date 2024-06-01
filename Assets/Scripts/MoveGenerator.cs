using UnityEditor.Tilemaps;
using UnityEngine.UIElements;
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
    //core
    public ChessBoard board;

    //constants
    public const int SHORT_CASTLING_WHITE = 0, LONG_CASTLING_WHITE = 1, SHORT_CASTLING_BLACK = 2, LONG_CASTLING_BLACK = 3;
    static readonly int[] SLIDE_DIRECTIONS = new int[] { 8, -8, 1, -1, 9, 7, -7, -9 };
    //up, down, right, left, ur, ul, dr, dl
    static readonly int[][] KNIGHT_DIRECTIONS = new int[][] { new int[] { 15, 17 }, new int[] { -15, -17 }, new int[] { -6, 10 }, new int[] { 6, -10 } };
    //up, down, right, left
    static int[][] SpacesToEdge;
    static BitBoard[] PrecomputedKnightSpaces, PrecomputedWhitePawnAttacks, PrecomputedBlackPawnAttacks;

    static readonly int[] CASTLING_SPACES_WHITE = new int[] { 5, 6, 2, 3, 1};
    static readonly int[] CASTLING_SPACES_BLACK = new int[] {5 + 8 * 7, 6 + 8 * 7, 2 + 8 * 7, 3 + 8 * 7, 1 + 8 * 7};

    static readonly int[][] CASTLING_SPACES = {CASTLING_SPACES_WHITE, CASTLING_SPACES_BLACK};

    //variables
    public ChessGameData gameData;

    //hashing
    ZobristHashing hashing;

    public MoveGenerator()
    {
        board = new ChessBoard();
        gameData = new ChessGameData(WHITE, 0, new bool[] { true, true, true, true });
        hashing = new ZobristHashing((ulong)WHITE_SPACES);
        SpacesToEdge = GenerateSpacesToEdgeData();
        PrecomputedKnightSpaces = PrecomputeKnightSpaces();
        PrecomputedWhitePawnAttacks = PrecomputeWhitePawnAttacks();	
        PrecomputedBlackPawnAttacks = PrecomputeBlackPawnAttacks();
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
    }

    //pseudo legal movegen
    private BitBoard GetSlideSpaces(int space, int color, int pieceType, int range = 8, bool capturesOnly = false)
    {
        int pieceOnNewSpaceColor, newSpace;
        int dirStart = (pieceType == BISHOP) ? 4 : 0;
        int dirEnd = (pieceType == ROOK) ? 4 : 8;
        BitBoard output = new BitBoard(0);
        for (int dirIndex = dirStart; dirIndex < dirEnd; dirIndex++)
        {
            for (int i = 0; i < SpacesToEdge[space][dirIndex]; i++)
            {
                newSpace = space + SLIDE_DIRECTIONS[dirIndex] * (i + 1);
                pieceOnNewSpaceColor = PieceColor(board[newSpace]);
                if (pieceOnNewSpaceColor == color || (i + 1) > range) break;
                if (pieceOnNewSpaceColor == (color ^ 1)) {output[newSpace] = true; break;}
                output[newSpace] = !capturesOnly;
            }
        }
        return output;
    }

    private BitBoard GetPawnPushes(int space, int color)
    {
        BitBoard pawn = BitBoard.FromIndex(space);
        var result = new BitBoard();
        if (color == WHITE) {
            result += BitBoard.ShiftNorth(pawn) & ~board.occupied;
            if (SpaceY(space) == 1 && !result.IsEmpty()) result += BitBoard.ShiftNorthDouble(pawn) & ~board.occupied;;
        } else {
            result += BitBoard.ShiftSouth(pawn) & ~board.occupied;
            if (SpaceY(space) == 6 && !result.IsEmpty()) result += BitBoard.ShiftSouthDouble(pawn) & ~board.occupied;;
        }
        return result;
    }

    private BitBoard GetPawnCaptures(int space, int color) 
    {
        if (color == WHITE) return PrecomputedWhitePawnAttacks[space] & (board.BlackOccupied() + BitBoard.FromIndex(gameData.epSpace));
        return PrecomputedBlackPawnAttacks[space] & (board.WhiteOccupied() + BitBoard.FromIndex(gameData.epSpace));
    }

    private BitBoard GetPawnSpaces(int space, int color, bool capturesOnly = false)
    {
        var output = new BitBoard();
        if (!capturesOnly) output += GetPawnPushes(space, color);
        output += GetPawnCaptures(space, color);
        return output;
    }

    private BitBoard GetSpacesAttackedByPawn(int space, int color) //useful for finding attacked spaces
    {
        if (color == WHITE) return PrecomputedWhitePawnAttacks[space] & ~board.WhiteOccupied();
        return PrecomputedBlackPawnAttacks[space] & ~board.BlackOccupied();
    }

    private BitBoard GetKnightSpaces(int space, int color, bool capturesOnly = false)
    {
        if (capturesOnly) return PrecomputedKnightSpaces[space] & board.ColorOccupied(color ^ 1);
        return PrecomputedKnightSpaces[space] & ~board.ColorOccupied(color);
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

    private BitBoard GetKingSpaces(int space, int color, bool capturesOnly, bool includeCastling)
    {
        var output = GetSlideSpaces(space, color, KING, 1, capturesOnly: capturesOnly);
        if (includeCastling == false || capturesOnly) return output;
        int castlingRow = 7 * color;
        bool shortCastlingValid, longCastlingValid;
        if (space == castlingRow * 8 + 4 && !IsPlayerInCheck(color))
        {
            BitBoard attackedSpaces = GenerateAttackedSpaceBitboard(color ^ 1);
            if (gameData.castling[2 * color]) // short
            {
                shortCastlingValid = true;
                for (int i = 0; i < 2; i++)
                {
                    if (board.occupied[CASTLING_SPACES[color][i]]) {shortCastlingValid = false; break;} //cant castle is pieces are in the way
                    if (attackedSpaces[CASTLING_SPACES[color][i]]) {shortCastlingValid = false; break;} //cant castle through check
                }
                if (shortCastlingValid) output[space + 2] = true;
            }
            if (gameData.castling[2 * color + 1]) // long
            {
                longCastlingValid = true;
                for (int i = 2; i < 5; i++)
                {
                    if (board.occupied[CASTLING_SPACES[color][i]]) {longCastlingValid = false; break;}
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
    public BitBoard GenerateAttackedSpaceBitboard(int player)
    {
        int piece;
        var output = new BitBoard();
        foreach (int space in board.ColorOccupied(player).GetActive())
        {
            if (space == -1) break;
            piece = board[space];
            if (PieceType(piece) == PAWN)
            {
                output += GetSpacesAttackedByPawn(space, player);
            }
            else
            {
                output += GetPossibleSpacesForPiece(space, piece, includeCastling: false);
            }
        }
        return output;
    }


    int KingPosition(int player)
    {
        return (player == WHITE) ? board.WhiteKingPosition() : board.BlackKingPosition();
    }

    public bool IsPlayerInCheck(int player)
    {
        var bitboardToCheck = (player == WHITE) ? GenerateAttackedSpaceBitboard(BLACK) : GenerateAttackedSpaceBitboard(WHITE);
        return bitboardToCheck[KingPosition(player)];
    }

    public BitBoard GetLegalMovesForPiece(int space)
    {
        //variables
        int piece = board[space];
        int player = PieceColor(piece);
        var output = GetPossibleSpacesForPiece(space, piece);

        //checking legality
        foreach (int newSpace in output.GetActive())
        {
            if (newSpace == -1) break;
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

    public BitBoard GetLegalCapturesForPiece(int space)
    {
        int piece = board[space];
        BitBoard output = GetPossibleSpacesForPiece(space, piece, true);
        int player = PieceColor(piece);
        //no need for castling, thats not a capture :D
        foreach (int newSpace in output.GetActive())
        {
            if (newSpace == -1) break;
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
        bool isCapture = capture != 0;
        int shortCastlingIndex = color * 2; // 0 for white, 2 for black
        int longCastlingIndex = color * 2 + 1; // 1 for white, 3 for black

        //may change due to branch
        gameData.epSpace = 0;

        //castling + castling prevention when king moved
        if (type == KING)
        {
            int deltaX = end - start;
            if (!isCapture && (deltaX == 2 || deltaX == -2)) //keeps castling code from running all the time, castling is never a capture
            {
                int castlingRook = PieceInt(ROOK, color);
                if (deltaX == 2) board.MovePieceToEmptySpace(ROOKS_BEFORE_CASTLING[shortCastlingIndex], ROOKS_AFTER_CASTLING[shortCastlingIndex], castlingRook); //short
                else if (deltaX == -2) board.MovePieceToEmptySpace(ROOKS_BEFORE_CASTLING[longCastlingIndex], ROOKS_AFTER_CASTLING[longCastlingIndex], castlingRook); //long
            }
            // no castling after moving kings
            gameData.castling[shortCastlingIndex] = false;
            gameData.castling[longCastlingIndex] = false;
        }

        //castling prevention after rook moved
        else if (type == ROOK)
        {
            if (SpaceX(start) == 7) gameData.castling[shortCastlingIndex] = false;
            else if (SpaceX(start) == 0) gameData.castling[longCastlingIndex] = false;
        }

        //prventing castling when rook needed for it was taken
        if (PieceType(capture) == ROOK && (end == 7 || end == 0 || end == 63 || end == 56))
        {
            if (PieceColor(capture) == WHITE)
            {
                gameData.castling[shortCastlingIndex] = (!(end == 7)) && gameData.castling[shortCastlingIndex];
                gameData.castling[longCastlingIndex] = (!(end == 0)) && gameData.castling[longCastlingIndex];
            }
            else if (PieceColor(capture) == BLACK) // ugly repetition
            {
                gameData.castling[shortCastlingIndex] = (!(end == 63)) && gameData.castling[shortCastlingIndex];
                gameData.castling[longCastlingIndex] = (!(end == 56)) && gameData.castling[longCastlingIndex];
            }
        }

        //en passant execution and space setting
        else if (type == PAWN)
        {
            if (end == gameData.epSpace && gameData.epSpace != 0) board.TakeEPPawn(end, color);
            else if (!isCapture && System.Math.Abs(SpaceY(start) - SpaceY(end)) == 2) gameData.epSpace = (color == BLACK) ? end + 8 : end - 8;
        }

        // making the actual move
        if (!isCapture) board.MovePieceToEmptySpace(start, end, piece);
        else board.MovePieceToFullSpace(start, end, piece, capture);

        // turning pawns to queens on promotion
        if ((SpaceY(end) == 7 || SpaceY(end) == 0) && type == PAWN) // pawn of own color never will go backwards
        {
            board.TurnPawnToQueen(end, color);
            undoData.wasPromotion = true;
        }

        gameData.playerOnTurn ^= 1;

        return undoData;
    }

    public void UndoMovePiece(UndoMoveData undoData)
    {
        board.MovePieceToEmptySpace(undoData.end, undoData.start, undoData.movedPiece);
        gameData.castling = undoData.castlingBefore;
        gameData.epSpace = undoData.epSpaceBefore;
        int movedPieceColor = PieceColor(undoData.movedPiece);
        if (undoData.takenPiece != 0)
        {
            board.CreatePiece(undoData.end, undoData.takenPiece);
        }
        if (undoData.wasPromotion)
        {
            board.TurnQueenToPawn(undoData.start, movedPieceColor);
        }
        if (undoData.end == undoData.epSpaceBefore && undoData.end != 0)
        { // move was en passant
            int pawnColor = (movedPieceColor == WHITE) ? BLACK_PIECE : WHITE_PIECE;
            int epOffset = (pawnColor == BLACK_PIECE) ? -8 : 8;
            board.CreatePiece(undoData.end + epOffset, PAWN | pawnColor);
            return; // ep cant be castling
        }
        if (undoData.castlingIndex != -1)
        { // move was castling
            int rookColor = (movedPieceColor == WHITE) ? WHITE_PIECE : BLACK_PIECE;
            board.MovePieceToEmptySpace(ROOKS_AFTER_CASTLING[undoData.castlingIndex], ROOKS_BEFORE_CASTLING[undoData.castlingIndex], ROOK | rookColor);
        }
        gameData.playerOnTurn ^= 1;
    }

    //Hashing for TranspoTable
    public ulong ZobristHash()
    {
        return hashing.Hash(board, gameData.playerOnTurn, gameData.castling, gameData.epSpace);
    }
}