using Parfy;
using Parfy.Model;
using System.CommandLine;

int weight = FuzzySharp.Fuzz.Ratio("гриб", "гребного");

IConsole console = new ConsoleWrapper();

Command update = new("update", "Сканировать магазины с компонентами и создать таблицу.");

Option<DirectoryInfo> updateOut = new("--out")
{
    DefaultValueFactory = parseResult => new DirectoryInfo(Directory.GetCurrentDirectory())
};
update.Options.Add(updateOut);

update.SetAction(async parseResult =>
{
    ParfclubScaner scaner = new (console);
    List<Component> components = await scaner.Scan();
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

    if(splitted.Count() < 1)
    {
        result.AddError("Не указаны ноты.");
    }
});
analyse.Options.Add(analyseNotes);

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
    NotesToComponentsAnalysis result = analyser.Analyse(components, parseResult.GetValue(analyseNotes)!);
    csvProcessor.GenerateAnalysis(result, parseResult.GetValue(analyseOut)!);
});

RootCommand rootCommand = [update, analyse];
ParseResult parseResult = rootCommand.Parse(args);
await parseResult.InvokeAsync();

FileInfo GetDefaultAnalyseSrc()
{
    DirectoryInfo currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
    IEnumerable<FileInfo> parfyFiles = currentDirectory.EnumerateFiles("parfy_*.csv");

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