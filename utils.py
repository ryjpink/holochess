from model import Outcome

def describe_outcome(outcome: Outcome) -> str:
    match outcome:
        case Outcome.Draw:
            return "Draw"
        case Outcome.WhiteWins:
            return "White won!"
        case Outcome.BlackWins:
            return "Black won!"
    return "Unknown outcome"