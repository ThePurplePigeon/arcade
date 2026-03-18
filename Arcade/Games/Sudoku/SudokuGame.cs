using System;
using System.Collections.Generic;

namespace Arcade.Games.Sudoku;

public sealed class SudokuGame
{
    private readonly Random random;
    private readonly IReadOnlyList<SudokuPuzzle> puzzles;
    private readonly Dictionary<SudokuDifficulty, DifficultyPoolState> pools = [];

    private SudokuBoardAnalysisSnapshot analysis = SudokuBoardAnalysisSnapshot.Empty;

    public SudokuGame(ISudokuPuzzleProvider provider, SudokuGameSettings? settings = null, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Settings = settings ?? new SudokuGameSettings();
        puzzles = provider.GetPuzzles();
        if (puzzles.Count == 0)
        {
            throw new InvalidOperationException("Sudoku game cannot start with an empty puzzle list.");
        }

        random = seed.HasValue ? new Random(seed.Value) : new Random();
        Board = new SudokuBoard();
        BuildDifficultyPools();

        SelectedDifficulty = NormalizeDifficulty(Settings.DefaultDifficulty);
        StartNewPuzzle();
    }

    public SudokuGameSettings Settings { get; }
    public SudokuBoard Board { get; }
    public SudokuGameState State { get; private set; }
    public SudokuDifficulty SelectedDifficulty { get; private set; }
    public SudokuPuzzle CurrentPuzzle { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public bool HasProgress { get; private set; }
    public SudokuBoardAnalysisSnapshot Analysis => analysis;

    public TimeSpan Elapsed
    {
        get
        {
            if (!StartedAtUtc.HasValue)
            {
                return TimeSpan.Zero;
            }

            var end = EndedAtUtc ?? DateTime.UtcNow;
            return end - StartedAtUtc.Value;
        }
    }

    public event Action<SudokuPuzzleSummary>? PuzzleEnded;

    public void StartNewPuzzle()
    {
        EmitAbandonedSummaryIfNeeded();

        var index = DrawNextPuzzleIndex(SelectedDifficulty);
        CurrentPuzzle = puzzles[index];
        Board.LoadPuzzle(CurrentPuzzle);
        ResetRuntimeState();
        RefreshAnalysis();
    }

    public void ResetCurrentPuzzle()
    {
        if (string.IsNullOrEmpty(CurrentPuzzle.Id))
        {
            return;
        }

        Board.LoadPuzzle(CurrentPuzzle);
        ResetRuntimeState();
        RefreshAnalysis();
    }

    public void SetDifficulty(SudokuDifficulty difficulty, bool startNewPuzzle = true)
    {
        var normalized = NormalizeDifficulty(difficulty);
        if (SelectedDifficulty == normalized && !startNewPuzzle)
        {
            return;
        }

        SelectedDifficulty = normalized;
        if (startNewPuzzle)
        {
            StartNewPuzzle();
        }
    }

    public SudokuMoveResult SetCellValue(SudokuCoordinate coordinate, int? value)
    {
        if (!Board.IsInBounds(coordinate) || State == SudokuGameState.Completed || !CanEdit(coordinate))
        {
            return SudokuMoveResult.InvalidMove;
        }

        var normalizedValue = value.GetValueOrDefault();
        if (normalizedValue is < 0 or > 9)
        {
            return SudokuMoveResult.InvalidMove;
        }

        if (normalizedValue == 0)
        {
            if (Board.GetPlayerValue(coordinate) == 0)
            {
                return SudokuMoveResult.NoChange;
            }

            BeginPuzzleIfNeeded();
            Board.SetPlayerValue(coordinate, 0);
            HasProgress = Board.HasProgress();
            RefreshAnalysis();
            return SudokuMoveResult.ValueCleared;
        }

        if (Board.GetPlayerValue(coordinate) == normalizedValue)
        {
            return SudokuMoveResult.NoChange;
        }

        BeginPuzzleIfNeeded();
        Board.SetPlayerValue(coordinate, normalizedValue);
        HasProgress = Board.HasProgress();
        RefreshAnalysis();

        if (analysis.IsSolved)
        {
            State = SudokuGameState.Completed;
            EndedAtUtc = DateTime.UtcNow;
            PuzzleEnded?.Invoke(BuildSummary(SudokuPuzzleOutcome.Completed));
            return SudokuMoveResult.Completed;
        }

        return SudokuMoveResult.ValueSet;
    }

    public SudokuMoveResult ToggleNote(SudokuCoordinate coordinate, int value)
    {
        if (!Board.IsInBounds(coordinate) || State == SudokuGameState.Completed || !CanEdit(coordinate))
        {
            return SudokuMoveResult.InvalidMove;
        }

        if (value is < 1 or > 9)
        {
            return SudokuMoveResult.InvalidMove;
        }

        if (Board.GetPlayerValue(coordinate) != 0)
        {
            return SudokuMoveResult.InvalidMove;
        }

        BeginPuzzleIfNeeded();
        Board.ToggleNote(coordinate, value, out var isNowSet);
        HasProgress = Board.HasProgress();
        return isNowSet ? SudokuMoveResult.NoteAdded : SudokuMoveResult.NoteRemoved;
    }

    public void ClearNotes(SudokuCoordinate coordinate)
    {
        if (!Board.IsInBounds(coordinate) || State == SudokuGameState.Completed || !CanEdit(coordinate))
        {
            return;
        }

        if (!Board.ClearNotes(coordinate))
        {
            return;
        }

        BeginPuzzleIfNeeded();
        HasProgress = Board.HasProgress();
    }

    public bool CanEdit(SudokuCoordinate coordinate)
    {
        return Board.IsInBounds(coordinate) && !Board.IsGiven(coordinate);
    }

    private void BuildDifficultyPools()
    {
        pools.Add(SudokuDifficulty.Any, BuildPool(SudokuDifficulty.Any));
        pools.Add(SudokuDifficulty.Easy, BuildPool(SudokuDifficulty.Easy));
        pools.Add(SudokuDifficulty.Medium, BuildPool(SudokuDifficulty.Medium));
        pools.Add(SudokuDifficulty.Hard, BuildPool(SudokuDifficulty.Hard));
    }

    private DifficultyPoolState BuildPool(SudokuDifficulty difficulty)
    {
        var eligible = new List<int>(puzzles.Count);
        for (var index = 0; index < puzzles.Count; index++)
        {
            if (difficulty == SudokuDifficulty.Any || puzzles[index].Difficulty == difficulty)
            {
                eligible.Add(index);
            }
        }

        if (eligible.Count == 0)
        {
            for (var index = 0; index < puzzles.Count; index++)
            {
                eligible.Add(index);
            }
        }

        return new DifficultyPoolState(eligible);
    }

    private int DrawNextPuzzleIndex(SudokuDifficulty difficulty)
    {
        var pool = pools[difficulty];
        if (pool.RemainingIndexes.Count == 0)
        {
            pool.RemainingIndexes.AddRange(pool.EligibleIndexes);
        }

        var slot = random.Next(pool.RemainingIndexes.Count);
        var selected = pool.RemainingIndexes[slot];
        pool.RemainingIndexes.RemoveAt(slot);

        var selectedGroupTag = puzzles[selected].SelectionGroupTag;
        if (pool.EligibleIndexes.Count > 1
            && pool.LastSelectedGroupTag is not null
            && string.Equals(selectedGroupTag, pool.LastSelectedGroupTag, StringComparison.Ordinal))
        {
            var alternateSlot = FindAlternateSlotWithDifferentGroup(pool.RemainingIndexes, pool.LastSelectedGroupTag);
            if (alternateSlot >= 0)
            {
                var alternate = pool.RemainingIndexes[alternateSlot];
                pool.RemainingIndexes.RemoveAt(alternateSlot);
                pool.RemainingIndexes.Add(selected);
                selected = alternate;
                selectedGroupTag = puzzles[selected].SelectionGroupTag;
            }
            else
            {
                var alternate = FindAlternateEligibleIndexWithDifferentGroup(pool.EligibleIndexes, pool.LastSelectedGroupTag);
                if (alternate >= 0)
                {
                    if (pool.RemainingIndexes.Remove(alternate))
                    {
                        // Removed from the cycle pool so we don't pick it twice.
                    }

                    pool.RemainingIndexes.Add(selected);
                    selected = alternate;
                    selectedGroupTag = puzzles[selected].SelectionGroupTag;
                }
            }
        }

        pool.LastSelectedIndex = selected;
        pool.LastSelectedGroupTag = selectedGroupTag;
        return selected;
    }

    private int FindAlternateSlotWithDifferentGroup(List<int> candidates, string previousGroupTag)
    {
        var distinctSlots = new List<int>(candidates.Count);
        for (var slot = 0; slot < candidates.Count; slot++)
        {
            var candidateIndex = candidates[slot];
            var candidateGroup = puzzles[candidateIndex].SelectionGroupTag;
            if (!string.Equals(candidateGroup, previousGroupTag, StringComparison.Ordinal))
            {
                distinctSlots.Add(slot);
            }
        }

        return distinctSlots.Count == 0
            ? -1
            : distinctSlots[random.Next(distinctSlots.Count)];
    }

    private int FindAlternateEligibleIndexWithDifferentGroup(List<int> eligibleIndexes, string previousGroupTag)
    {
        var distinctIndexes = new List<int>(eligibleIndexes.Count);
        for (var i = 0; i < eligibleIndexes.Count; i++)
        {
            var candidateIndex = eligibleIndexes[i];
            var candidateGroup = puzzles[candidateIndex].SelectionGroupTag;
            if (!string.Equals(candidateGroup, previousGroupTag, StringComparison.Ordinal))
            {
                distinctIndexes.Add(candidateIndex);
            }
        }

        return distinctIndexes.Count == 0
            ? -1
            : distinctIndexes[random.Next(distinctIndexes.Count)];
    }

    private void BeginPuzzleIfNeeded()
    {
        StartedAtUtc ??= DateTime.UtcNow;
        EndedAtUtc = null;
        if (State == SudokuGameState.Ready)
        {
            State = SudokuGameState.InProgress;
        }
    }

    private void ResetRuntimeState()
    {
        State = SudokuGameState.Ready;
        StartedAtUtc = null;
        EndedAtUtc = null;
        HasProgress = false;
    }

    private void RefreshAnalysis()
    {
        analysis = SudokuBoardAnalysis.Analyze(Board, CurrentPuzzle.Solution);
    }

    private void EmitAbandonedSummaryIfNeeded()
    {
        if (string.IsNullOrEmpty(CurrentPuzzle.Id) || !HasProgress || State == SudokuGameState.Completed)
        {
            return;
        }

        EndedAtUtc ??= DateTime.UtcNow;
        PuzzleEnded?.Invoke(BuildSummary(SudokuPuzzleOutcome.Abandoned));
    }

    private SudokuPuzzleSummary BuildSummary(SudokuPuzzleOutcome outcome)
    {
        return new SudokuPuzzleSummary(
            outcome,
            CurrentPuzzle.Id,
            CurrentPuzzle.Difficulty,
            Elapsed,
            analysis.FilledCellCount,
            Board.GivenCellCount);
    }

    private static SudokuDifficulty NormalizeDifficulty(SudokuDifficulty difficulty)
    {
        return Enum.IsDefined(difficulty) ? difficulty : SudokuDifficulty.Any;
    }

    private sealed class DifficultyPoolState
    {
        public DifficultyPoolState(List<int> eligibleIndexes)
        {
            EligibleIndexes = eligibleIndexes;
            RemainingIndexes = [];
        }

        public List<int> EligibleIndexes { get; }
        public List<int> RemainingIndexes { get; }
        public int LastSelectedIndex { get; set; } = -1;
        public string? LastSelectedGroupTag { get; set; }
    }
}
