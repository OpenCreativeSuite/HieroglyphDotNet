namespace HieroglyphDotNet.Core;

/// <summary>A point in font units (y up) or in device pixels, depending on context.</summary>
public readonly struct Vec2(float x, float y) : IEquatable<Vec2>
{
    /// <summary>The x component.</summary>
    public float X { get; } = x;

    /// <summary>The y component.</summary>
    public float Y { get; } = y;

    /// <summary>The origin.</summary>
    public static Vec2 Zero => new(0f, 0f);

    /// <summary>The vector length.</summary>
    public float Length => MathF.Sqrt((X * X) + (Y * Y));

    /// <summary>Adds two vectors.</summary>
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Subtracts two vectors.</summary>
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Negates a vector.</summary>
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);

    /// <summary>Scales a vector.</summary>
    public static Vec2 operator *(Vec2 a, float scale) => new(a.X * scale, a.Y * scale);

    /// <summary>Scales a vector.</summary>
    public static Vec2 operator *(float scale, Vec2 a) => a * scale;

    /// <summary>Divides a vector.</summary>
    public static Vec2 operator /(Vec2 a, float scale) => new(a.X / scale, a.Y / scale);

    /// <summary>Moves the point by a delta.</summary>
    public Vec2 Offset(float dx, float dy) => new(X + dx, Y + dy);

    /// <summary>The distance to another point.</summary>
    public float DistanceTo(Vec2 other) => (this - other).Length;

    /// <summary>The distance to another point, squared.</summary>
    public float DistanceSquared(Vec2 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>The point rotated by a quarter turn.</summary>
    public Vec2 Perp => new(-Y, X);

    /// <summary>A unit vector, or zero when the vector has no length.</summary>
    public Vec2 Normalized()
    {
        float length = Length;
        return length < 1e-6f ? Zero : this / length;
    }

    /// <summary>Linear interpolation between two points.</summary>
    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + ((b - a) * t);

    /// <summary>Rounds to whole font units.</summary>
    public Vec2 Rounded() => new(MathF.Round(X), MathF.Round(Y));

    /// <summary>Rotates the point around an origin.</summary>
    public Vec2 Rotated(Vec2 origin, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        float dx = X - origin.X;
        float dy = Y - origin.Y;
        return new Vec2(
            origin.X + (dx * cos) - (dy * sin),
            origin.Y + (dx * sin) + (dy * cos));
    }

    /// <inheritdoc/>
    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc/>
    public override string ToString() => $"{X:0.##}, {Y:0.##}";

    /// <summary>Exact equality.</summary>
    public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);

    /// <summary>Exact inequality.</summary>
    public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);
}

/// <summary>An axis-aligned rectangle in font units.</summary>
public readonly struct Bounds(float minX, float minY, float maxX, float maxY)
{
    /// <summary>The smallest x.</summary>
    public float MinX { get; } = minX;

    /// <summary>The smallest y.</summary>
    public float MinY { get; } = minY;

    /// <summary>The largest x.</summary>
    public float MaxX { get; } = maxX;

    /// <summary>The largest y.</summary>
    public float MaxY { get; } = maxY;

    /// <summary>An empty rectangle that absorbs any point it is given.</summary>
    public static Bounds Empty => new(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

    /// <summary>A zero rectangle at the origin.</summary>
    public static Bounds Zero => new(0f, 0f, 0f, 0f);

    /// <summary>The width.</summary>
    public float Width => MathF.Max(0f, MaxX - MinX);

    /// <summary>The height.</summary>
    public float Height => MathF.Max(0f, MaxY - MinY);

    /// <summary>The centre point.</summary>
    public Vec2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

    /// <summary>Whether the rectangle holds no area.</summary>
    public bool IsEmpty => MaxX < MinX || MaxY < MinY || Width <= 0f || Height <= 0f;

    /// <summary>Grows the rectangle to include a point.</summary>
    public Bounds With(Vec2 point) => new(
        MathF.Min(MinX, point.X),
        MathF.Min(MinY, point.Y),
        MathF.Max(MaxX, point.X),
        MathF.Max(MaxY, point.Y));

    /// <summary>Grows the rectangle to include another rectangle.</summary>
    public Bounds Union(Bounds other) => other.IsEmpty
        ? this
        : new Bounds(
            MathF.Min(MinX, other.MinX),
            MathF.Min(MinY, other.MinY),
            MathF.Max(MaxX, other.MaxX),
            MathF.Max(MaxY, other.MaxY));

    /// <summary>Expands the rectangle by the same amount on every side.</summary>
    public Bounds Inflate(float amount) => new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);

    /// <summary>Whether a point lies inside.</summary>
    public bool Contains(Vec2 point) =>
        point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

    /// <summary>Whether a point lies inside, with a tolerance.</summary>
    public bool Contains(Vec2 point, float tolerance) =>
        point.X >= MinX - tolerance && point.X <= MaxX + tolerance
        && point.Y >= MinY - tolerance && point.Y <= MaxY + tolerance;

    /// <summary>Whether two rectangles overlap.</summary>
    public bool Intersects(Bounds other) =>
        MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;

    /// <summary>Moves the rectangle.</summary>
    public Bounds Translate(float dx, float dy) => new(MinX + dx, MinY + dy, MaxX + dx, MaxY + dy);

    /// <inheritdoc/>
    public override string ToString() => $"({MinX:0.##}, {MinY:0.##}) – ({MaxX:0.##}, {MaxY:0.##})";
}

/// <summary>Maps between font units (y up) and canvas pixels (y down).</summary>
public readonly struct GlyphView
{
    /// <summary>Creates a view.</summary>
    public GlyphView(float zoom, float panX, float panY)
    {
        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    /// <summary>Font units per pixel multiplier.</summary>
    public float Zoom { get; }

    /// <summary>Canvas x of the glyph origin.</summary>
    public float PanX { get; }

    /// <summary>Canvas y of the baseline.</summary>
    public float PanY { get; }

    /// <summary>Converts font units to canvas pixels.</summary>
    public Vec2 ToScreen(Vec2 point) => new((point.X * Zoom) + PanX, PanY - (point.Y * Zoom));

    /// <summary>Converts font units to canvas pixels with a y offset.</summary>
    public Vec2 ToScreen(float x, float y) => new((x * Zoom) + PanX, PanY - (y * Zoom));

    /// <summary>Converts canvas pixels to font units.</summary>
    public Vec2 ToUnits(Vec2 point) => new((point.X - PanX) / Zoom, (PanY - point.Y) / Zoom);

    /// <summary>A view re-centred on the same baseline without changing zoom.</summary>
    public GlyphView WithPan(float panX, float panY) => new(Zoom, panX, panY);
}
