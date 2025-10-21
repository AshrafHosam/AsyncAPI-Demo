using AsyncAPI_Demo.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AsyncAPI_Demo;

internal static class Startup
{
    public static IMongoCollection<Message> RegisterMongo()
    {
        var mongoConnectionString = Environment.GetEnvironmentVariable("MongoDbConnection");

        var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);

        settings.UseTls = true;
        settings.AllowInsecureTls = false;
        settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(1);

        var mongoClient = new MongoClient(settings);

        var db = mongoClient.GetDatabase("GroupMessages");

        db.RunCommand((Command<BsonDocument>)"{ping:1}");

        return db.GetCollection<Message>("Messages");
    }
}

