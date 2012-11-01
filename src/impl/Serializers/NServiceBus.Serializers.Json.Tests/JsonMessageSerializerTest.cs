using NUnit.Framework;

namespace NServiceBus.Serializers.Json.Tests
{
    using MessageInterfaces.MessageMapper.Reflection;

    [TestFixture]
  public class JsonMessageSerializerTest : JsonMessageSerializerTestBase
  {
    protected override JsonMessageSerializerBase Serializer { get; set; }

    [SetUp]
    public void Setup()
    {
      var messageMapper = new MessageMapper();
      messageMapper.Initialize(new[] { typeof(IA), typeof(A) });

      Serializer = new JsonMessageSerializer(messageMapper);
    }
  }
}
