namespace Io.Database.Entities
{
    public class TestLogEntity
    {
        public const string NamePrimary = "__primary__";

        public long? Id { get; set; }

        public TestEntity? Test { get; set; }

        public string? Name { get; set; }

        public string? Data { get; set; }
    }
}
