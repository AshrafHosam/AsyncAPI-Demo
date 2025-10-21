using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using AsyncAPI_Demo.Entities;
using AsyncAPI_Demo.Enums;
using MongoDB.Driver;
using SentimentAnalyzer;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AsyncAPI_Demo;

public class Function
{
    const float NeutralityThreshold = 3.0f;
    private readonly IMongoCollection<Message> _messagesCollection;
    ILambdaLogger _logger;
    public Function()
    {
        _messagesCollection = Startup.RegisterMongo();
    }

    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        _logger = context.Logger;
        foreach (var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processed message {request.Body}");

        var message = JsonSerializer.Deserialize<Message>(request.Body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        _logger.LogInformation("Predicting sentiment...");

        message.Sentiment = PredictSentiment(message.Text);

        _logger.LogInformation($"Predicted sentiment: {message.Sentiment}");

        await _messagesCollection.InsertOneAsync(message);

        _logger.LogInformation($"Saved message in MongoDb {message.Id}");

        await Task.CompletedTask;
    }

    private string PredictSentiment(string text)
        => GetSentimentResult(Sentiments.Predict(text));

    private string GetSentimentResult(SentimentAnalyzer.Models.SentimentPrediction result)
    {
        _logger.LogInformation($"Text Analyzer Prediction Result: {result.Prediction}, Score: {result.Score}");

        return (result.Prediction && result.Score >= NeutralityThreshold)
                ? SentimentEnum.Positive.ToString()
                : (!result.Prediction && (1.0f - result.Score) >= NeutralityThreshold)
                    ? SentimentEnum.Negative.ToString()
                    : SentimentEnum.Neutral.ToString();
    }
}