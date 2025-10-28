# SNESIM with Reverse Query Search Tree (C# Implementation)

This project presents a high-performance implementation of **SNESIM (Single Normal Equation Simulation)** in **C#**, featuring a hybrid query mechanism designed to improve efficiency under sparse conditioning data.

---

## Key Innovation — Reverse Query Mechanism

1. Traditional SNESIM algorithms rely on **forward traversal** of the search tree (STree) to retrieve conditional probabilities.
2. However, at the **early stage of simulation**, when only a few conditioning data points exist around each prediction node,forward queries require exploring a large number of possible combinations, resulting in **high traversal cost** and **low efficiency**.
3. To address this, the present implementation introduces a **Reverse Query Search Tree (R-STree)** with a corresponding **Reverse Auxiliary Retrieval Structure (RARS)**. Together, they enable fast, parallelizable pattern retrieval.

---

## Principle of Reverse Query

**1. Early-stage optimization (≈ first 30–35%)**
When few conditioning nodes exist, the algorithm switches from forward to **reverse query** mode.

**2. Reverse query process**

* Identify the **farthest conditioning point** in the data event.
* Use its value and relative position to directly access matching nodes in the RARS at the corresponding tree depth.
* Verify whether these candidate nodes satisfy all remaining conditioning values.
* If no full match is found, remove the farthest point and repeat until sufficient matches are collected.
* Count the frequencies of central-node values and sample from the resulting conditional probability distribution.

**3. Transition to forward query**
As simulation progresses and local conditioning data become denser, the algorithm automatically switches back to the **standard forward query**, where early pruning becomes efficient.

---

## Reverse Auxiliary Retrieval Structure (RARS)

The **RARS** is a level-wise dictionary structure built on top of the STree:

| Component   | Description                                                                           |
| ----------- | ------------------------------------------------------------------------------------- |
| **Key**     | Attribute value at a given tree level                                                 |
| **Value**   | Set of all tree nodes at that level sharing the same key                              |
| **Purpose** | Enables constant-time access to all candidate nodes for a specific conditioning value |

This structure avoids scanning the full STree and allows **parallel filtering** at each level, making the reverse query inherently suitable for **multi-core execution**.

---

## Performance

| Simulation Strategy                       | Description     | Typical Speed-up |
| ----------------------------------------- | --------------- | ---------------- |
| **Hybrid (Reverse 30–35%, then Forward)** | Proposed method | **4×–10×**       |

- Empirical tests on 2D and 3D training images indicate that the hybrid query achieves an excellent balance between efficiency and stability.
- For small training images or templates, the search tree is relatively small, leading to a moderate acceleration (≈4×).
- As the tree size and spatial structure complexity increase, the acceleration becomes more pronounced, reaching up to 9.5×.

---

## Summary of Advantages

✅ Efficient probability retrieval and reduced tree traversal cost in sparse-data conditions
✅ Flexible control between reverse and forward search modes based on user-defined ratios
✅ Significant efficiency gain through parallel reverse-query processing
✅ Fully compatible with standard SNESIM workflow and templates

---

## Code Usage

### Dependencies

| Library    | Description                  |
| ---------- | ---------------------------- |
| `.NET 8.0` | Required runtime environment |

All dependencies are included within the repository — no external NuGet packages are required.

---

### Example Usage

Below is a **minimal working example** performing a 2D stochastic simulation
with **reverse query + multi-resolution pyramid acceleration**.

```csharp
using System;
using System.Diagnostics;
using JAM8.Algorithms.Geometry;
using JAM8.Utilities;

// Define training image grid size
int ti_nx = 101, ti_ny = 101, ti_nz = 1;
GridStructure ti_gs = GridStructure.create_simple(ti_nx, ti_ny, ti_nz);
var ti_grid = Grid.create(ti_gs);

// Locate training image file (relative to repo root)
string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
string tiPath = Path.Combine(repoRoot, "training images", "channel.out");

// Read training image from GSLIB file
ti_grid.read_from_gslib(tiPath, 0, -99);
GridProperty ti = ti_grid.first_gridProperty();

// Define simulation grid (same size as TI)
GridStructure re_gs = GridStructure.create_simple(ti_nx, ti_ny, ti_nz);
GridProperty origin_re = GridProperty.create(re_gs);

// Simulation parameters
List<(int, double, double, double)> snesim_paras =
[
    (40, 1, 1, 1),
    (40, 1, 1, 1),
    (40, 1, 1, 1)
];

int random_seed = 123456;
int progress_for_retrieve_inverse = 0;

// Start simulation
var sw = Stopwatch.StartNew();
MyConsoleHelper.write_value_to_console("===== Start SNESIM with Reverse Query =====");

var re = Snesim.run_multi_resolution(
    origin_re.deep_clone(),
    ti,
    snesim_paras,
    progress_for_retrieve_inverse,
    random_seed
);

sw.Stop();
MyConsoleHelper.write_value_to_console($"[SNESIM Simulation] Elapsed time: {sw.Elapsed.TotalSeconds:F3} seconds");

// Save result
string outDir = Path.Combine(repoRoot, "out");
Directory.CreateDirectory(outDir);
string outPath = Path.Combine(outDir, "sim.out");
re.convert_to_grid().save_to_gslib(outPath, "default_name", -99);
```

**Output file:**
`out/sim.out` — in GSLIB format, containing the simulated facies grid.

---

### Repository Structure

```
snesim_with_reverse_query_search_tree/
│
├── training images/
│   └── channel.out              # Example GSLIB training image
│
├── out/
│   └── sim.out                  # Simulation output
│
├── JAM8/
│   ├── Algorithms/
│   │   ├── Geometry/            # Grid, STree, Pyramid, Mould
│   │   └── Numerics/            # Random generator, statistics
│   └── Utilities/               # Console and I/O helpers
│
├── Program.cs                   # Example entry point (as above)
└── README.md                    # This file
```

---

### Build and Run

```bash
# Clone repository
git clone https://github.com/Yusiyu0601/snesim_with_reverse_query_search_tree.git
cd snesim_with_reverse_query_search_tree

# Build in Release mode
dotnet build -c Release

# Run example
dotnet run --project JAM8.Algorithms.Geometry
```

---

## Integration Notes

1. 
By extending the spatial-correlation control range level by level, it drastically reduces the number of iterations needed for large-scale structures.
2. 
Inside every level the reverse-query mechanism remains the core innovation; it can run stand-alone or be seamlessly combined with the pyramid workflow.
3. 
Conceptually this multi-resolution strategy is identical to classical multigrid ideas—its sole purpose is efficiency, introducing no additional theoretical difference.

---

## Reference

> Yusiyu (2025). *SNESIM with Reverse Query Search Tree (C# Implementation).*
> JAM8 Geological Modeling Library.
> [https://github.com/Yusiyu0601/snesim_with_reverse_query_search_tree](https://github.com/Yusiyu0601/snesim_with_reverse_query_search_tree)

