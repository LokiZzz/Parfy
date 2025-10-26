using FuzzySharp;
using Parfy.Model;
using System.Text.RegularExpressions;

namespace Parfy
{
    public partial class NotesToComponentsAnalyser(IConsole console)
    {
        private readonly static int _fuzzyWeightTreshold = 70;

        /// <summary>
        /// Найти подходящие вещества
        /// </summary>
        /// <param name="sourceComponents">Список компонентов, в которых нужно искать.</param>
        /// <param name="notesInput">Ноты, для которых ищутся компоненты</param>
        /// <param name="synergyDepth">
        /// При поиске компонентов в описании могут быть указаны другие компоненты,
        /// с которыми у источника синергия. У них в свою очередь тоже могут быть указаны синергии. 
        /// Данный параметр указывает сколько раз был проведён поиск синергий.
        /// </param>
        /// <returns></returns>
        public NotesToComponentsAnalysis Analyse(
            List<Component> sourceComponents,
            List<(string Note, string[] Exclude)> notesInput,
            string[]? excludeEntryTokens = null)
        {
            console.WriteLine("Начат анализ композиции.");

            NotesToComponentsAnalysis result = new();
            List<(string Note, string[] Exclude)> notes = GetNormalizedNotes(notesInput);

            console.WriteLine($"Нормализованный список нот: " +
                $"{notes.Select(x => x.Note).Aggregate((x, y) => $"{x},{y}")}");

            foreach ((string Note, string[] Exclude) item in notes)
            {
                console.WriteLine($"\nОбработка ноты: {item.Note} ({notes.IndexOf(item) + 1}/{notes.Count})");

                List<АppropriateComponent> foundComponents = [];

                foreach (Component component in sourceComponents)
                {
                    console.WriteLine($"Проверка вещества: {component.OriginalName}");

                    string[] exclude = excludeEntryTokens is null 
                        ? item.Exclude
                        : [.. excludeEntryTokens.Union(item.Exclude)];
                    bool found = ComponentHasEntries(
                        component,
                        item.Note,
                        exclude,
                        out List<(string Entry, int Weight)>? entries);

                    console.ClearLastLine();

                    if (found)
                    {
                        IEnumerable<string> entriesText = entries.Select(x => x.Entry);

                        console.WriteLine(
                            $"Найдено вещество: {component.OriginalName}");
                        console.WriteLine(
                            $"-- Вхождения ({entriesText.Count()}): {entriesText.Aggregate((x, y) => $"{x},{y}")}");

                        foundComponents.Add(
                            new АppropriateComponent
                            {
                                Entries = [.. entriesText],
                                FoundComponent = component
                            }
                        );
                    }
                }

                result.NoteToComponents.Add(item.Note, foundComponents);
            }

            console.WriteLine("\nПоиск синергий.");

            IEnumerable<Component> allFoundComponents = result.NoteToComponents
                .SelectMany(x => x.Value.Select(x => x.FoundComponent));
            List<Synergy> synergies = [];

            foreach (Component baseComponent in allFoundComponents)
            {
                console.WriteLine($"\nПоиск синергии для {baseComponent.OriginalName}");

                foreach (Component synergyCandidate in sourceComponents)
                {
                    if (TryFindSynergy(baseComponent, synergyCandidate, out string entry, out int weight)
                        && !baseComponent.NameENG.Equals(synergyCandidate.NameENG))
                    {
                        console.WriteLine($"Найдена синергия: {synergyCandidate.OriginalName}");
                        console.WriteLine($"-- Вхождение: {entry}, вес вхождения: {weight}");

                        synergies.Add(
                            new Synergy 
                            { 
                                Source = baseComponent,
                                Synergent = synergyCandidate,
                                Entry = entry,
                                Weight = weight
                            }
                        );
                    }
                }
            }

            result.Synergies.Add(1, synergies);

            console.WriteLine($"\nАнализ закончен.");

            return result;
        }

