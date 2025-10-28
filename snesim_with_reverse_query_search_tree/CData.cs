using JAM8.Utilities;

namespace JAM8.Algorithms.Geometry
{
    /// <summary>
    /// Conditional data class, using DataFrame as the data buffer for conditional data
    /// </summary>
    public class CData
    {
        private CData()
        {
        }

        public Dimension dim { get; internal set; }

        // Property names of the conditional data, excluding coordinates
        public string[] property_names { get; internal set; }

        // Number of conditional data items
        public int N_cdata_items => buffer.N_Record;

        //Data buffer for conditional data
        private MyDataFrame buffer;

        //The number of the x coordinate in the data buffer, starting at 0
        private int x_series_index { get; init; }

        //The number of the y coordinate in the data buffer, starting at 0
        private int y_series_index { get; init; }

        //The number of the z coordinate in the data buffer, starting at 0
        private int z_series_index { get; init; }

        /// <summary>
        /// Null value used to mark missing/invalid data. If null, null checking is disabled.
        /// This value is immutable once the object is constructed.
        /// </summary>
        private float? null_value { get; init; }

        //The target grid structure, if not empty, indicates that the conditional data
        //has been coarsened to the target grid structure
        public GridStructure target_gs { get; internal set; }

        /// <summary>
        /// Get the numerical value of the conditional data based on the sequence number and 
        /// attribute name of the conditional data
        /// </summary>
        /// <param name="idx">The serial number of the conditional data, starting from 0</param>
        /// <param name="property_name">Attribute name</param>
        /// <returns>Nullable float value</returns>
        public float? get_value(int idx, string property_name)
        {
            float? value = Convert.ToSingle(buffer[idx, property_name]);
            if (null_value.HasValue && null_value == value)
                value = null;
            return value;
        }

        /// <summary>
        /// Get the numerical value of the conditional data based on the sequence number and 
        /// property index of the conditional data
        /// </summary>
        /// <param name="idx">The serial number of the conditional data, starting from 0</param>
        /// <param name="property_idx">The index of the property, starting from 0</param>
        /// <returns>Nullable float value</returns>
        public float? get_value(int idx, int property_idx)
        {
            return get_value(idx, property_names[property_idx]);
        }

        /// <summary>
        /// Get a full record (including coordinates and all properties) by row index.
        /// </summary>
        /// <param name="idx">The record index</param>
        /// <returns>Dictionary of all field names to their values (nullable float)</returns>
        public (string[] field_names, float?[] values) get_cdata_item(int idx)
        {
            List<string> names = new();
            List<float?> values = new();

            // Add coordinates
            names.Add(buffer.series_names[x_series_index]);
            values.Add(Convert.ToSingle(buffer[idx, x_series_index]));

            names.Add(buffer.series_names[y_series_index]);
            values.Add(Convert.ToSingle(buffer[idx, y_series_index]));

            if (dim == Dimension.D3)
            {
                names.Add(buffer.series_names[z_series_index]);
                values.Add(Convert.ToSingle(buffer[idx, z_series_index]));
            }

            // Add properties
            foreach (var name in property_names)
            {
                names.Add(name);
                values.Add(get_value(idx, name));
            }

            return (names.ToArray(), values.ToArray());
        }

        /// <summary>
        /// Gets the spatial coordinate of the specified record.
        /// </summary>
        /// <param name="idx">The record index.</param>
        /// <returns>A Coord object representing the (x, y[, z]) location.</returns>
        public Coord get_coord(int idx)
        {
            float x = Convert.ToSingle(buffer[idx, x_series_index]);
            float y = Convert.ToSingle(buffer[idx, y_series_index]);

            if (dim == Dimension.D3)
            {
                float z = Convert.ToSingle(buffer[idx, z_series_index]);
                return Coord.create(x, y, z);
            }
            else
            {
                return Coord.create(x, y);
            }
        }

