using System.Collections.Generic;

public enum Role
{
    white,
    black,
}

public class RoleAssignmentMessage
{
    public Role role;
    public string board;
}

public class PrepareForMoveMessage
{
    public IReadOnlyList<string> valid_moves;
    public string board;
}

public class OpponentMoveMessage
{
    public string opponent_move;
    public string board;
    public GameOutcome? outcome;
}

public enum MoveValidationResult
{
    ok,
    illegal_move,
}

public enum GameOutcome
{
    draw,
    white_wins,
    black_wins,
}

public class MoveValidationMessage
{
    public MoveValidationResult validation_result;
    public string board;
    public GameOutcome? outcome;
}