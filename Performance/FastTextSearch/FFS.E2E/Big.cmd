@ECHO OFF
SET FFS="%~dp0..\FFS\bin\Release\net5.0\FFS.exe"

ECHO.
%FFS% Console C:\Download\CSV\Big *.* Log\CSV.UTF8.log Utf8
ECHO.
%FFS% Console C:\Download\CSV\Big *.* Log\CSV.DotNet.log DotNet
ECHO.