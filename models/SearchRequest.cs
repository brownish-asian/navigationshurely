
namespace navigationshurely.models
{
    public class SearchRequest
    {
        public string SearchText { get; set; } = "";

        public bool LanguageSpecified { get; set; }
        public bool OrderSpecified { get; set; }

        public bool CaseSensitive { get; set; } = false;

        public int? BookId { get; set; }
        public int? Chapter { get; set; }
        public int? Verse { get; set; }

        public Testament? Testament { get; set; }

        public int TranslationId { get; set; } = 1;

        public SearchMode SearchMode { get; set; }
            = SearchMode.AnyOrder;
    }
}