        /// <summary>
        /// Определить вхождения при помощи неточного поиска
        /// </summary>
        /// <param name="component">Кандидат на вещество</param>
        /// <param name="note">Нота для поиска</param>
        /// <param name="optimize">Если true, то сразу после нахождения вхождения выйдет из метода.</param>
        private static bool ComponentHasEntries(
            Component component,
            string note,
            string[] exclude,
            out List<(string Entry, int Weight)> entries,
            bool optimize = false)
        {
            entries = [];

            bool isNameENGEntry =
                TrySearch(
                    component.NameENG,
                    note,
                    exclude,
                    out List<(string Entry, int Weight)> e1, out _, out _);
            entries.AddRange(e1);

            if (isNameENGEntry && optimize)
            {
                return true;
            }

            bool isNameRUSEntry =
                TrySearch(
                    component.NameRUS,
                    note,
                    exclude,
                    out List<(string Entry, int Weight)> e2, out _, out _);
            entries.AddRange(e2);

            if (isNameRUSEntry && optimize)
            {
                return true;
            }

            bool isShortDescEntry =
                TrySearch(
                    component.ShortDescription,
                    note,
                    exclude,
                    out List<(string Entry, int Weight)> e3, out _, out _);
            entries.AddRange(e3);

            if (isShortDescEntry && optimize)
            {
                return true;
            }

            bool isDescEntry =
                TrySearch(
                    component.Description,
                    note,
                    exclude,
                    out List<(string Entry, int Weight)> e4, out _, out _);
            entries.AddRange(e4);

            if (isDescEntry && optimize)
            {
                return true;
            }

            return isNameENGEntry || isNameRUSEntry || isShortDescEntry || isDescEntry;
        }

        public static bool TryFindSynergy(
            Component component,
            Component synergent,
            out string entry,
            out int weight)
        {
            if(TrySearch(component.Description, synergent.NameENG, [], out _, out entry, out weight))
            {
                return true;
            }

            return TrySearch(component.Description, synergent.NameRUS, [], out _, out entry, out weight);
        }

        public static bool TrySearch(
            string largeText,
            string targetPhrase,
            string[] exclude,
            out List<(string Entry, int Weight)> entries,
            out string bestEntry,
            out int bestWeight)
        {
            entries = [];
            bestEntry = string.Empty;
            bestWeight = 0;

            largeText = NonLetters().Replace(largeText.ToLower(), "");
            IEnumerable<string> textWords = SplitByWords(largeText);

            targetPhrase = NormalizeComponentName(targetPhrase);
            int windowSize = SplitByWords(targetPhrase).Count();

            // Создаем фразы скользящим окном
            List<string> windows = [];

            for (int i = 0; i <= textWords.Count() - windowSize; i++)
            {
                string window = string.Join(" ", textWords.Skip(i).Take(windowSize));
                windows.Add(window);
            }

            foreach (string window in windows)
            {
                int currentWindowWeight = Fuzz.Ratio(targetPhrase, window);
                
                if (currentWindowWeight > bestWeight)
                {
                    bestWeight = currentWindowWeight;
                    bestEntry = window;
                }

                if (currentWindowWeight >= _fuzzyWeightTreshold)
                {
                    if (!exclude.Any( x => x.Equals(window, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        entries.Add((window, currentWindowWeight));
                    }
                }
            }

            return entries.Count != 0;
        }

        public static string NormalizeComponentName(string name)
        {
            name = NonLetters().Replace(name.ToLower(), "") + " ";
            name = RemoveTrashFromName(name);
            name = name.Trim();

            return name;
        }

        public static string RemoveTrashFromName(string name)
        {
            name = Regex.Replace(name, "iff", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "bedoukian", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "givaudan", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "synarome", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "firmenich", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "givaudan", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "symrise", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "robertet", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " pg", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " in ", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " dpg", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " alc ", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @" alc\.", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "кристаллическое", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "вещество", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "эфирное масло", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "абсолют", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "натуральный", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "natural", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "природный", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " мл", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, " eo ", string.Empty, RegexOptions.IgnoreCase);

            return name;
        }

        /// <summary>
        /// Разделить через запятую, если нота — это несколько слов, 
        /// то разделить по пробелу и дополнительно добавить все части в список нот.
        /// </summary>
        private static List<(string Note, string[] Exclude)> GetNormalizedNotes(
            List<(string Note, string[] Exclude)> notesInput)
        {
            List<(string Note, string[] Exclude)> result = [];

            foreach ((string Note, string[] Exclude) item in notesInput)
            {
                result.Add((item.Note, item.Exclude));
                string[] splitted = item.Note.Split(' ');

                if(splitted.Length > 1)
                {
                    result.AddRange(splitted.Select(x => (x.Trim(), item.Exclude)));
                }
            }

            return [.. result.Distinct()];
        }

        private static IEnumerable<string> SplitByWords(string input) => 
            input.Split(
                [' ', ',', '.', '!', '?', ';', ':', '\n', '\r', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        [GeneratedRegex(@"[^а-яёa-z ]", RegexOptions.IgnoreCase, "ru-RU")]
        private static partial Regex NonLetters();
    }
}
