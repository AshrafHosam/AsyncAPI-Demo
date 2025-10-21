//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http;
//using System.Reflection.Metadata;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;
//using Amazon.DynamoDBv2;
//using Amazon.DynamoDBv2.DocumentModel;
//using Amazon.Lambda.Core;
//using Amazon.Lambda.SQSEvents;
////using Amazon.SageMakerRuntime;
////using Amazon.SageMakerRuntime.Model;

//// Assembly attribute to enable the Lambda JSON input to be converted into a .NET class.
////[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

//namespace AsyncDemoWorker
//{
//    public class Function
//    {
//        private readonly string _rawTable;
//        private readonly string _processedTable;
//        private readonly string _aiProvider;
//        private readonly string _modelEndpoint;
//        private readonly AmazonDynamoDBClient _dynamo;
//        private readonly Table _rawTableRef;
//        private readonly Table _processedTableRef;
//        //private readonly AmazonSageMakerRuntimeClient _sagemaker;
//        //private static readonly HttpClient _httpClient = new HttpClient();

//        public Function()
//        {
//            _rawTable = Environment.GetEnvironmentVariable("RAW_TABLE") ?? "RawItems";
//            _processedTable = Environment.GetEnvironmentVariable("PROCESSED_TABLE") ?? "ProcessedItems";
//            _aiProvider = Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "mock";
//            _modelEndpoint = Environment.GetEnvironmentVariable("MODEL_ENDPOINT") ?? "";

//            _dynamo = new AmazonDynamoDBClient();
//            //Table.TryLoadTable(_dynamo, _rawTable, out var tableRef);
//            //_rawTableRef = Table.LoadTable(_dynamo, _rawTable);
//            //_processedTableRef = Table.LoadTable(_dynamo, _processedTable);
//            //_sagemaker = new AmazonSageMakerRuntimeClient();
//        }

//        public async Task Handler(SQSEvent sqsEvent, ILambdaContext context)
//        {
//            context.Logger.LogInformation($"Lambda invoked with {sqsEvent.Records.Count} records");
//            foreach (var record in sqsEvent.Records)
//            {
//                try
//                {
//                    var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(record.Body);
//                    if (payload == null || !payload.ContainsKey("itemId") || !payload.ContainsKey("texts"))
//                    {
//                        context.Logger.LogWarning($"Skipping invalid payload: {record.Body}");
//                        continue;
//                    }

//                    var itemId = payload["itemId"]?.ToString();
//                    if (string.IsNullOrEmpty(itemId))
//                    {
//                        context.Logger.LogWarning($"Skipping empty itemId: {record.Body}");
//                        continue;
//                    }

//                    // Parse texts list
//                    var textsElement = payload["texts"];
//                    var texts = ParseTexts(textsElement);
//                    if (texts == null)
//                    {
//                        context.Logger.LogWarning($"Skipping payload with non-list texts: {record.Body}");
//                        continue;
//                    }

//                    // Save raw with correlationId
//                    var correlationId = Guid.NewGuid().ToString();
//                    var ingestedAt = DateTime.UtcNow.ToString("o");

//                    var rawDoc = new Amazon.DynamoDBv2.DocumentModel.Document
//                    {
//                        ["itemId"] = itemId,
//                        ["texts"] = new DynamoDBList(texts.Select(t => new DynamoDBEntry(t)).ToArray()),
//                        ["ingestedAt"] = ingestedAt,
//                        ["correlationId"] = correlationId
//                    };
//                    await _rawTableRef.PutItemAsync(rawDoc);
//                    context.Logger.LogInformation($"Saved raw item {itemId} corr={correlationId}");

//                    // Build prompt (simple)
//                    var prompt = BuildPrompt(texts);

