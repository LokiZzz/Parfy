namespace Parfy.Model
{
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
}
