namespace Redpoint.Uet.BuildGraph
{
    public record class CookElementProperties : ElementProperties
    {
        public required string Project { get; set; }

        public required string Platform { get; set; }

        public required string Tag { get; set; }
    }
}