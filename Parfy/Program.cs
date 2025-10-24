using Parfy;
using Parfy.Model;
using System.CommandLine;

// Регенерация через JSON:
//string json = File.ReadAllText("C:\\Users\\lokiz\\OneDrive\\Рабочий стол\\parfy_source_134057677119011835.json");
//List<Component> components = JsonConvert.DeserializeObject<List<Component>>(json)!;
//new CsvProcessor(new ConsoleWrapper()).GenerateParfy(
//    components,
//    new DirectoryInfo($"C:\\Users\\lokiz\\OneDrive\\Рабочий стол"),
//    includeJson: false);

//return;

IConsole console = new ConsoleWrapper();

Command update = new("update", "Сканировать магазины с компонентами и создать таблицу.");

Option<DirectoryInfo> updateOut = new("--out")
{
    DefaultValueFactory = parseResult => new DirectoryInfo(Directory.GetCurrentDirectory())
};
update.Options.Add(updateOut);

Option<string[]> updateBan = new("--ban")
{
    DefaultValueFactory = parseResult => [
        "формула", "концентрат", "база", "base", "perfume oil", "урок", "консультация", "крышка", "флакон",
        "краситель", "набор", "основа", "палочка", "палочки", "пипетка", "пипетки", "сертификат", "построение",
        "пропиленгликоль", "тестирование", "фиолка", "фильтр", "укаршение", "хроматограмма", "введение",
        "пробирка", "стакан", "весы", "диэтилфталат", "воронка",
    ]
};
update.Options.Add(updateBan);

update.SetAction(async parseResult =>
{
    ParfclubScaner scaner = new(console);
    List<Component> components = await scaner.Scan(parseResult.GetValue(updateBan));
    new CsvProcessor(console).GenerateParfy(components, parseResult.GetValue(updateOut)!);
});

Command analyse = new("analyse", "Проанализировать список нот.");

Option<FileInfo> analyseSrc = new("--src")
{
    Description = "CSV-база с веществами.",
    DefaultValueFactory = result => GetDefaultAnalyseSrc()
};
analyseSrc.Validators.Add(result =>
{
    FileInfo? fileInfo = result.GetValue(analyseSrc);

    if (fileInfo is null || !fileInfo.Exists)
    {
        result.AddError("Файл с веществами для анализа не найден.");
    }
});
analyseSrc.AcceptLegalFileNamesOnly();
analyse.Options.Add(analyseSrc);

Option<string> analyseNotes = new("--notes") { Required = true, Description = "Список нот через запятую." };
analyseNotes.Validators.Add(result =>
{
    string? input = result.GetValue(analyseNotes);

    if (string.IsNullOrWhiteSpace(input))
    {
        result.AddError("Не указаны ноты.");

        return;
    }

    string[] splitted = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

    if (splitted.Count() < 1)
    {
        result.AddError("Не указаны ноты.");
    }
});
analyse.Options.Add(analyseNotes);

Option<string[]> analyseBan = new("--ban") 
{ 
    Required = false,
    Description = "Список забаненых вхождений для всей композиции.",
    DefaultValueFactory = parseValue => ["аромат", "как", "не", "формат", "тот", "том", "как", "каркас",
        "грань", "грани", "каком"]
};
analyse.Options.Add(analyseBan);

Option<FileInfo> analyseOut = new("--out")
{
    DefaultValueFactory = result => GetDefaultAnalyseOut()
};
analyseOut.AcceptLegalFileNamesOnly();
analyse.Options.Add(analyseOut);

analyse.SetAction(parseResult =>
{
    NotesToComponentsAnalyser analyser = new(console);
    CsvProcessor csvProcessor = new(console);
    List<Component> components = csvProcessor.ReadComponents(parseResult.GetValue(analyseSrc)!);

    if (components?.Count > 0)
    {
        List<(string Note, string[] Exclude)> notes = GetNotesFromParameter(parseResult.GetValue(analyseNotes)!);
        string[] bannedEntries = parseResult.GetValue(analyseBan)!;
        NotesToComponentsAnalysis result = analyser.Analyse(components, notes, excludeEntryTokens: bannedEntries);
        csvProcessor.GenerateAnalysis(result, parseResult.GetValue(analyseOut)!);
    }
});

RootCommand rootCommand = [update, analyse];
ParseResult parseResult = rootCommand.Parse(args);
await parseResult.InvokeAsync();

FileInfo GetDefaultAnalyseSrc()
{
    DirectoryInfo currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
    IEnumerable<FileInfo> parfyFiles = currentDirectory.EnumerateFiles("parfy_source_*.csv");

    return parfyFiles?.MaxBy(x => x.CreationTimeUtc) ?? new FileInfo("stub");
}

FileInfo GetDefaultAnalyseOut()
{
    return new FileInfo(
        Path.Combine(
            Directory.GetCurrentDirectory(),
            $"parfy_analysis_{DateTime.Now.ToFileTime()}.csv"
        )
    );
}

List<(string Note, string[] Exclude)> GetNotesFromParameter(string input)
{
    List<(string Note, string[] Exclude)> notes = [];
    string[] splitted = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    foreach (string noteWithExclude in splitted)
    {
        string[] splittedWithExclude = noteWithExclude
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (splittedWithExclude.Length > 1)
        {
            notes.Add((splittedWithExclude.First(), splittedWithExclude.Skip(1).ToArray()));
        }
        else
        {
            notes.Add((splittedWithExclude.First(), []));
        }
    }

    return notes;
}