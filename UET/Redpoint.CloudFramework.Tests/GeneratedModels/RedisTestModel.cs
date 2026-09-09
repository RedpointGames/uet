


namespace Redpoint.CloudFramework.Tests
{
using Google.Cloud.Datastore.V1;
using NodaTime;
using Redpoint.CloudFramework.Models;
using Redpoint.CloudFramework.Tests.Models;



[Kind("cf_TestLoadedEntityMatchesCreatedEntity")]
public sealed class TestLoadedEntityMatchesCreatedEntity_Model : Model<TestLoadedEntityMatchesCreatedEntity_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestLoadedEntityIsInCache")]
public sealed class TestLoadedEntityIsInCache_Model : Model<TestLoadedEntityIsInCache_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestMultipleEntityLoadWorks")]
public sealed class TestMultipleEntityLoadWorks_Model : Model<TestMultipleEntityLoadWorks_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestMultipleEntityLoadWorksWithoutCacheClear")]
public sealed class TestMultipleEntityLoadWorksWithoutCacheClear_Model : Model<TestMultipleEntityLoadWorksWithoutCacheClear_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestUpdatedEntityIsNotInCache")]
public sealed class TestUpdatedEntityIsNotInCache_Model : Model<TestUpdatedEntityIsNotInCache_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestUpsertedEntityIsNotInCache")]
public sealed class TestUpsertedEntityIsNotInCache_Model : Model<TestUpsertedEntityIsNotInCache_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestDeletedEntityIsNotInCache")]
public sealed class TestDeletedEntityIsNotInCache_Model : Model<TestDeletedEntityIsNotInCache_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestCreateThenQuery")]
public sealed class TestCreateThenQuery_Model : Model<TestCreateThenQuery_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestCreateThenQueryThenUpdateThenQuery")]
public sealed class TestCreateThenQueryThenUpdateThenQuery_Model : Model<TestCreateThenQueryThenUpdateThenQuery_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestReaderCountIsSetWhileReading")]
public sealed class TestReaderCountIsSetWhileReading_Model : Model<TestReaderCountIsSetWhileReading_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestTransactionalUpdateInvalidatesQuery")]
public sealed class TestTransactionalUpdateInvalidatesQuery_Model : Model<TestTransactionalUpdateInvalidatesQuery_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestCreateInvalidatesQuery")]
public sealed class TestCreateInvalidatesQuery_Model : Model<TestCreateInvalidatesQuery_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestUpdateWithNoOriginalDataDoesNotCrash")]
public sealed class TestUpdateWithNoOriginalDataDoesNotCrash_Model : Model<TestUpdateWithNoOriginalDataDoesNotCrash_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestUpdateInvalidatesRelevantQuery")]
public sealed class TestUpdateInvalidatesRelevantQuery_Model : Model<TestUpdateInvalidatesRelevantQuery_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestUpdateDoesNotInvalidateIrrelevantQuery")]
public sealed class TestUpdateDoesNotInvalidateIrrelevantQuery_Model : Model<TestUpdateDoesNotInvalidateIrrelevantQuery_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit")]
public sealed class TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model : Model<TestTransactionalUpdateDoesNotInvalidateCacheUntilCommit_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestTransactionalUpdateFromNull")]
public sealed class TestTransactionalUpdateFromNull_Model : Model<TestTransactionalUpdateFromNull_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestNonTransactionalUpdateFromNull")]
public sealed class TestNonTransactionalUpdateFromNull_Model : Model<TestNonTransactionalUpdateFromNull_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestQueryOrdering")]
public sealed class TestQueryOrdering_Model : Model<TestQueryOrdering_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestQueryEverything")]
public sealed class TestQueryEverything_Model : Model<TestQueryEverything_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



[Kind("cf_TestDeletedEntityIsNotInCachedQueryEverything")]
public sealed class TestDeletedEntityIsNotInCachedQueryEverything_Model : Model<TestDeletedEntityIsNotInCachedQueryEverything_Model>
{
    [Type(FieldType.String), Indexed]
    public string? forTest { get; set; }

    [Type(FieldType.String), Indexed]
    public string? string1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number1 { get; set; }

    [Type(FieldType.Integer), Indexed]
    public long? number2 { get; set; }

    [Type(FieldType.Timestamp), Indexed]
    public Instant? timestamp { get; set; }

    [Type(FieldType.Key)]
    public UntypedKey? keyValue { get; set; }

    public TestModel? untracked { get; set; }
    
#pragma warning disable CS0628 // New protected member declared in sealed type
    [Type(FieldType.String), Indexed]
    protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

    [Type(FieldType.String), Indexed]
    private string? privateString1 { get; set; }

    [Type(FieldType.String), Indexed]
    internal string? internalString1 { get; set; }
}



}