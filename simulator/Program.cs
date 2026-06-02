// Entry point — delegates entirely to Simulator.Run so the logic is
// testable without spawning a subprocess.
Simulator.Run(args.Length > 0 ? args[0] : "../web/frames.js");
