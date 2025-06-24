<#
.SYNOPSIS
    Deletes application log files older than a specified number of days.

.DESCRIPTION
    This script recursively searches a root log directory for all .log files
    and deletes any that have a LastWriteTime older than the specified age.
    It includes a -WhatIf switch for safe testing.

.PARAMETER Path
    The root directory where application logs are stored.
    Example: C:\Logs\AppLogs

.PARAMETER DaysToKeep
    The maximum age of log files to keep. Any file older than this will be deleted.
    Default is 30 days.

.EXAMPLE
    # Run in test mode to see which files WOULD be deleted (no actual deletion).
    .\Cleanup-AppLogs.ps1 -Path "C:\Logs\AppLogs" -DaysToKeep 30 -WhatIf -Verbose

.EXAMPLE
    # Run for real, deleting files older than 14 days.
    .\Cleanup-AppLogs.ps1 -Path "C:\Logs\AppLogs" -DaysToKeep 14 -Verbose
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$Path,

    [Parameter(Mandatory=$false)]
    [int]$DaysToKeep = 30
)

# --- Main Script ---

# Validate that the path exists to prevent errors.
if (-not (Test-Path -Path $Path -PathType Container)) {
    Write-Error "Error: The specified path '$Path' does not exist or is not a directory."
    # Exit with a non-zero code to indicate failure to the Task Scheduler.
    exit 1
}

# Calculate the cutoff date. Any file written to before this date will be deleted.
$cutoffDate = (Get-Date).AddDays(-$DaysToKeep)
Write-Verbose "Cutoff date for deletion: $cutoffDate"

try {
    Write-Verbose "Searching for log files older than $DaysToKeep days in '$Path'..."

    # Get all .log files recursively, then filter them by their last write time.
    $oldLogs = Get-ChildItem -Path $Path -Include "*.log" -Recurse | Where-Object { $_.LastWriteTime -lt $cutoffDate }

    if ($null -ne $oldLogs) {
        $fileCount = ($oldLogs | Measure-Object).Count
        Write-Verbose "Found $fileCount log files to delete."

        # Delete the files. The -WhatIf parameter is automatically handled by PowerShell
        # if the script is called with -WhatIf.
        $oldLogs | Remove-Item -Force -Verbose
    }
    else {
        Write-Verbose "No log files older than $DaysToKeep days were found."
    }

    Write-Verbose "Log cleanup script finished successfully."
    exit 0
}
catch {
    # Log any unexpected errors during the process.
    Write-Error "An unexpected error occurred during cleanup: $($_.Exception.Message)"
    exit 1
}