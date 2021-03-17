using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameMngr : MonoBehaviour
{
    public MoveGenerator moveGenerator;
    public PieceHandler pieceHandler;
    public SpaceHandler spaceHandler;
    public BoardCreation boardCreation;
    public bool boardExists = false;
    public bool dragAndDropRespectsTurns;
    public GameObject cursor;

    //sehr dumm bitte �ndern
    public bool theoIsBlack;
    public bool theoIsWhite;

    public int playerOnTurn;
    public float moveAnimationTime;

    public int perftTestDepth;

    [HideInInspector]
    public static readonly string startingPosString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public static readonly string pawnTestPos = "8/8/6p1/7P/8/2p1p3/3P4/8 w - - 0 1";
    public static readonly string knightTestPos = "8/8/2p5/5p2/3N4/1P6/4P3/8 w - - 0 1";
    public static readonly string rookTestPos = "8/p2p2p1/8/8/3R4/8/1P1P1P2/8 w - - 0 1";
    public static readonly string bishopTestPos = "8/p2p2p1/8/8/3B4/8/3P1P2/8 w - - 0 1";
    public static readonly string queenTestPos = "8/p2p2p1/8/8/3Q4/8/1P1P1P2/8 w - - 0 1";
    public static readonly string kingTestPos = "8/8/8/3p4/2pKP3/3P4/8/8 w - - 0 1";
    public static readonly string middleGameTestPos = "r4rk1/p3bppp/b1pp1n2/P1q1p3/4P3/2N1B1P1/1PP2PBP/R2QR1K1 b - - 2 13";

    public static readonly string perftTest1 = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ";
    public static readonly string perftTest2 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - ";
    public static readonly string perftTest3 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    public static readonly string perftTest4 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
    public static readonly string perftTest5 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";


    List<int[]> lastMove;
    public Engine theo;
    [HideInInspector]
    public UnityEvent moveMade = new UnityEvent();

    //TODO make this a singleton

    void Awake()
    {
        moveGenerator = new MoveGenerator();
        ChessBoard test = new ChessBoard();
        moveMade.AddListener(OnMove);
        FENHandler.FillFENDict();
    }

    void Start()
    {
        boardCreation.creationFinished.AddListener(OnBoardFinished);
    }

    public void OnBoardFinished()
    {
        boardExists = true;
        StartChessGame();
        
        theo = new Engine(moveGenerator);
        //MakeMoveAnimated(4, 1, 4, 3);
    }

    void OnMove()
    {
        if (theoIsBlack && playerOnTurn == ChessBoard.black)
        {
            var theosMove = theo.ChooseRandomMove(playerOnTurn);
            MakeMoveAnimated(theosMove.Start, theosMove.End);
        } else if (theoIsWhite && playerOnTurn == ChessBoard.white)
        {
            var theosMove = theo.ChooseRandomMove(playerOnTurn);
            MakeMoveAnimated(theosMove.Start, theosMove.End);
        }
    }

    public void MakeMoveNoGraphics(int start, int end)
    {
        lastMove = moveGenerator.MovePiece(start, end);
        if (lastMove.Count == 5) // Castling!
        {
            pieceHandler.MovePieceSprite(lastMove[3][0], lastMove[4][0]);
        } else if (lastMove.Count == 4) // en passant
        {
            pieceHandler.DisablePiece(lastMove[3][0]);
        }
        playerOnTurn = (playerOnTurn == ChessBoard.white) ? ChessBoard.black : ChessBoard.white;
        moveMade.Invoke();
    }

    public void MakeMove(int start, int end)
    {
        if (pieceHandler.GetPieceAtPos(end) != null)
        {
            pieceHandler.DisablePiece(end);
        }
        MakeMoveNoGraphics(start, end);
        pieceHandler.MovePieceSprite(start, end);
    }

    public void MakeMoveAnimated(int start, int end)
    {
        if (pieceHandler.GetPieceAtPos(end) != null)
        {
            pieceHandler.DisablePiece(end);
        }
        MakeMoveNoGraphics(start, end);
        pieceHandler.MovePieceSpriteAnimated(start, end, moveAnimationTime);
    }

    void Update()
    {
        if (!cursor.activeSelf) cursor.SetActive(true);
    }

    public void MoveGenerationTest(int piece)
    {
        switch (piece)
        {
            case 1:
                LoadPosition(pawnTestPos);
                break;
            case 2:
                LoadPosition(knightTestPos);
                break;
            case 3:
                LoadPosition(bishopTestPos);
                break;
            case 4:
                LoadPosition(rookTestPos);
                break;
            case 5:
                LoadPosition(queenTestPos);
                break;
            case 6:
                LoadPosition(kingTestPos);
                break;
        }
        if (piece != 1)
        {
            spaceHandler.UnHighlightAll();
            List<int> possibleMoves = moveGenerator.GetPossibleSpacesForPiece(3+ 3*8);
            spaceHandler.HighlightMoveList(possibleMoves, Color.cyan, 0.5f);
            spaceHandler.HighlightSpace(3, 3, Color.green, 0.5f);
        }
        else
        {
            spaceHandler.UnHighlightAll();
            List<int> possibleMoves1 = moveGenerator.GetPossibleSpacesForPiece(3+ 1*8);
            List<int> possibleMoves2 = moveGenerator.GetPossibleSpacesForPiece(6+ 5*8);
            spaceHandler.HighlightMoveList(possibleMoves1, Color.cyan, 0.5f);
            spaceHandler.HighlightMoveList(possibleMoves2, Color.magenta, 0.5f);
            spaceHandler.HighlightSpace(3, 1, Color.green, 0.5f);
            spaceHandler.HighlightSpace(6, 5, Color.red, 0.5f);
        }
    }

    public void AttackedSpaceGenerationTest()
    {
        LoadPosition(middleGameTestPos);
        ShowAttackedSpaces();
    }

    public void ShowAttackedSpaces()
    {
        spaceHandler.UnHighlightAll();
        var attackedSpacesBlack = moveGenerator.GetAttackedSpaces(0);
        spaceHandler.HighlightMoveList(attackedSpacesBlack, Color.red, 0.25f);
        var attackedSpacesWhite = moveGenerator.GetAttackedSpaces(1);
        spaceHandler.HighlightMoveList(attackedSpacesWhite, Color.green, 0.25f);
    }

    void StartChessGame()
    {
        LoadPosition(startingPosString);
    }

    public void LoadPosition(string fen)
    {
        pieceHandler.ClearBoard();
        moveGenerator.LoadFEN(fen);
        playerOnTurn = moveGenerator.gameData.playerOnTurn;
        pieceHandler.LayOutPieces(moveGenerator.board);
        /*for (int i = 0; i < 12; i++)
        {
            print(moveGenerator.board.piecePositionBoards[i]);
        }*/
    }

    public void EngineMoveCountTest()
    {
        for (int i = 1; i <= perftTestDepth; i++)
        {
            theo.originalDepth = i;
            float startTime = Time.realtimeSinceStartup;
            int moveCount = theo.MoveGenCountTest(i, playerOnTurn);
            float timeElapsed = Time.realtimeSinceStartup - startTime;
            print("Found " + moveCount.ToString("N0") + " moves with depth " + i.ToString());
            print("It took " + timeElapsed.ToString() + " seconds.");
        }
    }
}
