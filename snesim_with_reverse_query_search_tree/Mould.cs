using System.Collections.Concurrent;
using JAM8.Algorithms.Numerics;
using JAM8.Utilities;
using static System.Math;

namespace JAM8.Algorithms.Geometry
{
    /// <summary>
    /// 模板类（优化版）。
    /// 
    /// 📌 特点：
    /// - 内部使用并行数组存储（dx/dy/dz/dist），比 List<(float, SpatialIndex)> 更高效。
    /// - 去掉 LINQ，避免在热点路径中频繁分配对象。
    /// - 所有邻居点是相对于中心点（core）的位置偏移，核心点本身不包含在邻居列表中。
    /// 
    /// ✅ 用途：
    ///   用于 Direct Sampling / MPS 模拟时的邻居模板定义与快速取值。
    ///   - 核心点（core）作为参考点，邻居点以核心点为基准进行偏移，不包含核心点本身。
    ///   - 通过 `dx`, `dy`, `dz` 数组存储邻居点相对核心点的偏移量，确保高效的邻居查找和存储。
    /// </summary>
    public class Mould
    {
        public Dimension dim { get; internal set; }

        public int neighbors_number => _count;

        // ====== 内部存储（并行数组）======
        internal int[] _dx; // 相对 core 的偏移
        internal int[] _dy;
        internal int[] _dz; // 2D 时为 null
        internal float[] _dist;
        internal int _count;

        private Mould()
        {
        }

        /// <summary>
        /// 获取邻居偏移范围（相对 core 的 min/max）
        /// </summary>
        public (int min_dx, int max_dx, int min_dy, int max_dy, int? min_dz, int? max_dz) get_offset_range()
        {
            if (_count == 0)
                throw new InvalidOperationException("Neighbor list is empty.");

            int min_dx = _dx[0], max_dx = _dx[0];
            int min_dy = _dy[0], max_dy = _dy[0];
            int min_dz = 0, max_dz = 0;

            for (int i = 1; i < _count; i++)
            {
                if (_dx[i] < min_dx) min_dx = _dx[i];
                if (_dx[i] > max_dx) max_dx = _dx[i];

                if (_dy[i] < min_dy) min_dy = _dy[i];
                if (_dy[i] > max_dy) max_dy = _dy[i];

                if (dim == Dimension.D3)
                {
                    int dz = _dz[i];
                    if (i == 1) min_dz = max_dz = dz;
                    else
                    {
                        if (dz < min_dz) min_dz = dz;
                        if (dz > max_dz) max_dz = dz;
                    }
                }
            }

            return dim == Dimension.D2
                ? (min_dx, max_dx, min_dy, max_dy, null, null)
                : (min_dx, max_dx, min_dy, max_dy, min_dz, max_dz);
        }


        public static Mould create_by_location(SpatialIndex core, List<SpatialIndex> neighbors)
        {
            if (core == null) throw new ArgumentNullException(nameof(core));
            if (neighbors == null || neighbors.Count == 0)
                throw new ArgumentException("Neighbors cannot be null or empty.", nameof(neighbors));

            var dim = core.dim;
            var seen = new HashSet<(int, int, int)>();
            var tmp = new List<(int dx, int dy, int dz, float dist)>(neighbors.Count);

            if (dim == Dimension.D2)
            {
                int cx = core.ix, cy = core.iy;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var n = neighbors[i];

                    int dx = n.ix - cx, dy = n.iy - cy;
                    if (dx == 0 && dy == 0)
                        continue;

                    var key = (dx, dy, 0);
                    if (!seen.Add(key))
                        continue;

                    float d = (float)Sqrt((double)dx * dx + (double)dy * dy);
                    tmp.Add((dx, dy, 0, d));
                }
            }
            else
            {
                int cx = core.ix, cy = core.iy, cz = core.iz;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var n = neighbors[i];

                    int dx = n.ix - cx, dy = n.iy - cy, dz = n.iz - cz;
                    if (dx == 0 && dy == 0 && dz == 0)
                        continue;

                    var key = (dx, dy, dz);
                    if (!seen.Add(key))
                        continue;

                    float d = (float)Sqrt((double)dx * dx + (double)dy * dy + (double)dz * dz);
                    tmp.Add((dx, dy, dz, d));
                }
            }

            if (tmp.Count == 0)
                throw new InvalidOperationException("Neighbor list becomes empty after normalization.");

            // 距离升序 + 坐标字典序，等价于你原先的 OrderBy + ThenBy...
            tmp.Sort((a, b) =>
            {
                int c = a.dist.CompareTo(b.dist);
                if (c != 0) return c;
                c = a.dx.CompareTo(b.dx);
                if (c != 0) return c;
                c = a.dy.CompareTo(b.dy);
                if (c != 0) return c;
                return a.dz.CompareTo(b.dz);
            });

