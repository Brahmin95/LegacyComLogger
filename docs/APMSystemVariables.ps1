# =============================================================================
# Configure-ApmEnvironment.ps1
#
# Sets the required system-wide environment variables for the Elastic APM Agent.
# MUST be run with Administrator privileges to modify machine-level variables.
# =============================================================================

# --- BEGIN CONFIGURATION ---

$apmSettings = @{
    "ELASTIC_APM_SERVER_URL"   = "http://my-apm-server:8200";
    "ELASTIC_APM_SECRET_TOKEN" = "YourSecretTokenHere";
    "ELASTIC_APM_ENVIRONMENT"  = "production";
    # ELASTIC_APM_SERVICE_NAME is usually set by the application itself,
    # but can be overridden here if needed for all apps on the server.
    # "ELASTIC_APM_SERVICE_NAME" = "MyDefaultService"
}

# --- END CONFIGURATION ---


# --- SCRIPT LOGIC ---

Write-Host "Applying APM Environment Variables (System-wide)..." -ForegroundColor Yellow

foreach ($key in $apmSettings.Keys) {
    $value = $apmSettings[$key]
    
    try {
        Write-Host "Setting '$key' to '$value'..."
        # Set the environment variable at the 'Machine' scope.
        [System.Environment]::SetEnvironmentVariable($key, $value, [System.EnvironmentVariableTarget]::Machine)
        Write-Host " -> SUCCESS" -ForegroundColor Green
    }
    catch {
        Write-Error " -> FAILED to set variable '$key'. Please ensure this script is run as Administrator."
        # Stop on first error
        exit 1
    }
}

Write-Host ""
Write-Host "Configuration complete. A system reboot or user logoff/logon may be required for all processes to see the new variables." -ForegroundColor Cyan