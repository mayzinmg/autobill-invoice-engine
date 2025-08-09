# Autobill Invoice Engine

Business‑grade **Invoice Generator & Tax Engine** with:
- gRPC server (PDF generation via QuestPDF)
- Multi‑component tax rules (country / region / category / customer type)
- Azure Blob upload + SAS link (optional)
- Tiny .NET console client (HTTP→gRPC test client)

## ✨ Why this is valuable
- Real workflow: **JSON → PDF invoice → (optional) Blob URL**
- Fast internal comms via **gRPC**
- Tax logic separated as an **engine** (easy to extend)
- Cloud‑ready: **Azure Container Apps**, Key Vault, Blob Storage

---

## 🧭 Architecture

```
Invoice.Client (Console)
   ↓ gRPC over HTTPS (HTTP/2)
Invoice.API (gRPC Server)
   ├─ TaxEngine (multi-component)
   ├─ QuestPDF invoice rendering
   └─ BlobUploader → Azure Blob Storage (SAS URL)
```

---

## 📦 Projects

- `Invoice.API` — gRPC server (HTTP/2), QuestPDF, Tax Engine, Blob upload
- `Invoice.PDF` — PDF composer (QuestPDF). No dependency back to API.
- `Invoice.Client` — console client to call `GenerateInvoice`

---

## 🚀 Quick Start (local)

### 1) Prereqs
- .NET 8 SDK
- (Optional) Docker Desktop if you want to containerize locally

### 2) Restore & build
```bash
dotnet restore
dotnet build
```

### 3) Run the gRPC server locally
```bash
dotnet run --project Invoice.API
```
The server listens on **HTTP/2** (Kestrel) and exposes:
- `/: "gRPC service is running."` (health text)
- `InvoiceService.GenerateInvoice` (gRPC)

### 4) Run the client (with env var)
Set your API URL via environment variable (kept out of Git):

**PowerShell**
```powershell
set INVOICE_API_URL=https://YOUR_CONTAINER_APP_FQDN
dotnet run --project Invoice.Client
```

**cmd**
```cmd
set INVOICE_API_URL=https://YOUR_CONTAINER_APP_FQDN
dotnet run --project Invoice.Client
```

**bash**
```bash
export INVOICE_API_URL=https://YOUR_CONTAINER_APP_FQDN
dotnet run --project Invoice.Client
```

Expected output:
```
Invoice ID: INV-1001
Status: OK
Download URL: https://... (if Blob upload configured)
```
The client also saves the inline PDF as `INV-1001.pdf` if present.

---

## 🧪 Sample request used by the client

```json
{
  "invoiceId": "INV-1001",
  "customerName": "ACME Ltd",
  "countryCode": "SG",
  "regionCode": "",
  "customerType": "Company",
  "invoiceDate": "2025-08-09",
  "items": [
    { "description": "Widget A", "quantity": 2, "unitPrice": 50, "category": "standard" },
    { "description": "Book",     "quantity": 1, "unitPrice": 30, "category": "reduced" }
  ]
}
```

---

## 🧮 Tax Engine (tiny but useful)

- Rules match by `country`, optional `region`, optional `productCategory`, optional `customerType`
- Each rule can have **multiple components** (e.g., State + City, VAT + Eco fee)
- MVP math: `subtotal × rate` per component, rounded to 2dp

Add your rules in `Invoice.API.Domain.Tax.TaxEngine`.

---

## ☁️ Deploy to Azure Container Apps

### 0) One‑time: push image to ACR
```powershell
az acr login --name <yourACRname>
docker build -t <yourACRname>.azurecr.io/invoiceapi:v1 ./Invoice.API
docker push <yourACRname>.azurecr.io/invoiceapi:v1
```

### 1) Create / update Container App
Set **ingress: external**, **target port: 5000**, **transport: http2**.  
From Azure Portal (Ingress tab) or CLI (newer CA extension):

```powershell
# Using Azure Portal is easiest: Ingress → External, TargetPort=5000, Transport=HTTP/2
```

Also set **Minimum replicas = 1** to avoid cold‑start “no healthy upstream”.

### 2) Health check
Open:
```
https://<your-app>.region.azurecontainerapps.io/
```
You should see: `gRPC service is running.`

---

## 🔐 Blob Storage & Key Vault (optional but recommended)

You can authenticate either with **Connection String** (quick) or **Managed Identity + AccountUrl** (safer).

### A) Connection String (quickest)

1) **Key Vault secret**  
   - KV → Secrets → **Generate/Import**  
   - Name: `BlobConnectionString`  
   - Value: `DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net`

2) **Container App settings**
   - `KEY_VAULT_URI` = `https://<your-kv-name>.vault.azure.net/`

3) **Container App Identity**
   - Enable **System‑assigned identity**
   - Key Vault → Access control (IAM) → **Key Vault Secrets User** → assign to your Container App identity

> The app retrieves the secret at runtime and uploads the PDF to Blob.

### B) Managed Identity (no keys)

1) Container App → **Identity** → System‑assigned = On  
2) Storage Account → IAM → **Storage Blob Data Contributor** → assign to Container App identity  
3) App settings:  
   - `Storage:AccountUrl` = `https://<account>.blob.core.windows.net`  
   - `Storage:Container` = `invoices`

> If you use MI only, switch the uploader to **User Delegation SAS** (code already scaffolded in comments).

---

## 🔍 Test with grpcurl (optional)

List services (requires reflection on the server):
```bash
grpcurl <your-app>.azurecontainerapps.io:443 list
```

Call method (supply `-d` with payload):
```bash
grpcurl -d @ <your-app>.azurecontainerapps.io:443 invoice.InvoiceService/GenerateInvoice < payload.json
```

---

## 🧰 Troubleshooting

**HTTP/1.x request to HTTP/2 endpoint**  
- Container Apps ingress must be **HTTP/2** and **targetPort=5000**  
- Kestrel must bind **HTTP/2** on 5000  
- Use a gRPC client (console app or grpcurl), not a plain browser for the method

**no healthy upstream**  
- Set **Minimum replicas = 1**, check app logs for startup exceptions  
- Usually caused by missing Blob/Key Vault settings

**gRPC 400 Bad response**  
- Ingress transport isn’t HTTP/2 → fix Ingress settings

**Blob upload fails**  
- **404 SecretNotFound**: create `BlobConnectionString` secret in KV (or change the code to your actual secret name)  
- **403 AuthorizationPermissionMismatch**: grant **Storage Blob Data Contributor** to your Container App identity (if using MI)  
- **Invalid AccountUrl**: must be `https://<account>.blob.core.windows.net`

---

## 🔧 Local config tips

- Client hides URL via env var:
  - `INVOICE_API_URL=https://<your-app>.azurecontainerapps.io`
- Server local dev:
  - Add `AzureStorage:BlobConnectionString` to `appsettings.Development.json`  
  - Or set `KEY_VAULT_URI` to your KV and run with Azure CLI logged in

---

## 📝 License
MIT (or your choice)

---

## 🙌 Credits
- PDF generation: **QuestPDF**  
- Hosting: **Azure Container Apps**  
- Storage: **Azure Blob Storage**  
