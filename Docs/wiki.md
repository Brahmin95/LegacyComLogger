# Overview: The Modern Logging Framework

## 1. The Problem: A Black Box in Production

Our legacy application ecosystem, composed of both modern .NET services and critical VB6 applications, has historically been a "black box" in production. When issues arise in our Citrix environment, our support teams and developers struggle with:
-   **Lack of Insight:** Inconsistent, unstructured text-based logs make it nearly impossible to search for specific events or correlate actions across different parts of the system.
-   **Diagnosing User-Specific Issues:** It's incredibly difficult to isolate the actions of a single user in our multi-user Citrix environment.
-   **Reactive Troubleshooting:** We often only learn about problems after a user reports a crash, with little to no diagnostic information about what led to the failure.

## 2. The Goal: Achieving Observability

This logging framework was created to solve these problems by introducing modern observability practices to our entire application stack. The primary goal is to **transform our logs from simple text files into rich, structured, and searchable data streams.**

### Conceptual Goals:
-   **Unified Logging:** A single, consistent way to log from any application, whether it's VB6 or .NET.
-   **Structured Data:** Every log event is a structured JSON document, not just a line of text. This means we can filter, aggregate, and build dashboards on any piece of data in the log (e.g., `user.id`, `error.type`, `vb_error.number`).
-   **End-to-End Correlation:** Seamlessly connect a user's action from the first button click in the UI through every database call and business rule, even if it spans multiple applications.
-   **Resilience and Performance:** The logging system must be fast, efficient, and absolutely must not crash the application it's supposed to be monitoring.

## 3. The Solution at a Glance

We have built a highly decoupled logging framework that provides a simple API for developers and a powerful, structured data stream for ingestion into our Elasticsearch cluster. This allows us to trace a user's entire journey, from their session start to a single button click.

[CODE_BLOCK_MERMAID_START]
graph TD
    subgraph "User's Journey"
        U[User Session] -->|Contains| T(Traces)
        T -->|Contains| TX(Transactions)
        TX -->|Contains| S(Spans & Logs)
    end

    subgraph "Application Code"
        A[VB6 Application] -->|Calls COM Object| B(ComBridge)
        C[.NET Application] -->|Calls LogManager| D{Abstractions}
        B -->|Uses| D
    end
    
    subgraph "Framework & Infrastructure"
        D -- "Loads at Runtime" --> E(NLog Provider)
        E -- "Writes ECS JSON" --> F[Log Files on Disk]
        G[Filebeat Agent] -- "Harvests" --> F
        G --> H{Elasticsearch Cluster}
    end

    subgraph "Analysis & Monitoring"
        H --> I[Kibana & Elastic APM]
        I --> J[<a href='https://my-elastic-instance/kibana/dashboards'>Enterprise Dashboards</a>]
    end

    style J fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
[CODE_BLOCK_END]

---

# Architectural Deep Dive

## 1. Guiding Principles

The framework's architecture was guided by three core principles:
1.  **Loose Coupling:** The application code must never have a direct dependency on a specific logging library (like NLog). This allows us to swap the provider in the future without changing any application code.
2.  **Resilience:** The logging framework must be more stable than the application it monitors. A failure to log must never result in an application crash.
3.  **Clear Separation of Concerns:** Each component should have one job and do it well.

## 2. Component Architecture

The solution is composed of a core `MyCompany.Logging` project that contains the abstractions and the COM bridge, and a separate `MyCompany.Logging.NLogProvider` project.

[CODE_BLOCK_MERMAID_START]
graph LR
    subgraph "Consumer Layer (VB6)"
        A[VB6 App] --> B[MyCompany.Logging (COM Interop)]
    end
    subgraph "Consumer Layer (.NET)"
        C[.NET App] --> D[MyCompany.Logging (Abstractions)]
    end
    
    B --> D
    
    subgraph "Core Framework"
        D -- "ILogger, ITracer, LogManager" --> E{Provider-Agnostic Contract}
    end

    subgraph "Implementation Layer"
       F[MyCompany.Logging.NLogProvider] -- "Implements Contracts" --> E
    end

    style F fill:#f9f,stroke:#333,stroke-width:2px
[CODE_BLOCK_END]

-   **`MyCompany.Logging.Abstractions`**: This namespace within the core project is the lightweight, central contract. It contains only interfaces (`ILogger`, `ITracer`) and the static `LogManager`. It has **zero dependencies** on NLog or any other third-party library.
-   **`MyCompany.Logging.NLogProvider`**: This is the concrete implementation. It references the `Abstractions` and contains all the NLog-specific code. It is responsible for all data enrichment.
-   **`MyCompany.Logging.Interop`**: This namespace within the core project is the dedicated adapter for our VB6 clients. It is COM-visible and provides a simple, intuitive API for VB6 developers.

