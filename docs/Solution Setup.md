---
### **Revised Deployment Plan**

Here is the corrected, definitive deployment plan that fully honors your loose coupling principle.

---
#### **Part 1: File Manifest (No Change)**

The list of files to copy remains the same. The key is that **you do not need to create or modify an `app.config` file** for APM settings.

*   `MyCompany.Logging.dll`
*   `MyCompany.Logging.NLogProvider.dll`
*   `NLog.dll`
*   `Elastic.Apm.dll`
*   `nlog.config`

---
#### **Part 2: COM Registration (No Change)**

The `regasm.exe` step remains exactly the same. This is for the operating system to find your COM bridge.

```shell
C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe MyCompany.Logging.dll /tlb
```

---
#### **Part 3: Configuration (The Correct Way)**

This is where the new, correct approach comes in. Instead of editing `app.config`, you will configure the target server's environment.

**Step 1: Configure `nlog.config` (No Change)**
This is still required. Edit the `nlog.config` file in the `bin` directory to set the correct log file path for the server.

**Step 2: Set Environment Variables for Elastic APM**
This is the new, crucial step. You must set these environment variables **for the user account that will run the legacy application**. In a Citrix environment, this might be a system-wide setting or part of a user's logon script.

Here are the most common variables you will need to set:

*   **`ELASTIC_APM_SERVER_URL`**
    *   **Description:** The full URL of your APM Server.
    *   **Example Value:** `http://my-apm-server.my-domain:8200`

*   **`ELASTIC_APM_SERVICE_NAME`**
    *   **Description:** A logical name for your application. If this is not set, the APM agent will default to using the entry assembly's name (e.g., `LegacyApp.exe`), but setting it explicitly is better practice.
    *   **Example Value:** `My-Legacy-ERP`

*   **`ELASTIC_APM_SECRET_TOKEN`** or **`ELASTIC_APM_API_KEY`**
    *   **Description:** The authentication token for connecting to your APM Server. Use one or the other based on your APM Server's configuration.
    *   **Example Value:** `aVerySecretTokenString...`

*   **`ELASTIC_APM_ENVIRONMENT`**
    *   **Description:** The name of the environment (e.g., `production`, `staging`, `uat`). This is extremely useful for filtering in Kibana.
    *   **Example Value:** `production`

**How to Set Them:**
*   **Manually (for testing):** You can open the System Properties > Environment Variables dialog on the server and add these as System or User variables.
*   **Automated (Best Practice):** These should be set by your deployment scripts (e.g., PowerShell, Ansible) or as part of a Group Policy Object (GPO) or a Citrix logon script. This ensures the configuration is consistent and repeatable.

For example, a PowerShell command to set a system-wide variable would be:
```powershell
[System.Environment]::SetEnvironmentVariable('ELASTIC_APM_SERVER_URL', 'http://my-apm-server:8200', 'Machine')
```

By using environment variables, you have achieved the ideal state: your application requires **zero configuration changes** to enable or change its APM monitoring. The configuration lives entirely in the environment where it belongs.