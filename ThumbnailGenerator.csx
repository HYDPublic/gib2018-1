#r "Newtonsoft.Json"
#r "System.Web"
#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    //get content
    var jsonContent = await req.Content.ReadAsStringAsync();

    log.Info($"Event : {jsonContent}");

    //event data is an Json Array
    var data = JArray.Parse(jsonContent);
    
    //get url from event data
    foreach (JObject item in data)
    {
        var blobEventData = item.GetValue("data");
        log.Info($"blobEventData : {blobEventData}");

        var imageUrl = blobEventData.Value<string>("url");
        log.Info($"imageUrl : {imageUrl}");

        //read image
        var webClient = new WebClient();
        var image = webClient.DownloadData(imageUrl);
        var thumbnailFileName = $"Thumbnail{imageUrl.Substring(imageUrl.LastIndexOf(@"/") + 1)}";

        //generate image
        var thumbnailImageData = await GenerateThumbnail(image);

        //upload to blob
        UploadToBlob(thumbnailImageData, thumbnailFileName);
    }

    var response = req.CreateResponse(HttpStatusCode.OK);
    response.Content = new StringContent("Success", System.Text.Encoding.UTF8, "application/json");
    return response;
}

private static async Task<byte[]> GenerateThumbnail(byte[] image)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "<YOUR KEY>");

    var requestParameters = "width=200&height=150&smartCropping=true";
    var uri = "https://southeastasia.api.cognitive.microsoft.com/vision/v1.0/generateThumbnail?" + requestParameters;

    using (var content = new ByteArrayContent(image))
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var response = client.PostAsync(uri, content).Result;
        byte[] thumbnailImageData = await response.Content.ReadAsByteArrayAsync();

        return thumbnailImageData;
    }
}

private static async Task UploadToBlob(byte[] thumbnail, string thumbnailFileName)
{
    var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
    var blobClient = storageAccount.CreateCloudBlobClient();
    var container = blobClient.GetContainerReference("thumbnails");
    var blockBlob = container.GetBlockBlobReference(thumbnailFileName);

    using (var memoryStream = new MemoryStream(thumbnail)) {
        await blockBlob.UploadFromStreamAsync(memoryStream);
    }
}
