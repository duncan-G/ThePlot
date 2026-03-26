# ThePlot

**ThePlot** is a distributed screenplay import pipeline that ingests PDF screenplays, validates them, splits them into page chunks, parses scene structure (characters, locations, dialogue, action), and streams real-time import status to users. Built with .NET Aspire, it features a gRPC API, Angular frontend, PostgreSQL database, Azure Blob Storage, and Azure Service Bus.

---

## Architecture Overview

The system processes PDF uploads through a multi-stage pipeline: **upload → validation → splitting → processing → status sync**. Each stage is decoupled via queues and blob storage, enabling horizontal scaling and fast user feedback.

### High-Level Flow

```mermaid
graph TD
    Client[Angular Client]
    API[gRPC API Upload]
    Blob[Azure Blob pdf-uploads]
    Val[Validation Worker]
    Splitter[Splitter Worker pages 1–10 first]
    Processor[Processor Worker parse + persist]
    Listener[Status Listener DB + EventBus]

    Client -- "① SAS Token Request" --> API
    Client -- "② Direct PUT (SAS URL)" --> Blob
    Blob -- "③ Event Grid → Service Bus" --> Val
    Val -- "④ Splitter Queues" --> Splitter
    Splitter -- "⑤ Processor Queues" --> Processor
    Processor -- "⑥ Status Queue" --> Listener
    Listener -- "⑦ gRPC Stream (ImportStatus)" --> Client
    
    classDef default fill:#e8e8e8,stroke:#333,stroke-width:2px,color:#222;
```

---

## Detailed Architecture

### 1. Client → API: SAS Token Request

The client requests a short-lived SAS (Shared Access Signature) token from the gRPC API to upload directly to blob storage. This avoids proxying large PDFs through the API.

```mermaid
sequenceDiagram
    participant C as Client (Angular)
    participant API as gRPC Upload Service
    participant Blob as Blob Storage

    C->>API: RequestUploadToken
    API->>Blob: Generate SAS URI
    Note right of Blob: Container: pdf-uploads. Permissions: Create, Write. Expiry: 90 seconds
    API-->>C: UploadTokenResponse (upload_url, blob_name)
```

**Why SAS?** Direct client-to-blob upload reduces API load, improves throughput, and keeps PDF bytes off application servers.

---

### 2. Client → Blob: Direct Upload

Using the SAS URL, the client uploads the PDF with a single `PUT` request. No application server is in the path.

```mermaid
sequenceDiagram
    participant C as Client (Angular)
    participant Blob as Azure Blob (pdf-uploads/{guid}-{name}.pdf)

    C->>Blob: PUT (SAS URL) [Body: PDF bytes]
    Note right of Blob: Blob created. x-ms-meta-traceparent attached (for distributed tracing)
```

---

### 3. Blob → Validation Worker (Event Grid → Service Bus)

When a blob is created in `pdf-uploads`, Azure Blob Storage emits an Event Grid event that is routed to a Service Bus queue. The validation worker consumes from this queue and validates the PDF. (Locally, the worker polls the blob container directly.)

```mermaid
sequenceDiagram
    participant Blob as Azure Blob (pdf-uploads)
    participant EG as Event Grid (BlobCreated)
    participant SB as Service Bus (pdf-validation)
    participant Worker as PdfValidationWorker

    Blob->>EG: Event
    EG->>SB: Route to queue
    SB->>Worker: Consume message
    Note right of Worker: Validates: Size ≤ 10 MB, Content-Type: application/pdf, PDF magic bytes (%PDF-)
    Note right of Worker: On pass: Create Screenplay placeholder, Enqueue pages 1–10 → priority queue
    Note right of Worker: On fail: Send ValidationFailed → status queue
```

---

### 4. Validation → Splitter Queues: First 10 Pages Priority

After validation, **only pages 1–10** are enqueued to the **priority** splitter queue. Remaining pages are enqueued later by the splitter worker to the **standard** queue.

```mermaid
graph TD
    Val[PdfValidationWorker]
    Q1[(pdf-splitting-priority)]
    Q2[(pdf-splitting-standard)]
    N1[First chunk only - pages 1-10]
    N2[Remaining chunks enqueued later - 11-20, 21-30, ...]

    Val -- "Single message: BlobName, StartPage 1, EndPage 10, ScreenplayId" --> Q1
    Q1 -.-> N1
    Q2 -.-> N2
```

**Why split the first 10 pages first?** - **Fast time-to-content**: Users see the beginning of their screenplay quickly.  
- **Early feedback**: If parsing fails on page 1, the user learns sooner.  
- **Progressive loading**: The UI can render scenes as each chunk completes, instead of waiting for the full document.

---

### 5. Splitter Worker → Processor Queues

The splitter worker consumes from both queues, extracts page ranges using MuPDF, uploads chunks to `pdf-chunks`, and enqueues processing requests.

```mermaid
graph TD
    SP_Q[(pdf-splitting-priority)]
    SS_Q[(pdf-splitting-standard)]
    
    Worker["PdfSplittingWorker: Download from pdf-uploads, Extract pages (MuPDF), Upload to pdf-chunks, Enqueue to processor"]
    
    PP_Q[(pdf-processing-priority chunk 1–10)]
    PS_Q[(pdf-processing-standard chunks 11–20, ...)]

    SP_Q --> Worker
    SS_Q --> Worker
    Worker --> PP_Q
    Worker --> PS_Q
```

