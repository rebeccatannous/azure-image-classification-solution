namespace TensorFlow
{
    using System.IO;

    // Taken and adapted from: https://github.com/migueldeicaza/TensorFlowSharp/blob/master/Examples/ExampleCommon/ImageUtil.cs
    public static class ImageUtil
    {
        // Convert the image in filename to a Tensor suitable as input to the Inception model.
        public static TFTensor CreateTensorFromImageFile(Stream file, TFDataType destinationDataType = TFDataType.Float)
        {
            byte[] contents = ReadFully(file);

            // DecodeJpeg uses a scalar String-valued tensor as input.
            var tensor = TFTensor.CreateString(contents);
           
            // Construct a graph to normalize the image
            using (var graph = ConstructGraphToNormalizeImage(out TFOutput input, out TFOutput output, destinationDataType))
            {
                // Execute that graph to normalize this one image
                using (var session = new TFSession(graph))
                {
                    var normalized = session.Run(
                        inputs: new[] { input },
                        inputValues: new[] { tensor },
                        outputs: new[] { output });

                    return normalized[0];
                }
            }
        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        // Additional pointers for using TensorFlow & CustomVision together
        // Python: https://github.com/tensorflow/tensorflow/blob/master/tensorflow/examples/label_image/label_image.py
        // C++: https://github.com/tensorflow/tensorflow/blob/master/tensorflow/examples/label_image/main.cc
        // Java: https://github.com/Azure-Samples/cognitive-services-android-customvision-sample/blob/master/app/src/main/java/demo/tensorflow/org/customvision_sample/MSCognitiveServicesClassifier.java
        private static TFGraph ConstructGraphToNormalizeImage(out TFOutput input, out TFOutput output, TFDataType destinationDataType = TFDataType.Float)
        {
            //const int W = 227;
            //const int H = 227;
            const int W = 224;
            const int H = 224;
            const float Scale = 1;

            // Depending on your CustomVision.ai Domain - set appropriate Mean Values (RGB)
            // https://github.com/Azure-Samples/cognitive-services-android-customvision-sample for RGB values (in BGR order)
            //var bgrValues = new TFTensor(new float[] { 104.0f, 117.0f, 123.0f }); // General (Compact) & Landmark (Compact)
            //var bgrValues = new TFTensor(new float[] { 124.0f, 117.0f, 105.0f }); // General (Compact) & Landmark (Compact)
            var bgrValues = new TFTensor(0f); // Retail (Compact)

            var graph = new TFGraph();
            input = graph.Placeholder(TFDataType.String);

            var caster = graph.Cast(graph.DecodeJpeg(contents: input, channels: 3), DstT: TFDataType.Float);
            var dims_expander = graph.ExpandDims(caster, graph.Const(0, "batch"));
            var resized = graph.ResizeBilinear(dims_expander, graph.Const(new int[] { H, W }, "size"));
            var resized_mean = graph.Sub(resized, graph.Const(bgrValues, "mean"));
            var normalised = graph.Div(resized_mean, graph.Const(Scale));
            output = normalised;
            return graph;
        }
    }
}

/*public static class CustomVisionFunction
    {
        [FunctionName("CustomVisionFunction")]
        public static void Run([BlobTrigger("images/{name}", Connection = "ImagesStorage")]Stream myBlob, string name, TraceWriter log, ExecutionContext context)
        {
            log.Info($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");


            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var graph = new TFGraph();

            var model = File.ReadAllBytes(@"C:\Users\miche\source\repos\CustomVisionFunctions\assets\model.pb");//Path.Combine(context.FunctionDirectory, "../Assets/model.pb"));
            var labels = File.ReadAllLines(@"C:\Users\miche\source\repos\CustomVisionFunctions\assets\labels.txt");//Path.Combine(context.FunctionDirectory, "../Assets/labels.txt"));
            graph.Import(model);

            log.Info($"{name}");

            using (var session = new TFSession(graph))
            {
                Console.WriteLine("I am before creating image");
                var tensor = ImageUtil.CreateTensorFromImageFile(myBlob);
                Console.WriteLine("I am after creating image");
                var runner = session.GetRunner();
                runner.AddInput(graph["Placeholder"][0], tensor).Fetch(graph["loss"][0]);
                //    runner.AddInput(graph["input"][0], tensor).Fetch(graph["final_result"][0]);
                var output = runner.Run();
                var result = output[0];
                var threshold = 0.25; // 25%

                var probabilities = ((float[][])result.GetValue(jagged: true))[0];
                for (int i = 0; i < probabilities.Length; i++)
                {
                    // output the tags over the threshold
                    if (probabilities[i] > threshold)
                    {
                        log.Info($"{labels[i]} ({Math.Round(probabilities[i] * 100.0, 2)}%)");
                    }
                }
            }

            stopwatch.Stop();
            log.Info($"Total time: {stopwatch.Elapsed}");
        }
    }*/
/*public class Global
{
    public string result;
}
public class Prediction
{
    public string TagId { get; set; }
    public string Tag { get; set; }
    public double Probability { get; set; }
}
public class PayloadPrediction
{
    public PayloadPrediction(RootObject rootObject)
    {
        this.IsCar = (rootObject.Predictions[0].Probability > 0.6) ? true : false;
    }
    public bool IsCar { get; set; }
}
public class RootObject
{
    public string Id { get; set; }
    public string Project { get; set; }
    public string Iteration { get; set; }
    public DateTime Created { get; set; }
    public List<Prediction> Predictions { get; set; }
}

public class AutoFunction
{
    [FunctionName("AutoFunction")]
    public static void Run([BlobTrigger("images/{name}", Connection = "ImagesStorage")] Stream myBlob, string name, TraceWriter log, ExecutionContext context)//[HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, TraceWriter log)
    {
        // Passing in the image URL, in our case, it was a url to a file uploaded in Azure Blob Storage (done by different members of our team) 
        //string imageFileURL = req.Query["imageFileURL"];
        //var webClient = new WebClient();
        MemoryStream ms = new MemoryStream();
        myBlob.CopyTo(ms);
        byte[] imageBytes = ms.ToArray(); //webClient.DownloadData(imageFileURL);

        var result = MakePredictionRequest(imageBytes).Result;
        Console.WriteLine("Finished");
        //return imageFileURL != null
          //? (ActionResult)new OkObjectResult(result) : new BadRequestObjectResult("Please pass a image file URL on the query string or in the request body");
    }
    private static byte[] GetImageAsByteArray(string imageFilePath)
    {
        FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
        BinaryReader binaryReader = new BinaryReader(fileStream);
        return binaryReader.ReadBytes((int)fileStream.Length);
    }
    static async Task<string> MakePredictionRequest(byte[] imageByteArray)
    {
        var client = new HttpClient();
        var jsonResponse = string.Empty;
        // Request headers - replace with your valid subscription key. 
        client.DefaultRequestHeaders.Add("441294527aa74202a918f6a21e705adb", "a119f7e7a91a4a7db4569d6ecedb51f5");//"fa4bdc25-9cb6-49b4-87f6-c1a3e6d9c758");
        // Prediction URL - replace with your valid prediction URL. 
        string url = "https://customv2resource-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/0300db3e-779d-4bf5-bb3a-50c41710aff8/classify/iterations/Project2/url";
        HttpResponseMessage response;
        // Request body. Try this sample with a locally stored image. 
        using (var content = new ByteArrayContent(imageByteArray))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response = await client.PostAsync(url, content);
            jsonResponse = await response.Content.ReadAsStringAsync();
            RootObject deserializedRootOjbect = JsonConvert.DeserializeObject<RootObject>(jsonResponse);
            //PayloadPrediction payloadPrediction = new PayloadPrediction(deserializedRootOjbect);
            //Console.WriteLine("Is Car = " + payloadPrediction.IsCar.ToString());
            foreach (var i in deserializedRootOjbect.Predictions)
            {
                Console.WriteLine(i.Tag + ": " + i.Probability);
            }
            //Console.WriteLine(deserializedRootOjbect.Predictions[0]);
            //string result = payloadPrediction.IsCar.ToString();
            //return result;
            return "0";
        }
    }
}*/