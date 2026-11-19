using System.Windows;

namespace Bael;

internal sealed class WanderMotion
{
    private Point? target;
    private Vector velocity;
    private double rest;
    public bool IsMoving { get; private set; }
    public double HorizontalSpeed => velocity.X;

    public void Stop()
    {
        velocity = default;
        IsMoving = false;
    }

    public Point Advance(Point position, Rect bounds, double seconds)
    {
        double dt = Math.Clamp(seconds, 0, 0.06);
        if (rest > 0)
        {
            rest -= dt;
            Stop();
            return position;
        }

        target ??= new Point(bounds.Left + Random.Shared.NextDouble() * bounds.Width,
                            bounds.Top + Random.Shared.NextDouble() * bounds.Height);
        Vector distance = target.Value - position;
        if (distance.Length < 8)
        {
            target = null;
            rest = 0.8 + Random.Shared.NextDouble() * 1.8;
            Stop();
            return position;
        }

        distance.Normalize();
        Vector desired = distance * 70;
        velocity += (desired - velocity) * (1 - Math.Exp(-dt * 5));
        Point next = position + velocity * dt;
        IsMoving = velocity.Length > 2;
        return new Point(Math.Clamp(next.X, bounds.Left, bounds.Right),
                         Math.Clamp(next.Y, bounds.Top, bounds.Bottom));
    }

    internal static void Verify()
    {
        var motion = new WanderMotion { target = new Point(160, 130) };
        var position = new Point(20, 20);
        var bounds = new Rect(0, 0, 180, 160);
        for (int index = 0; index < 40; index++)
            position = motion.Advance(position, bounds, 0.016);
        if (position.X <= 20 || position.Y <= 20 || !bounds.Contains(position))
            throw new InvalidOperationException("La caminata libre perdió el desplazamiento diagonal.");
        motion.Stop();
        if (motion.IsMoving || motion.HorizontalSpeed != 0)
            throw new InvalidOperationException("La mascota no se detuvo al interactuar.");
    }
}
