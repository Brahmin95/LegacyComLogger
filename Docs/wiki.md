### **Wiki Page 1: Overview - The Modern Logging Framework**
*(This would be the landing page for the "Logging Framework" section of your wiki)*

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

```mermaid
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
```

---
### **Wiki Page 2: Architectural Deep Dive**
*(A child page of the Overview)*

# Architectural Deep Dive

## 1. Guiding Principles

The framework's architecture was guided by three core principles:
1.  **Loose Coupling:** The application code must never have a direct dependency on a specific logging library (like NLog). This allows us to swap the provider in the future without changing any application code.
2.  **Resilience:** The logging framework must be more stable than the application it monitors. A failure to log must never result in an application crash.
3.  **Clear Separation of Concerns:** Each component should have one job and do it well.

## 2. Component Architecture

The solution is divided into three key projects, each with a distinct responsibility.

```mermaid
graph LR
    subgraph "Consumer Layer (VB6)"
        A[VB6 App] --> B[MyCompany.Logging.Interop]
    end
    subgraph "Consumer Layer (.NET)"
        C[.NET App] --> D[MyCompany.Logging.Abstractions]
    end
    
    B --> D
    
    subgraph "Core Framework"
        D -- "ILogger, LogManager" --> E{Provider-Agnostic Contract}
    end

    subgraph "Implementation Layer"
       F[MyCompany.Logging.NLogProvider] -- "Implements ILogger" --> E
    end

    style F fill:#f9f,stroke:#333,stroke-width:2px
```

-   **`MyCompany.Logging.Abstractions`**: This is the lightweight, central contract. It contains only interfaces (`ILogger`, `IInternalLogger`) and the static `LogManager`. It has **zero dependencies** on NLog or any other third-party library.
-   **`MyCompany.Logging.NLogProvider`**: This is the concrete implementation. It references the `Abstractions` project and contains all the NLog-specific code. It is responsible for all data enrichment, such as adding APM correlation IDs and translating VB6 error details into structured objects.
-   **`MyCompany.Logging`**: This is a dedicated adapter for our VB6 clients. It is COM-visible and provides a simple, intuitive API for VB6 developers. Internally, it calls the abstract `ILogger` interface.

## 3. The Decoupling Mechanism: Runtime Initialization

The key to the loose coupling is the static `LogManager.Initialize()` method.
-   An application (e.g., a WinForms `Program.cs` or the `ComBridge` constructor) calls `LogManager.Initialize("MyCompany.Logging.NLogProvider", ...)`.
-   The `LogManager` uses **`Assembly.Load()`** to load the provider DLL at runtime.
-   It then uses reflection to find and instantiate the `NLogLoggerFactory`.
-   This means the consuming application **never needs a compile-time reference** to `MyCompany.Logging.NLogProvider`, allowing the provider to be swapped out in the future by simply changing a configuration string.

## 4. Developer Concerns: The Ambient Context "Backpack"

For VB6, which lacks modern context-propagation features, we built a robust "ambient context" system into the `ComBridge`.

-   **The Problem:** Manually passing a properties dictionary through every function call is tedious and error-prone.
-   **The Solution:** The `ComBridge` exposes `BeginTrace()` and `BeginSpan()` methods. When called, they return a "handle" object (`ILoggingTransaction`). Behind the scenes, the bridge pushes a new scope onto a thread-safe stack.
-   **Automatic Enrichment:** Any log call made while that scope is active will be **automatically enriched** with the correct `trace.id`, `transaction.id`, and `span.id` from the active scope.
-   **Guaranteed Cleanup:** The "handle" object implements `IDisposable`. When the VB6 developer sets the handle variable to `Nothing`, the COM Interop layer guarantees that the `.Dispose()` method on the .NET object is called, which safely pops the scope from the stack. The bridge is also resilient to out-of-order disposal to prevent context corruption.

