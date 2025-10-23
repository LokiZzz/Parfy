using Parfy.Model;

namespace Parfy
{
    public class NotesToComponentsAnalyser(IConsole console)
    {
        private readonly int _fuzzyWeightTreshold = 60;

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
            string notesInput,
            int synergyDepth = 2)
        {
            console.WriteLine("Начат анализ композиции.");
            NotesToComponentsAnalysis result = new();
            List<string> notes = GetNormalizedNotes(notesInput);
            console.WriteLine($"Нормализованный список нот: {notes.Aggregate((x, y) => $"{x},{y}")}");

            foreach (string note in notes)
            {
                console.WriteLine($"Обработка ноты: {note} ({notes.IndexOf(note) + 1}/{notes.Count})");
                List<АppropriateComponent> foundComponents = [];

                foreach (Component component in sourceComponents)
                {
                    console.WriteLine($"Проверка вещества: {component.NameENG}");
                    bool found = ComponentHasFuzzyEntries(component, note, out List<string> entries);
                    console.ClearLastLine();

                    if (found)
                    {
                        console.WriteLine($"Найдено вещество: {component.NameENG}");
                        console.WriteLine($"Совпадения: {entries.Aggregate((x, y) => $"{x},{y}")}");

                        foundComponents.Add(
                            new АppropriateComponent
                            {
                                Entries = entries,
                                FoundComponent = component
                            }
                        );
                    }
                }

                result.NoteToComponents.Add(note, foundComponents);
            }

            console.WriteLine($"Анализ закончен.");

            return result;
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
                int weight = FuzzySharp.Fuzz.Ratio(word, query);

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
        private List<string> GetNormalizedNotes(string notesInput)
        {
            IEnumerable<string> splittedTrimmed = notesInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            List<string> normalized = [.. splittedTrimmed];

            foreach (string item in splittedTrimmed)
            {
                IEnumerable<string> splittedBySpace = item
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());

                if (splittedBySpace.Count() > 1)
                {
                    normalized.AddRange(splittedBySpace);
                }
            }

            return [.. normalized.Select(x => x.ToLower())];
        }

        private IEnumerable<string> SplitByWords(string input)
        {
            return input.Split([' ', ',', '.', '!', '?', ';', ':', '\n', '\r', '\t'])
                .Select(x => x.Trim());
        }
    }

    public class NotesToComponentsAnalysis
    {
        /// <summary>
        /// Список компонентов для каждой перечисленной ноты.
        /// </summary>
        public Dictionary<string, List<АppropriateComponent>> NoteToComponents { get; set; } = [];

        /// <summary>
        /// При поиске компонентов в описании могут быть указаны другие компоненты, с которыми у источника синергия.
        /// У них в свою очередь тоже могут быть указаны синергии. 
        /// Ключ-параметр указывает сколько раз был проведён поиск синергий, то есть на уровень поиска синергий.
        /// </summary>
        public Dictionary<int, List<Synergy>> Synergies { get; set; } = [];
    }

    public class АppropriateComponent
    {
        public Component FoundComponent { get; set; } = new();

        public List<string> Entries { get; set; } = [];
    }

    public class Synergy
    {
        public Component Source { get; set; } = new();

        public Component Synergent { get; set; } = new();

        public List<string> Entries { get; set; } = [];
    }
}
