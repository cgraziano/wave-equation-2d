# wave-equation-2d
Displays a 2D wave that expands out from a clicked point and interacts with boundaries.

## Live viewer

The wave simulation is automatically built and deployed to GitHub Pages on every push to `main`. Visit:

```
https://cgraziano.github.io/wave-equation-2d/
```

Controls: **Space** = play/pause · **← →** = step one frame · scrubber = jump to any frame

## Running the tests

### GitHub Actions (recommended)

Tests run automatically in the cloud on every push or pull request to `main` — no local setup required. Results are visible under the **Actions** tab of your GitHub repository.

### Manual (if you already have .NET 8 SDK installed)

```
dotnet test
```

Run from the repository root. It will build all projects and execute every test in `tests/`.
