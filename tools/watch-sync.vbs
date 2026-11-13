' Starts tools\watch-sync.ps1 with no visible window.
' Task Scheduler runs this through wscript.exe at logon; it stays alive and only acts
' when a file in the mod source folder changes.
Dim fso, shell, here, script
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
here = fso.GetParentFolderName(WScript.ScriptFullName)
script = fso.BuildPath(here, "watch-sync.ps1")

If Not fso.FileExists(script) Then
	WScript.Quit 1
End If

' 0 = hidden window, False = do not wait
shell.Run "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & script & """", 0, False
