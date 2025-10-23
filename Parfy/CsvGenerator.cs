using Parfy.Model;
using System.Text;

namespace Parfy
{
    public class CsvProcessor(IConsole console)
    {
        public void GenerateParfy(List<Component> components, DirectoryInfo outputDirectory)
        {
            StringBuilder sb = new(
                "Название вещества (RUS);" +
                "Название вещества (ENG);" +
                "Описание;" +
                "Короткое описание;" +
                "Ссылка");
            sb.AppendLine();

            foreach (Component component in components)
            {
                sb.AppendLine(
                    $"{EscapeForCsv(component.NameRUS)};" +
                    $"{EscapeForCsv(component.NameENG)};" +
                    $"{EscapeForCsv(component.Description)};" +
                    $"{EscapeForCsv(component.ShortDescription)};" +
                    $"{component.Url}");
            }

            FileInfo outputFile = WriteFile(
                new(Path.Combine(outputDirectory.FullName, $"parfy_source_{DateTime.Now.ToFileTime()}.csv")),
                sb);

            console.WriteLine($"Готово. Файл записан по пути {outputFile.FullName}", EConsoleStatus.Success);
        }

        public void GenerateAnalysis(NotesToComponentsAnalysis analysis, FileInfo outputFile)
        {
            StringBuilder sb = new(
                "Название вещества (RUS);" +
                "Название вещества (ENG);" +
                "Описание;" +
                "Короткое описание;" +
                "Ссылка");
            sb.AppendLine();

            foreach (KeyValuePair<string, List<АppropriateComponent>> note in analysis.NoteToComponents)
            {
                string capitalizedNote = note.Key.Substring(0, 1).ToUpper() + note.Key.Substring(1);
                sb.AppendLine(capitalizedNote);

                foreach (АppropriateComponent component in note.Value)
                {
                    sb.AppendLine(
                        $"{EscapeForCsv(component.FoundComponent.NameRUS)};" +
                        $"{EscapeForCsv(component.FoundComponent.NameENG)};" +
                        $"{EscapeForCsv(component.FoundComponent.Description)};" +
                        $"{EscapeForCsv(component.FoundComponent.ShortDescription)};" +
                        $"{component.FoundComponent.Url};" +
                        $"{component.Entries.Aggregate((x, y) => $"{x},{y}")}");
                }
            }

            sb.AppendLine("Синергии 1-ого уровня глубины");
            sb.AppendLine(
                "Название вещества (RUS);" +
                "Название вещества (ENG);" +
                "Описание;" +
                "Короткое описание;" +
                "Ссылка" +
                "Вхождение;" +
                "Вес вхождения");

            if (analysis.Synergies.Count > 0)
            {
                IEnumerable<IGrouping<string, Synergy>> groupsByName = analysis.Synergies.First()
                    .Value.GroupBy(x => x.Source.NameENG);

                foreach (IGrouping<string, Synergy> group in groupsByName)
                {
                    sb.AppendLine($"Синергия для;{group.Key}");

                    foreach (Synergy synergy in group)
                    {
                        Component synergent = synergy.Synergent;

                        sb.AppendLine(
                            $"{EscapeForCsv(synergent.NameRUS)};" +
                            $"{EscapeForCsv(synergent.NameENG)};" +
                            $"{EscapeForCsv(synergent.Description)};" +
                            $"{EscapeForCsv(synergent.ShortDescription)};" +
                            $"{synergent.Url};" +
                            $"{synergy.Entry}" +
                            $"{synergy.Weight}");
                    }
                }
            }

            WriteFile(outputFile, sb);

            console.WriteLine($"Готово. Файл записан по пути {outputFile.FullName}", EConsoleStatus.Success);
        }

        public List<Component> ReadComponents(FileInfo input)
        {
            List<Component> components = [];

            try
            {
                string[] lines = File.ReadAllLines(input.FullName);

                if (lines.Length == 0)
                {
                    console.WriteLine("Файл пуст.", EConsoleStatus.Error);

                    return components;
                }

                string[] headers = lines[0].Split(';').Select(h => h.Trim()).ToArray();

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] values = ParseCsvLine(lines[i]);

                    if (values.Length != headers.Length)
                    {
                        console.WriteLine(
                            $"Предупреждение: строка {i + 1} содержит {values.Length} значений, " +
                            $"ожидается {headers.Length}. Строка пропущена.",
                            EConsoleStatus.Error);

                        continue;
                    }

                    Component component = new();

                    for (int j = 0; j < headers.Length; j++)
                    {
                        if (!Enum.IsDefined(typeof(EComponentSourceMapping), j))
                        {
                            Console.WriteLine(
                                $"Предупреждение: неизвестный заголовок '{headers[j]}' в строке {i + 1}",
                                EConsoleStatus.Error);

                            break;
                        }

                        switch ((EComponentSourceMapping)j)
                        {
                            case EComponentSourceMapping.NameRUS:
                                component.NameRUS = values[j];
                                break;
                            case EComponentSourceMapping.NameENG:
                                component.NameENG = values[j];
                                break;
                            case EComponentSourceMapping.Description:
                                component.Description = values[j];
                                break;
                            case EComponentSourceMapping.ShortDescription:
                                component.ShortDescription = values[j];
                                break;
                            case EComponentSourceMapping.Url:
                                component.Url = values[j];
                                break;
                            default:
                                break;
                        }
                    }

                    components.Add(component);
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Файл не найден: {input.FullName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении файла: {ex.Message}");
            }

            return components;
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> values = [];
            string current = string.Empty;
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ';' && !inQuotes)
                {
                    values.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            values.Add(current.Trim());

            return [.. values];
        }

        private static FileInfo WriteFile(FileInfo outputFile, StringBuilder sb)
        {
            using (StreamWriter writer = new(outputFile.FullName, false, new UTF8Encoding(true)))
            {
                writer.Write(sb.ToString());
            }

            return outputFile;
        }

        private string EscapeForCsv(string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

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

        private enum EComponentSourceMapping
        {
            NameRUS = 0,
            NameENG = 1,
            Description = 2,
            ShortDescription = 3,
            Url = 4
        }
    }
}
