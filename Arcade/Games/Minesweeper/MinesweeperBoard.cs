using System;
using System.Collections.Generic;

namespace Arcade.Games.Minesweeper;

public sealed class MinesweeperBoard
{
    private readonly MinesweeperTile[,] tiles;

    public MinesweeperBoard(MinesweeperGameSettings settings)
        : this(settings.Width, settings.Height, settings.MineCount)
    {
    }

    public MinesweeperBoard(int width, int height, int mineCount)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Board width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Board height must be greater than zero.");
        }

        if (mineCount < 0 || mineCount >= width * height)
        {
            throw new ArgumentOutOfRangeException(nameof(mineCount), "Mine count must be between 0 and board area - 1.");
        }

        Width = width;
        Height = height;
        MineCount = mineCount;

        tiles = new MinesweeperTile[Width, Height];
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                tiles[x, y] = new MinesweeperTile();
            }
        }

        Clear();
    }

    public int Width { get; }
    public int Height { get; }
    public int MineCount { get; }
    public int SafeTileCount => (Width * Height) - MineCount;

    public void Clear()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                tiles[x, y].Reset();
            }
        }
    }

    public bool IsInBounds(MinesweeperCoordinate coordinate)
    {
        return coordinate.X >= 0 && coordinate.X < Width && coordinate.Y >= 0 && coordinate.Y < Height;
    }

    public MinesweeperTile GetTile(MinesweeperCoordinate coordinate)
    {
        if (!IsInBounds(coordinate))
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Tile coordinate is outside the board.");
        }

        return tiles[coordinate.X, coordinate.Y];
    }

    public IEnumerable<MinesweeperCoordinate> GetAllCoordinates()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                yield return new MinesweeperCoordinate(x, y);
            }
        }
    }

    public IEnumerable<MinesweeperCoordinate> GetNeighbors(MinesweeperCoordinate coordinate)
    {
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var neighbor = new MinesweeperCoordinate(coordinate.X + offsetX, coordinate.Y + offsetY);
                if (IsInBounds(neighbor))
                {
                    yield return neighbor;
                }
            }
        }
    }

    public void PlaceMines(Random random, MinesweeperCoordinate safeCoordinate)
    {
        PlaceMines(random, safeCoordinate, 0);
    }

    public void PlaceMines(Random random, MinesweeperCoordinate safeCoordinate, int safeRadius)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!IsInBounds(safeCoordinate))
        {
            throw new ArgumentOutOfRangeException(nameof(safeCoordinate), "Safe coordinate is outside the board.");
        }
        
        if (safeRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(safeRadius), "Safe radius must be zero or greater.");
        }

        Clear();

        var effectiveSafeRadius = safeRadius;
        while (effectiveSafeRadius > 0 && !CanFitMinesWithSafeZone(safeCoordinate, effectiveSafeRadius))
        {
            effectiveSafeRadius--;
        }

        if (!CanFitMinesWithSafeZone(safeCoordinate, effectiveSafeRadius))
        {
            throw new InvalidOperationException("Unable to place mines with the provided safe zone constraints.");
        }

        var candidates = new List<MinesweeperCoordinate>((Width * Height) - 1);
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var coordinate = new MinesweeperCoordinate(x, y);
                if (!IsInSafeZone(coordinate, safeCoordinate, effectiveSafeRadius))
                {
                    candidates.Add(coordinate);
                }
            }
        }

        for (var i = 0; i < MineCount; i++)
        {
            var selectedIndex = random.Next(i, candidates.Count);
            (candidates[i], candidates[selectedIndex]) = (candidates[selectedIndex], candidates[i]);

            var mineCoordinate = candidates[i];
            tiles[mineCoordinate.X, mineCoordinate.Y].HasMine = true;
        }

        RecalculateAdjacentMineCounts();
    }

    private bool CanFitMinesWithSafeZone(MinesweeperCoordinate safeCoordinate, int safeRadius)
    {
        var protectedCount = 0;
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var coordinate = new MinesweeperCoordinate(x, y);
                if (IsInSafeZone(coordinate, safeCoordinate, safeRadius))
                {
                    protectedCount++;
                }
            }
        }

        var candidateCount = (Width * Height) - protectedCount;
        return candidateCount >= MineCount;
    }

    private static bool IsInSafeZone(MinesweeperCoordinate coordinate, MinesweeperCoordinate safeCoordinate, int safeRadius)
    {
        return Math.Abs(coordinate.X - safeCoordinate.X) <= safeRadius &&
               Math.Abs(coordinate.Y - safeCoordinate.Y) <= safeRadius;
    }

    private void RecalculateAdjacentMineCounts()
    {
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var tile = tiles[x, y];
                if (tile.HasMine)
                {
                    tile.AdjacentMineCount = 0;
                    continue;
                }

                var minX = Math.Max(0, x - 1);
                var maxX = Math.Min(Width - 1, x + 1);
                var minY = Math.Max(0, y - 1);
                var maxY = Math.Min(Height - 1, y + 1);

                var adjacentMineCount = 0;
                for (var neighborX = minX; neighborX <= maxX; neighborX++)
                {
                    for (var neighborY = minY; neighborY <= maxY; neighborY++)
                    {
                        if (neighborX == x && neighborY == y)
                        {
                            continue;
                        }

                        if (tiles[neighborX, neighborY].HasMine)
                        {
                            adjacentMineCount++;
                        }
                    }
                }

                tile.AdjacentMineCount = adjacentMineCount;
            }
        }
    }
}
