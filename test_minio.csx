using Minio;
using Minio.DataModel.Args;

var client = new MinioClient()
    .WithEndpoint("localhost:9000")
    .WithCredentials("minioadmin", "minioadmin123")
    .Build();

var data = System.Text.Encoding.UTF8.GetBytes("test file content");
using var ms = new MemoryStream(data);
var result = await client.PutObjectAsync(new PutObjectArgs()
    .WithBucket("ivf-signed-pdfs")
    .WithObject("test/upload-test.txt")
    .WithStreamData(ms)
    .WithObjectSize(data.Length)
    .WithContentType("text/plain"));

Console.WriteLine("Upload OK: ETag=" + result.Etag);
