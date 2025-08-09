using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Google.Protobuf;
using Grpc.Core;
using Invoice.API.Domain.Tax;
using Invoice.API.Protos;
using Invoice.Models.InvoicePdf;
using Invoice.Models.Tax;
using static Invoice.PDF.InvoiceGenerator;

namespace Invoice.API.Services
{

    public class InvoiceGrpcService : InvoiceService.InvoiceServiceBase
    {
        private readonly ITaxEngine _tax;
        private readonly BlobUploader _blob; // if you have it
        public InvoiceGrpcService(ITaxEngine tax, BlobUploader blob /* ...other deps */)
        {
            _tax = tax;
            _blob = blob;
        }

        public override async Task<InvoiceResponse> GenerateInvoice(InvoiceRequest request, ServerCallContext context)
        {
            try
            {
                // 1) compute subtotal
                decimal subtotal = 0m;
                foreach (var it in request.Items)
                    subtotal += (decimal)it.UnitPrice * it.Quantity;

                // 2) resolve taxes
                var components = new List<TaxComponent>();
                foreach (var it in request.Items)
                {
                    components.AddRange(_tax.Resolve(
                        request.CountryCode,
                        string.IsNullOrWhiteSpace(request.RegionCode) ? null : request.RegionCode,
                        string.IsNullOrWhiteSpace(it.Category) ? null : it.Category,
                        string.IsNullOrWhiteSpace(request.CustomerType) ? null : request.CustomerType));
                }

                // 3) calc totals
                var (taxTotal, breakdown) = _tax.Calculate(subtotal, components);
                var grand = subtotal + taxTotal;

                // 4) render PDF
                var pdfBytes = await InvoicePdfComposer.RenderAsync(new InvoicePdf
                {
                    InvoiceId = request.InvoiceId,
                    CustomerName = request.CustomerName,
                    Items = request.Items.Select(i => (i.Description, i.Quantity, (decimal)i.UnitPrice)).ToList(),
                    Subtotal = subtotal,
                    TaxBreakdown = breakdown,
                    GrandTotal = grand
                });

                // 5) upload (guard this – most common crash point)
                string? url = null;
                try
                {
                    url = await _blob.UploadAsync(pdfBytes, $"{request.InvoiceId}.pdf");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Blob upload failed: {ex.Message}");
                    // continue; we still return inline PDF
                }

                return new InvoiceResponse
                {
                    InvoiceId = request.InvoiceId,
                    Status = "OK",
                    DownloadUrl = url ?? "",
                    PdfContent = Google.Protobuf.ByteString.CopyFrom(pdfBytes),
                    Subtotal = (double)subtotal,
                    TaxTotal = (double)taxTotal,
                    GrandTotal = (double)grand
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GenerateInvoice error: {ex}");
                return new InvoiceResponse
                {
                    InvoiceId = request.InvoiceId,
                    Status = "ERROR",
                    ErrorMessage = ex.Message
                };
            }
        }

    }

}


