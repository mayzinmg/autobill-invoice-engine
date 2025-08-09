using Grpc.Net.Client;
using Invoice.API.Protos; // generated from your proto

class Program
{
    static async Task Main(string[] args)
    {

         string defaultAzureUrl = Environment.GetEnvironmentVariable("INVOICE_API_URL");

        string link = (args.Length > 0 && !string.IsNullOrEmpty(args[0])) ? args[0] : defaultAzureUrl;

        // Create the gRPC channel
        using var channel = GrpcChannel.ForAddress(link);

        var client = new InvoiceService.InvoiceServiceClient(channel);

        var request = new InvoiceRequest
        {
            InvoiceId = "INV-1001",
            CustomerName = "ACME Ltd",
            CountryCode = "SG",
            RegionCode = "",
            CustomerType = "Company",
            InvoiceDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };

        request.Items.Add(new InvoiceItem
        {
            Description = "Widget A",
            Quantity = 2,
            UnitPrice = 50,
            Category = "standard"
        });
        request.Items.Add(new InvoiceItem
        {
            Description = "Book",
            Quantity = 1,
            UnitPrice = 30,
            Category = "reduced"
        });

        var reply = await client.GenerateInvoiceAsync(request);

        Console.WriteLine($"Invoice ID: {reply.InvoiceId}");
        Console.WriteLine($"Status: {reply.Status}");
        Console.WriteLine($"Download URL: {reply.DownloadUrl}");
        // checking if the PDF content is not null and has length greater than 0 even though the blob failed
        if (reply.PdfContent != null && reply.PdfContent.Length > 0)
        {
            var path = Path.Combine(Environment.CurrentDirectory, $"{reply.InvoiceId}.pdf");
            await File.WriteAllBytesAsync(path, reply.PdfContent.ToByteArray());
            Console.WriteLine($"Saved PDF → {path}");
        }
        else
        {
            Console.WriteLine("No inline PDF bytes returned.");
        }
    }
}
