namespace ERSwapper.Core;

public readonly record struct ViewportRange(int Start, int Count)
{
    public int EndExclusive => Start + Count;

    public bool IsEmpty => Count <= 0;
}

public static class ViewportMath
{
    public const int LookaheadScreens = 1;

    public static int TilesPerRow(int clientWidth, int tileWidth)
    {
        if (clientWidth <= 0 || tileWidth <= 0) return 1;
        return Math.Max(1, clientWidth / tileWidth);
    }

    public static int RowsVisible(int clientHeight, int tileHeight)
    {
        if (clientHeight <= 0 || tileHeight <= 0) return 1;
        return Math.Max(1, (int)Math.Ceiling((double)clientHeight / tileHeight));
    }

    public static ViewportRange Compute(int topIndex, int itemCount, int tilesPerRow, int rowsVisible)
    {
        if (itemCount <= 0) return new ViewportRange(0, 0);

        tilesPerRow = Math.Max(1, tilesPerRow);
        rowsVisible = Math.Max(1, rowsVisible);

        int onScreen = tilesPerRow * rowsVisible;
        int budget = onScreen * (1 + LookaheadScreens);

        int start = Math.Clamp(topIndex, 0, Math.Max(0, itemCount - 1));
        int count = Math.Min(budget, itemCount - start);

        return new ViewportRange(start, Math.Max(0, count));
    }
}
