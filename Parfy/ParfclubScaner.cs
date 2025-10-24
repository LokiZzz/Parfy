using HtmlAgilityPack;
using Parfy.Model;
using System.Text.RegularExpressions;
using System.Web;

namespace Parfy
{
    public partial class ParfclubScaner(IConsole console)
    {
        private readonly HttpClient http = new();

        public async Task<List<Component>> Scan(string[]? excludeTokens = null)
        {
            console.WriteLine($"Начато сканирование parfclub.");

            List<string> links = [];
            int lastPageNumber = await GetLastPageNumber();

            foreach (int pageNumber in Enumerable.Range(1, lastPageNumber))
            {
                HttpResponseMessage response = await http.GetAsync($"https://parfclub.shop/shop/page/{pageNumber}/");
                string responseString = await response.Content.ReadAsStringAsync();

                if (responseString.Contains("Извините, но страница, которую вы ищете, не существует."))
                {
                    break;
                }

                List<string> pageLinks = GetLinksFromPage(responseString);
                links.AddRange(pageLinks);

                if (pageNumber != 1)
                {
                    console.ClearLastLine();
                }

                console.WriteLine($"Найдено {links.Count} ссылок. Текущая страница: {pageNumber}/{lastPageNumber}");
            }

            List<Component> components = [];
            List<(string Url, string Error)> errors = [];

            foreach (string link in links)
            {
                try
                {
                    HttpResponseMessage response = await http.GetAsync(link);
                    string responseString = await response.Content.ReadAsStringAsync();

                    Component component = GetComponent(responseString);
                    component.Url = link;
                    components.Add(component);

                    console.ClearLastLine();
                    double percent = Math.Round((double)components.Count / links.Count * 100, 0);
                    console.WriteLine($"Найдено и считано {components.Count} веществ из {links.Count} : ({percent}%)");
                }
                catch (Exception ex)
                {
                    errors.Add((link, ex.Message));
                }
            }

            foreach ((string Url, string Error) in errors)
            {
                console.WriteLine($"{Url} :: {Error}", EConsoleStatus.Error);
            }

            console.WriteLine($"Сканирование parfclub завершено.", EConsoleStatus.Success);

            if (excludeTokens?.Length > 0)
            {
                components = [.. 
                    components.Where(component =>
                        excludeTokens.All(token => 
                            !component.OriginalName.Contains(token, StringComparison.CurrentCultureIgnoreCase))
                    )
                ];
            }

            return components;
        }

        private async Task<int> GetLastPageNumber()
        {
            HttpResponseMessage response = await http.GetAsync($"https://parfclub.shop/shop/");
            string responseString = await response.Content.ReadAsStringAsync();
            HtmlDocument htmlDoc = new();
            htmlDoc.LoadHtml(responseString);

            HtmlNode navUl = htmlDoc.DocumentNode
                .SelectSingleNode("//ul[@class='pagination -center-flex -default -unlist']");
            HtmlNode lastPageA = navUl.ChildNodes[^2].SelectSingleNode(".//a");

            return int.Parse(lastPageA.InnerText);
        }

        private static List<string> GetLinksFromPage(string html)
        {
            List<string> links = [];
            HtmlDocument htmlDoc = new();
            htmlDoc.LoadHtml(html);

            // Находим контейнеры карточек товара
            HtmlNodeCollection containers = htmlDoc.DocumentNode
                .SelectNodes("//div[@class='card-details s s']");

            foreach (HtmlNode container in containers)
            {
                if (container is null)
                {
                    break;
                }

                // Находим первую ссылку внутри этого контейнера
                HtmlNode anchor = container
                    .SelectSingleNode(".//a[@class='woo-product-name title titles-typo -undash']");

                string hrefValue = anchor.GetAttributeValue("href", string.Empty);

                if (!string.IsNullOrEmpty(hrefValue))
                {
                    links.Add(hrefValue);
                }
            }

            return links;
        }

        private static Component GetComponent(string html)
        {
            Component component = new();
            HtmlDocument htmlDoc = new();
            htmlDoc.LoadHtml(html);

            // Название вещества из заголовка (рус, анг)
            HtmlNode h1 = htmlDoc.DocumentNode
                .SelectSingleNode("//h1[@class='woo-product-details-title product_title entry-title']");
            string decoded = HttpUtility.HtmlDecode(h1.InnerText); // Бывает кривоватое кодирование
            component.OriginalName = decoded;

            // Длинное описание с рекомендациями из вкладок внизу
            HtmlNode tab = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='tab-description']");

            if (tab is not null)
            {
                HtmlNode wrap = tab.SelectSingleNode("//div[@class='wrap']");
                component.Description = Spaces().Replace(wrap.InnerText, " ").Trim();
            }

            // Короткое описание-таблица справа от иллюстрации
            HtmlNode shortDescr = htmlDoc.DocumentNode
                .SelectSingleNode("//div[@class='woocommerce-product-details__short-description']");
            string oneLineShortDescr = NewLine().Replace(shortDescr.InnerText.Trim(), ", ");
            component.ShortDescription = MultiSpace().Replace(oneLineShortDescr, " ").Trim();

            return component;
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Spaces();

        [GeneratedRegex(@"\n")]
        private static partial Regex NewLine();

        [GeneratedRegex(@"\s+")]
        private static partial Regex MultiSpace();
    }
}