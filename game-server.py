from typing import Optional
import trio
import json
import chess
import model
from model import Role, Outcome, ValidationResult
from trio_websocket import serve_websocket, ConnectionClosed
from enum import Enum
from utils import describe_outcome
lobby = []

def get_outcome(board) -> Optional[Outcome]:
    if board.is_game_over():
        outcome = board.outcome()
        if outcome.winner is None:
            return Outcome.Draw
        elif outcome.winner == chess.WHITE:
            return Outcome.WhiteWins
        elif outcome.winner == chess.BLACK:
            return Outcome.BlackWins
    return None

async def play_match(player1, player2):
    async with player1, player2:
        print(f"Match starting!")
        board = chess.Board()
        await player1.send_message(model.RoleAssignment(role=Role.White, board=str(board)).json())
        await player2.send_message(model.RoleAssignment(role=Role.Black, board=str(board)).json())
        cur, next = player1, player2
        while not board.is_game_over():
            moves = list(board.legal_moves)
            await cur.send_message(
                model.PrepareForMove(valid_moves=[str(move) for move in moves], board=str(board)).json())
            cmd = await cur.get_message()
            move = board.parse_san(cmd)
            if move is None:
                await cur.send_message(model.MoveValidation(validation_result=ValidationResult.IllegalMove).json())
                continue
            board.push(move)
            outcome = get_outcome(board)
            await cur.send_message(model.MoveValidation(validation_result=ValidationResult.Ok, board=str(board), outcome=outcome).json())
            await next.send_message(model.OpponentMove(opponent_move=str(move), board=str(board), outcome=outcome).json())
            cur, next = next, cur
        print(f"Match finished: {describe_outcome(outcome)}")

        # Wait for both players to acknowledge the outcome.
        await player1.get_message()
        await player2.get_message()


def handle_connection_error(error):
    if isinstance(error, ConnectionClosed | trio.TooSlowError | trio.Cancelled):
        return None
    return error

def handle_shutdown_request(error):
    if isinstance(error, trio.Cancelled | KeyboardInterrupt):
        print("Shutting down")
        return None
    return error

async def heartbeat(ws, timeout=300, interval=1):
    while True:
        with trio.fail_after(timeout):
            await ws.ping()
        await trio.sleep(interval)

async def run_session(request):
    ws = None
    try:
        with trio.MultiError.catch(handle_connection_error):
            async with trio.open_nursery() as nursery:
                ws = await request.accept()
                nursery.start_soon(heartbeat, ws)
                lobby.append(ws)
                print(f"Player joined lobby. Currently waiting players: {len(lobby)}")
                if len(lobby) == 2:
                    this_player = lobby.pop()
                    other_player = lobby.pop()
                    await play_match(other_player, this_player)
    finally:
        if ws and ws in lobby:
            lobby.remove(ws)
        print("Connection closed")

async def main():
    with trio.MultiError.catch(handle_shutdown_request):
        await serve_websocket(run_session, '0.0.0.0', 8000, ssl_context=None)

if __name__ == '__main__':
    trio.run(main)