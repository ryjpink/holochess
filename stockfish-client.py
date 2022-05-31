import model
from model import Role, ValidationResult
from utils import describe_outcome

from stockfish import Stockfish
from trio_websocket import open_websocket_url
import trio
from sys import stderr

async def main():
    try:
        async with open_websocket_url('ws://127.0.0.1:8000') as ws:
            stockfish = Stockfish(path="D:\\tmp\\stockfish_15_x64_avx2.exe")
            stockfish.set_elo_rating(300)
            role_assignment_message = model.RoleAssignment.parse_raw(await ws.get_message())
            role = role_assignment_message.role
            print(f"Playing as {role}")
            my_turn = (role == Role.White)
            outcome = None
            while not outcome:
                if my_turn:
                    prepare_for_move_message = model.PrepareForMove.parse_raw(await ws.get_message())
                    move = stockfish.get_best_move()
                    if not move in prepare_for_move_message.valid_moves:
                        raise Exception("Engine found move which the server doesn't accept")
                    await ws.send_message(move)
                    move_validation_message = model.MoveValidation.parse_raw(await ws.get_message())
                    if not move_validation_message.validation_result == ValidationResult.Ok:
                        raise Exception("Server considers engine move invalid")
                    stockfish.make_moves_from_current_position([move])
                    outcome = move_validation_message.outcome
                else:
                    opponent_move_message = model.OpponentMove.parse_raw(await ws.get_message())
                    stockfish.make_moves_from_current_position([opponent_move_message.opponent_move])
                    outcome = opponent_move_message.outcome
                my_turn = not my_turn
                print(stockfish.get_board_visual())
            print(describe_outcome(outcome))
            await ws.send_message('bye')
    except OSError as ose:
        print('Connection attempt failed: %s' % ose, file=stderr)

if __name__ == '__main__':
    trio.run(main)