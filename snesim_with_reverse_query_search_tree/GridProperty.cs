using System.Diagnostics;
using System.Runtime.CompilerServices;
using JAM8.Algorithms.Numerics;

namespace JAM8.Algorithms.Geometry
{
    /// <summary>
    /// 网格属性
    /// </summary>
    public class GridProperty
    {
        #region properties

        /// <summary>
        /// 数据缓存
        /// </summary>
        public float?[] buffer { get; internal set; }

        /// <summary>
        /// 网格单元等于Null的数量
        /// </summary>
        public int N_Nulls { get; internal set; } = 0;

        /// <summary>
        /// 网格结构
        /// </summary>
        public GridStructure grid_structure { get; internal set; }

        public float? Min
        {
            get { return buffer.Min(); }
        }

        public float? Max
        {
            get { return buffer.Max(); }
        }

        public float? Average
        {
            get { return buffer.Average(); }
        }

        /// <summary>
        /// 获取离散变量的值及其对应频率
        /// </summary>
        /// <param name="buffer">包含离散变量的集合</param>
        /// <param name="nullReserve">是否保留空值的统计</param>
        /// <returns>值和频率的列表</returns>
        public List<(float? value, float freq)> discrete_category_freq(bool nullReserve = true)
        {
            // 检查输入是否为空
            if (buffer == null || buffer.Length == 0)
                return [];

            // 按值分组统计频数，并将 null 转为 "null" 字符串作为键
            var frequencyDict = buffer
                .GroupBy(x => x?.ToString() ?? "null")
                .ToDictionary(
                    g => g.Key,
                    g => (float)g.Count()
                );

            // 如果不保留空值，移除 "null" 键
            if (!nullReserve)
                frequencyDict.Remove("null");

            // 计算总频数，用于归一化
            float total = frequencyDict.Values.Sum();

            // 构造结果，解析键为浮点数或 null，并计算归一化频率
            return frequencyDict
                .Select(kv => (
                    kv.Key == "null" ? (float?)null : float.Parse(kv.Key),
                    kv.Value / total
                ))
                .ToList();
        }


        /// <summary>
        /// 获取离散变量的值及其对应的频数
        /// </summary>
        /// <param name="null_reserve">是否保留空值的统计</param>
        /// <returns>值和频数的列表</returns>
        public List<(float? value, int count)> discrete_category_count(bool null_reserve = true)
        {
            // 检查 buffer 是否为空
            if (buffer == null || buffer.Length == 0)
                return [];

            // 按值统计频数，处理 null 值为 "null"
            var frequencyDict = buffer
                .GroupBy(x => x?.ToString() ?? "null")
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );

            // 如果不保留空值，移除 "null" 键
            if (!null_reserve)
                frequencyDict.Remove("null");

            // 构造结果列表，将键解析为 float? 类型
            return frequencyDict
                .Select(kv => (
                    kv.Key == "null" ? (float?)null : float.Parse(kv.Key),
                    kv.Value
                ))
                .ToList();
        }

        #endregion

        private GridProperty()
        {
        }

        #region create

        /// <summary>
        /// 创建GridProperty
        /// </summary>
        /// <param name="gs"></param>
        /// <returns></returns>
        public static GridProperty create(GridStructure gs)
        {
            GridProperty gp = new() //开辟缓存空间
            {
                grid_structure = gs,
                buffer = new float?[gs.N],
                N_Nulls = gs.N
            };
            return gp;
        }

