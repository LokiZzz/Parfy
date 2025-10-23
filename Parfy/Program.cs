using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.SimilarityRatio.Scorer.StrategySensitive;
using Parfy;
using Parfy.Model;
using System.CommandLine;

string t = "синергия iso";
string c = "acetanisole";

var r0 = Fuzz.Ratio(c, t);
var r1 = Fuzz.PartialRatio(c, t);
var r3 = Fuzz.TokenSortRatio(c, t);
var r2 = Fuzz.TokenSetRatio(c, t);
var r4 = Fuzz.PartialTokenSetRatio(c, t);
var r5 = Fuzz.PartialTokenSortRatio(c, t);
var r6 = Fuzz.WeightedRatio(c, t);

var result = Process.ExtractOne(c, [t], scorer: ScorerCache.Get<PartialRatioScorer>());

string text = "Свежий, характерный запах лесных грибов, влажной земли, мха. " +
    "Один из самых узнаваемых ароматов, связанных с дождём, грибами и лесом. " +
    "Этот материал обладает очень натуральным и реалистичным звучанием. " +
    "Мнение парфюмеров: Арктандер описывает его как «наиболее характерную " +
    "грибную ноту, которую можно встретить в природе», отмечая, что в сильно " +
    "разведённом виде компонент раскрывает необычайную и полезную свежесть. " +
    "Синергия: Iso E Super — добавляет прозрачности и амбровой теплоты Evernyl (мох) — " +
    "усиливает лесную и мшистую грань Floralozone и Helional — создают атмосферу утреннего " +
    "леса Muscenone или Cosmone — усиливают skin scent-эффект Ноты пачули, ветивера, " +
    "кедра Применение: В малых дозировках (0.01–0.1%) придаёт композиции ощущение реалистичной глубины, природной ауры, свежести утреннего леса. Особенно популярен в нишевой парфюмерии и ароматерапевтических композициях.";
string component = "Eugenol 99.5% (Indonesia)";
// Мусценон	Muscenone® Delta 962191 (Firmenich)
// Амбреин природный Ambrain de labdanum (IFF)

bool b2 = new NotesToComponentsAnalyser(new ConsoleWrapper())
    .TrySearchComponent(text, component, out string e2, out int w2);

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
        "пробирка", "стакан", "весы", "диэтилфталат",
    ]
};
update.Options.Add(updateBan);

update.SetAction(async parseResult =>
{
    ParfclubScaner scaner = new (console);
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
    string stop = string.Empty;
    //csvProcessor.GenerateAnalysis(result, parseResult.GetValue(analyseOut)!);
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