using Xunit;

public class WaveSolverTests
{
    // -------------------------------------------------------------------------
    // Construction / validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_UniformSpeed_DoesNotThrow()
    {
        var ex = Record.Exception(() => new WaveSolver(100, 100, waveSpeed: 1.0, dx: 1.0, dt: 0.4));
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_SpeedMap_DoesNotThrow()
    {
        var map = UniformMap(100, 100, 1.0);
        var ex = Record.Exception(() => new WaveSolver(map, dx: 1.0, dt: 0.4));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(1.0, 1.0, 0.8)]   // courant ≈ 0.8 > 0.7071
    [InlineData(2.0, 1.0, 0.4)]   // courant = 0.8
    public void Constructor_UniformSpeed_ThrowsWhenCourantViolated(double speed, double dx, double dt)
    {
        Assert.Throws<ArgumentException>(() => new WaveSolver(100, 100, speed, dx, dt));
    }

    [Fact]
    public void Constructor_SpeedMap_ThrowsWhenMaxSpeedViolatesCourant()
    {
        var map = UniformMap(100, 100, 2.0); // max speed 2.0, courant = 2*0.4/1 = 0.8
        Assert.Throws<ArgumentException>(() => new WaveSolver(map, dx: 1.0, dt: 0.4));
    }

    [Fact]
    public void Constructor_InvalidDimensions_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WaveSolver(0, 100));
        Assert.Throws<ArgumentException>(() => new WaveSolver(100, 0));
    }

    // -------------------------------------------------------------------------
    // Initial state
    // -------------------------------------------------------------------------

    [Fact]
    public void Amplitude_InitiallyAllZero()
    {
        var solver = new WaveSolver(50, 50);
        AssertAllZero(solver.Amplitude);
    }

    // -------------------------------------------------------------------------
    // Impulse
    // -------------------------------------------------------------------------

    [Fact]
    public void AddImpulse_PeakAtCentre()
    {
        var solver = new WaveSolver(51, 51);
        solver.AddImpulse(cx: 25, cy: 25, magnitude: 1.0, sigma: 3.0);

        double peak = solver.Amplitude[25, 25];
        Assert.True(peak > 0.99, $"Expected peak ≈ 1.0, got {peak}");
    }

    [Fact]
    public void AddImpulse_AmplitudeDecaysWithDistance()
    {
        var solver = new WaveSolver(51, 51);
        solver.AddImpulse(cx: 25, cy: 25, magnitude: 1.0, sigma: 3.0);

        double atCentre = solver.Amplitude[25, 25];
        double atEdge   = solver.Amplitude[25, 40];
        Assert.True(atCentre > atEdge, "Amplitude should be higher at impulse centre than far away.");
    }

    // -------------------------------------------------------------------------
    // Symmetry
    // -------------------------------------------------------------------------

    [Fact]
    public void Step_CentredImpulse_RemainsSymmetricAfterPropagation()
    {
        int size = 101;
        int mid  = size / 2;
        var solver = new WaveSolver(size, size, waveSpeed: 1.0, dx: 1.0, dt: 0.4);
        solver.AddImpulse(cx: mid, cy: mid);

        for (int t = 0; t < 20; t++) solver.Step();

        double[,] a = solver.Amplitude;
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                int mirrorX = size - 1 - x;
                int mirrorY = size - 1 - y;
                Assert.Equal(a[y, x], a[y, mirrorX], precision: 10);
                Assert.Equal(a[y, x], a[mirrorY, x], precision: 10);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Wave speed / propagation
    // -------------------------------------------------------------------------

    [Fact]
    public void Step_WavefrontReachesExpectedDistanceAtExpectedTime()
    {
        // Place impulse well away from all boundaries so the Dirichlet edges
        // do not interfere with the wavefront. Measure arrival at a target
        // directly to the right at distance d.
        int size = 300;
        double waveSpeed = 1.0, dx = 1.0, dt = 0.4;
        int sourceX = 100;
        int targetX = 140;  // distance d = 40 cells
        int midY    = size / 2;

        var solver = new WaveSolver(size, size, waveSpeed, dx, dt);
        // Use a tight sigma so the initial Gaussian does not already overlap the target.
        solver.AddImpulse(cx: sourceX, cy: midY, sigma: 1.0);

        // Expected arrival step: d / (c * dt) = 40 / (1.0 * 0.4) = 100
        int expectedStep = (int)((targetX - sourceX) / (waveSpeed * dt));

        // Run until well past the expected arrival
        int arrivalStep = -1;
        for (int t = 0; t < expectedStep + 40; t++)
        {
            solver.Step();
            if (arrivalStep < 0 && Math.Abs(solver.Amplitude[midY, targetX]) > 1e-6)
                arrivalStep = t;
        }

        Assert.True(arrivalStep >= 0, "Wavefront never arrived at target cell.");

        // Allow ±35% tolerance around the expected arrival step
        double tolerance = expectedStep * 0.35;
        Assert.InRange(arrivalStep, expectedStep - tolerance, expectedStep + tolerance);
    }

    // -------------------------------------------------------------------------
    // Stability
    // -------------------------------------------------------------------------

    [Fact]
    public void Step_ManySteps_AmplitudeRemainsFinite()
    {
        var solver = new WaveSolver(100, 100, waveSpeed: 1.0, dx: 1.0, dt: 0.4);
        solver.AddImpulse(cx: 50, cy: 50, magnitude: 1.0);

        for (int t = 0; t < 500; t++) solver.Step();

        double[,] a = solver.Amplitude;
        for (int y = 0; y < 100; y++)
            for (int x = 0; x < 100; x++)
                Assert.True(double.IsFinite(a[y, x]), $"Non-finite amplitude at [{y},{x}] after 500 steps.");
    }

    // -------------------------------------------------------------------------
    // Hard walls (zero-speed cells)
    // -------------------------------------------------------------------------

    [Fact]
    public void Step_ZeroSpeedCells_AlwaysZeroAmplitude()
    {
        int size = 50;
        var map = UniformMap(size, size, 1.0);

        // Vertical wall at x = 25
        for (int y = 0; y < size; y++) map[y, 25] = 0.0;

        var solver = new WaveSolver(map, dx: 1.0, dt: 0.4);
        solver.AddImpulse(cx: 10, cy: size / 2);

        for (int t = 0; t < 100; t++) solver.Step();

        for (int y = 0; y < size; y++)
            Assert.Equal(0.0, solver.Amplitude[y, 25]);
    }

    // -------------------------------------------------------------------------
    // SolveAmplitude convenience method
    // -------------------------------------------------------------------------

    [Fact]
    public void SolveAmplitude_ReturnsNonZeroGridAfterPropagation()
    {
        var solver = new WaveSolver(100, 100);
        solver.AddImpulse(cx: 50, cy: 50);

        double[,] result = solver.SolveAmplitude(steps: 10);

        double maxAbs = 0.0;
        foreach (double v in result)
            if (Math.Abs(v) > maxAbs) maxAbs = Math.Abs(v);

        Assert.True(maxAbs > 0.0, "Expected non-zero amplitude after propagation.");
    }

    [Fact]
    public void SolveAmplitude_ZeroSteps_ReturnsCurrentState()
    {
        var solver = new WaveSolver(50, 50);
        solver.AddImpulse(cx: 25, cy: 25, magnitude: 1.0, sigma: 3.0);

        double before = solver.Amplitude[25, 25];
        double[,] result = solver.SolveAmplitude(steps: 0);

        Assert.Equal(before, result[25, 25]);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static double[,] UniformMap(int height, int width, double speed)
    {
        var map = new double[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map[y, x] = speed;
        return map;
    }

    private static void AssertAllZero(double[,] grid)
    {
        for (int y = 0; y < grid.GetLength(0); y++)
            for (int x = 0; x < grid.GetLength(1); x++)
                Assert.Equal(0.0, grid[y, x]);
    }
}
