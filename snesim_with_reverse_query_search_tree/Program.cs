// Include the System.Diagnostics namespace for the Stopwatch class
using System.Diagnostics;

// Include custom namespaces for geometry algorithms and utilities
using JAM8.Algorithms.Geometry;
using JAM8.Utilities;


// Define the dimensions of the training image grid
int ti_nx = 101; // Number of cells in the X direction
int ti_ny = 101; // Number of cells in the Y direction
int ti_nz = 1; // Number of cells in the Z direction (2D case)

// Create a simple grid structure for the training image
GridStructure ti_gs = GridStructure.create_simple(ti_nx, ti_ny, ti_nz);

// Create a Grid object based on the grid structure
var ti_grid = Grid.create(ti_gs);

// Build a path relative to the executable's output folder.
// For .NET 8 the default layout is bin\Release\net8.0\, so going up three levels
// lands us back at the repository root.
string repoRoot = Path.GetFullPath(
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, // net8.0\
        "..", // bin\
        "..", // Release\
        "..")); // repository root

string tiPath = Path.Combine(repoRoot, "training images", "channel.out");

// Read training image data from a GSLIB file
// "channel.out" is the input file name
// 0 is the property index to read
// -99 is the null value marker
ti_grid.read_from_gslib(tiPath, 0, -99);

// Extract the first grid property from the training image grid
GridProperty ti = ti_grid.first_gridProperty();

// Set the random seed for reproducibility
int random_seed = 123456;

// Initialize progress tracking variable for inverse retrieval
int progress_for_retrieve_inverse = 0;

// Define parameters for the SNESIM algorithm
// Each tuple represents: (number of multi-grid levels, min template size, max template size, dilation factor)
List<(int, double, double, double)> snesim_paras =
[
    (40, 1, 1, 1), // First resolution level parameters
    (40, 1, 1, 1), // Second resolution level parameters
    (40, 1, 1, 1) // Third resolution level parameters
];

// Define the dimensions of the realization grid (same as training image)
int re_nx = 101;
int re_ny = 101;
int re_nz = 1;

// Create a grid structure for the realization
GridStructure re_gs = GridStructure.create_simple(re_nx, re_ny, re_nz);

// Create an empty grid property for the realization
GridProperty origin_re = GridProperty.create(re_gs);

// Start timing the simulation
var sw = Stopwatch.StartNew();

// Print a message to indicate the start of the simulation
MyConsoleHelper.write_value_to_console(
    $"= = = = = = = = = = = = = Start SNESIM (multi-resolution) simulation = = = = = = = = = = = = =");

// Run the multi-resolution SNESIM simulation
// Parameters:
// - origin_re.deep_clone(): A deep copy of the initial realization grid
// - ti: The training image property
// - snesim_paras: List of parameters for each resolution level
// - progress_for_retrieve_inverse: Progress tracking variable
// - random_seed: Seed for random number generation
var re = Snesim.run_multi_resolution(
    origin_re.deep_clone(),
    ti,
    snesim_paras,
    progress_for_retrieve_inverse,
    random_seed);

// Stop the timer after simulation completes
sw.Stop();

// Print the elapsed time in seconds with 3 decimal places
MyConsoleHelper.write_value_to_console(
    $"[SNESIM Simulation] Elapsed time: {sw.Elapsed.TotalSeconds:F3} seconds");

// Build output directory: repository-root/out
string outDir = Path.Combine(repoRoot, "out");
Directory.CreateDirectory(outDir);              // create the folder if it doesn't exist

string outPath = Path.Combine(outDir, "sim.out");

// Convert the resulting realization to a grid format
// Save it to a GSLIB file named "sim.out"
// "default_name" is the property name in the output file
// -99 is the null value marker
re.convert_to_grid().save_to_gslib(outPath, "default_name", -99);