            int N = tmp.Count;
            var mould = new Mould
            {
                dim = dim,
                _dx = new int[N],
                _dy = new int[N],
                _dz = (dim == Dimension.D3) ? new int[N] : null,
                _dist = new float[N],
                _count = N
            };
            for (int i = 0; i < N; i++)
            {
                mould._dx[i] = tmp[i].dx;
                mould._dy[i] = tmp[i].dy;
                if (mould._dz != null) mould._dz[i] = tmp[i].dz;
                mould._dist[i] = tmp[i].dist;
            }

            return mould;
        }

        /// <summary>
        /// 创建二维各向异性模板（只保留 K 个点）
        /// 在二维平面上根据不同方向的比例，取距离最近的 K 个邻居点。
        /// </summary>
        /// <param name="k">模板中邻居点数量</param>
        /// <param name="ratioX">X 方向比例（越大 → X 方向拉伸）</param>
        /// <param name="ratioY">Y 方向比例</param>
        /// <param name="multi_grid">多重网格层级（>=1）</param>
        /// <returns>二维各向异性 Mould 模板</returns>
        public static Mould create_by_anisotropic_topk_2d(int k, double ratioX, double ratioY, int multi_grid = 1)
        {
            if (k <= 0)
                throw new ArgumentException("K must be > 0.");
            if (ratioX <= 0 || ratioY <= 0)
                throw new ArgumentException("Ratios must be > 0.");
            if (multi_grid < 1)
                throw new ArgumentException("multi_grid must be >= 1.");

            int r = 1, scale = 1 << (multi_grid - 1);
            var cand = new List<(int dx, int dy, float dist)>(k * 8);

            while (true)
            {
                cand.Clear();
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x == 0 && y == 0)
                        continue;
                    float d = (float)Sqrt((x / ratioX) * (x / ratioX) + (y / ratioY) * (y / ratioY));
                    cand.Add((x * scale, y * scale, d));
                }

                if (cand.Count >= k)
                    break;
                r <<= 1;
            }

            cand.Sort((a, b) =>
            {
                int c = a.dist.CompareTo(b.dist);
                if (c != 0)
                    return c;

                c = a.dx.CompareTo(b.dx);
                if (c != 0)
                    return c;

                return a.dy.CompareTo(b.dy);
            });

            int N = k;
            var mould = new Mould
            {
                dim = Dimension.D2,
                _dx = new int[N],
                _dy = new int[N],
                _dz = null,
                _dist = new float[N],
                _count = N
            };
            for (int i = 0; i < N; i++)
            {
                mould._dx[i] = cand[i].dx;
                mould._dy[i] = cand[i].dy;
                mould._dist[i] = cand[i].dist;
            }

            return mould;
        }

        /// <summary>
        /// 创建三维各向异性椭球模板（只保留 K 个点）
        /// 根据 XYZ 各向异性比例生成一个最近邻域模板，支持多重网格层级。
        /// </summary>
        /// <param name="k">模板中邻居点数量</param>
        /// <param name="ratioX">X方向比例（越大 → 越拉伸）</param>
        /// <param name="ratioY">Y方向比例</param>
        /// <param name="ratioZ">Z方向比例</param>
        /// <param name="multi_grid">多重网格层级（>=1）</param>
        /// <returns>三维各向异性 Mould 模板</returns>
        public static Mould create_by_anisotropic_topk_3d(int k, double rx, double ry, double rz, int multi_grid = 1)
        {
            if (k <= 0)
                throw new ArgumentException("K must be > 0.");
            if (rx <= 0 || ry <= 0 || rz <= 0)
                throw new ArgumentException("All ratios must be > 0.");
            if (multi_grid < 1)
                throw new ArgumentException("multi_grid must be >= 1.");

            int r = 1, scale = 1 << (multi_grid - 1);
            var cand = new List<(int dx, int dy, int dz, float dist)>(k * 16);

            while (true)
            {
                cand.Clear();
                for (int z = -r; z <= r; z++)
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x == 0 && y == 0 && z == 0)
                        continue;

                    float d = (float)Sqrt((x / rx) * (x / rx) + (y / ry) * (y / ry) + (z / rz) * (z / rz));
                    cand.Add((x * scale, y * scale, z * scale, d));
                }

                if (cand.Count >= k)
                    break;

                r <<= 1;
            }

            cand.Sort((a, b) =>
            {
                int c = a.dist.CompareTo(b.dist);
                if (c != 0)
                    return c;

                c = a.dx.CompareTo(b.dx);
                if (c != 0)
                    return c;

                c = a.dy.CompareTo(b.dy);
                if (c != 0)
                    return c;

                return a.dz.CompareTo(b.dz);
            });

            int N = k;
            var mould = new Mould
            {
                dim = Dimension.D3,
                _dx = new int[N],
                _dy = new int[N],
                _dz = new int[N],
                _dist = new float[N],
                _count = N
            };
            for (int i = 0; i < N; i++)
            {
                mould._dx[i] = cand[i].dx;
                mould._dy[i] = cand[i].dy;
                mould._dz[i] = cand[i].dz;
                mould._dist[i] = cand[i].dist;
            }

            return mould;
        }

        /// <summary>
        /// 根据Grid尺寸新建IrregularMould(默认包括CoreLoc自身)
        /// 注意：Core是模板的中心（设定的中心，不一定是实际中心）
        /// </summary>
        /// <param name="core_in_gridProperty">Core在Grid里的位置</param>
        /// <param name="gp">Grid包含 Null 和 非Null的节点，Mould只记录非Null的节点位置</param>
        /// <returns></returns>
        public static Mould create_by_gridProperty(SpatialIndex core_in_gridProperty, GridProperty gp)
        {
            var gs = gp.grid_structure;
            if (gs.dim != core_in_gridProperty.dim) return null;

            var (idxs, _) = gp.get_values_by_condition(null, CompareType.NotEqual);
            var neighbors = new List<SpatialIndex>(idxs.Count);
            for (int i = 0; i < idxs.Count; i++)
                neighbors.Add(gs.get_spatial_index(idxs[i]));
            return create_by_location(core_in_gridProperty, neighbors);
        }

        public override string ToString() => $"[dim:{dim} N:{neighbors_number}]";

        /// <summary>
        /// 计算距离
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static float get_distance(float?[] left, float?[] right)
        {
            float distance = 0f;
            for (int n = 0; n < left.Length; n++)
            {
                if (left[n] != null && right[n] != null)
                    distance += Abs(left[n]!.Value - right[n]!.Value);
            }

            return distance;
        }

        /// <summary>
        /// 从 GridProperty 中提取邻居数据，检查是否有有效数据，并返回相关信息。
        /// 
        /// ✅ 主要功能：
        /// - 提取邻居数据并存储到 buffer 中。
        /// - 返回是否至少有一个有效邻居数据（hasValidNeighborData）。
        /// - 返回是否所有邻居数据都有效（hasAllValidNeighborData）。
        /// </summary>
        /// <param name="core">核心点，表示当前需要提取邻居数据的位置。</param>
        /// <param name="gp">GridProperty，包含网格的属性数据。</param>
        /// <param name="buffer">输出缓存数组，存储提取的邻居数据。</param>
        /// <param name="core_value">输出核心点的值。</param>
        /// <param name="has_valid_neighbor_data">输出是否至少有一个有效邻居数据。</param>
        /// <param name="has_all_valid_neighbor_data">输出是否所有邻居数据都有效。</param>
        /// <returns>返回是否所有邻居数据都有效。</returns>
        public void get_neighbor_values(
            SpatialIndex core,
            GridProperty gp,
            float?[] buffer,
            out float? core_value, // 输出核心点的值
            out bool has_valid_neighbor_data, // 是否至少有一个有效邻居数据
            out bool has_all_valid_neighbor_data) // 是否所有邻居数据都有效
        {
            if (buffer.Length < this._count)
                throw new ArgumentException("缓存数组长度不足", nameof(buffer));

            core_value = gp.get_value(core);
            int cx = core.ix, cy = core.iy, cz = core.iz;
            int validCount = 0;
            has_valid_neighbor_data = false; // 初始化为 false
            has_all_valid_neighbor_data = true; // 默认假设所有邻居数据有效

            if (this.dim == Dimension.D2)
            {
                for (int i = 0; i < this._count; i++)
                {
                    int x = cx + this._dx[i];
                    int y = cy + this._dy[i];

                    var v = gp.get_value(x, y);
                    buffer[i] = v;

                    if (v != null)
                    {
                        validCount++;
                        has_valid_neighbor_data = true; // 如果有有效数据，则标记为 true
                    }
                    else
                    {
                        has_all_valid_neighbor_data = false; // 如果有任何无效数据，则标记为 false
                    }
                }
            }
            else // 3D
            {
                for (int i = 0; i < this._count; i++)
                {
                    int x = cx + this._dx[i];
                    int y = cy + this._dy[i];
                    int z = cz + this._dz[i];

                    var v = gp.get_value(x, y, z);
                    buffer[i] = v;

                    if (v != null)
                    {
                        validCount++;
                        has_valid_neighbor_data = true; // 如果有有效数据，则标记为 true
                    }
                    else
                    {
                        has_all_valid_neighbor_data = false; // 如果有任何无效数据，则标记为 false
                    }
                }
            }
        }


        /// <summary>
        /// 从 GridProperty 中提取所有完整样式（邻居全非空的）
        /// 
        /// ✅ 高性能版：
        /// - 无封装类
        /// - 无GC分配（除数组）
        /// - 支持并行
        ///
        /// 返回：
        ///   Dictionary<int, (float?[] buffer, float? core_value)>
        ///   其中 key = core 的 array_index
        /// </summary>
        public Dictionary<int, (float?[] buffer, float? core_value)> extract_pattern_buffers(
            GridProperty gp,
            bool parallel = true)
        {
            GridStructure gs = gp.grid_structure;
            var result = new ConcurrentDictionary<int, (float?[], float?)>();

            if (parallel)
            {
                var counter = new ConcurrentBag<int>();
                Parallel.For(0, gs.N, n =>
                {
                    var core = gs.get_spatial_index(n);
                    float?[] buffer = new float?[this._count];

                    // 提取邻居值
                    get_neighbor_values(core, gp, buffer, out float? coreValue, out bool hasValidNeighborData,
                        out bool hasAllValidNeighborData);

                    // 如果没有有效的邻居值，跳过当前操作
                    if (hasAllValidNeighborData)
                    {
                        // 必须克隆 buffer，否则被线程覆盖
                        result[n] = ((float?[])buffer.Clone(), coreValue);
                    }

                    counter.Add(1);
                    if (counter.Count % 1000 == 0)
                        MyConsoleProgress.print(counter.Count, gs.N, "Extract Pattern Buffers");
                });
            }
            else
            {
                int progress = 0;
                for (int n = 0; n < gs.N; n++)
                {
                    var core = gs.get_spatial_index(n);
                    float?[] buffer = new float?[this._count];

                    // 提取邻居值
                    this.get_neighbor_values(core, gp, buffer, out float? coreValue, out bool hasValidNeighborData,
                        out bool hasAllValidNeighborData);

                    // 如果有有效的邻居数据，执行后续操作
                    if (hasValidNeighborData)
                    {
                        result[n] = (buffer, coreValue);
                    }


                    progress++;
                    MyConsoleProgress.print(progress, gs.N, "Extract Pattern Buffers");
                }
            }

            return new Dictionary<int, (float?[], float?)>(result);
        }

        /// <summary>
        /// 显示二维模板形状（控制台打印）
        /// </summary>
        /// <param name="title">标题</param>
        public void Show2d(string title)
        {
            if (dim != Dimension.D2)
            {
                Console.WriteLine("⚠️  This mould is not 2D. Show2d is only for 2D templates.");
                return;
            }

            if (_count == 0 || _dx == null || _dy == null)
            {
                Console.WriteLine("⚠️  No neighbors to show.");
                return;
            }

            // 找出所有邻居的坐标范围
            int minX = _dx.Min();
            int maxX = _dx.Max();
            int minY = _dy.Min();
            int maxY = _dy.Max();

            Console.WriteLine($"\n📐 {title}");
            Console.WriteLine($"范围：X=[{minX},{maxX}], Y=[{minY},{maxY}]\n");

            // 将邻居点转为 HashSet，便于快速查找
            var neighborSet = new HashSet<(int, int)>();
            for (int i = 0; i < _count; i++)
                neighborSet.Add((_dx[i], _dy[i]));

            // y 从上到下（maxY → minY）
            for (int y = maxY; y >= minY; y--)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == 0 && y == 0)
                        Console.Write(" O"); // 中心点
                    else if (neighborSet.Contains((x, y)))
                        Console.Write(" *"); // 邻居点
                    else
                        Console.Write("  "); // 空白
                }

                Console.WriteLine();
            }

            Console.WriteLine();
        }


        private Mould clone()
        {
            var m = new Mould
            {
                dim = dim,
                _count = _count,
                _dx = new int[_count],
                _dy = new int[_count],
                _dz = (dim == Dimension.D3) ? new int[_count] : null,
                _dist = new float[_count]
            };
            Array.Copy(_dx, m._dx, _count);
            Array.Copy(_dy, m._dy, _count);
            if (m._dz != null) Array.Copy(_dz, m._dz, _count);
            Array.Copy(_dist, m._dist, _count);
            return m;
        }
    }
}