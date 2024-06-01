using System.Collections;
using System.Collections.Generic;

using static ChessBoard;

//reads provided FEN strings and returns piece positions

public class FENHandler
{
    public static readonly char[] fenPieceLetters = new char[] { 'P', 'N', 'B', 'R', 'Q', 'K' };
    public static Dictionary<char, int> fenParsing = new Dictionary<char, int>
    {
        {'P', PAWN | WHITE_PIECE },
        {'N', KNIGHT | WHITE_PIECE },
        {'B', BISHOP | WHITE_PIECE },
        {'R', ROOK | WHITE_PIECE },
        {'Q', QUEEN | WHITE_PIECE },
        {'K', KING | WHITE_PIECE },
        {'p', PAWN | BLACK_PIECE },
        {'n', KNIGHT | BLACK_PIECE },
        {'b', BISHOP | BLACK_PIECE },
        {'r', ROOK | BLACK_PIECE },
        {'q', QUEEN | BLACK_PIECE },
        {'k', KING | BLACK_PIECE }
    };

    public static ChessBoard ReadFEN(string fenNotation)
    {
        //TODO error handling this is quite dangerous
        int pieceToPlace;
        string[] fenGroups = fenNotation.Split(' ');
        string[] fenRows = fenGroups[0].Split('/');
        ChessBoard output = new ChessBoard();
        string currentRow;
        for (int y = 0; y < 8; y++)
        {
            currentRow = fenRows[y];
            int currentX = 0;
            foreach (char piece in currentRow)
            {
                if (char.IsDigit(piece))
                {
                    currentX += int.Parse(piece.ToString());
                    if (currentX >= 8)
                    {
                        break;
                    }
                }
                else
                {
                    pieceToPlace = fenParsing[piece];
                    output[8 * (7 - y) + currentX] = pieceToPlace;
                    currentX++;
                }
            }
        }
        return output;
    }

    public static ChessGameData GameDataFromFEN(string fenNotation)
    {
        //TODO support for en passant space loading and error handling!
        var output = new ChessGameData(WHITE, 0, new bool[4]);
        string[] fenGroups = fenNotation.Split(' ');
        output.playerOnTurn = (fenGroups[1] == "w") ? WHITE : BLACK;
        string castlingStr = fenGroups[2];
        foreach (char currentLetter in castlingStr)
        {
            switch (currentLetter)
            {
                case 'K':
                    output.castling[MoveGenerator.SHORT_CASTLING_WHITE] = true;
                    break;
                case 'Q':
                    output.castling[MoveGenerator.LONG_CASTLING_WHITE] = true;
                    break;
                case 'k':
                    output.castling[MoveGenerator.SHORT_CASTLING_BLACK] = true;
                    break;
                case 'q':
                    output.castling[MoveGenerator.LONG_CASTLING_BLACK] = true;
                    break;
            }
        }
        return output;
    }
}
