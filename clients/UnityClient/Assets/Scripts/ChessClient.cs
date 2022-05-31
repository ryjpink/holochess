using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;

public class ChessClient : MonoBehaviour
{
    public string ServerUri = "ws://127.0.0.1:8000";

    public GameObject WhitePawn;
    public GameObject WhiteKnight;
    public GameObject WhiteBishop;
    public GameObject WhiteRook;
    public GameObject WhiteQueen;
    public GameObject WhiteKing;

    public GameObject BlackPawn;
    public GameObject BlackKnight;
    public GameObject BlackBishop;
    public GameObject BlackRook;
    public GameObject BlackQueen;
    public GameObject BlackKing;

    private Dictionary<string, GameObject> _cells = new();
    private Dictionary<char, GameObject> _prefabs;
    private Dictionary<char, List<GameObject>> _unusedPieces = new();
    private List<(char, GameObject)> _usedPieces = new();
    private IReadOnlyList<string> _validMoves = new List<string>();
    private TaskCompletionSource<string> _requestedMove;

    private static char[] PieceLetters = new[] { 'P', 'N', 'B', 'R', 'Q', 'K', 'p', 'n', 'b', 'r', 'q', 'k' };
    private static char[] ColumnNames = new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

    public ChessClient()
    {
        foreach (char pieceLetter in PieceLetters)
        {
            _unusedPieces.Add(pieceLetter, new List<GameObject>());
        }
    }