//                    // Call AI provider
//                    object aiResult;
//                    //if (_aiProvider.Equals("sagemaker", StringComparison.OrdinalIgnoreCase))
//                    //{
//                    //    aiResult = await CallSageMaker(prompt, context);
//                    //}
//                    //else
//                    //{
//                    //    aiResult = CallMock(texts);
//                    //}
//                    aiResult = CallMock(texts);
//                    // Save processed result
//                    var processedDoc = new Amazon.DynamoDBv2.DocumentModel.Document
//                    {
//                        ["itemId"] = itemId,
//                        ["processedAt"] = DateTime.UtcNow.ToString("o"),
//                        ["aiResult"] = Amazon.DynamoDBv2.DocumentModel.Document.FromJson(JsonSerializer.Serialize(aiResult)),
//                        ["correlationId"] = correlationId
//                    };
//                    await _processedTableRef.PutItemAsync(processedDoc);
//                    context.Logger.LogInformation($"Saved processed item {itemId} corr={correlationId}");
//                }
//                catch (Exception ex)
//                {
//                    context.Logger.LogError($"Processing failed for record: {ex}. Letting SQS retry.");
//                    throw; // let Lambda fail so SQS can retry / DLQ
//                }
//            }
//        }

//        private static List<string>? ParseTexts(object? element)
//        {
//            if (element is JsonElement je && je.ValueKind == JsonValueKind.Array)
//            {
//                var result = new List<string>();
//                foreach (var e in je.EnumerateArray())
//                {
//                    if (e.ValueKind == JsonValueKind.String)
//                        result.Add(e.GetString() ?? string.Empty);
//                }
//                return result;
//            }

//            // Fallback: try to convert if element is a List<object>
//            if (element is System.Text.Json.JsonElement je2 && je2.ValueKind == JsonValueKind.Array)
//            {
//                var result = new List<string>();
//                foreach (var e in je2.EnumerateArray())
//                {
//                    if (e.ValueKind == JsonValueKind.String)
//                        result.Add(e.GetString() ?? string.Empty);
//                }
//                return result;
//            }

//            return null;
//        }

//        private static string BuildPrompt(List<string> texts)
//        {
//            // Simple prompt asking the model to return a JSON string with summary and sentiment
//            var joined = string.Join("\n", texts);
//            if (joined.Length > 1800)
//                joined = joined.Substring(0, 1800); // limit prompt size
//            var prompt = $"Return a JSON object with keys: summary, sentiment (positive/neutral/negative). " +
//                         $"Summary max 200 chars. Texts:\n{joined}";
//            return prompt;
//        }

//        private static object CallMock(List<string> texts)
//        {
//            var joined = string.Join(" ", texts.Select(t => t.Trim()));
//            var summary = joined.Length > 200 ? joined.Substring(0, 200) : joined;
//            var count = texts.Count;
//            var avgLen = texts.Any() ? (double)texts.Sum(t => t.Length) / texts.Count : 0.0;
//            var lowered = joined.ToLowerInvariant();
//            var sentiment = lowered.Contains("error") || lowered.Contains("fail") || lowered.Contains("crash")
//                ? "negative" : lowered.Contains("good") || lowered.Contains("great") ? "positive" : "neutral";

//            return new { summary, count, avgLength = Math.Round(avgLen, 1), sentiment };
//        }

//        //private async Task<object> CallSageMaker(string prompt, ILambdaContext context)
//        //{
//        //    if (string.IsNullOrEmpty(_modelEndpoint))
//        //        throw new InvalidOperationException("MODEL_ENDPOINT is not set for SageMaker invocation");

//        //    var request = new InvokeEndpointRequest
//        //    {
//        //        EndpointName = _modelEndpoint,
//        //        ContentType = "text/plain",
//        //        Body = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(prompt))
//        //    };

//        //    var response = await _sagemaker.InvokeEndpointAsync(request);
//        //    using var reader = new System.IO.StreamReader(response.Body);
//        //    var text = await reader.ReadToEndAsync();

//        //    // Try parse JSON result, otherwise return raw text
//        //    try
//        //    {
//        //        var doc = JsonSerializer.Deserialize<JsonElement>(text);
//        //        return doc;
//        //    }
//        //    catch
//        //    {
//        //        return new { output = text };
//        //    }
//        //}
//    }
//}