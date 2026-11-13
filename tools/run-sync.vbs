' Launches tools\sync-to-repo.ps1 without showing a console window.
' Task Scheduler runs this through wscript.exe, which never creates a window.
Dim fso, shell, here, script
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
here = fso.GetParentFolderName(WScript.ScriptFullName)
script = fso.BuildPath(here, "sync-to-repo.ps1")

If Not fso.FileExists(script) Then
	WScript.Quit 1
End If

' 0 = hidden window, False = do not wait
shell.Run "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & script & """", 0, False