## 3. The Concept of Logging Scopes: Trace, Transaction, and Span

The most powerful feature of this framework is its ability to correlate log messages to a specific user action. When a user clicks "Save Customer," dozens of log messages might be generated from different parts of the code. A **logging scope** is what groups all of these messages together into a single, understandable story.

This framework uses a three-level hierarchy to tell that story:

-   **Trace:** The entire, end-to-end operation. It acts as the "umbrella" for everything that happens as a result of a single trigger.
    -   *Analogy:* The entire story of "The User Saved a Customer".
    -   *ID:* `trace.id`. All events within the trace share this ID.

-   **Transaction:** The main, high-level phase of the operation. For a user-facing application, the first transaction is often the trace itself.
    -   *Analogy:* The main chapter of the story, e.g., "Processing the SaveCustomerClick Event".
    -   *ID:* `transaction.id`.

-   **Span:** A specific, timed sub-operation within a transaction. This is used to measure the performance of individual pieces of work, like a database call or an external API request.
    -   *Analogy:* A paragraph in the chapter, e.g., "Validated Customer Address" or "Updated a record in the database."
    -   *ID:* `span.id`. A span gets its own unique ID but inherits the `trace.id` and `transaction.id` of its parent.

By wrapping a unit of work in a **Trace**, you gain the ability to search for a single `trace.id` in Kibana and see every single log, from every component, in the exact order it happened, allowing you to instantly reconstruct the entire operation.

## 4. The Decoupling Mechanism: Runtime Initialization

The key to the loose coupling is the static `LogManager.Initialize()` method.
-   An application (e.g., a WinForms `Program.cs` or the `ComBridge` constructor) calls the simple `LogManager.Initialize(AppRuntime.DotNet)`.
-   The `LogManager` uses internal configuration to find the provider assembly name (e.g., "MyCompany.Logging.NLogProvider").
-   It then uses **`Assembly.Load()`** to load the provider DLL at runtime and reflection to find and instantiate the provider's factory and tracer classes.
-   This means the consuming application **never needs a compile-time reference** to `MyCompany.Logging.NLogProvider`, allowing the provider to be swapped out in the future by changing the internal configuration in `LogManager`.

## 5. Developer Concerns: The Ambient Context "Backpack"

For VB6, which lacks modern context-propagation features to manage the scopes described above, we built a robust "ambient context" system into the `ComBridge`.

-   **The Problem:** Manually passing a properties dictionary with trace IDs through every function call is tedious and error-prone.
-   **The Solution:** The `ComBridge` exposes `BeginTrace()` and `BeginSpan()` methods. When called, they return a "handle" object (`ILoggingTransaction`). Behind the scenes, the bridge pushes a new scope onto a thread-safe stack.
-   **Automatic Enrichment:** Any log call made while that scope is active will be **automatically enriched** with the correct `trace.id`, `transaction.id`, and `span.id` from the active scope.
-   **Guaranteed Cleanup:** The "handle" object implements `IDisposable`. When the VB6 developer sets the handle variable to `Nothing`, the COM Interop layer guarantees that the `.Dispose()` method on the .NET object is called, which safely pops the scope from the stack. The bridge is also resilient to out-of-order disposal to prevent context corruption.

---

# VB6 Logging: Usage and Examples

## 1. Setup (One-Time Project Configuration)

Before you can use the logger, you must add it to your VB6 project and initialize it once when your application starts.

### Step 1: Register the DLL and Add the Type Library Reference
The `MyCompany.Logging.dll` must be registered on your development machine using `regasm.exe`. The `/tlb` switch is crucial as it creates the Type Library file (`.tlb`) that VB6 needs. Run this from an **Administrator Command Prompt**:

[CODE_BLOCK_SHELL_START]
regasm.exe "C:\Path\To\Your\DLL\MyCompany.Logging.dll" /codebase /tlb
[CODE_BLOCK_END]

Once the `.tlb` file exists, add the reference to your project. **This is the most reliable method.**
1.  In the VB6 IDE, go to **Project -> References...**
2.  Click the **Browse...** button.
3.  In the file dialog, change the file type dropdown to **Type Libraries (*.olb, *.tlb)**.
4.  Navigate to the folder where your `MyCompany.Logging.dll` was built (e.g., `bin\Debug`) and select the generated **`MyCompany.Logging.tlb`** file.
5.  Click **Open**, then **OK**.

*(Note: After referencing the `.tlb` file, you may now see a friendly name like "MyCompany Logging Framework" checked in the references list. Using the Browse method is simply the most direct way to ensure the correct reference is added.)*

