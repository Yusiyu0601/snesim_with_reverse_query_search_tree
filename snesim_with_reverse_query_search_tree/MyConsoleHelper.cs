using System.Globalization;
using System.Text;

namespace JAM8.Utilities
{
    /// <summary>
    /// Console 辅助类，提供从控制台读取不同类型输入的方法。
    /// </summary>
    public class MyConsoleHelper
    {
        /// <summary>
        /// 从控制台读取一个值（支持 int、float、double、string），支持 Esc 取消。
        /// </summary>
        /// <typeparam name="T">输入值的类型：int、float、double 或 string</typeparam>
        /// <param name="content">主要提示信息</param>
        /// <param name="prompt">附加提示，可为空</param>
        /// <param name="min">允许的最小值（仅数值类型生效，string 忽略）</param>
        /// <param name="max">允许的最大值（仅数值类型生效，string 忽略）</param>
        /// <param name="cancel_value">按 ESC 取消时返回的值</param>
        /// <returns>用户输入的值，或取消时返回 cancelValue</returns>
        public static T read_value_from_console<T>(string content, string prompt = null, T min = default,
            T max = default, T cancel_value = default)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // **新增：自动设置更合理的默认 cancelValue**
            if (EqualityComparer<T>.Default.Equals(cancel_value, default))
            {
                if (typeof(T) == typeof(int))
                    cancel_value = (T)(object)int.MinValue;
                else if (typeof(T) == typeof(float))
                    cancel_value = (T)(object)float.NaN;
                else if (typeof(T) == typeof(double))
                    cancel_value = (T)(object)double.NaN;
                else if (typeof(T) == typeof(string))
                    cancel_value = (T)(object)string.Empty;
            }

            bool isNumericType = typeof(T) == typeof(int) ||
                                 typeof(T) == typeof(float) ||
                                 typeof(T) == typeof(double);

            while (true)
            {
                // **关键：每次读取前清空缓冲区**
                while (Console.KeyAvailable)
                    Console.ReadKey(intercept: true);

                Console.WriteLine();
                Console.WriteLine(!string.IsNullOrEmpty(prompt) ? $"{content} [{prompt}]" : content);
                Console.Write($"请输入{GetTypeName<T>()} (按 ESC 取消) => ");

                var inputBuilder = new StringBuilder();
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);

                    // 按 ESC → 直接返回 cancelValue
                    if (key.Key == ConsoleKey.Escape)
                    {
                        Console.WriteLine();
                        Console.WriteLine("用户取消输入。");
                        return cancel_value;
                    }

                    // 按 Enter → 完成输入
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        break;
                    }

                    // 支持退格
                    if (key.Key == ConsoleKey.Backspace && inputBuilder.Length > 0)
                    {
                        inputBuilder.Remove(inputBuilder.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        inputBuilder.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                }

                string input = inputBuilder.ToString();

                // **字符串类型直接返回**
                if (typeof(T) == typeof(string))
                {
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine("输入不能为空，请重新输入。");
                        continue;
                    }

                    return (T)(object)input;
                }

                // **数值类型：解析与范围检查**
                try
                {
                    var convertedValue = (T)Convert.ChangeType(input, typeof(T), CultureInfo.InvariantCulture);

                    // 如果设置了 min/max，则检查范围
                    if (!EqualityComparer<T>.Default.Equals(min, default) ||
                        !EqualityComparer<T>.Default.Equals(max, default))
                    {
                        double val = Convert.ToDouble(convertedValue);
                        double minVal = Convert.ToDouble(min);
                        double maxVal = Convert.ToDouble(max);

                        if (minVal != 0 || maxVal != 0) // 避免默认值限制
                        {
                            if (val < minVal || val > maxVal)
                            {
                                Console.WriteLine($"请输入 {minVal} 到 {maxVal} 之间的值。");
                                continue;
                            }
                        }
                    }

                    return convertedValue;
                }
                catch
                {
                    Console.WriteLine($"输入无效，请输入一个有效的{GetTypeName<T>()}。");
                }
            }
        }

        /// <summary>
        /// 获取类型的友好名称
        /// </summary>
        private static string GetTypeName<T>()
        {
            if (typeof(T) == typeof(int)) return "整数";
            if (typeof(T) == typeof(float)) return "浮点数";
            if (typeof(T) == typeof(double)) return "双精度浮点数";
            return "字符串";
        }

        /// <summary>
        /// 向控制台输出一段内容（支持任意类型），并根据提供的提示信息显示额外说明。
        /// 
        /// 【功能特点】
        /// - ✅ 支持任意类型（泛型 T），通过 ToString() 自动转换；
        /// - ✅ 默认在输出前添加空行，便于控制台输出的视觉分隔；
        /// - ✅ 若 content 为 null 或空字符串，则只输出空行（防止无效信息输出）；
        /// - ✅ 若指定 prompt，将以“[说明]”形式追加在主内容后；
        /// - ✅ 适用于调试、结果标注、结构化打印等场景。
        /// </summary>
        /// <typeparam name="T">要输出的内容类型，例如 string、int、float、double、对象等。</typeparam>
        /// <param name="content">
        /// 要输出的主体内容。可以是任意类型（值类型、引用类型、对象等）。
        /// 若为 null 或空字符串，将不会输出内容，仅插入空行。
        /// </param>
        /// <param name="prompt">
        /// 可选提示信息，作为内容的附加说明，会显示为 “[提示]”。
        /// 若为 null 或空字符串，则仅输出主体内容。
        /// </param>
        public static void write_value_to_console<T>(T content = default, string prompt = null)
        {
            // 输出空行，保持视觉分隔
            Console.WriteLine();

            // 如果内容为 null 或（对字符串类型）为空，则直接返回
            if (content == null)
            {
                return;
            }

            // 转换为字符串
            string contentStr = content.ToString();

            // 特殊处理：如果是 string 且为空，也只输出空行
            if (typeof(T) == typeof(string) && string.IsNullOrEmpty(contentStr))
            {
                return;
            }

            // 输出内容，带或不带提示
            if (!string.IsNullOrEmpty(prompt))
            {
                Console.WriteLine($"{contentStr} [{prompt}]");
            }
            else
            {
                Console.WriteLine(contentStr);
            }
        }

    }
}