    private async Task<GameOutcome> Play(CancellationToken cancellationToken)
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(ServerUri), cancellationToken);
        var roleAssignmentMessage = await ws.ReceiveMessage<RoleAssignmentMessage>(cancellationToken);
        Role role = roleAssignmentMessage.role;
        bool myTurn = (role == Role.white);
        ApplyBoardConfiguration(role, roleAssignmentMessage.board);
        GameOutcome? outcome;
        do
        {
            if (myTurn)
            {
                var prepareForMoveMessage = await ws.ReceiveMessage<PrepareForMoveMessage>(cancellationToken);
                _validMoves = prepareForMoveMessage.valid_moves;
                MoveValidationMessage moveValidationMessage;
                do
                {
                    // Wait for the player to make a move.
                    _requestedMove = new TaskCompletionSource<string>();
                    string selectedMove = await _requestedMove.Task;

                    await ws.SendMessage(selectedMove, cancellationToken);
                    moveValidationMessage = await ws.ReceiveMessage<MoveValidationMessage>(cancellationToken);
                } while (moveValidationMessage.validation_result == MoveValidationResult.illegal_move);
                outcome = moveValidationMessage.outcome;
                ApplyBoardConfiguration(role, moveValidationMessage.board);
            }
            else
            {
                var opponentMoveMessage = await ws.ReceiveMessage<OpponentMoveMessage>(cancellationToken);
                ApplyBoardConfiguration(role, opponentMoveMessage.board);
                outcome = opponentMoveMessage.outcome;
            }
            myTurn = !myTurn;
        } while (outcome == null);
        await ws.SendMessage("bye", cancellationToken);
        return outcome.Value;
    }

    void Start()
    {
        // Allow only grab interactions
        PointerUtils.SetHandRayPointerBehavior(PointerBehavior.AlwaysOff);
        PointerUtils.SetHandPokePointerBehavior(PointerBehavior.AlwaysOff);
        PointerUtils.SetGazePointerBehavior(PointerBehavior.AlwaysOff);
        PointerUtils.SetMotionControllerRayPointerBehavior(PointerBehavior.AlwaysOff);

        IndexPrefabs();
        IndexCells();

        Play(CancellationToken.None);
    }

    private void IndexPrefabs()
    {
        _prefabs = new()
        {
            ['P'] = WhitePawn,
            ['N'] = WhiteKnight,
            ['B'] = WhiteBishop,
            ['R'] = WhiteRook,
            ['Q'] = WhiteQueen,
            ['K'] = WhiteKing,

            ['p'] = BlackPawn,
            ['n'] = BlackKnight,
            ['b'] = BlackBishop,
            ['r'] = BlackRook,
            ['q'] = BlackQueen,
            ['k'] = BlackKing
        };
    }

    private void IndexCells()
    {
        for (int row = 0; row < 8; ++row)
        {
            for (int col = 0; col < 8; ++col)
            {
                string cellName = GetCellName(row, col);
                _cells.Add(cellName, transform.Find($"Cells/Row{row + 1}/{cellName}").gameObject);
            }
        }
    }

    void ApplyBoardConfiguration(Role role, string board)
    {
        ResetPieces();
        string[] rows = board.Split('\n');
        if (rows.Length != 8)
        {
            throw new ArgumentException("Invalid board");
        }
        Array.Reverse(rows);
        for (int row = 0; row < 8; ++row)
        {
            string[] cols = rows[row].Split(' ');
            if (cols.Length != 8)
            {
                throw new ArgumentException("Invalid board");
            }
            for (int col = 0; col < 8; ++col)
            {
                string cellContent = cols[col];
                if (cellContent.Length != 1)
                {
                    throw new ArgumentException("Invalid board");
                }
                char symbol = cellContent[0];
                if (symbol == '.')
                {
                    // Empty cell
                }
                else
                {
                    if (!PieceLetters.Contains(symbol))
                    {
                        throw new ArgumentException("Invalid board");
                    }

                    GameObject pieceGameObject = ReuseOrInstantiatePiece(symbol);
                    var objectManipulator = pieceGameObject.GetComponent<ObjectManipulator>();
                    objectManipulator.enabled = (role == GetPieceOwner(symbol));
                    string cellName = GetCellName(row, col);
                    TeleportTo(pieceGameObject, _cells[cellName].transform);
                }
            }
        }
        CleanupUnusedPieces();
    }

    void ResetPieces()
    {
        foreach (var (piece, gameObject) in _usedPieces)
        {
            _unusedPieces[piece].Add(gameObject);
        }
        _usedPieces.Clear();
    }

    void CleanupUnusedPieces()
    {
        foreach (List<GameObject> instances in _unusedPieces.Values)
        {
            foreach (GameObject instance in instances)
            {
                UnityEngine.Object.Destroy(instance);
            }
            instances.Clear();
        }
    }

    GameObject ReuseOrInstantiatePiece(char piece)
    {
        var availablePieces = _unusedPieces[piece];
        GameObject gameObject;
        if (availablePieces.Count == 0)
        {
            gameObject = UnityEngine.Object.Instantiate(_prefabs[piece]);

            var objectManipulator = gameObject.GetComponent<ObjectManipulator>();
            if (objectManipulator != null)
            {
                objectManipulator.OnManipulationStarted.AddListener(OnManipulationStarted);
                objectManipulator.OnManipulationEnded.AddListener(OnManipulationEnded);
            }
        }
        else
        {
            gameObject = availablePieces.Last();
            availablePieces.RemoveAt(availablePieces.Count - 1);
        }
        _usedPieces.Add((piece, gameObject));
        return gameObject;
    }

    private void OnManipulationStarted(ManipulationEventData manipulationEventData)
    {
        GameObject gameObject = manipulationEventData.ManipulationSource;
        GameObject cell = gameObject.transform.parent.gameObject;
        string cellName = cell.name;

        if (_requestedMove != null)
        {
            string[] targetCells =  _validMoves
                .Where(move => move.StartsWith(cellName))
                .Select(move => move.Substring(2, 2))
                .Where(cell => _cells.ContainsKey(cell))
                .Distinct()
                .ToArray();
            foreach (string targetCell in targetCells)
            {
                var meshRenderer = _cells[targetCell].GetComponent<MeshRenderer>();
                meshRenderer.enabled = true;
            }
        }
    }

    private void OnManipulationEnded(ManipulationEventData manipulationEventData)
    {
        GameObject draggedObject = manipulationEventData.ManipulationSource;
        string startCell = draggedObject.transform.parent.gameObject.name;

        foreach (GameObject cell in _cells.Values)
        {
            var meshRenderer = cell.GetComponent<MeshRenderer>();
            meshRenderer.enabled = false;
        }

        if (_requestedMove != null)
        {
            string endCell = string.Empty;
            var placementSensor = draggedObject.GetComponent<ChessPiecePlacementSensor>();
            if (placementSensor != null)
            {
                endCell = placementSensor.TargetedCellName;
            }
            if (!string.IsNullOrEmpty(endCell))
            {
                string move = _validMoves.FirstOrDefault(move => move.StartsWith(startCell + endCell));
                if (!string.IsNullOrEmpty(move))
                {
                    _requestedMove.SetResult(move);
                    _requestedMove = null;
                    return;
                }
            }
        }

        // Snap all pieces back to their current placement.
        foreach (var (piece, gameObject) in _usedPieces)
        {
            TeleportTo(gameObject, gameObject.transform.parent);
        }
    }

    private void TeleportTo(GameObject gameObject, Transform parent)
    {
        // Disable rigid body dynamics for the next tick.
        var rigidBody = gameObject.GetComponent<Rigidbody>();
        rigidBody.Sleep();

        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
    }

    private static string GetCellName(int row, int col)
    {
        return $"{ColumnNames[col]}{row + 1}";
    }

    private static Role GetPieceOwner(char symbol)
    {
        return char.IsUpper(symbol) ? Role.white : Role.black;
    }
}