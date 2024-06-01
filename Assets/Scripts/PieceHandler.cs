using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static ChessBoard;

public class PieceHandler : MonoBehaviour
{
    //core + linking 
    [SerializeField] Dictionary<int, GameObject> piecePositions;
    public SpaceHandler spaceHandler;
    public GameMngr manager;
    MoveGenerator moveGenerator;

    //piece creation
    [SerializeField] GameObject[] pieceObjects;
    float pieceSize;

    //picking up pieces
    public GameObject pieceUnderCursor;
    public Color highlightColor;

    //drag and drop
    GameObject selectedPiece;
    Vector3 transformDelta;
    int[] possibleMovesForClickedPiece;
    bool inDrag;

    //piece moving
    int startSpace;
    int endSpace;
    bool respectTurn;

    //init
    void Awake()
    {
        moveGenerator = manager.moveGenerator;
        respectTurn = manager.dragAndDropRespectsTurns;
        pieceSize = pieceObjects[0].transform.localScale.x;
        piecePositions = new Dictionary<int, GameObject>();
        pieceUnderCursor = null;
    }

    public void LayOutPieces(ChessBoard board)
    {
        piecePositions = new Dictionary<int, GameObject>();
        for (int i = 0; i < 12; i++)
        {
            for (int space = 0; space < 64; space++)
            {
                if (board.piecePositionBoards[i][space])
                {
                    GameObject newPiece = Instantiate(pieceObjects[i], spaceHandler.ChessSpaceToWorldSpace(space), Quaternion.identity, gameObject.transform);
                    piecePositions.Add(space, newPiece);
                }
            }
        }
    }

    //finding certain pieces
    public GameObject GetPieceAtPos(int position)
    {
        if (piecePositions.ContainsKey(position))
        {
            return piecePositions[position];
        }
        return null;
    }