---
### **Wiki Page 3: VB6 Logging - Usage and Examples**
*(A child page of the Overview)*

# VB6 Logging: Usage and Examples

## 1. How Correlation IDs Work in VB6

Our framework automatically adds several correlation IDs to your logs. Understanding them is key to effective troubleshooting.

-   **`session.id` (The User Journey):** A unique ID is generated **automatically** the first time the logger is used in an application process. This ID is attached to *every single log* for that user's session, allowing us to see everything they did from start to finish.
-   **`trace.id` & `transaction.id` (A Single Operation):** When you want to group all logs related to a single user action (like clicking a button), you use the `BeginTrace` method. This creates a "trace" for the operation and a "transaction" for the initial step.
-   **`span.id` (A Sub-Operation):** Within a trace, you can use `BeginSpan` to measure and group logs for a smaller piece of work, like a database call.

## 2. A Note on Instrumenting Legacy Code

This logging framework provides the **capability** for rich, structured logging. However, it does not automatically add logging to the existing VB6 codebase. The value of this system will grow over time as developers:
-   **Add new logging statements** to key areas of the application.
-   **Retrofit existing error handlers** to use the new `g_Logger.ErrorHandler` method.
-   **Wrap critical business logic** in `BeginTrace` blocks.

This is an ongoing effort. When you are working on a piece of legacy code, take the opportunity to improve its observability by adding logging calls.

## 3. Usage and Examples

### Basic Logging (No Trace Context)
These logs will automatically have a `session.id` but no `trace.id` or `transaction.id`.

```vb
' This log will be tied to the user's session, but not a specific action.
g_Logger.Info "frmMain", "Form_Load", "Main application form loaded successfully."
```

### Tracing a Unit of Work (The "Backpack") - BEST PRACTICE

This is the **recommended pattern** for any significant user action. It creates a full trace, allowing you to see all related logs together in Kibana.

**Rule:** The object returned by `BeginTrace` or `BeginSpan` **MUST** be set to `Nothing` when the operation is complete to clean up the context. Always use the `On Error GoTo...Cleanup` pattern to guarantee this.

```vb
Public Sub cmdSave_Click()
    Dim trace As MyCompanyLogging.ILoggingTransaction
    Dim dbSpan As MyCompanyLogging.ILoggingTransaction
    
    On Error GoTo Handle_Error
    
    ' 1. START THE TRACE: This creates the trace.id and transaction.id.
    Set trace = g_Logger.BeginTrace("SaveCustomerClick", "ui.interaction")

    ' This log now has session, trace, and transaction IDs.
    g_Logger.Info "frmCustomer", "cmdSave_Click", "Save operation initiated by user."
    
    ' 2. START A SPAN: Measure a specific sub-operation.
    Set dbSpan = g_Logger.BeginSpan("SaveToDatabase", "db.sql")
    
        ' This log has session, trace, transaction, AND span IDs.
        g_Logger.Debug "frmCustomer", "cmdSave_Click", "Executing UPDATE statement."
        
    ' 3. END THE SPAN
    Set dbSpan = Nothing
    
    g_Logger.Info "frmCustomer", "cmdSave_Click", "Database save complete."

' --- Cleanup Block: This runs on both success and error paths ---
Cleanup:
    ' Safely end scopes in reverse order of creation.
    ' Setting the object to Nothing triggers the .Dispose() method on the .NET side,
    ' which safely pops the context from the stack.
    If Not dbSpan Is Nothing Then Set dbSpan = Nothing
    If Not trace Is Nothing Then Set trace = Nothing
    Exit Sub

' --- Error Handling Block ---
Handle_Error:
    ' The active trace/span context is added automatically to this error log!
    g_Logger.ErrorHandler "frmCustomer", "cmdSave_Click", _
                         "An unexpected database error occurred during save.", _
                         Err.Description, Err.Number, Err.Source, Erl
    ' After logging, jump to the cleanup block to ensure context is released.
    GoTo Cleanup
End Sub
```

