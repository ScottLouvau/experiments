:: Compare UTF8 and DotNet search implementations. 
:: Do a 'cold' run of each search first for a fairer comparison.

@ECHO OFF
SET FFS="%~dp0..\FFS\bin\Release\net5.0\FFS.exe"

%FFS% Convert C:\Code\bion > nul
ECHO.
%FFS% Convert C:\Code\bion "*.*" Log\Bion.Convert.Utf8.v2 Utf8 20
ECHO.
%FFS% Convert C:\Code\bion "*.*" Log\Bion.Convert.DotNet.v2 DotNet 20

%FFS% Console.WriteLine C:\Code > nul
ECHO.
%FFS% Console.WriteLine C:\Code "*.*" Log\Console.WriteLine.Code.Utf8.v2 Utf8
ECHO.
%FFS% Console.WriteLine C:\Code "*.*" Log\Console.WriteLine.Code.DotNet.v2 DotNet
ECHO.