**Flow for first chunk (1–10):** 1. Split pages 1–10 → upload chunk blob.
2. Enqueue to `pdf-processing-priority`.
3. If total pages > 10, enqueue remaining ranges (11–20, 21–30, …) to `pdf-splitting-standard`.

**Flow for remaining chunks:** 1. Split pages N–(N+9) → upload chunk blob.
2. Enqueue to `pdf-processing-standard`.

---

### 6. Processor Worker → gRPC Service: Status Sync with DB and User

The processor worker parses each chunk, persists scenes to PostgreSQL, and sends status events to the `screenplay-import-status` queue. The gRPC API’s **ScreenplayImportStatusListener** consumes this queue, updates the DB, and pushes events to connected streaming clients.

```mermaid
graph TD
    PP_Q[(pdf-processing-priority)]
    PS_Q[(pdf-processing-standard)]
    
    Worker["PdfProcessingWorker: Download chunk from pdf-chunks, Parse screenplay (regex), Persist to PostgreSQL, Send Status Event"]
    
    Stat_Q[(screenplay-import-status)]
    
    Listener["ScreenplayImportStatus Listener (gRPC API): Update chunk status in DB, Publish to EventBus"]
    
    DB[(PostgreSQL)]
    Bus[EventBus in-memory fan-out]
    Client[User / Angular Client]

    PP_Q --> Worker
    PS_Q --> Worker
    Worker --> Stat_Q
    Stat_Q --> Listener
    Listener --> DB
    Listener --> Bus
    Bus -- "gRPC Stream: ImportStatusEvent" --> Client
```

**Status event kinds:** `BlobUploaded` → `ValidationPassed` / `ValidationFailed` → `ChunkSplitDone` → `ChunkProcessDone` / `ChunkProcessFailed` → `ImportFailed` (on terminal error)

---

## Component Diagram

```mermaid
flowchart TD
    subgraph Client_Side["Client Side"]
        Client[Angular Client]
    end

    subgraph API_Layer["API Layer"]
        Envoy[gRPC API Proxy Envoy]
        UploadSvc[Upload Service]
        ScreenplaySvc[Screenplay Service]
        StatusListener[Status Listener Background Service]
    end

    subgraph Azure_Cloud["Azure Cloud"]
        Blob[(Blob Storage pdf-uploads / pdf-chunks)]
        ASB((Azure Service Bus))
    end

    subgraph Workers["Workers"]
        Val[Validation Worker]
        Splitter[Splitter Worker 3 replicas]
        Processor[Processor Worker 3 replicas]
    end

    subgraph Data["Data"]
        DB[(PostgreSQL pgvector)]
    end

    %% Force strict vertical stacking of layers
    Client ~~~ Envoy
    UploadSvc ~~~ Blob
    ASB ~~~ Val
    Processor ~~~ DB

    %% Network / Client Flows
    Client -- "StreamImportStatus" <--> Envoy
    Client -- "Generate SAS" --> Envoy
    Client -- "Direct PUT" --> Blob

    Envoy --> UploadSvc
    Envoy --> ScreenplaySvc
    StatusListener -. "Updates" .-> Envoy

    %% Event & Queue Flows
    Blob -- "Event Grid → pdf-validation" --> ASB
    ASB -- "pdf-validation" --> Val
    Val -- "Queue Split" --> ASB

    ASB -- "pdf-splitting-*" --> Splitter
    Splitter -- "pdf-processing-*" --> ASB
    ASB -- "pdf-processing-*" --> Processor
    Processor -- "screenplay-import-status" --> ASB
    ASB -- "screenplay-import-status" --> StatusListener

    %% Data Flows
    Processor -- "EF Core" --> DB
    StatusListener -- "Updates Status" --> DB
    
    classDef storage fill:#c9a227,stroke:#333,color:#fff;
    classDef worker fill:#1e5a8e,stroke:#333,color:#fff;
    class Blob,DB,ASB storage;
    class Val,Splitter,Processor worker;
```

---

## Data Flow Summary


| Step | From                | To                  | Protocol/Transport                             |
| ---- | ------------------- | ------------------- | ---------------------------------------------- |
| 1    | Client              | gRPC API            | gRPC `RequestUploadToken`                      |
| 2    | Client              | Blob Storage        | HTTP PUT (SAS URL)                             |
| 3    | Blob Storage        | Validation Worker   | Event Grid → Service Bus `pdf-validation`      |
| 4    | Validation Worker   | Splitter Queues     | Service Bus `pdf-splitting-priority`           |
| 5    | Splitter Worker     | Processor Queues    | Service Bus `pdf-processing-priority/standard` |
| 6    | Processor Worker    | Status Queue        | Service Bus `screenplay-import-status`         |
| 7    | Processor Worker    | PostgreSQL          | EF Core (scenes, characters, locations)        |
| 8    | Status Listener     | EventBus → Client   | gRPC `StreamImportStatus` (server streaming)   |


---

## How to start app

**Software Requirements**
- .NET 10
- Node/npm
- Docker
- [Aspire CLI](https://aspire.dev/get-started/install-cli/)


```sh
aspire run
```

In console logs, you will see a link to the Aspire dashboard. There you can access every service in the app.

## How to Deploy
**Requirements**
- Azure Subscription
- [Aspire Developer CLI](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/)


```sh
# Provision and Deploy
azd up

# Provision only
azd provision

# Deploy only
azd deploy [optional] <APP_NAME>

# Genenerate bicep files to make modifications to deployment
azd infra gen

# Teardown
azd down
```

