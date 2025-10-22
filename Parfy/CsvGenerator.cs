using Parfy.Model;
using System.Text;

namespace Parfy
{
    public class CsvGenerator(IConsole console)
    {
        public void Generate(List<Component> components, DirectoryInfo outputDirectory)
        {
            StringBuilder sb = new("Название вещества (RUS);Название вещества (ENG);Описание;Короткое описание;Ссылка");

            foreach (Component component in components)
            {
                sb.Append(Environment.NewLine);
                sb.Append(
                    $"{EscapeForCsv(component.NameRUS)};" +
                    $"{EscapeForCsv(component.NameENG)};" +
                    $"{EscapeForCsv(component.Description)};" +
                    $"{EscapeForCsv(component.ShortDescription)};" +
                    $"{component.Url}");
            }

            string fileName = $"parfy_{DateTime.Now.ToFileTime()}.csv";
            FileInfo outputFile = new(Path.Combine(outputDirectory.FullName, fileName));

            using (StreamWriter writer = new(outputFile.FullName, false, new UTF8Encoding(true)))
            {
                writer.Write(sb.ToString());
            }

            console.WriteLine($"Готово. Файл записан по пути {outputFile.FullName}", EConsoleStatus.Success);
        }

        private string EscapeForCsv(string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            // Если есть запятые, кавычки или переносы строк — заключаем в кавычки
            if (input.Contains(';')
                || input.Contains('"')
                || input.Contains('\n')
                || input.Contains('\r'))
            {
                string escaped = input.Replace("\"", "\"\"");

                return $"\"{escaped}\"";
            }

            return input;
        }
    }
}