        /// <summary>
        /// 从二维 float?[,] 数组创建一个 GridProperty 对象
        /// </summary>
        /// <param name="gs">目标网格结构，决定网格尺寸与坐标系</param>
        /// <param name="data">二维属性值数组，大小应为 [nx, ny]</param>
        /// <returns>构造好的 GridProperty</returns>
        /// <exception cref="ArgumentException">当 data 尺寸与 gs 不一致时抛出</exception>
        public static GridProperty create_from_data2d(GridStructure gs, float?[,] data)
        {
            int nx = gs.nx;
            int ny = gs.ny;

            if (data.GetLength(0) != nx || data.GetLength(1) != ny)
                throw new ArgumentException(
                    $"输入数组尺寸 [{data.GetLength(0)}, {data.GetLength(1)}] 与网格结构尺寸 [{nx}, {ny}] 不一致。");

            GridProperty gp = GridProperty.create(gs);

            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    float? value = data[ix, iy];
                    gp.set_value(ix, iy, value);
                }
            }

            return gp;
        }

        /// <summary>
        /// 从二维 double[,] 数组创建一个 GridProperty 对象
        /// </summary>
        /// <param name="gs">目标网格结构，决定网格尺寸与坐标系</param>
        /// <param name="data">二维属性值数组，大小应为 [nx, ny]</param>
        /// <param name="null_value">指定的无效值，将会被转换为 null</param>
        /// <returns>构造好的 GridProperty</returns>
        /// <exception cref="ArgumentException">当 data 尺寸与 gs 不一致时抛出</exception>
        public static GridProperty create_from_data2d(GridStructure gs, double[,] data, double null_value = -99)
        {
            int nx = gs.nx;
            int ny = gs.ny;

            if (data.GetLength(0) != nx || data.GetLength(1) != ny)
                throw new ArgumentException(
                    $"输入数组尺寸 [{data.GetLength(0)}, {data.GetLength(1)}] 与网格结构尺寸 [{nx}, {ny}] 不一致。");

            GridProperty gp = GridProperty.create(gs);

            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    double val = data[ix, iy];
                    float? value = Math.Abs(val - null_value) < 1e-6 ? null : (float)val;
                    gp.set_value(ix, iy, value);
                }
            }

            return gp;
        }


        /// <summary>
        /// 从三维 float?[,,] 数组创建一个 GridProperty 对象（用于体积属性）
        /// </summary>
        /// <param name="gs">网格结构，必须与 data 尺寸一致</param>
        /// <param name="data">三维体数据 [nx, ny, nz]</param>
        /// <returns>构造好的 GridProperty</returns>
        /// <exception cref="ArgumentException">当尺寸不一致时抛出</exception>
        public static GridProperty create_from_data3d(GridStructure gs, float?[,,] data)
        {
            int nx = gs.nx;
            int ny = gs.ny;
            int nz = gs.nz;

            if (data.GetLength(0) != nx || data.GetLength(1) != ny || data.GetLength(2) != nz)
                throw new ArgumentException(
                    $"输入数组尺寸 [{data.GetLength(0)}, {data.GetLength(1)}, {data.GetLength(2)}] 与网格结构尺寸 [{nx}, {ny}, {nz}] 不一致。");

            GridProperty gp = GridProperty.create(gs);

            for (int iz = 0; iz < nz; iz++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    for (int ix = 0; ix < nx; ix++)
                    {
                        float? value = data[ix, iy, iz];
                        gp.set_value(ix, iy, iz, value);
                    }
                }
            }

            return gp;
        }


        /// <summary>
        /// 根据指定的多个条件判断网格节点值，返回新的修改后的 GridProperty。
        /// 条件按顺序执行，只有前一个条件满足，后续条件才会继续判断。
        /// 每个条件使用其自己的 NewValue 更新值，后续条件基于之前的修改值计算。
        /// </summary>
        /// <param name="gp">原始 GridProperty 对象</param>
        /// <param name="conditions">条件列表，每个条件包含比较值、目标值和比较类型</param>
        /// <returns>修改后的新的 GridProperty 对象</returns>
        public static GridProperty create(GridProperty gp,
            params (float? ComparedValue, float? NewValue, CompareType CompareType)[] conditions)
        {
            GridProperty clone = gp.deep_clone();

            for (int n = 0; n < clone.grid_structure.N; n++)
            {
                float? currentValue = clone.get_value(n);

                foreach (var (comparedValue, newValue, compareType) in conditions)
                {
                    // 判断当前条件是否满足
                    bool shouldReplace = compareType switch
                    {
                        CompareType.NoCompared => true,
                        CompareType.Equals => currentValue == comparedValue,
                        CompareType.NotEqual => currentValue != comparedValue,
                        CompareType.GreaterThan => currentValue > comparedValue,
                        CompareType.GreaterEqualsThan => currentValue >= comparedValue,
                        CompareType.LessThan => currentValue < comparedValue,
                        CompareType.LessEqualsThan => currentValue <= comparedValue,
                        _ => false
                    };

                    // 如果当前条件满足，更新值，并使用该条件的 NewValue
                    if (shouldReplace)
                    {
                        currentValue = newValue; // 更新当前值为当前条件的 NewValue
                        clone.set_value(n, currentValue); // 更新节点值
                    }
                }
            }

            return clone;
        }

        /// <summary>
        /// 根据指定的多个区间条件判断网格节点值，返回新的修改后的 GridProperty。
        /// 每个区间条件使用其自己的 NewValue 更新值，后续条件基于之前的修改值计算。
        /// </summary>
        /// <param name="gp">原始 GridProperty 对象</param>
        /// <param name="conditions">区间条件列表，每个条件包含区间的起始值、结束值和目标值</param>
        /// <returns>修改后的新的 GridProperty 对象</returns>
        public static GridProperty create(GridProperty gp,
            params (float? MinValue, float? MaxValue, float? NewValue)[] conditions)
        {
            GridProperty clone = gp.deep_clone();

            for (int n = 0; n < clone.grid_structure.N; n++)
            {
                float? currentValue = clone.get_value(n);

                foreach (var (minValue, maxValue, newValue) in conditions)
                {
                    // 判断当前值是否在区间内
                    if (currentValue >= minValue && currentValue <= maxValue)
                    {
                        clone.set_value(n, newValue); // 更新值
                        break; // 找到匹配的区间后，跳出当前条件判断
                    }
                }
            }

            return clone;
        }

        #endregion

        #region + - * /

        public static GridProperty operator +(GridProperty left, GridProperty right)
        {
            if (!left.grid_structure.Equals(right.grid_structure))
                throw new Exception("gridStructure不一致");
            GridProperty result = create(left.grid_structure);
            for (int n = 0; n < left.grid_structure.N; n++)
            {
                float? value = left.get_value(n) + right.get_value(n);
                result.set_value(n, value);
            }

            return result;
        }

        public static GridProperty operator -(GridProperty left, GridProperty right)
        {
            if (!left.grid_structure.Equals(right.grid_structure))
                throw new Exception("gridStructure不一致");
            GridProperty result = create(left.grid_structure);
            for (int n = 0; n < left.grid_structure.N; n++)
            {
                float? value = left.get_value(n) - right.get_value(n);
                result.set_value(n, value);
            }

            return result;
        }

        public static GridProperty operator *(GridProperty left, GridProperty right)
        {
            if (!left.grid_structure.Equals(right.grid_structure))
                throw new Exception("gridStructure不一致");
            GridProperty result = create(left.grid_structure);
            for (int n = 0; n < left.grid_structure.N; n++)
            {
                float? value = left.get_value(n) * right.get_value(n);
                result.set_value(n, value);
            }

            return result;
        }

        public static GridProperty operator /(GridProperty left, GridProperty right)
        {
            if (!left.grid_structure.Equals(right.grid_structure))
                throw new Exception("gridStructure不一致");
            GridProperty result = create(left.grid_structure);
            for (int n = 0; n < left.grid_structure.N; n++)
            {
                float? value = left.get_value(n) / right.get_value(n);
                result.set_value(n, value);
            }

            return result;
        }

        #endregion

        #region get_value

        /// <summary>
        /// 根据array索引获取value
        /// </summary>
        /// <param name="arrayIndex">arrayIndex范围从0到gs.N-1</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? get_value(int array_index)
        {
            // 检查 arrayIndex 是否在合法范围内
            if ((uint)array_index >= (uint)grid_structure.N)
            {
                return null; // 如果超出范围，直接返回 null
            }

            // 根据索引返回对应的 Buffer 值
            return buffer[array_index];
        }

        /// <summary>
        /// 根据spatial索引获取value，spatial index的ix、iy、iz从0开始，到N-1结束
        /// </summary>
        /// <param name="si"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? get_value(SpatialIndex si)
        {
            int array_index = grid_structure.get_array_index(si);
            if (array_index < 0)
                return null;

            return buffer[array_index];
        }

        /// <summary>
        /// 根据二维spatial索引的ix、iy获取value，ix、iy从0开始，到N-1结束
        /// </summary>
        /// <param name="ix"></param>
        /// <param name="iy"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? get_value(int ix, int iy)
        {
            int array_index = grid_structure.get_array_index(ix, iy, 0);
            if (array_index < 0)
                return null;

            return buffer[array_index];
        }

        /// <summary>
        /// 根据三维spatial索引的ix、iy、iz获取value，ix、iy、iz从0开始，到N-1结束
        /// </summary>
        /// <param name="ix"></param>
        /// <param name="iy"></param>
        /// <param name="iz"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? get_value(int ix, int iy, int iz)
        {
            int array_index = grid_structure.get_array_index(ix, iy, iz);
            if (array_index < 0)
                return null;

            return buffer[array_index];
        }

        /// <summary>
        /// 获取满足特定条件的网格值及其索引。
        /// 用于从 GridProperty 中筛选出满足比较条件（等于、不等于、大于等）
        /// 的网格单元的索引和值，常用于掩膜、条件提取、统计等操作。
        /// </summary>
        /// <param name="compare_value">
        /// 参与比较的数值（可以为 null）。
        /// 若 compare_type 为 Equals 或 NotEqual 且 compare_value 为 null，
        /// 则可用于判断值是否为缺失值（null）。
        /// </param>
        /// <param name="compare_type">
        /// 比较类型（CompareType 枚举）：
        /// - Equals：等于 compare_value
        /// - NotEqual：不等于 compare_value
        /// - GreaterThan：大于 compare_value
        /// - GreaterEqualsThan：大于等于 compare_value
        /// - LessThan：小于 compare_value
        /// - LessEqualsThan：小于等于 compare_value
        /// - NoCompared：不做比较，全部返回
        /// </param>
        /// <returns>
        /// 返回两个列表：
        /// - 第一个为满足条件的网格索引列表（List&lt;int&gt;）
        /// - 第二个为对应的属性值列表（List&lt;float?&gt;）
        /// </returns>
        public (List<int>, List<float?>) get_values_by_condition(float? compare_value, CompareType compare_type)
        {
            List<int> idx = [];
            List<float?> values = [];

            // 遍历网格节点并进行条件判断
            for (int n = 0; n < grid_structure.N; n++)
            {
                float? currentValue = get_value(n);

                // 使用通用方法来进行比较
                bool shouldAdd = compare_type switch
                {
                    CompareType.NoCompared => true,
                    CompareType.Equals => currentValue == compare_value,
                    CompareType.NotEqual => currentValue != compare_value,
                    CompareType.GreaterThan => currentValue > compare_value,
                    CompareType.GreaterEqualsThan => currentValue >= compare_value,
                    CompareType.LessThan => currentValue < compare_value,
                    CompareType.LessEqualsThan => currentValue <= compare_value,
                    _ => false
                };

                // 如果满足条件，添加索引和值
                if (shouldAdd)
                {
                    idx.Add(n);
                    values.Add(currentValue);
                }
            }

            return (idx, values);
        }

        /// <summary>
        /// 获取满足区间范围条件的values
        /// </summary>
        /// <param name="minValue">区间下限（包含）</param>
        /// <param name="max_value">区间上限（包含）</param>
        /// <returns>符合条件的索引和对应值</returns>
        public (List<int>, List<float?>) get_values_by_range(float? min_value, float? max_value)
        {
            List<int> idx = [];
            List<float?> values = [];

            // 遍历网格节点并进行区间判断
            for (int n = 0; n < grid_structure.N; n++)
            {
                float? currentValue = get_value(n);

                // 判断值是否在区间范围内
                if (currentValue >= min_value && currentValue <= max_value)
                {
                    idx.Add(n);
                    values.Add(currentValue);
                }
            }

            return (idx, values);
        }

        #endregion

        #region set_value

        /// <summary>
        /// 根据array索引对网格单元赋值value
        /// </summary>
        /// <param name="arrayIndex">array index从0开始，到gs.N-1结束</param>
        /// <param name="value"></param>
        public void set_value(int arrayIndex, float? value)
        {
            if (arrayIndex >= 0 && arrayIndex < grid_structure.N)
            {
                float? old = get_value(arrayIndex);
                if (old == null && value != null)
                    N_Nulls -= 1;
                if (old != null && value == null)
                    N_Nulls += 1;
                buffer[arrayIndex] = value;
            }
        }

        /// <summary>
        /// 根据spatial索引对网格单元赋值value，spatial_index的ix、iy、iz从1开始，到gs.N结束
        /// </summary>
        /// <param name="si"></param>
        /// <param name="value"></param>
        public void set_value(SpatialIndex si, float? value)
        {
            set_value(grid_structure.get_array_index(si), value);
        }

        /// <summary>
        /// 根据ix、iy对网格单元赋值value，ix、iy从0开始，到N-1结束
        /// </summary>
        /// <param name="ix"></param>
        /// <param name="iy"></param>
        /// <param name="value"></param>
        public void set_value(int ix, int iy, float? value)
        {
            set_value(grid_structure.get_array_index(ix, iy, 0), value);
        }

        /// <summary>
        /// 根据ix、iy、iz对网格单元赋值value，ix、iy、iz从0开始，到N-1结束
        /// </summary>
        /// <param name="ix"></param>
        /// <param name="iy"></param>
        /// <param name="iz"></param>
        /// <param name="value"></param>
        public void set_value(int ix, int iy, int iz, float? value)
        {
            set_value(grid_structure.get_array_index(ix, iy, iz), value);
        }

        /// <summary>
        /// 对所有网格单元均赋值为value
        /// </summary>
        /// <param name="value"></param>
        public void set_value(float? value)
        {
            for (int n = 0; n < grid_structure.N; n++)
            {
                set_value(n, value);
            }
        }

        /// <summary>
        /// 根据指定条件在原先网格属性上修改网格节点值
        /// </summary>
        /// <param name="compared_value">用于比较的值</param>
        /// <param name="new_value">需要设置的新值</param>
        /// <param name="compare_type">比较条件</param>
        /// <returns>发生修改的网格节点的array_index集合</returns>
        public List<int> set_values_by_condition(float? compared_value, float? new_value, CompareType compare_type)
        {
            List<int> idx = [];
            for (int n = 0; n < grid_structure.N; n++)
            {
                float? currentValue = get_value(n);

                // 在循环中直接实现比较逻辑
                bool shouldReplace = compare_type switch
                {
                    CompareType.NoCompared => true,
                    CompareType.Equals => currentValue == compared_value,
                    CompareType.NotEqual => currentValue != compared_value,
                    CompareType.GreaterThan => currentValue > compared_value,
                    CompareType.GreaterEqualsThan => currentValue >= compared_value,
                    CompareType.LessThan => currentValue < compared_value,
                    CompareType.LessEqualsThan => currentValue <= compared_value,
                    _ => false
                };

                if (shouldReplace)
                {
                    idx.Add(n);
                    set_value(n, new_value);
                }
            }

            return idx;
        }

        /// <summary>
        /// 根据指定区间范围修改网格节点值
        /// </summary>
        /// <param name="minValue">区间下限（包含）</param>
        /// <param name="maxValue">区间上限（包含）</param>
        /// <param name="newValue">需要设置的新值</param>
        /// <returns>发生修改的网格节点的 array_index 集合</returns>
        public List<int> set_values_by_range(float? minValue, float? maxValue, float? newValue)
        {
            List<int> idx = [];

            // 遍历所有网格节点并判断是否在区间范围内
            for (int n = 0; n < grid_structure.N; n++)
            {
                float? currentValue = get_value(n);

                // 判断值是否在区间范围内
                if (currentValue >= minValue && currentValue <= maxValue)
                {
                    idx.Add(n);
                    set_value(n, newValue); // 更新值
                }
            }

            return idx;
        }

        #endregion

        #region get_region_by_range

        /// <summary>
        /// 根据索引的界限提取区域部分网格，[ix_min,ix_max]和[iy_min,iy_max]是闭区间
        /// </summary>
        /// <param name="ix_min">从1开始</param>
        /// <param name="ix_max">gs.nx结束</param>
        /// <param name="iy_min">从1开始</param>
        /// <param name="iy_max">gs.ny结束</param>
        /// <returns></returns>
        public (GridProperty region, bool index_out_of_bounds) get_region_by_range(int ix_min, int ix_max, int iy_min,
            int iy_max)
        {
            bool index_out_of_bounds = false; //是否越界，默认是假
            if (Dimension.D3 == grid_structure.dim)
                throw new Exception(MyExceptions.Geometry_DimensionException);

            int extent_x = ix_max - ix_min;
            int extent_y = iy_max - iy_min;

            ix_min = Math.Max(ix_min, 0);
            ix_max = Math.Min(ix_max, grid_structure.nx - 1);
            iy_min = Math.Max(iy_min, 0);
            iy_max = Math.Min(iy_max, grid_structure.ny - 1);

            if (extent_x != ix_max - ix_min || extent_y != iy_max - iy_min)
                index_out_of_bounds = true;

            //创建一个新网格对象
            var gs = GridStructure.create_simple(ix_max - ix_min + 1, iy_max - iy_min + 1, 1);
            var region = create(gs);

            for (int iy = iy_min; iy <= iy_max; iy++)
            for (int ix = ix_min; ix <= ix_max; ix++)
                region.set_value(ix - ix_min, iy - iy_min, get_value(ix, iy));

            return (region, index_out_of_bounds);
        }

        #endregion

        /// <summary>
        /// 基于vtk显示三维模型的窗口
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        public void show_win3d_vtk()
        {
            void LaunchFy3DViewer(string modelPath, int nx, int ny, int nz)
            {
                // 获取当前主程序的运行目录
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // 拼出 fy3DModelControllib.exe 的相对路径
                string exePath = Path.Combine(baseDir, "fy3DViewer", "fy3DModelControllib.exe");

                // 确认文件存在
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException("找不到 fy3DModelControllib.exe", exePath);
                }

                // 组合命令行参数
                string args = $"\"{modelPath}\" {nx} {ny} {nz}";

                // 启动进程
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(psi);
            }

            // ✅ 获取主程序（JAM8.exe）所在的目录
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // ✅ 在该目录下生成一个文件路径，例如：JAM8\test_grid.out
            string file_path_new = Path.Combine(baseDir, "test_grid.out");

            // === 保存模型 ===
            Grid g = Grid.create(grid_structure);
            g.add_gridProperty("GridProperty", this);
            g.save_to_gslib(file_path_new, "test_grid", -99);

            // === 获取尺寸信息 ===
            int nx = g.gridStructure.nx;
            int ny = g.gridStructure.ny;
            int nz = g.gridStructure.nz;

            // === 启动显示 ===
            LaunchFy3DViewer(file_path_new, nx, ny, nz);
        }

        /// <summary>
        /// 深度复制
        /// </summary>
        /// <returns></returns>
        public GridProperty deep_clone()
        {
            GridProperty gp = create(grid_structure);
            for (int n = 0; n < grid_structure.N; n++)
                gp.set_value(n, get_value(n));
            return gp;
        }

        /// <summary>
        /// 将当前 GridProperty 转换为对应的 Grid，并添加自身为属性
        /// </summary>
        /// <param name="grid_property_name">添加的属性名，默认值为 "default gp_name"</param>
        /// <returns>包含该属性的 Grid 实例</returns>
        public Grid convert_to_grid(string grid_property_name = "default gp_name")
        {
            var g = Grid.create(grid_structure);
            g.add_gridProperty(grid_property_name, deep_clone());
            return g;
        }

    }
}