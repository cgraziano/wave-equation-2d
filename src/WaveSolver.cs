using System;

/// <summary>
/// Solves the 2D scalar wave equation using a finite-difference time-domain (FDTD) scheme.
///
/// The wave equation being solved:
///   ∂²u/∂t² = c(x,y)² · (∂²u/∂x² + ∂²u/∂y²)
///
/// where c(x,y) is a per-cell wave speed, allowing the material to vary across the grid.
///
/// Discretised with second-order central differences:
///   u[t+1] = 2*u[t] - u[t-1] + (c[y,x]*dt/dx)² * (∇²u)[t]
///
/// Stability condition: max(c) * dt / dx ≤ 1 / √2
/// </summary>
public class WaveSolver
{
    private readonly int _width;
    private readonly int _height;
    private readonly double _dx;          // spatial step (same for x and y)
    private readonly double _dt;          // time step (s)
    private readonly double[,] _r2;       // precomputed (c[y,x]*dt/dx)² per cell

    // Triple-buffered grids: previous, current, next
    private double[,] _prev;
    private double[,] _curr;
    private double[,] _next;

    /// <summary>
    /// Creates a solver with a uniform wave speed across the entire grid.
    /// </summary>
    /// <param name="width">Number of grid columns.</param>
    /// <param name="height">Number of grid rows.</param>
    /// <param name="waveSpeed">Uniform propagation speed c (grid-units per second).</param>
    /// <param name="dx">Spatial step size. Both axes use the same value.</param>
    /// <param name="dt">Time step. Must satisfy c*dt/dx ≤ 1/√2 for stability.</param>
    public WaveSolver(int width, int height, double waveSpeed = 1.0, double dx = 1.0, double dt = 0.4)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Grid dimensions must be positive.");

        double courant = waveSpeed * dt / dx;
        if (courant > 1.0 / Math.Sqrt(2.0))
            throw new ArgumentException($"Courant number {courant:F4} exceeds stability limit 1/√2 ≈ 0.7071. Reduce dt or waveSpeed, or increase dx.");

        _width = width;
        _height = height;
        _dx = dx;
        _dt = dt;

        _r2 = new double[height, width];
        double r2 = (waveSpeed * dt / dx) * (waveSpeed * dt / dx);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                _r2[y, x] = r2;

        _prev = new double[height, width];
        _curr = new double[height, width];
        _next = new double[height, width];
    }

    /// <summary>
    /// Creates a solver with a spatially varying wave speed.
    /// </summary>
    /// <param name="speedMap">
    /// Per-cell wave speed indexed as [row, col] i.e. [y, x].
    /// Set a cell to 0 to make it a hard wall (the wave cannot enter).
    /// The Courant condition is enforced against the maximum speed in the map.
    /// </param>
    /// <param name="dx">Spatial step size. Both axes use the same value.</param>
    /// <param name="dt">Time step. Must satisfy max(c)*dt/dx ≤ 1/√2 for stability.</param>
    public WaveSolver(double[,] speedMap, double dx = 1.0, double dt = 0.4)
    {
        _height = speedMap.GetLength(0);
        _width  = speedMap.GetLength(1);
        if (_width <= 0 || _height <= 0) throw new ArgumentException("Speed map dimensions must be positive.");

        _dx = dx;
        _dt = dt;

        // Validate stability against the maximum speed in the map
        double maxSpeed = 0.0;
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                if (speedMap[y, x] > maxSpeed) maxSpeed = speedMap[y, x];

        double courant = maxSpeed * dt / dx;
        if (courant > 1.0 / Math.Sqrt(2.0))
            throw new ArgumentException($"Courant number {courant:F4} (based on max speed {maxSpeed}) exceeds stability limit 1/√2 ≈ 0.7071. Reduce dt or the maximum speed, or increase dx.");

        // Precompute r² = (c[y,x]*dt/dx)² for each cell
        _r2 = new double[_height, _width];
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                double r = speedMap[y, x] * dt / dx;
                _r2[y, x] = r * r;
            }

        _prev = new double[_height, _width];
        _curr = new double[_height, _width];
        _next = new double[_height, _width];
    }

    /// <summary>
    /// Current amplitude grid (read-only view — do not mutate).
    /// Indexed as [row, col] i.e. [y, x].
    /// </summary>
    public double[,] Amplitude => _curr;

    /// <summary>
    /// Adds a Gaussian impulse at (cx, cy) to the current grid, simulating a point source.
    /// </summary>
    /// <param name="cx">Column index of the impulse centre.</param>
    /// <param name="cy">Row index of the impulse centre.</param>
    /// <param name="magnitude">Peak amplitude of the impulse.</param>
    /// <param name="sigma">Spatial spread (standard deviation in grid units).</param>
    public void AddImpulse(int cx, int cy, double magnitude = 1.0, double sigma = 3.0)
    {
        double twoSigmaSq = 2.0 * sigma * sigma;
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                // Skip hard wall cells — they must always remain zero.
                if (_r2[y, x] == 0.0) continue;

                double ddx = x - cx;
                double ddy = y - cy;
                _curr[y, x] += magnitude * Math.Exp(-(ddx * ddx + ddy * ddy) / twoSigmaSq);
            }
        }
    }

    /// <summary>
    /// Advances the simulation by one time step using the FDTD update rule
    /// with absorbing (Dirichlet zero) boundary conditions.
    /// </summary>
    public void Step()
    {
        for (int y = 1; y < _height - 1; y++)
        {
            for (int x = 1; x < _width - 1; x++)
            {
                double laplacian =
                    _curr[y, x + 1] + _curr[y, x - 1] +
                    _curr[y + 1, x] + _curr[y - 1, x] -
                    4.0 * _curr[y, x];

                _next[y, x] = 2.0 * _curr[y, x] - _prev[y, x] + _r2[y, x] * laplacian;
            }
        }

        // Boundaries remain zero (absorbing / Dirichlet)
        (_prev, _curr, _next) = (_curr, _next, _prev);
        Array.Clear(_next, 0, _next.Length);
    }

    /// <summary>
    /// Runs the solver for <paramref name="steps"/> time steps and returns the final amplitude grid.
    /// </summary>
    public double[,] SolveAmplitude(int steps)
    {
        for (int t = 0; t < steps; t++)
            Step();
        return Amplitude;
    }
}
