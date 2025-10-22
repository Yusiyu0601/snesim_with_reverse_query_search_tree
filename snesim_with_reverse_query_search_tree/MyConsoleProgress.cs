namespace JAM8.Utilities
{
    /// <summary>
    /// 控制台进度显示
    /// </summary>
    public class MyConsoleProgress
    {
        #region 静态方法

        private static string preview = "";
        private static readonly object lockObj = new();

        /// <summary>
        /// 打印进度（百分比版），支持控制台高亮、完成提示与标签。
        /// </summary>
        /// <param name="progress">进度百分比（范围 0.0 ~ 100.0）</param>
        /// <param name="text">进度条说明文本</param>
        /// <param name="tag">可选标签</param>
        /// <param name="nextline_at_end">是否在100%时换行打印完成提示</param>
        public static void print(double progress, string text, string tag = null, bool nextline_at_end = true)
        {
            // 进度值范围检查
            if (progress < 0 || progress > 100)
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");

            string progressStr = progress.ToString("0.0");

            lock (lockObj)
            {
                if (progress >= 100.0 && nextline_at_end)
                {
                    Console.Write("\r"); // 清除之前的输出
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"[{text}] progress = {progressStr}%");
                    if (tag != null)
                    {
                        Console.Write($"  [{tag}]");
                    }
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.Cyan; // 与“按任意键返回”区分
                    Console.WriteLine("  Finished");
                    Console.ResetColor();

                    preview = ""; // 重置状态
                    return;
                }

                if (preview == progressStr) return;
                preview = progressStr;

                Console.Write("\r");
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(text);
                Console.ResetColor();
                Console.Write("] progress = ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{progressStr}%");
                Console.ResetColor();

                if (tag != null)
                {
                    Console.Write("  [");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write(tag);
                    Console.ResetColor();
                    Console.Write("]");
                }

                Console.Write("   ");
            }
        }

        /// <summary>
        /// 打印任务进度（控制台单行刷新，可选颜色与标签）
        /// </summary>
        /// <param name="current">当前位置</param>
        /// <param name="max">总数（必须大于 0）</param>
        /// <param name="text">任务描述</param>
        /// <param name="tag">附加标签（可选）</param>
        /// <param name="nextline_at_end">是否在完成后换行并打印完成提示</param>
        public static void print(long current, long max, string text, string tag = null, bool nextline_at_end = true)
        {
            if (max <= 0)
                throw new ArgumentException("The total count (max) must be greater than 0.", nameof(max));

            // 计算进度百分比
            string progressStr = Math.Round((double)current / max * 100, 1).ToString("0.0");

            lock (lockObj)
            {
                // 完成后输出带颜色的“完成”
                if (current >= max && nextline_at_end)
                {
                    Console.Write("\r"); // 回到行首清除前一行
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"[{text}] progress = {progressStr}%");
                    if (tag != null)
                    {
                        Console.Write($"  [{tag}]");
                    }
                    Console.Write("  Finished\n");
                    Console.ResetColor();
                    preview = ""; // 重置状态
                    return;
                }

                // 若进度未变化则不输出
                if (preview == progressStr) return;
                preview = progressStr;

                // 输出进度（高亮颜色）
                Console.Write("\r"); // 回到行首
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(text);
                Console.ResetColor();
                Console.Write("] progress = ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{progressStr}%");
                Console.ResetColor();

                if (tag != null)
                {
                    Console.Write("  [");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write(tag);
                    Console.ResetColor();
                    Console.Write("]");
                }

                Console.Write("   ");
            }
        }


        #endregion

        #region 实例方法，可打印剩余时间

        private DateTime time_start = DateTime.Now;//起始时间
        private DateTime time_last = DateTime.Now;//上一次调用print的时间
        private DateTime time_last_100;//前N次调用print的时间
        private TimeSpan time_span_100;//N次时间间隔
        private int flag = 0;
        public MyConsoleProgress()
        {
            time_last = DateTime.Now;
            time_start = DateTime.Now;
        }

        /// <summary>
        /// 打印进度，可以显示剩余时间
        /// </summary>
        /// <param name="current"></param>
        /// <param name="max"></param>
        /// <param name="text"></param>
        /// <param name="tag"></param>
        public void PrintWithRemainTime(long current, long max, string text, string tag = null)
        {
            flag++;
            if (flag % 100 == 0)//计算N次的间隔时间
            {
                time_last_100 = time_last;
                time_last = DateTime.Now;
                time_span_100 = time_last - time_last_100;
            }
            double time_remain = (max - current) * time_span_100.TotalMilliseconds / 100;
            print(current, max, text, $"{tag} --- 已用:{(int)(time_last - time_start).TotalSeconds}秒 " +
                $"剩余:{Convert.ToInt32(time_remain / 1000)}秒");

        }

        #endregion
    }
}
