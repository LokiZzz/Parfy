using FuzzySharp;
using Parfy.Model;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Parfy
{
    public partial class NotesToComponentsAnalyser(IConsole console)
    {
        private readonly int _fuzzyWeightTreshold = 70;
        private readonly int _fuzzyWeightWindowTreshold = 70;

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
                    console.WriteLine($"Проверка вещества: {component.NameENG}");
                    bool found = ComponentHasFuzzyEntries(component, item.Note, out List<string> entries);
                    console.ClearLastLine();

                    if (found)
                    {
                        console.WriteLine(
                            $"Найдено вещество: {component.NameENG}");
                        console.WriteLine(
                            $"-- Вхождения ({entries.Count}): {entries.Aggregate((x, y) => $"{x},{y}")}");

                        if (CheckBannedEntries(entries, excludeEntryTokens)
                            || CheckBannedEntries(entries, item.Exclude))
                        {
                            continue;
                        }

                        foundComponents.Add(
                            new АppropriateComponent
                            {
                                Entries = entries,
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
                console.WriteLine($"\nПоиск синергии для {baseComponent.NameENG}");

                foreach (Component synergyCandidate in sourceComponents)
                {
                    if (TryToFindSynergy(baseComponent, synergyCandidate, out string entry, out int weight)
                        && !baseComponent.NameENG.Equals(synergyCandidate.NameENG))
                    {
                        console.WriteLine($"Найдена синергия: {synergyCandidate.NameENG}");
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
        /// Проверить наличие вхождений в бане.
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="exclude"></param>
        /// <returns></returns>
        private bool CheckBannedEntries(List<string> entries, string[]? exclude)
        {
            if (exclude is not null && exclude.Length > 0)
            {
                exclude = exclude.Select(x => x.ToLower()).ToArray();

                return entries.All(entry => 
                    exclude.Any(ban => entry.Contains(ban, StringComparison.CurrentCultureIgnoreCase))
                );
            }

            return false;
        }

        private bool TryToFindSynergy(
            Component baseComponent,
            Component synergyCandidate,
            out string entry,
            out int weight)
        {
            if (TrySearchComponent(baseComponent.Description, synergyCandidate.NameENG, out entry, out weight))
            {
                return true;
            }
            else if (TrySearchComponent(baseComponent.Description, synergyCandidate.NameRUS, out entry, out weight))
            {
                return true;
            }

            return false;
        }

        public bool TrySearchComponent(
            string largeText,
            string targetPhrase,
            out string bestEntry,
            out int bestWeight)
        {
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

                if (currentWindowWeight > _fuzzyWeightWindowTreshold)
                {
                    return true;
                }
            }

            return false;
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
        /// Определить вхождения при помощи неточного поиска
        /// </summary>
        /// <param name="component">Кандидат на вещество</param>
        /// <param name="note">Нота для поиска</param>
        /// <param name="optimize">Если true, то сразу после нахождения вхождения выйдет из метода.</param>
        private bool ComponentHasFuzzyEntries(
            Component component,
            string note,
            out List<string> entries,
            bool optimize = false)
        {
            entries = [];
            bool isNameENGEntry = 
                TrySearchFuzzy(component.NameENG, note, out List<string> entriesInNameENG);
            entries.AddRange(entriesInNameENG);

            if(isNameENGEntry && optimize)
            {
                return true;
            }

            bool isNameRUSEntry = 
                TrySearchFuzzy(component.NameRUS, note, out List<string> entriesInNameRUS);
            entries.AddRange(entriesInNameRUS);

            if (isNameRUSEntry && optimize)
            {
                return true;
            }

            bool isShortDescEntry = 
                TrySearchFuzzy(component.ShortDescription, note, out List<string> entriesInShortDescription);
            entries.AddRange(entriesInShortDescription);

            if (isShortDescEntry && optimize)
            {
                return true;
            }

            bool isDescEntry = 
                TrySearchFuzzy(component.Description, note, out List<string> entriesInDescription);
            entries.AddRange(entriesInDescription);

            if (isDescEntry && optimize)
            {
                return true;
            }

            return isNameENGEntry || isNameRUSEntry || isShortDescEntry || isDescEntry;
        }

        private bool TrySearchFuzzy(string input, string query, out List<string> entries)
        {
            List<(string Entry, int Weigth)> entriesWithWeights = [];

            foreach (string word in SplitByWords(input))
            {
                int weight = Fuzz.Ratio(word, query);

                if (weight >= _fuzzyWeightTreshold)
                {
                    entriesWithWeights.Add((word, weight));
                }
            }

            entries = [.. entriesWithWeights.Select(x => x.Entry)];

            return entries.Count != 0;
        }

        /// <summary>
        /// Разделить через запятую, если нота — это несколько слов, то разделить по пробелу и дополнительно добавить
        /// все части в список нот.
        /// </summary>
        private List<(string Note, string[] Exclude)> GetNormalizedNotes(
            List<(string Note, string[] Exclude)> notesInput)
        {
            List<(string Note, string[] Exclude)> result = [];

            foreach ((string Note, string[] Exclude) item in notesInput)
            {
                result.Add((item.Note, item.Exclude));
                string[] splitted = item.Note.Split('-');

                if(splitted.Length > 1)
                {
                    result.AddRange(splitted.Select(x => (x.Trim(), item.Exclude)));
                }
            }

            return result;
        }

        private IEnumerable<string> SplitByWords(string input)
        {
            return input.Split(
                    [' ', ',', '.', '!', '?', ';', ':', '\n', '\r', '\t'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        [GeneratedRegex(@"[^а-яёa-z ]", RegexOptions.IgnoreCase, "ru-RU")]
        private static partial Regex NonLetters();
    }
}