    public GameObject GetPieceAtCursor(){
        Vector2 mouseKoordsInWorld = new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y);
        int piecePosition = spaceHandler.WorldSpaceToChessSpace(mouseKoordsInWorld);
        return GetPieceAtPos(piecePosition);
    }

    public Vector2 CursorPos() {
        return new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y);
    }

    public int CursorSpace(){
        return spaceHandler.WorldSpaceToChessSpace(CursorPos());
    }

    //reloading pieces
    public void ClearBoard()
    {
        piecePositions = new Dictionary<int, GameObject>();
        GameObject[] pieces = GameObject.FindGameObjectsWithTag("Piece");
        foreach (GameObject piece in pieces)
        {
            DestroyImmediate(piece);
        }
    }

    public void ReloadPieces()
    {
        ClearBoard();
        LayOutPieces(moveGenerator.board);
    }

    //moving pieces
    public void DisablePiece(int position)
    {
        if(GetPieceAtPos(position) == null) return;
        GetPieceAtPos(position).SetActive(false);
        piecePositions.Remove(position);
    }

    public void ChangePieceSprite(int position, Sprite newSprite)
    {
        GameObject piece = GetPieceAtPos(position);
        if (piece == null) return;
        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        renderer.sprite = newSprite;
    }

    public void ChangePieceToQueen(int position, int color)
    {
        if (color == ChessBoard.BLACK) ChangePieceSprite(position, pieceObjects[10].GetComponent<SpriteRenderer>().sprite);
        else ChangePieceSprite(position, pieceObjects[4].GetComponent<SpriteRenderer>().sprite);
    }

    public void MovePieceSprite(int oldSpace, int newSpace)
    {
        GameObject pieceToMove = GetPieceAtPos(oldSpace);
        pieceToMove.transform.position = spaceHandler.ChessSpaceToWorldSpace(newSpace);
        GameObject takenPiece = GetPieceAtPos(newSpace);
        if (takenPiece != null)
        {
            DisablePiece(newSpace);
        }
        piecePositions.Remove(oldSpace);
        piecePositions[newSpace] = pieceToMove;
        if ((SpaceY(newSpace) == 0 || SpaceY(newSpace) == 7)) //pawn got maybe promoted
        {
            if (moveGenerator.board.Contains(newSpace, WHITE_PIECE | QUEEN)) ChangePieceToQueen(newSpace, WHITE);
            if (moveGenerator.board.Contains(newSpace, BLACK_PIECE | QUEEN)) ChangePieceToQueen(newSpace, BLACK);
        }
    }

    IEnumerator PieceAnimationCoroutine(int oldSpace, int newSpace, float animTime)
    {
        float startTime = Time.time;
        GameObject pieceToMove = GetPieceAtPos(oldSpace);
        if (pieceToMove == null)
        {
            ReloadPieces();
            yield break;
        }
        GameObject takenPiece = GetPieceAtPos(newSpace);
        if (takenPiece != null)
        {
            DisablePiece(newSpace);
        }
        piecePositions.Remove(oldSpace);
        piecePositions[newSpace] = pieceToMove;
        if ((SpaceY(newSpace) == 0 || SpaceY(newSpace) == 7)) //pawn got maybe promoted
        {
            if (moveGenerator.board.Contains(newSpace, WHITE_PIECE | QUEEN)) ChangePieceToQueen(newSpace, WHITE);
            if (moveGenerator.board.Contains(newSpace, BLACK_PIECE | QUEEN)) ChangePieceToQueen(newSpace, BLACK);
        }
        while (Time.time - startTime <= animTime)
        {        
            if (pieceToMove == null) //a bit ugly, but should prevent weird bugs in very fast AI v AI games
            {
                ReloadPieces();
                yield break;
            }
            pieceToMove.transform.position = Vector3.Lerp(spaceHandler.ChessSpaceToWorldSpace(oldSpace), spaceHandler.ChessSpaceToWorldSpace(newSpace), (Time.time - startTime) / animTime);
            yield return 0;
        }
        pieceToMove.transform.position = spaceHandler.ChessSpaceToWorldSpace(newSpace); //this prevents a bug only arising in the built version, no idea why i have to do this but so be it
    }

    public void MovePieceSpriteAnimated(int oldSpace, int newSpace, float animTime)
    {
        StartCoroutine(PieceAnimationCoroutine(oldSpace, newSpace, animTime));
    }

    //drag and drop functionality
    int SnapToSpace(GameObject piece, int startSpace, int[] possibleSpaces)
    {
        int space = spaceHandler.WorldSpaceToChessSpace(piece.transform.position);
        //resetting the piece in case of an invalid move
        if (space < 0 || space > 63 || space == startSpace || !possibleSpaces.Contains(space) || manager.searching)
        {
            piece.transform.position = spaceHandler.ChessSpaceToWorldSpace(startSpace);
            return startSpace;
        }
        //taking a piece, only one piece per space
        if (GetPieceAtPos(space) != null)
        {
            DisablePiece(space);
        }
        //actual snapping
        piece.transform.position = spaceHandler.ChessSpaceToWorldSpace(space);
        piecePositions.Remove(startSpace);
        piecePositions[space] = piece;
        return space;
    }

    void DragDrop(bool respectTurn = false)
    {
        if (manager.searching) return;
        if (Input.GetMouseButtonDown(0))
        {
            selectedPiece = GetPieceAtCursor();
            if (selectedPiece == null) return;

            startSpace = spaceHandler.WorldSpaceToChessSpace(selectedPiece.transform.position);
            possibleMovesForClickedPiece = moveGenerator.GetLegalMovesForPiece(startSpace).GetActive();
            
            if (!respectTurn || manager.playerOnTurn == ChessBoard.PieceColor(moveGenerator.board[startSpace]))
            {
                //selectedPiece.GetComponent<SpriteRenderer>().color = Color.Lerp(Color.white, highlightColor, 0.5f);

                spaceHandler.HighlightSpaceList(possibleMovesForClickedPiece, Color.cyan, 0.5f);
                spaceHandler.HighlightSpace(startSpace, Color.green, 0.5f);

                transformDelta = selectedPiece.transform.position - (Vector3)CursorPos();
                selectedPiece.GetComponent<BoxCollider2D>().enabled = false;
                selectedPiece.GetComponent<SpriteRenderer>().sortingOrder = 1;

                inDrag = true;
            }
        }
        else if (Input.GetMouseButton(0) && selectedPiece != null && inDrag)
        {
            selectedPiece.transform.position = (Vector3) CursorPos() +  transformDelta;
        }
        else if (Input.GetMouseButtonUp(0) && selectedPiece != null && inDrag)
        {
            endSpace = SnapToSpace(selectedPiece, startSpace, possibleMovesForClickedPiece);
            spaceHandler.UnHighlightAll(); // Doesnt need to be fast :D
            selectedPiece.GetComponent<SpriteRenderer>().color = Color.white; 
            
            if(moveGenerator.IsPlayerInCheck(manager.playerOnTurn)) { //oh god nooo
                int kingSpace = (manager.playerOnTurn == WHITE) ? moveGenerator.board.WhiteKingPosition() : moveGenerator.board.BlackKingPosition();
                spaceHandler.HighlightSpace(kingSpace, Color.red, 0.5f);
            }
            
            if (manager.lastMove.movedPiece != 0) {
                spaceHandler.HighlightSpace(manager.lastMove.start, Color.yellow, 0.7f);
                spaceHandler.HighlightSpace(manager.lastMove.end, Color.yellow, 0.7f);
            }

            selectedPiece.GetComponent<BoxCollider2D>().enabled = true;
            selectedPiece.GetComponent<SpriteRenderer>().sortingOrder = 0;

            if (startSpace != endSpace)
            {
                manager.MakeMoveNoGraphics(startSpace, endSpace, false);
            }

            selectedPiece = null;
            inDrag = false;
        }
    }

    void Update()
    {
        if (pieceUnderCursor != null && !inDrag) { //ouch
            pieceUnderCursor.GetComponent<SpriteRenderer>().color = Color.Lerp(Color.white, highlightColor, 0.5f);
        }
        if ((GetPieceAtCursor() != pieceUnderCursor || inDrag) && pieceUnderCursor != null) { //piece just got unhighlighted
            pieceUnderCursor.GetComponent<SpriteRenderer>().color = Color.white;
        }
        pieceUnderCursor = GetPieceAtCursor();
        DragDrop(respectTurn);
    }
}
