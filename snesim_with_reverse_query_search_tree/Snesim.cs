using System;
using System.Diagnostics;
using JAM8.Algorithms.Numerics;
using JAM8.Utilities;

namespace JAM8.Algorithms.Geometry
{
    public static class Snesim
    {
        /// <summary>
        /// 使用提供的训练图像和模板，对指定的模拟网格执行 SNESIM 模拟。
        /// </summary>
        /// <param name="re">The simulation grid (to be filled during simulation)<br/>待模拟的网格（模拟过程中将被填充值）</param>
        /// <param name="ti">The training image grid<br/>训练图像网格</param>
        /// <param name="mould">The simulation template (search neighborhood)<br/>模拟模板（搜索邻域）</param>
        /// <param name="progress_for_retrieve_inverse">
        /// The fraction of simulation progress to apply reverse query constraint (default = 0)<br/>
        /// 用于设置反向查询概率约束的模拟进度阈值（默认值为 0）
        /// </param>
        /// <param name="random_seed">Random seed for simulation reproducibility<br/>随机数种子，用于保证模拟的可重复性</param>
        /// <returns>The simulated GridProperty with filled values<br/>完成模拟后的网格属性</returns>
        public static GridProperty run(GridProperty re, GridProperty ti, Mould mould,
            int progress_for_retrieve_inverse = 0, int random_seed = 1111)
        {
            MyConsoleHelper.write_value_to_console("\nSNESIM模拟开始");

            MersenneTwister mt = new((uint)random_seed);

            STree tree = STree.create(mould, ti);
            if (tree == null)
                return (null);

            Dictionary<int, int> nod_cut = [];
            Dictionary<int, double> pdf = []; //全局相概率
            Dictionary<int, double> cpdf = []; //条件约束相概率
            List<float?> categories = []; //离散变量的取值范围

            var category_freq = ti.discrete_category_freq(false);
            for (int i = 0; i < category_freq.Count; i++)
            {
                nod_cut.Add((int)category_freq[i].value, 0);
                pdf.Add((int)category_freq[i].value, category_freq[i].freq);
                categories.Add(category_freq[i].value);
            }

            var path = SimulationPath.create(re.grid_structure, 1, mt);

            while (path.is_visit_over() == false)
            {
                var si = path.visit_next();
                var value_si = re.get_value(si);
                if (value_si == null)
                {
                    float?[] buffer_re = new float?[mould.neighbors_number];
                    mould.get_neighbor_values(si, re, buffer_re, out float? core_value,
                        out bool has_valid_neighbor_data,
                        out bool has_all_valid_neighbor_data);

                    //有条件数据的情况,从搜索树取回条件数据的cpdf
                    if (has_valid_neighbor_data)
                    {
                        cpdf = [];
                        Dictionary<int, int> core_values;
                        if (path.progress <= progress_for_retrieve_inverse)
                            core_values = tree.retrieve_inverse(mould, buffer_re, core_value, 1);
                        else
                            core_values = tree.retrieve(mould, buffer_re, 1);

                        //有取回重复数，计算条件概率
                        if (core_values != null)
                        {
                            int sumrepl = 0; //重复数总数
                            sumrepl = core_values.Sum(a => a.Value);

                            foreach (var category in tree.categories)
                            {
                                cpdf.Add(category, core_values[category] / (float)sumrepl);
                            }
                        }
                    }

                    if (cpdf.Count == 0)
                        cpdf = pdf;
                    var value = SamplingHelper.sample<int>(cpdf.Select(kv => (kv.Key, kv.Value)), mt.NextDouble());
                    re.set_value(si, value);
                }

                MyConsoleProgress.print(path.progress, $"snesim"); //更新进度
            }

            return re;
        }

        /// <summary>
        /// 多分辨率模拟
        /// </summary>
        /// <param name="re"></param>
        /// <param name="ti"></param>
        /// <param name="template_settings_per_level"></param>
        /// <param name="progress_for_retrieve_inverse"></param>
        /// <param name="random_seed"></param>
        /// <returns></returns>
        public static GridProperty run_multi_resolution(
            GridProperty re,
            GridProperty ti,
            List<(int max_number, double rx, double ry, double rz)> template_settings_per_level,
            int progress_for_retrieve_inverse = 0,
            int random_seed = 1111)
        {
            int N_pyramid = template_settings_per_level.Count - 1;

            int factor = 2;
            int factor_z = 2;

            // 构建 TI 金字塔（fine -> coarse）
            List<GridProperty> ti_pyramid = [ti];
            GridProperty current_ti = ti;
            for (int i = 0; i < N_pyramid; i++)
            {
                current_ti = GridPyramid.pyramid_downsample_smooth(current_ti, factor, factor, factor_z);
                ti_pyramid.Add(current_ti); // fine在前 coarse在后
            }

            //打印ti金字塔
            // for (int i = 0; i < ti_pyramid.Count; i++)
            // {
            //     ti_pyramid[i].show_win($"第{i}个TI");
            // }


            // 构建 RE 金字塔（fine -> coarse）
            List<GridProperty> re_pyramid = [re];
            GridProperty current_re = re;
            for (int i = 0; i < N_pyramid; i++)
            {
                GridStructure coarse_gs = ti.grid_structure.dim == Dimension.D2
                    ? current_re.grid_structure.coarse_by_factor(factor, factor, 1)
                    : current_re.grid_structure.coarse_by_factor(factor, factor, factor_z);

                current_re = GridPyramid.project_hard_data_to_coarse(current_re, coarse_gs, true);
                re_pyramid.Add(current_re);
            }

            //打印re金字塔
            // for (int i = 0; i < re_pyramid.Count; i++)
            // {
            //     re_pyramid[i].show_win($"第{i}个RE");
            // }
            //
            // // return null;


            // 多分辨率模拟(coarse -> fine)
            for (int i = N_pyramid; i >= 0; i--)
            {
                // 每层的模板设置
                var (max_number, rx, ry, rz) = template_settings_per_level[i];

                Mould mould = ti.grid_structure.dim == Dimension.D2
                    ? Mould.create_by_anisotropic_topk_2d(max_number, rx, ry, 1)
                    : Mould.create_by_anisotropic_topk_3d(max_number, rx, ry, rz, 1);

                if (i == N_pyramid) // 最粗层
                {
                    current_re = run(re_pyramid[N_pyramid], ti_pyramid[N_pyramid], mould, progress_for_retrieve_inverse,
                        random_seed);
                }
                else
                {
                    // 次一层的TI
                    current_ti = ti_pyramid[i];
                    // 上一层插值作为当前层的条件
                    current_re = GridPyramid.project_hard_data_to_fine_loose(current_re, re_pyramid[i]);

                    current_re = run(current_re, current_ti, mould, progress_for_retrieve_inverse, random_seed);
                }

                re_pyramid[i] = current_re;
            }

            return re_pyramid[0]; // 返回最高分辨率结果
        }
    }
}