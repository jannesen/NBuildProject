rd /s /q %HOMEDRIVE%%HOMEPATH%\AppData\Local\Microsoft\VisualStudio\17.0_6ecc92deExp
rd /s /q %HOMEDRIVE%%HOMEPATH%\AppData\Roaming\Microsoft\VisualStudio\17.0_6ecc92deExp
"C:\Program Files\Microsoft Visual Studio\2022\Professional\Msbuild\Current\Bin\amd64\MSBuild.exe" NBuildProject.sln -target:Clean
pause