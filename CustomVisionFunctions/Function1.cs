using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Threading.Tasks;
using System.Configuration;

namespace CustomVisionFunctions
{
    static class Result
    {
        public static string result;
    }

    public class JsonO
    {
        public string type = "Fruit";
        public string id { get; set; }
        public string project{ get; set; }
        public string iteration { get; set; }
        public string created { get; set; }
        public string url { get; set; }
        public IList<Prediction> predictions { get; set; }

    }

    public class Prediction
    {
        public string probability { get; set; }
        public string tagId { get; set; }
        public string tagName { get; set; }
    
    }
    
    public class Function
    {
        // The Azure Cosmos DB endpoint for running this sample.
        private static readonly string EndpointUri = "https://cosmosdbproject2.documents.azure.com:443/";

        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = "pcykDmNagmSr90SIB7b9WaTg05hoFFiRzyuaUyzyox5yk3EYWNOyrSIGQfWdRivLtpdTNcyByVdd1DPgUbygBw==";

        // The Cosmos client instance
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        // The name of the database and container we will create
        private string databaseId = "results";
        private string containerId = "CustomVision1";

        [FunctionName("Function")]
        public static async Task RunAsync([BlobTrigger("images/{name}", Connection = "ImagesStorage")] Stream myBlob, string name, TraceWriter log, ExecutionContext context, System.Uri Uri)
        {
            string predictionURL = "https://customv2resource-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/0300db3e-779d-4bf5-bb3a-50c41710aff8/classify/iterations/Project2/url";
            string predictionKey = "441294527aa74202a918f6a21e705adb";
            string url = Uri.ToString();
            
            Function f = new Function();
            Console.WriteLine("1--------------------------------");
            string result = await PredictImageContentsAsync(myBlob);
            Console.WriteLine(JsonConvert.DeserializeObject(result));
            Console.WriteLine("2--------------------------------");
            f.UploadToCosmos(result, name, url);

        }

        public async void UploadToCosmos(string js, string name, string url)
        {
            this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            await this.CreateDatabaseAsync();
            await this.CreateContainerAsync();
            await this.AddItemsToContainerAsync(js, name, url);

        }
        private async Task AddItemsToContainerAsync(string js, string name, string url)
        {
            string [] uid = name.Split(".");
            // Create a family object for the Andersen family
            JsonO json = JsonConvert.DeserializeObject<JsonO>(js);
            json.id = uid[0];
            json.url = url;
            try
            {
                // Read the item to see if it exists.  
                ItemResponse<JsonO> jso = await this.container.ReadItemAsync<JsonO>(uid[0], new PartitionKey("Fruit"));
                Console.WriteLine("Item in database with id: {0} already exists\n", jso.Resource.id);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<JsonO> jso = await this.container.CreateItemAsync<JsonO>(json, new PartitionKey("Fruit"));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", jso.Resource.id, jso.RequestCharge);
            }

            
        }
        private async Task CreateContainerAsync()
        {
            // Create a new container
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/type");
            Console.WriteLine("Created Container: {0}\n", this.container.Id);
        }
        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }
        public static async Task<string> PredictImageContentsAsync(Stream imageStream)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Prediction-key", "441294527aa74202a918f6a21e705adb");

            MemoryStream ms = new MemoryStream();
            imageStream.CopyTo(ms);
            byte[] imageData = ms.ToArray();

            HttpResponseMessage response;
            using (var content = new ByteArrayContent(imageData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync("https://customv2resource-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/0300db3e-779d-4bf5-bb3a-50c41710aff8/classify/iterations/Project2/image", content);
            }

            var resultJson = await response.Content.ReadAsStringAsync();

            return resultJson;
        }
    }
}

