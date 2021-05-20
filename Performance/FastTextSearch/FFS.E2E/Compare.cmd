SET Search="%~dp0..\StringSearch\bin\Release\net5.0\StringSearch.exe"

%Search% Convert C:\Code\bion "*.*" Convert.bion.log
%Search% Convert C:\Code\bion "*.*" Convert.bion.DotNet.log DotNet

%Search% Console.WriteLine C:\Code "*.*" CWR.log
%Search% Console.WriteLine C:\Code "*.*" CWR.DotNet.log DotNet
