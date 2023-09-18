using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {

        int[] pieceValues = { 0, 125, 300, 300, 500, 900, 100000 }; //0 - pawn, 1 - knight, 2 - bishop, 3 - rook, 4 - queen, 5 - king
        int globalDepth = 3;


        private bool CheckMateInOne(Board board, Move move)
        {
            /*
             Only checks if the move is a checkmate
             */
            board.MakeMove(move);
            bool isMate = board.IsInCheckmate();
            board.UndoMove(move);
            return isMate;
        }

        private void Endgame(Board board)
        {
            int numberOfPieces = board.GetAllPieceLists().Length;
            if (numberOfPieces < 12 && numberOfPieces > 5) globalDepth = 4;
            else if (board.GetAllPieceLists().Length < 5) globalDepth = 5;
        }

        public Move Think(Board board, Timer timer)
        {
            Span<Move> moves = stackalloc Move[256];
            board.GetLegalMovesNonAlloc(ref moves, false); // Generate all legal moves

            // Sort the moves using MVV-LVA criteria
            moves.Sort((move1, move2) => CompareMoves(board, move1, move2));


            Move nextMove = moves[0]; // The first move after sorting

            int bestValue = (board.IsWhiteToMove) ? -999999999 : 999999999;
            int temp = 0;

            bool turn = board.IsWhiteToMove;

            Endgame(board);

            for (int x = 0; x < moves.Length; x++)
            {
                Move move = moves[x];

                if (CheckMateInOne(board, move)) return move;

                board.MakeMove(move);
                temp = MinMax(board, globalDepth, -999999999, 999999999, !turn);
                board.UndoMove(move);

                if ((temp > bestValue && board.IsWhiteToMove) || (temp < bestValue && !board.IsWhiteToMove))
                {
                    nextMove = move;
                    bestValue = temp;
                }
            }

            return nextMove;
        }

        private int CompareMoves(Board board, Move move1, Move move2)
        {
            int attackerValue1 = GetPieceValue(board.GetPiece(move1.StartSquare));
            int victimValue1 = GetPieceValue(board.GetPiece(move1.TargetSquare));
            int attackerValue2 = GetPieceValue(board.GetPiece(move2.StartSquare));
            int victimValue2 = GetPieceValue(board.GetPiece(move2.TargetSquare));

            // Compare moves based on MVV-LVA
            int score1 = victimValue2 - attackerValue1;
            int score2 = victimValue1 - attackerValue2;

            return score2 - score1; // Higher score means a better move
        }

        private int GetPieceValue(Piece piece)
        {
            if (piece == null)
                return 0;

            return pieceValues[(int)piece.PieceType];
        }



        private int Eval(Board board)
        {
            /*
             This is a simple evaluation function that counts the number of pieces and 
             strength of each side and subtracts the difference. Positive is good for 
             white, negative for black

            This will need improvement, as this is a very crude way of doing it, 
            and needs much improvement but for now this is good enough for further 
            development of a move prediction model. But I will have to come back for this
             */

            PieceList[] pieces = board.GetAllPieceLists();

            int white = 0, black = 0; // Points for black and white
            bool last = false;      // Using the king as a separator for white and black

            foreach (PieceList pieceList in pieces)
            {

                foreach (Piece piece in pieceList)
                {
                    if (!last)
                    {
                        white += pieceValues[(int)piece.PieceType];
                        if (pieceValues[(int)piece.PieceType] == 100000) last = true;
                    }
                    else black += pieceValues[(int)piece.PieceType];
                }
            }
            return (white - black) + KnightPosition(board.GetFenString());
        }

        public int MinMax(Board board, int depth, int alpha, int beta, bool maximizingPlayer)
        {
            if (depth == 0 || board.IsInCheckmate()) return Eval(board);


            Span<Move> moves = stackalloc Move[256];
            board.GetLegalMovesNonAlloc(ref moves, false); // Use capturesOnly = false for all legal moves

            int eval;

            if (maximizingPlayer)
            {
                eval = int.MinValue; // Initialize eval to negative infinity for maximizing player (WHITE)
                foreach (Move move in moves)
                {
                    board.MakeMove(move);
                    eval = Math.Max(eval, MinMax(board, depth - 1, alpha, beta, false));
                    board.UndoMove(move);

                    alpha = Math.Max(alpha, eval); // Update alpha
                    if (beta <= alpha)
                        break; // Beta cut-off
                }
            }
            else
            {
                eval = int.MaxValue; // Initialize eval to positive infinity for minimizing player (BLACK)
                foreach (Move move in moves)
                {
                    board.MakeMove(move);
                    eval = Math.Min(eval, MinMax(board, depth - 1, alpha, beta, true));
                    board.UndoMove(move);

                    beta = Math.Min(beta, eval); // Update beta
                    if (beta <= alpha)
                        break; // Alpha cut-off
                }
            }
            return eval;
        }
        int KnightPosition(string fen)
        {
            //This can be sped up to check for K and return 0 if none are found

            if (!fen.ToLower().Contains("k")) return 0; // if there is no knights just skip this block of code
            int[] ringValues = { -50, -10, -25, 50 };
            string[] fenParts = fen.Split(' ');
            int whiteKnightScore = 0, blackKnightScore = 0;

            char[][] board = new char[8][];
            for (int i = 0; i < 8; i++)
                board[i] = new char[8];

            for (int row = 0, col = 0, i = 0; i < fenParts[0].Length; i++)
            {
                char c = fenParts[0][i];
                if (c == '/')
                {
                    row++;
                    col = 0;
                }
                else if (char.IsDigit(c))
                    col += c - '0';
                else
                    board[row][col++] = c;
            }

            int[] dx = { 1, 2, 2, 1, -1, -2, -2, -1 };
            int[] dy = { 2, 1, -1, -2, -2, -1, 1, 2 };

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    char piece = board[r][c];
                    if (piece == 'n' || piece == 'N')
                    {
                        int ring = Math.Min(Math.Min(r, 7 - r), Math.Min(c, 7 - c));
                        (piece == 'n' ? ref blackKnightScore : ref whiteKnightScore) += ringValues[ring];
                    }
                }
            }

            return whiteKnightScore - blackKnightScore;
        }


    }
}