from enum import Enum
from pydantic import BaseModel
from typing import List, Optional

class Role(str, Enum):
    White = 'white'
    Black = 'black'

class Outcome(str, Enum):
    Draw = 'draw'
    WhiteWins = 'white_wins'
    BlackWins = 'black_wins'

class ValidationResult(str, Enum):
    Ok = 'ok'
    IllegalMove = 'illegal_move'

class RoleAssignment(BaseModel):
    role: Role
    board: str

class PrepareForMove(BaseModel):
    valid_moves: List[str]
    board: str

class MoveValidation(BaseModel):
    validation_result: ValidationResult
    board: Optional[str]
    outcome: Optional[Outcome]

class OpponentMove(BaseModel):
    opponent_move: str
    board: str
    outcome: Optional[Outcome]