### Step 2: Add the Global Logger Module
Our framework uses a shared code module to provide a safe, consistent way to access the logger.
1.  In the VB6 IDE, go to **Project -> Add Module**.
2.  Do not add a new module. Instead, choose the **Existing** tab.
3.  Navigate to the central source control location for shared code (e.g., `\\TFS_Server\Common\VB6_Modules\`) and select **`modLogging.bas`**.
4.  Click **Open**. **Do not copy and paste the code**; linking to the existing file ensures all projects get updates automatically.

The contents of `modLogging.bas` should be:
[CODE_BLOCK_VB_START]
' In modLogging.bas

' The global variable is Private to this module.
Private g_Logger As MyCompanyLogging.LoggingComBridge

' This is called once by the host application's startup code.
Public Sub InitializeLogging()
    If Not g_Logger Is Nothing Then Exit Sub
    Set g_Logger = New MyCompanyLogging.LoggingComBridge
End Sub

' This is the ONLY public entry point for accessing the logger.
' All application code MUST use this function.
Public Function Logger() As MyCompanyLogging.LoggingComBridge
    ' Safety-net fallback in case InitializeLogging was not called.
    If g_Logger Is Nothing Then
        InitializeLogging
    End If
    ' Return the singleton instance.
    Set Logger = g_Logger
End Function
[CODE_BLOCK_END]

### Step 3: Initialize the Logger on Startup
Call the `InitializeLogging` sub from your application's main entry point (e.g., `Sub Main` or the `Form_Load` event of your startup form).

[CODE_BLOCK_VB_START]
' In your application's startup Sub or Form
Private Sub Form_Load()
    ' Initialize the logger once when the application starts.
    InitializeLogging
    
    ' Now you can use the Logger() function anywhere in your application.
    Logger.Info "frmMain", "Form_Load", "Application startup complete."
End Sub
[CODE_BLOCK_END]

## 2. How Correlation IDs Work

Our framework automatically adds several correlation IDs to your logs.

-   **`session.id` (The User Journey):** A unique ID is generated automatically the *very first time* the logging framework is initialized within a process. This ID is then stored in a machine-wide environment variable. Crucially, any **child processes** (other EXEs) launched by your application will automatically inherit this environment variable and therefore share the same `session.id`. This links the entire user journey together, even across multiple applications, for the lifetime of the initial parent process.
-   **`trace.id` & `transaction.id` & `span.id` (Logging Scopes):** As described in the Architectural Deep Dive, these IDs are used to group all logs related to a single operation. You create these scopes using the `BeginTrace` and `BeginSpan` methods.

## 3. Usage and Examples

### Using the Safe Accessor Function
Always use the `Logger()` function from `modLogging.bas` to get the logger object. This guarantees your code will not crash even if initialization order is unexpected.

[CODE_BLOCK_VB_START]
Public Sub cmdSave_Click()
    Dim trace As MyCompanyLogging.ILoggingTransaction
    On Error GoTo Handle_Error
    
    ' Always use the Logger() function to start the trace
    Set trace = Logger.BeginTrace("SaveCustomerClick", TxType_UserInteraction)
    
    Logger.Info "frmCustomer", "cmdSave_Click", "Save operation initiated."
    
    ' ... your business logic here ...

Cleanup:
    If Not trace Is Nothing Then Set trace = Nothing
    Exit Sub
    
Handle_Error:
    Logger.ErrorHandler "frmCustomer", "cmdSave_Click", _
                         "Failed to save customer.", _
                         Err.Description, Err.Number, Err.Source, Erl
    GoTo Cleanup
End Sub
[CODE_BLOCK_END]

---

# .NET Logging - Usage and Examples

## 1. Setup (One-Time Application Configuration)

### Step 1: Initialize the Framework
In your application's main entry point (typically `Program.cs` for Console/WinForms or `Global.asax.cs` for web apps), add a single line to initialize the logging framework.

[CODE_BLOCK_CSHARP_START]
// In Program.cs or equivalent startup file
using MyCompany.Logging.Abstractions;
using System;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Initialize the logger once when the application starts.
        LogManager.Initialize(AppRuntime.DotNet);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form1());
    }
}
[CODE_BLOCK_END]

### Step 2: Getting a Logger Instance
In any class where you need to log, get a logger instance. The best practice is to create a `private static readonly` field. This is highly efficient and the logger object is safe for reuse.

[CODE_BLOCK_CSHARP_START]
// At the top of your class file
using MyCompany.Logging.Abstractions;

public class MyService
{
    // Get a logger instance once per class.
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
    
    // ... now you can use _log in all methods of this class ...
}
[CODE_BLOCK_END]

## 2. How Correlation IDs Work

-   **`session.id` (The User Journey):** A unique ID is generated automatically the *very first time* the logging framework is initialized. This ID is then stored in an environment variable for the lifetime of the process. Crucially, any **child processes** launched by your application will automatically inherit this ID, allowing you to trace a user's entire workflow across multiple executables.
-   **`trace.id`, `transaction.id`, `span.id` (Logging Scopes):** In .NET, these are managed by our framework's `ITracer` interface. You wrap your code in a `LogManager.Tracer.Trace` call, and the framework handles creating the scopes and correlating all logs within them.

## 3. Usage and Examples

### Tracing a Unit of Work - BEST PRACTICE
This is the standard, provider-agnostic pattern for tracing an operation in .NET.

[CODE_BLOCK_CSHARP_START]
using MyCompany.Logging.Abstractions;
using System;

public class OrderProcessor
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
    
    public void FulfillOrder(int orderId)
    {
        // Use the framework's tracer to capture this method as a single transaction.
        LogManager.Tracer.Trace("FulfillOrder", TxType.Process, () =>
        {
            _log.Info("Fulfilling order {OrderId}", orderId);
            
            // To create a child span, you simply nest another Trace call.
            LogManager.Tracer.Trace("NotifyShippingDept", TxType.Process, () =>
            {
                _log.Debug("Calling shipping department API for order {OrderId}", orderId);
            });
            
            _log.Info("Order {OrderId} fulfillment complete.", orderId);
        });
    }
}
[CODE_BLOCK_END]

---

# Post-Deployment Configuration & Analysis

## 1. Adjusting Log Levels with `nlog.config`
The logging verbosity is controlled by the `<rules>` section in the `nlog.config` file, which is deployed alongside your application. This file can be edited on a server **without recompiling or redeploying the application**. Because `autoReload="true"` is set, NLog will automatically pick up the changes within a few seconds.

### Default Configuration
The default rule logs `Info` level and above for all loggers.
[CODE_BLOCK_XML_START]
<rules>
  <!-- DEFAULT PRODUCTION RULE -->
  <logger name="*" minlevel="Info" writeTo="app-log-file" />
</rules>
[CODE_BLOCK_END]

### How to Enable Debug Logging for a Specific Area
To troubleshoot an issue, you can add a more specific rule **above** the default rule. The `final="true"` attribute is critical to prevent duplicate logging.

**Scenario:** We need to enable `Trace` level logging for user `jdoe` but only when they are using the `frmOrders.frm` screen in `LegacyApp.exe`.

1.  Open `nlog.config` on the server.
2.  Add the following `<logger>` block inside the `<rules>` section, **before** the default rule.

[CODE_BLOCK_XML_START]
<rules>
  <!-- 
    TEMPORARY DIAGNOSTIC RULE:
    This rule enables TRACE logging only for user 'jdoe' when the logger name
    starts with 'LegacyApp.exe.frmOrders.frm'.
  -->
  <logger name="LegacyApp.exe.frmOrders.frm.*" minlevel="Trace" writeTo="app-log-file" final="true">
    <filters>
      <when condition="equals('${windows-identity:domain=false}', 'jdoe', ignoreCase=true)" action="Log" />
    </filters>
  </logger>
  
  <!-- DEFAULT PRODUCTION RULE -->
  <logger name="*" minlevel="Info" writeTo="app-log-file" />
</rules>
[CODE_BLOCK_END]

3.  Save the file. The new logging level will take effect almost immediately. Once you are done troubleshooting, simply remove the temporary rule block and save the file again.

## 2. Analyzing Logs in Kibana and Elastic

All logs are enriched with correlation IDs, allowing for powerful analysis.

**[Link to Enterprise Logging Dashboards](https://my-elastic-instance/kibana/app/dashboards)**

### Common Search Queries (KQL) in Kibana

-   **See a user's entire session from start to finish:**
    `session.id: "a8c3e0b1f2d44e5f8a7b6c5d4e3f2a1b"`

-   **See everything related to one specific operation (VB6 or .NET):**
    `trace.id: "e4a9c8b7f6d5e4f3a2b1c0d9e8f7a6b5"`

-   **Find all errors from VB6 applications:**
    `log.level: error and labels.app_type: "VB6"`

-   **Find a specific VB6 runtime error number:**
    `vb_error.number: 76`

-   **Find all logs from a specific .NET service:**
    `service.name: "MyCompany.PaymentService.exe"`

### Using the APM UI in Kibana

For operations traced with our framework's `LogManager.Tracer` or the VB6 `BeginTrace` method, you can use the APM UI:
1.  Navigate to the **APM** section in Kibana.
2.  Find your service (`MyCompany.PaymentService.exe` or `LegacyApp.exe`).
3.  Click on a transaction (e.g., "FulfillOrder" or "SaveCustomerClick") to see the **transaction waterfall view**.
4.  This view shows you the timing of all spans. At the bottom, there is a section for **"Logs"** which will show only the log messages correlated with that specific transaction. This is the fastest way to go from a performance problem to the logs that explain it.