using ChessChallenge.API;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    /*
     * TODO: Add lategame sense
     * TODO: Avoid 3 fold repetition when in a winning position and 50 move rule
     * TODO: SPEED UP the code
     * 
     */

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

        foreach (Move move in moves)
        {
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
        int attackerValue1 = pieceValues[(int)board.GetPiece(move1.StartSquare).PieceType];
        int victimValue1 = pieceValues[(int)board.GetPiece(move1.TargetSquare).PieceType];
        int attackerValue2 = pieceValues[(int)board.GetPiece(move2.StartSquare).PieceType];
        int victimValue2 = pieceValues[(int)board.GetPiece(move2.TargetSquare).PieceType];

        // Compare moves based on MVV-LVA
        int score1 = victimValue2 - attackerValue1;
        int score2 = victimValue1 - attackerValue2;

        return score2 - score1; // Higher score means a better move
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

        if (board.IsWhiteToMove && board.IsInCheck()) white -= 65;
        if (!board.IsWhiteToMove && board.IsInCheck()) black -= 65;
        return white - black + MobilityScore(board);
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
                if (beta <= alpha) break; // Beta cut-off
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
                if (beta <= alpha) break; // Alpha cut-off
            }
        }
            
        return eval;
    }

    int CalculatePieceMobility(Board board, string[] pieceCoordinates, bool isWhite)
    {
        int mobilitySum = 0;

        foreach (string coord in pieceCoordinates)
        {
            Piece piece = board.GetPiece(new Square(coord));
            int mobility = 0;

            switch (piece.PieceType)
            {
                case PieceType.Pawn:
                    mobility = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPawnAttacks(new Square(coord), isWhite)) * 3;
                    break;
                case PieceType.Rook:
                    mobility = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(PieceType.Rook, new Square(coord), board)) * 2;
                    break;
                case PieceType.Knight:
                    mobility = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetKnightAttacks(new Square(coord))) * 4;
                    break;
                case PieceType.Bishop:
                    mobility = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(PieceType.Bishop, new Square(coord), board)) * 2;
                    break;
                case PieceType.Queen:
                    mobility = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(PieceType.Queen, new Square(coord), board));
                    break;
                    // case PieceType.King: Handle King if needed.
            }

            mobilitySum += mobility;
        }

        return mobilitySum;
    }

    int MobilityScore(Board board)
    {
        var pieceCoordinates = GetPieceCoordinates(board);
        string[] WhitePieceCoordinates = pieceCoordinates.WhitePieceCoordinates;
        string[] BlackPieceCoordinates = pieceCoordinates.BlackPieceCoordinates;

        int WhiteMobility = CalculatePieceMobility(board, WhitePieceCoordinates, true);
        int BlackMobility = CalculatePieceMobility(board, BlackPieceCoordinates, false);

        return WhiteMobility - BlackMobility;
    }

    public static (string[] WhitePieceCoordinates, string[] BlackPieceCoordinates) GetPieceCoordinates(Board board)
    {

        //int BoardSize = 8;
        // Create lists to store the coordinates of squares with white and black pieces
        List<string> whitePieceCoordinates = new List<string>();
        List<string> blackPieceCoordinates = new List<string>();

        // Iterate through each square on the chessboard
        for (int rank = 0; rank < 8; rank++) // 8 is the boardsize iam saving tokens 
        {
            for (int file = 0; file < 8; file++) // 8 is the boardsize iam saving tokens 
            {
                // Create a Square object representing the current square
                Square square = new Square(file, rank);

                // Get the piece on the current square
                Piece piece = board.GetPiece(square);

                // If there is a piece on the square, add its coordinates to the respective list
                
                    string position = square.Name;

                if (piece.IsWhite) whitePieceCoordinates.Add(position);
                else blackPieceCoordinates.Add(position);
                    
                
            }
        }

        // Return the lists of coordinates for white and black pieces
        return (whitePieceCoordinates.ToArray(), blackPieceCoordinates.ToArray());

    }

    public static PieceType GetPieceTypeAtCoordinate(Board board, string coordinate)
    {
        // Create a Square object from the given coordinate
        Square square = new Square(coordinate);

        // Get the piece on the square
        Piece piece = board.GetPiece(square);

        // Return the piece type
        return piece.PieceType;
    }
}

