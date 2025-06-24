' A module to wrap a safe logging accessor
' it's implementation is Idempotic in case IniliazeLogging is accidenally re-called
' or the developer forgets to initialise prior to calling the logger() accessor.
' ensure that the COM assembly is also added to any app that uses this module.

Private g_Logger As MyCompanyLogging.LoggingComBridge

' This initialization sub remains Public so it can be called
' by the host application's startup code (e.g., Sub Main).
Public Sub InitializeLogging()
    ' If the logger already exists, do nothing.
    If Not g_Logger Is Nothing Then Exit Sub
    
    ' Create the one and only logger instance for the application.
    Set g_Logger = New MyCompanyLogging.LoggingComBridge
End Sub

' This is now the ONLY public entry point for accessing the logger.
' All application code MUST use this function.
Public Function Logger() As MyCompanyLogging.LoggingComBridge
    ' Check if the host application has already initialized the global logger.
    If g_Logger Is Nothing Then
        ' This is the safety-net fallback.
        InitializeLogging()
    End If
    
    ' Return the singleton instance.
    Set Logger = g_Logger
End Function