Set shell =Wscript.createobject("wscript.shell")
path =createobject("Scripting.FileSystemObject").GetFile(Wscript.ScriptFullName).ParentFolder.Path
a = shell.run(path& "\ToastFish.exe",0,false)