        /// <summary>
        /// coarsening the conditional data to the target grid structure, and adjust the 
        /// conditional data to the grid cells of the target grid structure
        /// 将条件数据粗化到目标网格结构，实现条件数据调整至目标网格结构的网格单元上
        /// </summary>
        /// <param name="gs"></param>
        /// <returns></returns>
        public (CData coarsened_cd, Grid coarsened_grid) coarsened(GridStructure gs)
        {
            CData cd_coarsened = new()
            {
                dim = dim,
                x_series_index = x_series_index,
                y_series_index = y_series_index,
                z_series_index = z_series_index,
                null_value = null_value,
                target_gs = gs,
                property_names = property_names.Clone() as string[], //复制属性名称
                buffer = MyDataFrame.create(buffer.series_names) //复制原始数据缓冲区的结构
            };

            for (int idx_record = 0; idx_record < buffer.N_Record; idx_record++)
            {
                SpatialIndex si = null; //粗化后条件数据的空间索引
                Coord coord = null; //条件数据的坐标
                if (dim == Dimension.D2)
                {
                    float x = Convert.ToSingle(buffer[idx_record, x_series_index]);
                    float y = Convert.ToSingle(buffer[idx_record, y_series_index]);
                    coord = Coord.create(x, y);
                    si = gs.coord_to_spatial_index(coord);
                    if (si != null) //保留落在grid范围内的cdi
                    {
                        MyRecord record = buffer.get_record(idx_record); //从原始表里提取记录，然后修改
                        record[buffer.series_names[x_series_index]] = si.ix;
                        record[buffer.series_names[y_series_index]] = si.iy;
                        cd_coarsened.buffer.add_record(record);
                    }
                }

                if (dim == Dimension.D3)
                {
                    float x = Convert.ToSingle(buffer[idx_record, x_series_index]);
                    float y = Convert.ToSingle(buffer[idx_record, y_series_index]);
                    float z = Convert.ToSingle(buffer[idx_record, z_series_index]);
                    coord = Coord.create(x, y, z);
                    si = gs.coord_to_spatial_index(coord);
                    if (si != null) //保留落在grid范围内的cdi
                    {
                        MyRecord record = buffer.get_record(idx_record); //从原始表里提取记录，然后修改
                        record[buffer.series_names[x_series_index]] = si.ix;
                        record[buffer.series_names[y_series_index]] = si.iy;
                        record[buffer.series_names[z_series_index]] = si.iz;
                        cd_coarsened.buffer.add_record(record);
                    }
                }
            }

            Grid g = Grid.create(gs, "coarsened");
            foreach (var property_name in cd_coarsened.property_names)
            {
                g.add_gridProperty(property_name);
            }

            //循环将coarsened里的数据赋值给g
            for (int idx_record = 0; idx_record < cd_coarsened.buffer.N_Record; idx_record++)
            {
                SpatialIndex si = null;
                if (dim == Dimension.D2)
                {
                    int ix = Convert.ToInt32(cd_coarsened.buffer[idx_record, x_series_index]);
                    int iy = Convert.ToInt32(cd_coarsened.buffer[idx_record, y_series_index]);
                    si = SpatialIndex.create(ix, iy);
                }

                if (dim == Dimension.D3)
                {
                    int ix = Convert.ToInt32(cd_coarsened.buffer[idx_record, x_series_index]);
                    int iy = Convert.ToInt32(cd_coarsened.buffer[idx_record, y_series_index]);
                    int iz = Convert.ToInt32(cd_coarsened.buffer[idx_record, z_series_index]);
                    si = SpatialIndex.create(ix, iy, iz);
                }

                if (si != null)
                {
                    for (int j = 0; j < cd_coarsened.property_names.Length; j++)
                    {
                        g[cd_coarsened.property_names[j]].set_value(si, cd_coarsened.get_value(idx_record, j));
                    }
                }
            }

            return (cd_coarsened, g);
        }

        /// <summary>
        /// Gets the spatial boundary of the conditional data (min/max values of x, y, and z coordinates).
        /// For 2D data, the z boundary values will be null.
        /// </summary>
        /// <returns>(min_x, max_x, min_y, max_y, min_z, max_z)</returns>
        public (float min_x, float max_x, float min_y, float max_y, float? min_z, float? max_z) get_boundary()
        {
            float min_x = float.MaxValue, max_x = float.MinValue;
            float min_y = float.MaxValue, max_y = float.MinValue;
            float? min_z = null, max_z = null;

            for (int i = 0; i < N_cdata_items; i++)
            {
                float x = Convert.ToSingle(buffer[i, x_series_index]);
                float y = Convert.ToSingle(buffer[i, y_series_index]);

                if (x < min_x) min_x = x;
                if (x > max_x) max_x = x;

                if (y < min_y) min_y = y;
                if (y > max_y) max_y = y;

                if (dim == Dimension.D3)
                {
                    float z = Convert.ToSingle(buffer[i, z_series_index]);
                    if (min_z == null || z < min_z) min_z = z;
                    if (max_z == null || z > max_z) max_z = z;
                }
            }

            return (min_x, max_x, min_y, max_y, min_z, max_z);
        }

        /// <summary>
        /// Deep copy
        /// </summary>
        /// <returns></returns>
        public CData deep_clone()
        {
            CData cd = new()
            {
                dim = dim,
                null_value = null_value,

                x_series_index = x_series_index,
                y_series_index = y_series_index,
                z_series_index = z_series_index,

                target_gs = target_gs, //这个不需要深度复制，因为是占用空间较大，而且只是引用

                buffer = buffer.deep_clone(),
                property_names = [.. property_names]
            };

            return cd;
        }

        /// <summary>
        /// Read CData from gslib.
        /// </summary>
        /// <param name="file_name"></param>
        /// <param name="x_series_index"></param>
        /// <param name="y_series_index"></param>
        /// <param name="z_series_index"></param>
        /// <param name="null_value"></param>
        /// <returns></returns>
        public static CData read_from_gslib(string file_name, int x_series_index, int y_series_index,
            int z_series_index, float? null_value)
        {
            CData cdata = new()
            {
                buffer = GSLIB.gslib_to_df(file_name),
                x_series_index = x_series_index,
                y_series_index = y_series_index,
                z_series_index = z_series_index,
                null_value = null_value,
                dim = z_series_index == -1 ? Dimension.D2 : Dimension.D3
            };

            //将cdata.buffer里所有除了x_series_index y_series_index z_series_index以外的属性名称提取出来
            List<string> property_names = [];
            for (int i = 0; i < cdata.buffer.series_names.Length; i++)
            {
                if (cdata.dim == Dimension.D2 && i != x_series_index && i != y_series_index)
                {
                    property_names.Add(cdata.buffer.series_names[i]);
                }

                if (cdata.dim == Dimension.D3 && i != x_series_index && i != y_series_index && i != z_series_index)
                {
                    property_names.Add(cdata.buffer.series_names[i]);
                }
            }

            cdata.property_names = [.. property_names];

            return cdata;
        }
    }
}