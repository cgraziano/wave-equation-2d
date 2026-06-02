using System.Text;

/// <summary>
/// Runs all wave scenarios and writes the combined frames.js output file.
/// Extracted from Program.cs so the logic can be called directly from tests.
/// </summary>
public static class Simulator
{
    private const int TotalSteps    = 400;
    private const int FrameInterval = 4;   // save every Nth step → 100 frames

    public static void Run(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        var sb = new StringBuilder();

        // -------------------------------------------------------------------
        // Scenario 1 — Double-slit diffraction
        // -------------------------------------------------------------------
        Console.WriteLine("Running scenario 1: double-slit diffraction...");
        {
            const int W = 120, H = 120;
            var map = UniformMap(H, W);

            int barrierX = W / 2;
            int slitW    = 5;
            int slitGap  = 20;
            int centreY  = H / 2;
            int slit1Top = centreY - slitGap / 2 - slitW;
            int slit1Bot = centreY - slitGap / 2;
            int slit2Top = centreY + slitGap / 2;
            int slit2Bot = centreY + slitGap / 2 + slitW;

            for (int y = 0; y < H; y++)
            {
                bool inSlit1 = y >= slit1Top && y < slit1Bot;
                bool inSlit2 = y >= slit2Top && y < slit2Bot;
                if (!inSlit1 && !inSlit2)
                    map[y, barrierX] = 0.0;
            }

            var solver = new WaveSolver(map, dx: 1.0, dt: 0.4);
            solver.AddImpulse(cx: 20, cy: H / 2, magnitude: 1.0, sigma: 8.0);

            var frames = RunScenario(solver);
            AppendScenario(sb, "doubleSlit", "Double-Slit Diffraction",
                "A broad pulse passes through two narrow openings. " +
                "The wave fans out from each slit and the two wavefronts interfere, " +
                "producing bright bands (constructive) and dark bands (destructive).",
                W, H, frames);
            Console.WriteLine($"  → {frames.Count} frames");
        }

        // -------------------------------------------------------------------
        // Scenario 2 — Two-source interference
        // -------------------------------------------------------------------
        Console.WriteLine("Running scenario 2: two-source interference...");
        {
            const int W = 120, H = 120;
            var solver = new WaveSolver(W, H, waveSpeed: 1.0, dx: 1.0, dt: 0.4);

            int centreX    = W / 2;
            int centreY    = H / 2;
            int separation = 24;

            solver.AddImpulse(cx: centreX, cy: centreY - separation / 2, magnitude: 1.0, sigma: 2.0);
            solver.AddImpulse(cx: centreX, cy: centreY + separation / 2, magnitude: 1.0, sigma: 2.0);

            var frames = RunScenario(solver);
            AppendScenario(sb, "twoSource", "Two-Source Interference",
                "Two point sources pulse simultaneously, 24 cells apart. " +
                "Their circular wavefronts overlap: where crests meet crests the amplitude " +
                "doubles (constructive interference); where a crest meets a trough they cancel " +
                "(destructive interference), forming a symmetric star-shaped pattern.",
                W, H, frames);
            Console.WriteLine($"  → {frames.Count} frames");
        }

        // -------------------------------------------------------------------
        // Close the SCENARIOS object and write file
        // -------------------------------------------------------------------
        sb.AppendLine("};");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Wrote {outputPath}");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static List<string> RunScenario(WaveSolver solver)
    {
        int width    = solver.Amplitude.GetLength(1);
        int height   = solver.Amplitude.GetLength(0);
        double globalMax = double.Epsilon;
        var rawFrames = new List<double[]>();

        for (int t = 0; t < TotalSteps; t++)
        {
            solver.Step();
            if (t % FrameInterval == 0)
            {
                double[,] amp = solver.Amplitude;
                var frame = new double[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        double v = amp[y, x];
                        frame[y * width + x] = v;
                        if (Math.Abs(v) > globalMax) globalMax = Math.Abs(v);
                    }
                rawFrames.Add(frame);
            }
        }

        var encoded = new List<string>(rawFrames.Count);
        foreach (double[] frame in rawFrames)
        {
            var bytes = new byte[frame.Length];
            for (int i = 0; i < frame.Length; i++)
            {
                double normalised = frame[i] / globalMax;
                int quantised = (int)Math.Round(normalised * 127);
                bytes[i] = (byte)(Math.Clamp(quantised, -127, 127) + 128);
            }
            encoded.Add(Convert.ToBase64String(bytes));
        }
        return encoded;
    }

    private static void AppendScenario(StringBuilder sb, string key, string title,
        string description, int width, int height, List<string> frames)
    {
        if (sb.Length == 0)
        {
            sb.AppendLine("// Auto-generated by Simulator — do not edit by hand.");
            sb.AppendLine("const SCENARIOS = {");
        }

        sb.AppendLine($"  {key}: {{");
        sb.AppendLine($"    title: {JsonString(title)},");
        sb.AppendLine($"    description: {JsonString(description)},");
        sb.AppendLine($"    width: {width},");
        sb.AppendLine($"    height: {height},");
        sb.AppendLine($"    frameCount: {frames.Count},");
        sb.AppendLine("    frames: [");
        for (int i = 0; i < frames.Count; i++)
        {
            string comma = i < frames.Count - 1 ? "," : "";
            sb.AppendLine($"      \"{frames[i]}\"{comma}");
        }
        sb.AppendLine("    ]");
        sb.AppendLine("  },");
    }

    private static double[,] UniformMap(int height, int width, double speed = 1.0)
    {
        var map = new double[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map[y, x] = speed;
        return map;
    }

    private static string JsonString(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