---
### **Wiki Page 4: .NET Logging - Usage and Examples**
*(A child page of the Overview)*

# .NET Logging: Usage and Examples

## 1. How Correlation IDs Work in .NET

-   **`session.id` (The User Journey):** Just like in VB6, this is generated **automatically** when the application starts and is added to all logs from that process.
-   **`trace.id`, `transaction.id`, `span.id` (Tracing):** In .NET, these are managed **automatically by the Elastic APM Agent**. You do not need to call `BeginTrace` or `BeginSpan`. You simply tell the APM agent what constitutes a "transaction," and the logging framework automatically enriches all log messages created within that scope.

## 2. Tracing with Elastic APM

This is the standard pattern for .NET code. The logging framework is designed to integrate seamlessly.

```csharp
using Elastic.Apm; // Add reference to the APM agent
using MyCompany.Logging.Abstractions;

public class OrderProcessor
{
    private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
    
    public void FulfillOrder(int orderId)
    {
        // Use the APM agent to capture this entire method as a single transaction.
        // The agent creates the trace.id and transaction.id.
        Agent.Tracer.CaptureTransaction("FulfillOrder", "business.logic", (transaction) =>
        {
            // This log will automatically have session, trace, and transaction IDs.
            logger.Info("Fulfilling order {OrderId}", orderId);
            
            // Start a child span to measure a specific sub-operation.
            transaction.CaptureSpan("NotifyShippingDept", "external.http", (span) =>
            {
                // This log will have session, trace, transaction, AND span IDs.
                logger.Debug("Calling shipping department API for order {OrderId}", orderId);
            });
            
            logger.Info("Order {OrderId} fulfillment complete.", orderId);
        });
    }

    public void HandleError()
    {
        try
        {
            throw new InvalidOperationException("Could not connect to warehouse inventory.");
        }
        catch (Exception ex)
        {
            // If an APM transaction is active, this error log will be automatically
            // correlated with it.
            logger.Error(ex, "An error occurred while handling inventory.");
        }
    }
}
```

---
### **Wiki Page 5: Post-Deployment Configuration & Analysis**
*(A child page of the Overview)*

# Post-Deployment Configuration & Analysis

## 1. Adjusting Log Levels with `nlog.config`
The logging verbosity is controlled by the `<rules>` section in the `nlog.config` file, which is deployed alongside your application. This file can be edited on a server **without recompiling or redeploying the application**. Because `autoReload="true"` is set, NLog will automatically pick up the changes within a few seconds.

### Default Configuration
The default rule logs `Info` level and above for all loggers.
```xml
<rules>
  <!-- DEFAULT PRODUCTION RULE -->
  <logger name="*" minlevel="Info" writeTo="app-log-file" />
</rules>
```

### How to Enable Debug Logging for a Specific Area
To troubleshoot an issue, you can add a more specific rule **above** the default rule. The `final="true"` attribute is critical to prevent duplicate logging.

**Scenario:** We need to enable `Trace` level logging for user `jdoe` but only when they are using the `frmOrders.frm` screen in `LegacyApp.exe`.

1.  Open `nlog.config` on the server.
2.  Add the following `<logger>` block inside the `<rules>` section, **before** the default rule.

```xml
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
```
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

For operations traced with the .NET APM agent or the VB6 `BeginTrace` method, you can use the APM UI:
1.  Navigate to the **APM** section in Kibana.
2.  Find your service (`MyCompany.PaymentService.exe` or `LegacyApp.exe`).
3.  Click on a transaction (e.g., "FulfillOrder" or "SaveCustomerClick") to see the **transaction waterfall view**.
4.  This view shows you the timing of all spans. At the bottom, there is a section for **"Logs"** which will show only the log messages correlated with that specific transaction. This is the fastest way to go from a performance problem to the logs that explain it.