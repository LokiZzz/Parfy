using Parfy;
using Parfy.Model;
using System.CommandLine;

NotesToComponentsAnalyser analyser = new(new ConsoleWrapper());

string text = "Свежий, характерный запах лесных грибов, влажной земли, мха. " +
    "Один из самых узнаваемых ароматов, связанных с дождём, грибами и лесом. " +
    "Этот материал обладает очень натуральным и реалистичным звучанием. " +
    "Мнение парфюмеров: Арктандер описывает его как «наиболее характерную грибную ноту, " +
    "которую можно встретить в природе», отмечая, что в сильно разведённом виде компонент " +
    "раскрывает необычайную и полезную свежесть. Синергия: Iso E Super — добавляет прозрачности и " +
    "амбровой теплоты Evernyl (мох) — усиливает лесную и мшистую грань Floralozone и Helional " +
    "— создают атмосферу утреннего леса Muscenone или Cosmone — усиливают skin scent-эффект " +
    "Ноты пачули, ветивера, кедра Применение: В малых дозировках (0.01–0.1%) придаёт " +
    "композиции ощущение реалистичной глубины, природной ауры, свежести утреннего леса. " +
    "Особенно популярен в нишевой парфюмерии и ароматерапевтических композициях.\r\n";

string entry = string.Empty;
int weight = 0;

// Iso E Super, Evernyl, Floralozone, Helional, Muscenone, Cosmone, кедр, пачули, ветивер
bool b1 = analyser.TrySearchPhrase(text, "Iso E Super® (IFF)", out entry, out weight);
bool b2 = analyser.TrySearchPhrase(text, "Изо Е Супер", out entry, out weight);

string stop = string.Empty;

// EVERNYL 10% in alc. Эвернил
// Floralozone (IFF) Флоралозон
// Helional (IFF) Гелиональ
// Muscenone® Delta 962191 (Firmenich) Мусценон
// COSMONE® (Givaudan) Космон
// Кедр лист 100% эфирное масло Cedar leaf EO
// CEDARWOOD ATLAS EO (КЕДР АТЛАССКИЙ 100% эфирное масло)
// Cedarwood China EO (Кедр китайский 100% эфирное масло)
// CEDARWOOD HIMALAYAN EO (КЕДР ГИМАЛАЙСКИЙ 100% эфирное масло)
// Кедр терпены	CEDARWOOD TERPENES (Penta)
// CEDARWOOD TEXAS EO (КЕДР ТЕХАС 100% эфирное масло)
// CEDARWOOD VIRGINIA EO (КЕДР ВИРГИНИЯ 100% эфирное масло) США
// Кедрамбер Cedramber (IFF)
// Цедрат (100% эфирное масло) Нота сердца	Cedrat Heart (Robertet) Natural
// Кедренил ацетат	CEDRENYL ACETATE CRYSTALS (Penta)
// Кедрол(кристаллическое вещество)	Cedrol (IFF)
// Кедрон С	CEDRONE S (IFF)
// Кедроксид CEDROXYDE® 922470 (Firmenich)
// Кедрил ацетат Cedryl Acetate liquid (IFF)
// Патчон кристаллическое вещество	Patchone (IFF) aka PTBCH
// Пачули молекулярная дистилляция	PATCHOULI EO MOLECULAR DISTILLED (IFF)
// Пачули терпены	Patchouli terpenes (Natural)
// Вердокс VERDOX(IFF)
// Веротил VEROTYL ™ (PFW)
// Вертенекс	VERTENEX (IFF)
// Вертенекс Vertenex® HC (IFF)
// Вертофикс Vertofix® Coeur (IFF)
// Вертолифф Vertoliff (IFF)
// Вертосин VERTOSINE 600271 (Symrise)
// Ветиколацетат Veticolacetate 197009 (Symrise) aka Gardenia pentyl acetate
// Ветикон Vetikon® 131014 (Symrise)
// Ветивал Vetival 105274 (Symrise)
// Ветивер ацетат	Vetiver (Vetiveryl) acetate Haiti (IFF)
// Vetiver EO (ВЕТИВЕР 100% ЭФИРНОЕ МАСЛО) ГАИТИ
// Ветивер эфир	Vetiver Ether
// Vetiver Java EO (ВЕТИВЕР 100% ЭФИРНОЕ МАСЛО) ЯВА
// Ветивер терпены	Vetiver terpenes (Natural)
// Ветиверол Экстра натуральный 100% Гаити	Vetiverol ex Vetiver Haiti Natural
// Ветирисия Vetyrisia® 991002 (Firmenich)


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
    csvProcessor.GenerateAnalysis(result, parseResult.GetValue(analyseOut)!);
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